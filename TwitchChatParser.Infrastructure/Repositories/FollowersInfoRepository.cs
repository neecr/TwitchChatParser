using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;

namespace TwitchChatParser.Infrastructure.Repositories;

public class FollowersInfoRepository(DataContext context)
    : BaseRepository<FollowersInfo, Guid>(context), IFollowersInfoRepository;
