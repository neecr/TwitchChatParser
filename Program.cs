using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TwitchChatParser.Data;
using TwitchChatParser.Models;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace TwitchChatParser;

internal class Program
{
    private static IConfiguration? _configuration;
    private static string _connectionString = string.Empty;

    private static void ValidateAccessToken(TokenInfo tokenInfo)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenInfo.AccessToken}");

        var response = client.GetAsync("https://id.twitch.tv/oauth2/validate");
        response.Result.EnsureSuccessStatusCode();

        Console.WriteLine("Token is validated.");
    }

    private static TokenInfo RefreshAccessToken(string refreshToken, DataContext dbContext)
    {
        using var client = new HttpClient();

        var values = new Dictionary<string, string>
        {
            { "client_id", _configuration["ClientId"]! },
            { "client_secret", _configuration["ClientSecret"]! },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        var content = new FormUrlEncodedContent(values);
        var response = client.PostAsync("https://id.twitch.tv/oauth2/token", content);

        response.Result.EnsureSuccessStatusCode();

        var jsonResponse = JsonDocument.Parse(response.Result.Content.ReadAsStringAsync().Result);

        var jsonRoot = jsonResponse.RootElement;

        var newToken = new TokenInfo
        {
            Id = Guid.NewGuid(),
            AccessToken = jsonRoot.GetProperty("access_token").GetString()!,
            ExpirationTime = jsonRoot.GetProperty("expires_in").GetInt32(),
            RefreshToken = jsonRoot.GetProperty("refresh_token").GetString()!,
            CreationTime = DateTime.UtcNow
        };

        dbContext.TokenInfos.Add(newToken);

        dbContext.SaveChanges();

        return newToken;
    }

    private static string GetAccessToken(DataContext dbContext)
    {
        var lastToken = dbContext.TokenInfos
            .OrderByDescending(t => t.CreationTime)
            .FirstOrDefault();

        if (lastToken == null) throw new Exception("Token not found.");

        if (DateTime.UtcNow >= lastToken.CreationTime.AddSeconds(lastToken.ExpirationTime))
        {
            Console.WriteLine(
                $"Current token expired at {lastToken.CreationTime + TimeSpan.FromSeconds(lastToken.ExpirationTime) + TimeZoneInfo.Local.BaseUtcOffset} {TimeZoneInfo.Local.DisplayName}");
            Console.WriteLine("Creating new access token...");
            return RefreshAccessToken(lastToken.RefreshToken, dbContext).AccessToken;
        }

        ValidateAccessToken(lastToken);

        Console.WriteLine(
            $"Current token expires at {lastToken.CreationTime + TimeSpan.FromSeconds(lastToken.ExpirationTime) + TimeZoneInfo.Local.BaseUtcOffset} {TimeZoneInfo.Local.DisplayName}");

        return lastToken.AccessToken;
    }

    private static async Task WriteInDatabase(OnMessageReceivedArgs e, DataContext dbContext)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            // Add a user in db if not exists
            if (!await dbContext.Users.AnyAsync(u => u.Id == e.ChatMessage.UserId))
                await dbContext.Users.AddAsync(new User
                {
                    Id = e.ChatMessage.UserId,
                    Username = e.ChatMessage.DisplayName,
                    CreationTime = DateTime.UtcNow
                });

            // Add a user message in db
            await dbContext.Messages.AddAsync(new Message
            {
                Id = Guid.NewGuid(),
                UserId = e.ChatMessage.UserId,
                MessageText = e.ChatMessage.Message.Trim(),
                CreationTime = DateTime.UtcNow,
                ChannelName = e.ChatMessage.Channel
            });

            // Adding a channel and user relation in db
            if (!await dbContext.ChannelUsers.AnyAsync
                (u => u.UserId == e.ChatMessage.UserId &&
                      u.ChannelName == e.ChatMessage.Channel))
                await dbContext.ChannelUsers.AddAsync(new ChannelUser
                {
                    Id = Guid.NewGuid(),
                    UserId = e.ChatMessage.UserId,
                    ChannelName = e.ChatMessage.Channel
                });


            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static Task LogInConsole(OnMessageReceivedArgs e)
    {
        var log =
            $"[{e.ChatMessage.Channel}] \u001b[1;38;2;" +
            $"{e.ChatMessage.Color.R};" +
            $"{e.ChatMessage.Color.G};" +
            $"{e.ChatMessage.Color.B}m{e.ChatMessage.DisplayName}\u001b[0m: {e.ChatMessage.Message}";

        Console.WriteLine(log);

        return Task.CompletedTask;
    }

    private static void InitializeTwitchClient(string userName, string accessToken, string channelName,
        IServiceProvider serviceProvider)
    {
        var credentials = new ConnectionCredentials(userName, "oauth:" + accessToken);
        var client = new TwitchClient();
        client.Initialize(credentials, channelName);

        var dbContext = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<DataContext>();

        client.OnMessageReceived += async (_, e) =>
        {
            await LogInConsole(e);
            await WriteInDatabase(e, dbContext);
        };

        client.OnIncorrectLogin += (_, e) =>
        {
            Console.WriteLine("Login failed: " + e.Exception.Message);
            return Task.CompletedTask;
        };

        client.OnConnectionError += (_, e) =>
        {
            Console.WriteLine("Connection error: " + e.Error.Message);
            return Task.CompletedTask;
        };

        client.ConnectAsync();

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"Connected to {channelName}.");
        Console.ResetColor();
    }

    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No channels provided.");
            return;
        }

        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        if (_configuration["ClientSecret"] == null)
            throw new InvalidOperationException("ClientSecret is missing in secrets.");
        if (_configuration["ClientId"] == null)
            throw new InvalidOperationException("ClientId is missing in secrets.");

        var username = _configuration["Username"] ??
                       throw new InvalidOperationException("Username is missing in secrets.");

        _connectionString = _configuration["ConnectionString"] ??
                            throw new InvalidOperationException("ConnectionString is missing in secrets.");

        var services = new ServiceCollection();

        services.AddDbContext<DataContext>(options => options.UseNpgsql(_connectionString));

        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<DataContext>();

        var accessToken = GetAccessToken(dbContext);

        foreach (var channelName in args)
            InitializeTwitchClient(username, accessToken, channelName, serviceProvider);

        Console.ReadLine();
        Environment.Exit(0);
    }
}