using System.Collections.Concurrent;
using TwitchLib.Client.Events;

namespace TwitchChatParser.Application.Utils;

public class MessageQueue
{
    public ConcurrentQueue<OnMessageReceivedArgs> Queue { get; } = new();
}