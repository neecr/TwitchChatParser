using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Services;
using TwitchChatParser.Utils;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace TwitchChatParser.HostedServices;

public class TwitchHost(
    IConfiguration config,
    IServiceScopeFactory scopeFactory,
    ILogger<TwitchHost> logger,
    QueueProvider queueProvider) : IHostedService
{
    private readonly List<string> _channels = config.GetSection("TwitchSettings:Channels").Get<List<string>>() ??
                                              throw new InvalidOperationException("Channels is missing in secrets.");

    private readonly TwitchClient _twitchClient = new();

    private readonly string _username = config["TwitchSettings:Username"] ??
                                        throw new InvalidOperationException("Username is missing in secrets.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();
        var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var accessToken = await tokenService.GetAccessTokenAsync();
        var credentials = new ConnectionCredentials(_username, accessToken);

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
        logger.LogInformation("Stopping application...");

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
        if (e.ChatMessage.Username is "fossabot" or "streamelements")
        {
            return;
        }
        
        queueProvider.Queue.Enqueue(e);

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            
            using var scope = scopeFactory.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();
            
            var followersCount = (await tokenService.GetFollowersByIdAsync(e.ChatMessage.UserId)).Count;

            if (followersCount >= 10000)
            {
                logger.LogInformation("{username} has {followerCount} followers.",
                    e.ChatMessage.Username, followersCount);
            }
        });
    }

    private async void OnIncorrectLogin(object? sender, OnIncorrectLoginArgs e)
    {
        try
        {
            logger.LogError(e.Exception, "Incorrect login. Attempting to refresh token and reconnect...");

            _twitchClient.Disconnect();

            using var scope = scopeFactory.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();

            var accessToken = await tokenService.GetAccessTokenAsync(true);
            var credentials = new ConnectionCredentials(_username, accessToken);

            _twitchClient.Initialize(credentials, _twitchClient.JoinedChannels.Select(c => c.Channel).ToList());
            _twitchClient.Connect();
            logger.LogInformation("Reconnected successfully with a new token.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to reconnect after token refresh.");
        }
    }

    private void OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        logger.LogError(e.Error.Message, "Connection error.");
    }
}