using System.Threading.Channels;
using TwitchLib.Client.Events;

namespace TwitchChatParser.Application.Utils;

public class MessageQueue
{
    private readonly Channel<OnMessageReceivedArgs> _queue = Channel.CreateUnbounded<OnMessageReceivedArgs>();
    private int _count;

    public int Count => _count;

    public void Enqueue(OnMessageReceivedArgs message)
    {
        if (_queue.Writer.TryWrite(message))
        {
            Interlocked.Increment(ref _count);
        }
    }

    public bool TryRead(out OnMessageReceivedArgs? message)
    {
        if (_queue.Reader.TryRead(out message))
        {
            Interlocked.Decrement(ref _count);
            return true;
        }

        message = null;
        return false;
    }
}