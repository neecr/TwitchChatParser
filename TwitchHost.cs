using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace TwitchChatParser;

public class TwitchHost(
    IConfiguration config,
    TokenService tokenService,
    IServiceScopeFactory scopeFactory,
    ILogger<TwitchHost> logger,
    QueueProvider queueProvider) : IHostedService
{
    private readonly List<string> _channels = config.GetSection("Channels").Get<List<string>>() ??
                                              throw new InvalidOperationException("Channels is missing in secrets.");

    private readonly TwitchClient _twitchClient = new();

    private readonly string _username = config["Username"] ??
                                        throw new InvalidOperationException("Username is missing in secrets.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var accessToken = tokenService.GetAccessToken();
        var credentials = new ConnectionCredentials(_username, accessToken);

        using var scope = scopeFactory.CreateScope();
        var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        
        var processedChannels = await dbService.GetProcessedChannels(_channels.Select(c => c.ToLower()).ToList());

        _twitchClient.Initialize(credentials, processedChannels);

        _twitchClient.OnMessageReceived += OnMessageReceived;
        _twitchClient.OnIncorrectLogin += OnIncorrectLogin;
        _twitchClient.OnConnectionError += OnConnectionError;
        _twitchClient.OnUserBanned += OnUserBanned;

        _twitchClient.Connect();
        logger.LogInformation("Connected to {channels}.", string.Join(", ", processedChannels));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Disconnecting...");

        _twitchClient.OnMessageReceived -= OnMessageReceived;
        _twitchClient.OnIncorrectLogin -= OnIncorrectLogin;
        _twitchClient.OnConnectionError -= OnConnectionError;
        _twitchClient.OnUserBanned -= OnUserBanned;

        if (_twitchClient is not { IsInitialized: true, IsConnected: true })
        {
            logger.LogWarning("Client was not connected or initialized.");
            return Task.CompletedTask;
        }

        _twitchClient.Disconnect();

        return Task.CompletedTask;
    }

    private async void OnUserBanned(object? sender, OnUserBannedArgs e)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

            await dbService.AddBanAsync(e.UserBan.TargetUserId, e.UserBan.Username, e.UserBan.RoomId,
                e.UserBan.BanReason);

            logger.LogInformation("User '{UserBanUsername}' was banned in '{UserBanChannel}' for {UserBanBanReason}.",
                e.UserBan.Username, e.UserBan.Channel, e.UserBan.BanReason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process user ban.");
        }
    }

    private void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        queueProvider.Queue.Enqueue(e);
    }

    private void OnIncorrectLogin(object? sender, OnIncorrectLoginArgs e)
    {
        logger.LogError(e.Exception, "Incorrect login.");
    }

    private void OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        logger.LogError(e.Error.Message, "Connection error.");
    }
}