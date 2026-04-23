using TwitchChatParser.Domain.Models;
using TwitchLib.Client.Models;

namespace TwitchChatParser.Application.Extensions;

public static class TwitchLibMapper
{
    public static Ban ToBan(this UserBan userBan)
    {
        return new Ban
        {
            Id = Guid.NewGuid(),
            UserId = userBan.TargetUserId,
            ChannelId = userBan.RoomId,
            CreationTime = DateTime.UtcNow
        };
    }
}