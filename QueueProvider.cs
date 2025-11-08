using System.Collections.Concurrent;
using TwitchLib.Client.Events;

namespace TwitchChatParser;

public class QueueProvider
{
    public ConcurrentQueue<OnMessageReceivedArgs> Queue { get; } = new();
}