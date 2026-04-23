using TwitchChatParser.Domain.Models;
using TwitchLib.Client.Events;

namespace TwitchChatParser.Infrastructure.Repositories.Interfaces;

public interface IMessageRepository : IRepository<Message, string>
{
    Task WriteBatchAsync(IReadOnlyCollection<OnMessageReceivedArgs> messages, CancellationToken cancellationToken = default);
}
