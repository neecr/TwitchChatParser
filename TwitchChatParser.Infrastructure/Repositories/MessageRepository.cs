using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Application.Utils;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;
using TwitchLib.Client.Events;

namespace TwitchChatParser.Infrastructure.Repositories;

public class MessageRepository(
    DataContext context,
    ILogger<MessageRepository> logger,
    FollowersQueue followersQueue) : BaseRepository<Message, string>(context), IMessageRepository
{
    public async Task WriteBatchAsync(IReadOnlyCollection<OnMessageReceivedArgs> messages, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Starting saving {Count} messages...", messages.Count);

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            
            var uniqueUsers = messages
                .Select(m => m.ChatMessage)
                .DistinctBy(m => m.UserId)
                .Select(m => new { m.UserId, m.DisplayName }).ToList();

            var userIds = uniqueUsers.Select(u => u.UserId).ToList();
            var existingUserIds = (await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var newUsers = uniqueUsers
                .Where(u => !existingUserIds.Contains(u.UserId))
                .Select(u => new User
                {
                    Id = u.UserId,
                    Username = u.DisplayName,
                    CreationTime = DateTime.UtcNow
                });
            await _context.Users.AddRangeAsync(newUsers, cancellationToken);
            
            var uniqueRelations = messages
                .Select(m => new { m.ChatMessage.UserId, m.ChatMessage.RoomId })
                .Distinct();

            var existingRelationKeys = (await _context.ChannelUserRelations
                    .Where(r => userIds.Contains(r.UserId))
                    .Select(r => new { r.UserId, r.ChannelId })
                    .ToListAsync(cancellationToken))
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
            await _context.ChannelUserRelations.AddRangeAsync(newRelations, cancellationToken);
            
            var messageIds = messages.Select(m => m.ChatMessage.Id).ToList();
            var existingMessageIds = (await _dbSet
                    .Where(m => messageIds.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync(cancellationToken))
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
            await _dbSet.AddRangeAsync(messageModels, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Saved {Count} messages.", messages.Count);
            
            followersQueue.Enqueue(userIds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write message batch to database after multiple retries.");
        }
    }
}
