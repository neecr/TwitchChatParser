using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Application.Utils;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchLib.Client.Events;

namespace TwitchChatParser.Infrastructure.Services;

public class DatabaseService(
    DataContext dbContext,
    ILogger<DatabaseService> logger,
    TwitchApiService twitchApiService,
    FollowersQueue followersQueue)
{
    public async Task WriteBatchAsync(IReadOnlyCollection<OnMessageReceivedArgs> messages)
    {
        logger.LogDebug("Starting saving {Count} messages...", messages.Count);

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            var uniqueUsers = messages
                .Select(m => m.ChatMessage)
                .DistinctBy(m => m.UserId)
                .Select(m => new { m.UserId, m.DisplayName }).ToList();

            var userIds = uniqueUsers.Select(u => u.UserId).ToList();
            var existingUserIds = (await dbContext.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync())
                .ToHashSet();

            var newUsers = uniqueUsers
                .Where(u => !existingUserIds.Contains(u.UserId))
                .Select(u => new User
                {
                    Id = u.UserId,
                    Username = u.DisplayName,
                    CreationTime = DateTime.UtcNow
                });
            await dbContext.Users.AddRangeAsync(newUsers);

            var uniqueRelations = messages
                .Select(m => new { m.ChatMessage.UserId, m.ChatMessage.RoomId })
                .Distinct();

            var existingRelationKeys = (await dbContext.ChannelUserRelations
                    .Where(r => userIds.Contains(r.UserId))
                    .Select(r => new { r.UserId, r.ChannelId })
                    .ToListAsync())
                .Select(r => (r.UserId, r.ChannelId))
                .ToHashSet();

            var newRelations = uniqueRelations
                .Where(r => !existingRelationKeys.Contains((r.UserId, r.RoomId)))
                .Select(r => new ChannelUserRelation
                {
                    Id = Guid.NewGuid(),
                    UserId = r.UserId,
                    ChannelId = r.RoomId,
                    CreationTime = DateTime.UtcNow
                });
            await dbContext.ChannelUserRelations.AddRangeAsync(newRelations);

            var messageIds = messages.Select(m => m.ChatMessage.Id).ToList();
            var existingMessageIds = (await dbContext.Messages
                    .Where(m => messageIds.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync())
                .ToHashSet();

            var newMessageArgs = messages.Where(m => !existingMessageIds.Contains(m.ChatMessage.Id));

            var messageModels = newMessageArgs.Select(m => new Message
            {
                Id = m.ChatMessage.Id,
                UserId = m.ChatMessage.UserId,
                ChannelId = m.ChatMessage.RoomId,
                MessageText = m.ChatMessage.Message.Trim(),
                CreationTime = m.ChatMessage.TmiSent.UtcDateTime
            });
            await dbContext.Messages.AddRangeAsync(messageModels);

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            logger.LogInformation("Saved {Count} messages.", messages.Count);

            // Отправляем ID пользователей в очередь для фонового обновления фолловеров
            followersQueue.Enqueue(userIds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write message batch to database after multiple retries.");
        }
    }

    public async Task<List<string>> GetProcessedChannels(List<string> channelsNames)
    {
        var existingChannelNames = await dbContext.Channels
            .Where(c => channelsNames.Contains(c.Name))
            .Select(c => c.Name)
            .ToListAsync();

        var processedNames = new List<string>(existingChannelNames);
        var candidatesForApiLookup = channelsNames.Except(existingChannelNames).ToList();

        if (candidatesForApiLookup.Count == 0) return processedNames;

        var newChannelsUserData = await twitchApiService.GetUserDataByUsernameAsync(candidatesForApiLookup);

        var foundNamesByApi = newChannelsUserData
            .Select(userData => userData.DisplayName)
            .ToList();

        var nonExistentNames = candidatesForApiLookup
            .Except(foundNamesByApi, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (nonExistentNames.Count > 0)
            foreach (var name in nonExistentNames)
                logger.LogWarning("Channel {channel} doesn't exist.", name);

        var channelsToAdd = newChannelsUserData
            .Select(userData => new Channel
            {
                Id = userData.Id,
                Name = userData.Login
            })
            .ToList();

        if (channelsToAdd.Count > 0)
        {
            await dbContext.Channels.AddRangeAsync(channelsToAdd);
            await dbContext.SaveChangesAsync();

            processedNames.AddRange(channelsToAdd.Select(c => c.Name));
        }

        return processedNames;
    }

    public async Task AddBanAsync(string userId, string username, string channelName)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId))
        {
            dbContext.Users.Add(new User
            {
                Id = userId,
                Username = username,
                CreationTime = DateTime.UtcNow
            });
        }

        var channel = await dbContext.Channels.FirstOrDefaultAsync(c => c.Name == channelName);

        if (channel == null)
        {
            logger.LogWarning("Channel {channel} doesn't exist.", channelName);
            return;
        }

        var ban = new Ban
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelId = channel.Id,
            BanReason = null,
            CreationTime = DateTime.UtcNow
        };

        dbContext.Bans.Add(ban);

        logger.LogInformation("{UserUsername} was banned in {ChannelName}.", ban.User?.Username, ban.Channel?.Name);

        await dbContext.SaveChangesAsync();
    }
}