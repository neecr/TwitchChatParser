using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Application.Utils;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Services;

namespace TwitchChatParser.Worker.HostedServices;

public class FollowersUpdateHostedService(
    FollowersQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<FollowersUpdateHostedService> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly int followersInfoLifetime = Convert.ToInt32(configuration["MessageProcessingSettings:FollowersInfoLifetime"] ??
                                                                 throw new InvalidOperationException(
                                                                     "FollowersInfoLifetime is missing."));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("FollowersUpdateHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var userIds = await queue.ReadAsync(stoppingToken);

                await ProcessUserIdsAsync(userIds);
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение при остановке
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing followers update queue.");
            }
        }

        logger.LogDebug("FollowersUpdateHostedService stopped.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Finishing updating followers...");

        // Пытаемся вычитать все оставшиеся элементы из очереди
        while (queue.TryRead(out var userIds))
        {
            try
            {
                if (userIds != null)
                {
                    await ProcessUserIdsAsync(userIds);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing remaining followers updates during shutdown.");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessUserIdsAsync(List<string> userIds)
    {
        if (userIds.Count == 0) return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var twitchApiService = scope.ServiceProvider.GetRequiredService<TwitchApiService>();

            var expirationTime = DateTime.UtcNow.AddHours(-followersInfoLifetime);

            // 1. Находим пользователей, у которых ЕСТЬ запись за последний час.
            var usersWithRecentUpdates = await dbContext.FollowersInfos
                .Where(f => userIds.Contains(f.UserId) && f.CreationTime > expirationTime)
                .Select(f => f.UserId)
                .Distinct()
                .ToListAsync();

            // 2. Оставляем только тех, кого НУЖНО обновить.
            var usersToUpdate = userIds.Except(usersWithRecentUpdates).ToList();

            if (usersToUpdate.Count == 0) return;

            logger.LogDebug("Updating followers for {Count} users...", usersToUpdate.Count);

            foreach (var userId in usersToUpdate)
            {
                try
                {
                    var followersDto = await twitchApiService.GetFollowersByIdAsync(userId);

                    dbContext.FollowersInfos.Add(new FollowersInfo
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
            }

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Done updating {Count} followers.", usersToUpdate.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ProcessUserIdsAsync.");
        }
    }
}