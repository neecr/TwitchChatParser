using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchChatParser.Application.Utils;
using TwitchChatParser.Domain.Configuration;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;
using TwitchChatParser.Infrastructure.Services;

namespace TwitchChatParser.Worker.HostedServices;

public class FollowersUpdateHostedService(
    FollowersQueue queue,
    ILogger<FollowersUpdateHostedService> logger,
    IOptions<MessageProcessingSettings> messageProcessingSettingsOptions,
    IFollowersInfoRepository followersInfoRepository,
    TwitchApiService twitchApiService) : BackgroundService
{
    private readonly int followersInfoLifetime = messageProcessingSettingsOptions.Value.FollowersInfoLifetime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("FollowersUpdateHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var userIds = await queue.ReadAsync(stoppingToken);

                await ProcessUserIdsAsync(userIds);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing followers update queue.");
            }

        logger.LogDebug("FollowersUpdateHostedService stopped.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Finishing updating followers...");

        while (queue.TryRead(out var userIds))
            try
            {
                if (userIds != null) await ProcessUserIdsAsync(userIds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing remaining followers updates during shutdown.");
            }

        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessUserIdsAsync(List<string> userIds)
    {
        if (userIds.Count == 0) return;

        try
        {
            var expirationTime = DateTime.UtcNow.AddHours(-followersInfoLifetime);

            var usersWithRecentUpdates =
                await followersInfoRepository.GetUsersWithRecentUpdates(expirationTime, userIds);
            var usersToUpdate = userIds.Except(usersWithRecentUpdates).ToList();

            if (usersToUpdate.Count == 0) return;

            logger.LogDebug("Updating followers for {Count} users...", usersToUpdate.Count);

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 2 };
            var newFollowers = new ConcurrentBag<FollowersInfo>();

            await Parallel.ForEachAsync(usersToUpdate, parallelOptions, async (userId, _) =>
            {
                try
                {
                    var followersDto = await twitchApiService.GetFollowersByIdAsync(userId);

                    newFollowers.Add(new FollowersInfo
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        FollowersCount = followersDto.Count,
                        CreationTime = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fetch followers count for user {UserId}", userId);
                }
            });

            if (!newFollowers.IsEmpty)
            {
                await followersInfoRepository.AddAsync(newFollowers.ToList());
                await followersInfoRepository.SaveChangesAsync();
            }

            logger.LogInformation("Done updating {Count} followers.", usersToUpdate.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ProcessUserIdsAsync.");
        }
    }
}