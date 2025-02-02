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

internal static class Program
{
    private static IConfigurationRoot _configuration;
    private static string _connectionString = string.Empty;

    private static TokenInfo MakeNewAccessToken(string refreshToken, DataContext dbContext)
    {
        using var client = new HttpClient();

        var values = new Dictionary<string, string>
        {
            { "client_id", _configuration["Twitch:ClientId"]! },
            { "client_secret", _configuration["Twitch:ClientSecret"]! },
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
            ExpiresAt = jsonRoot.GetProperty("expires_in").GetInt32(),
            RefreshToken = jsonRoot.GetProperty("refresh_token").GetString()!,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.TokenInfos.Add(newToken);

        dbContext.SaveChanges();

        return newToken;
    }

    private static string GetAccessToken(DataContext dbContext)
    {
        var lastToken = dbContext.TokenInfos
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault()!;

        if (DateTime.UtcNow >= lastToken.CreatedAt.AddSeconds(lastToken.ExpiresAt))
        {
            Console.WriteLine(
                $"Current token expired at {lastToken.CreatedAt + TimeSpan.FromSeconds(lastToken.ExpiresAt) +
                                            TimeZoneInfo.Local.BaseUtcOffset} {TimeZoneInfo.Local.DisplayName}");
            Console.WriteLine("Creating new access token...");
            return MakeNewAccessToken(lastToken.RefreshToken, dbContext).AccessToken;
        }

        Console.WriteLine(
            $"Current token is valid, expires at {lastToken.CreatedAt + TimeSpan.FromSeconds(lastToken.ExpiresAt) +
                                                  TimeZoneInfo.Local.BaseUtcOffset} {TimeZoneInfo.Local.DisplayName}");

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
                    CreatedAt = DateTime.UtcNow
                });

            // Add a user message in db
            await dbContext.Messages.AddAsync(new Message
            {
                Id = Guid.NewGuid(),
                UserId = e.ChatMessage.UserId,
                MessageText = e.ChatMessage.Message.Trim(),
                CreatedAt = DateTime.UtcNow,
                ChannelName = e.ChatMessage.Channel
            });

            // Adding a channel and user relation in db
            if (!await dbContext.ChannelUsers.AnyAsync
                (u => u.UserId == e.ChatMessage.UserId &&
                      u.ChannelName == e.ChatMessage.Channel))
            {
                await dbContext.ChannelUsers.AddAsync(new ChannelUser
                {
                    Id = Guid.NewGuid(),
                    UserId = e.ChatMessage.UserId,
                    ChannelName = e.ChatMessage.Channel
                });
            }


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
            $"[{e.ChatMessage.Channel}] \e[38;5;\e[1;38;2;" +
            $"{e.ChatMessage.Color.R};" +
            $"{e.ChatMessage.Color.G};" +
            $"{e.ChatMessage.Color.B}m{e.ChatMessage.DisplayName}\e[0m: {e.ChatMessage.Message}";

        Console.WriteLine(log);

        return Task.CompletedTask;
    }

    private static void InitializeTwitchClient(string userName, string accessToken, string channelName,
        IServiceProvider serviceProvider)
    {
        var credentials = new ConnectionCredentials(userName, accessToken);
        var client = new TwitchClient();
        client.Initialize(credentials, channelName);

        var dbContext = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<DataContext>();

        client.OnMessageReceived += async (_, e) =>
        {
            await WriteInDatabase(e, dbContext);
            await LogInConsole(e);
        };

        client.ConnectAsync();

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"Connected to {channelName}.");
        Console.ResetColor();
    }

    private static void Main(string[] args)
    {
        if (args.Length == 0) throw new Exception("No channel names provided.");

        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("Missing appsettings.json file", configPath);

        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        _connectionString = _configuration["ConnectionStrings:DefaultDB"]
                            ?? throw new InvalidOperationException("ConnectionString in appsettings.json is missing.");

        var userName = _configuration["Twitch:Username"]
                       ?? throw new InvalidOperationException("Twitch username appsettings.json is missing.");

        if (_configuration["Twitch:ClientId"] == null)
            throw new InvalidOperationException("Client ID in appsettings.json is missing.");
        if (_configuration["Twitch:ClientSecret"] == null)
            throw new InvalidOperationException("Client secret in appsettings.json is missing.");

        var services = new ServiceCollection();

        services.AddDbContext<DataContext>(options => options.UseNpgsql(_connectionString));

        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<DataContext>();

        var accessToken = GetAccessToken(dbContext);

        foreach (var channelName in args) InitializeTwitchClient(userName, accessToken, channelName, serviceProvider);

        Console.ReadLine();
        Environment.Exit(0);
    }
}