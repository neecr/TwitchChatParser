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
    private static string ConnectionString = string.Empty;

    private static async Task ClientOnMessageReceived(object? sender, OnMessageReceivedArgs e, DataContext dbContext)
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

            await dbContext.SaveChangesAsync();

            Console.WriteLine($"[{e.ChatMessage.Channel}] " +
                              $"{e.ChatMessage.DisplayName}: " +
                              $"{e.ChatMessage.Message}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void InitializeTwitchClient(string userName, string accessToken, string channelName,
        IServiceProvider serviceProvider)
    {
        var credentials = new ConnectionCredentials(userName, accessToken);
        var client = new TwitchClient();
        client.Initialize(credentials, channelName);

        var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

        client.OnMessageReceived += (sender, e) => ClientOnMessageReceived(sender, e, dbContext);
        client.Connect();

        Console.WriteLine($"Connected to {channelName}.");
    }

    private static void Main(string[] args)
    {
        if (args.Length == 0) throw new Exception("No arguments provided.");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.FullName)
            .AddJsonFile("appsettings.json", true, true)
            .Build();

        var services = new ServiceCollection();

        ConnectionString = configuration["ConnectionStrings:DefaultDB"]
                           ?? throw new InvalidOperationException("ConnectionString is missing.");

        services.AddDbContext<DataContext>(options => options.UseNpgsql(ConnectionString));

        var serviceProvider = services.BuildServiceProvider();

        var userName = configuration["Twitch:Username"]
                       ?? throw new InvalidOperationException("Username is missing.");
        var accessToken = configuration["Twitch:AccessToken"]
                          ?? throw new InvalidOperationException("AccessToken is missing.");

        foreach (var channelName in args) InitializeTwitchClient(userName, accessToken, channelName, serviceProvider);

        Console.ReadLine();
    }
}