using TwitchChatParser.Domain.Models;

namespace TwitchChatParser.Infrastructure.Repositories.Interfaces;

public interface IChannelRepository : IRepository<Channel, string>
{
    Task<List<string>> GetProcessedChannelsAsync(List<string> channelsNames, CancellationToken cancellationToken = default);
}
