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
    private static string _connectionString = string.Empty;

    private static async Task WriteInDatabase(OnMessageReceivedArgs e, DataContext dbContext)
    {
        try
        {
            if (!await dbContext.Users.AnyAsync(u => u.Id == e.ChatMessage.UserId))
                await dbContext.Users.AddAsync(new User
                {
                    Id = e.ChatMessage.UserId,
                    Username = e.ChatMessage.DisplayName,
                    CreatedAt = DateTime.UtcNow
                });

            await dbContext.Messages.AddAsync(new Message
            {
                Id = Guid.NewGuid(),
                UserId = e.ChatMessage.UserId,
                MessageText = e.ChatMessage.Message.Trim(),
                CreatedAt = DateTime.UtcNow,
                ChannelName = e.ChatMessage.Channel
            });

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
        var ansiStyle =
            $"\e[1;38;2;{e.ChatMessage.Color.R};{e.ChatMessage.Color.G};{e.ChatMessage.Color.B}m";

        var log =
            $"[{e.ChatMessage.Channel}] \e[38;5;{ansiStyle}{e.ChatMessage.DisplayName}\e[0m: {e.ChatMessage.Message}";

        Console.WriteLine(log);

        return Task.CompletedTask;
    }

    private static void InitializeTwitchClient(string userName, string accessToken, string channelName,
        IServiceProvider serviceProvider)
    {
        var credentials = new ConnectionCredentials(userName, accessToken);
        var client = new TwitchClient();
        client.Initialize(credentials, channelName);

        var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        
        client.OnMessageReceived += (_, e) => LogInConsole(e);
        client.OnMessageReceived += (_, e) => WriteInDatabase(e, dbContext);
        
        client.ConnectAsync();
        
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"Connected to {channelName}.");
        Console.ResetColor();
    }

    private static void Main(string[] args)
    {
        if (args.Length == 0) throw new Exception("No arguments provided.");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.FullName)
            .AddJsonFile("appsettings.json", true, true)
            .Build();

        var services = new ServiceCollection();

        _connectionString = configuration["ConnectionStrings:DefaultDB"]
                            ?? throw new InvalidOperationException("ConnectionString is missing.");

        services.AddDbContext<DataContext>(options => options.UseNpgsql(_connectionString));

        var serviceProvider = services.BuildServiceProvider();

        var userName = configuration["Twitch:Username"]
                       ?? throw new InvalidOperationException("Username is missing.");
        var accessToken = configuration["Twitch:AccessToken"]
                          ?? throw new InvalidOperationException("AccessToken is missing.");

        foreach (var channelName in args) InitializeTwitchClient(userName, accessToken, channelName, serviceProvider);

        Console.ReadLine();
    }
}