using Microsoft.EntityFrameworkCore;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;

namespace TwitchChatParser.Infrastructure.Repositories;

public class FollowersInfoRepository(DataContext context)
    : BaseRepository<FollowersInfo, Guid>(context), IFollowersInfoRepository
{
    public async Task<List<string>> GetUsersWithRecentUpdates(DateTime expirationTime, List<string> userIds)
    {
        return await _dbSet
            .Where(f => userIds.Contains(f.UserId) && f.CreationTime > expirationTime)
            .Select(f => f.UserId)
            .Distinct()
            .ToListAsync();
    }
}