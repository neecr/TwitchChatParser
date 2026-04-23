using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Application.Extensions;
using TwitchChatParser.Application.Utils;
using TwitchChatParser.Infrastructure.Repositories;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;
using TwitchChatParser.Infrastructure.Services;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace TwitchChatParser.Worker.HostedServices;

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
        logger.LogDebug("Connected to Twitch.");
        return Task.CompletedTask;
    }

    private async Task ConnectAsync(bool forceRefresh)
    {
        if (_isReconnecting) return;
        _isReconnecting = true;

        int delay = 10000;
        
        try
        {
            while (true)
            {
                try
                {
                    if (_twitchClient.IsConnected)
                    {
                        logger.LogInformation("Disconnecting before reconnecting...");
                        await _twitchClient.DisconnectAsync();
                    }

                    using var scope = scopeFactory.CreateScope();
                    var tokenService = scope.ServiceProvider.GetRequiredService<TwitchTokenService>();
                    var channelRepository = scope.ServiceProvider.GetRequiredService<IChannelRepository>();

                    logger.LogInformation(forceRefresh ? "Refreshing access token..." : "Getting access token...");
                        
                    var accessToken = await tokenService.GetAccessTokenAsync(forceRefresh);
                        
                    var credentials = new ConnectionCredentials(_username, accessToken);

                    List<string> channelsToJoin;
                    if (_twitchClient.JoinedChannels.Any())
                    {
                        channelsToJoin = _twitchClient.JoinedChannels.Select(c => c.Channel).ToList();
                    }
                    else
                    {
                        channelsToJoin = await channelRepository.GetProcessedChannelsAsync(_channels.Select(c => c.ToLower()).ToList());
                    }

                    logger.LogDebug("Initializing client...");
                    _twitchClient.Initialize(credentials, channelsToJoin);

                    if (await _twitchClient.ConnectAsync())
                    {
                        logger.LogInformation("Connected successfully to {ChannelsToJoin}.", string.Join(", ", channelsToJoin));
                        return; 
                    }
                    else
                    {
                        logger.LogError("Connection failed. Retrying in {Delay} seconds...", delay / 1000);
                        await Task.Delay(delay);
                        delay *= 2;
                        forceRefresh = true; 
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during connection attempt. Retrying in {Delay} seconds...", delay / 1000);
                    await Task.Delay(delay);
                    delay *= 2;
                    forceRefresh = true; 
                }
            }
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
            var banRepository = scope.ServiceProvider.GetRequiredService<BanRepository>();
            
            await banRepository.AddAsync(e.UserBan.ToBan(), e.UserBan.Username);

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
        messageQueue.Enqueue(e);
        return Task.CompletedTask;
    }

    private async Task OnIncorrectLogin(object? sender, OnIncorrectLoginArgs e)
    {
        logger.LogWarning(e.Exception, "Incorrect login detected. Reconnecting with new token...");
        await Task.Delay(5000);
        await ConnectAsync(forceRefresh: true);
    }

    private async Task OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        logger.LogError("Connection error: {Message}. Reconnecting...", e.Error.Message);
        await Task.Delay(5000);
        await ConnectAsync(forceRefresh: true);
    }
}