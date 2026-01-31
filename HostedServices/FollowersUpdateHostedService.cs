using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchChatParser.EfCore.Data;
using TwitchChatParser.EfCore.Models;
using TwitchChatParser.Services;
using TwitchChatParser.Utils;

namespace TwitchChatParser.HostedServices;

public class FollowersUpdateHostedService(
    FollowersUpdateQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<FollowersUpdateHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FollowersUpdateHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var userIds = await queue.Reader.ReadAsync(stoppingToken);
                
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

        logger.LogInformation("FollowersUpdateHostedService stopped.");
    }

    private async Task ProcessUserIdsAsync(List<string> userIds)
    {
        if (userIds.Count == 0) return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();
            
            var oneHourAgo = DateTime.UtcNow.AddDays(-1);

            // 1. Находим пользователей, у которых ЕСТЬ запись за последний час.
            var usersWithRecentUpdates = await dbContext.FollowersInfos
                .Where(f => userIds.Contains(f.UserId) && f.CreationTime > oneHourAgo)
                .Select(f => f.UserId)
                .Distinct()
                .ToListAsync();

            // 2. Оставляем только тех, кого НУЖНО обновить.
            var usersToUpdate = userIds.Except(usersWithRecentUpdates).ToList();

            if (usersToUpdate.Count == 0) return;

            logger.LogInformation("Updating followers for {Count} users...", usersToUpdate.Count);

            foreach (var userId in usersToUpdate)
            {
                try
                {
                    var followersDto = await tokenService.GetFollowersByIdAsync(userId);

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
            logger.LogInformation("Done updating followers.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ProcessUserIdsAsync.");
        }
    }
}