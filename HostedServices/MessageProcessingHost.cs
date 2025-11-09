using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Services;
using TwitchChatParser.Utils;

namespace TwitchChatParser.HostedServices;

public class MessageProcessingHost(
    ILogger<MessageProcessingHost> logger,
    QueueProvider queueProvider,
    IServiceProvider serviceProvider)
    : BackgroundService
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(5));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            if (queueProvider.Queue.IsEmpty) continue;

            logger.LogInformation("Messages in queue: {Count}.", queueProvider.Queue.Count);

            if (queueProvider.Queue.Count >= 500)
            {
                using var scope = serviceProvider.CreateScope();
                var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                var messagesToProcess = queueProvider.Queue.ToList();
                await databaseService.WriteBatchAsync(messagesToProcess);
                queueProvider.Queue.Clear();
            }
        }
    }
}