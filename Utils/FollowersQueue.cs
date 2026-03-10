using System.Threading.Channels;

namespace TwitchChatParser.Utils;

public class FollowersQueue
{
    private readonly Channel<List<string>> _queue = Channel.CreateUnbounded<List<string>>();
    private int _count;

    public int Count => _count;

    public void Enqueue(List<string> userIds)
    {
        if (_queue.Writer.TryWrite(userIds))
        {
            Interlocked.Increment(ref _count);
        }
    }

    public async ValueTask<List<string>> ReadAsync(CancellationToken cancellationToken)
    {
        var item = await _queue.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _count);
        return item;
    }

    public bool TryRead(out List<string>? userIds)
    {
        if (_queue.Reader.TryRead(out userIds))
        {
            Interlocked.Decrement(ref _count);
            return true;
        }

        userIds = null;
        return false;
    }
}