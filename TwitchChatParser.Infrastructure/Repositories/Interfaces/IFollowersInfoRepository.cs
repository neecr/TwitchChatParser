using TwitchChatParser.Domain.Models;

namespace TwitchChatParser.Infrastructure.Repositories.Interfaces;

public interface IFollowersInfoRepository : IRepository<FollowersInfo, Guid>
{
    public Task<List<string>> GetUsersWithRecentUpdates(DateTime expirationTime, List<string> userIds);
}
