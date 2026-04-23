using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;

namespace TwitchChatParser.Infrastructure.Repositories;

public class ChannelUserRelationRepository(DataContext context)
    : BaseRepository<ChannelUserRelation, Guid>(context), IChannelUserRelationRepository;
