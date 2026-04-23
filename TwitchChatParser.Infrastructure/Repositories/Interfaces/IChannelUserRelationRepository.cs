using TwitchChatParser.Domain.Models;

namespace TwitchChatParser.Infrastructure.Repositories.Interfaces;

public interface IChannelUserRelationRepository : IRepository<ChannelUserRelation, Guid>
{
}
