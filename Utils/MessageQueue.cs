using System.Collections.Concurrent;
using TwitchLib.Client.Events;

namespace TwitchChatParser.Utils;

public class MessageQueue
{
    public ConcurrentQueue<OnMessageReceivedArgs> Queue { get; } = new();
}