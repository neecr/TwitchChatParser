using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Services;
using TwitchChatParser.Utils;

namespace TwitchChatParser.HostedServices;

public class MessageProcessingHost(
    ILogger<MessageProcessingHost> logger,
    QueueProvider queueProvider,
    IServiceProvider serviceProvider,
    IConfiguration configuration)
    : IHostedService
{
    private readonly int _buffer =
        int.Parse(configuration["MessageProcessingSettings:Buffer"] ??
                  throw new InvalidOperationException("Buffer is missing in configuration."));

    private readonly PeriodicTimer _timer =
        new(TimeSpan.FromSeconds(int.Parse(configuration
                                               ["MessageProcessingSettings:Interval"] ??
                                           throw new InvalidOperationException(
                                               "Interval is missing in configuration."))));

    private int _counter;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                if (queueProvider.Queue.IsEmpty) continue;

                if (_counter != queueProvider.Queue.Count)
                {
                    logger.LogDebug("Messages in queue: {Count}.", queueProvider.Queue.Count);
                    _counter = queueProvider.Queue.Count;
                }

                if (queueProvider.Queue.Count >= _buffer)
                {
                    using var scope = serviceProvider.CreateScope();
                    var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                    var messagesToProcess = queueProvider.Queue.ToList();
                    await databaseService.WriteBatchAsync(messagesToProcess);
                    queueProvider.Queue.Clear();
                }
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Finishing saving messages...");

        if (!queueProvider.Queue.IsEmpty)
        {
            using var scope = serviceProvider.CreateScope();
            var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

            var messagesToProcess = queueProvider.Queue.ToList();
            await databaseService.WriteBatchAsync(messagesToProcess);
        }
    }
}