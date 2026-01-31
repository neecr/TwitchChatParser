using System.Threading.Channels;

namespace TwitchChatParser.Utils;

public class FollowersUpdateQueue
{
    private readonly Channel<List<string>> _queue = Channel.CreateUnbounded<List<string>>();
    
    public void Enqueue(List<string> userIds)
    {
        _queue.Writer.TryWrite(userIds);
    }

    public ChannelReader<List<string>> Reader => _queue.Reader;
}