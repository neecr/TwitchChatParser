using System.Collections.Concurrent;
using TwitchLib.Client.Events;

namespace TwitchChatParser.Utils;

public class QueueProvider
{
    public ConcurrentQueue<OnMessageReceivedArgs> Queue { get; } = new();
}