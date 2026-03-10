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
    MessageQueue messageQueue) : IHostedService
{
    private readonly List<string> _channels = config.GetSection("TwitchSettings:Channels").Get<List<string>>() ??
                                              throw new InvalidOperationException("Channels is missing in secrets.");

    private readonly TwitchClient _twitchClient = new();

    private readonly string _username = config["TwitchSettings:Username"] ??
                                        throw new InvalidOperationException("Username is missing in secrets.");

    private bool _isReconnecting;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _twitchClient.OnMessageReceived += OnMessageReceived;
        _twitchClient.OnIncorrectLogin += OnIncorrectLogin;
        _twitchClient.OnConnectionError += OnConnectionError;
        _twitchClient.OnUserBanned += OnUserBanned;
        _twitchClient.OnConnected += OnConnected;

        await ConnectAsync(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping application...");
        
        _twitchClient.OnMessageReceived -= OnMessageReceived;
        _twitchClient.OnIncorrectLogin -= OnIncorrectLogin;
        _twitchClient.OnConnectionError -= OnConnectionError;
        _twitchClient.OnUserBanned -= OnUserBanned;
        _twitchClient.OnConnected -= OnConnected;

        if (_twitchClient is { IsInitialized: true, IsConnected: true })
        {
            _twitchClient.DisconnectAsync();
        }

        return Task.CompletedTask;
    }

    private Task OnConnected(object? sender, OnConnectedEventArgs e)
    {
        logger.LogInformation("Connected to Twitch.");
        return Task.CompletedTask;
    }

    private async Task ConnectAsync(bool forceRefresh)
    {
        if (_isReconnecting) return;
        _isReconnecting = true;

        try
        {
            if (_twitchClient.IsConnected)
            {
                logger.LogInformation("Disconnecting before reconnecting...");
                await _twitchClient.DisconnectAsync();
            }

            using var scope = scopeFactory.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<TwitchTokenService>();
            var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

            logger.LogInformation(forceRefresh ? "Refreshing access token..." : "Getting access token...");
            
            var accessToken = await tokenService.GetAccessTokenAsync(forceRefresh);
            
            var credentials = new ConnectionCredentials(_username, accessToken);

            List<string> channelsToJoin;
            if (_twitchClient.JoinedChannels.Count > 0)
            {
                channelsToJoin = _twitchClient.JoinedChannels.Select(c => c.Channel).ToList();
            }
            else
            {
                channelsToJoin = await dbService.GetProcessedChannels(_channels.Select(c => c.ToLower()).ToList());
            }

            logger.LogInformation("Initializing client...");
            _twitchClient.Initialize(credentials, channelsToJoin);

            if (await _twitchClient.ConnectAsync())
            {
                logger.LogInformation("Connected successfully.");
            }
            else
            {
                logger.LogError("Connection failed.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during connection attempt. Retrying in 10 seconds...");
            await Task.Delay(10000);
            _isReconnecting = false; 
            await ConnectAsync(forceRefresh); 
        }
        finally
        {
            _isReconnecting = false;
        }
    }
    private async Task OnUserBanned(object? sender, OnUserBannedArgs e)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

            await dbService.AddBanAsync(e.UserBan.TargetUserId, e.UserBan.Username, e.UserBan.Channel);

            logger.LogInformation("User '{UserBanUsername}' was banned in '{UserBanChannel}'",
                e.UserBan.Username, e.UserBan.Channel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process user ban.");
        }
    }

    private Task OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        messageQueue.Queue.Enqueue(e);
        return Task.CompletedTask;
    }

    private async Task OnIncorrectLogin(object? sender, OnIncorrectLoginArgs e)
    {
        logger.LogWarning(e.Exception, "Incorrect login detected. Reconnecting with new token...");
        await Task.Delay(5000);
        _ = ConnectAsync(forceRefresh: true);
    }

    private async Task OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        logger.LogError("Connection error: {Message}. Reconnecting...", e.Error.Message);
        await Task.Delay(5000);
        _ = ConnectAsync(forceRefresh: true);
    }
}