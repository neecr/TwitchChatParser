using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Application.Utils;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;
using TwitchLib.Client.Events;

namespace TwitchChatParser.Worker.HostedServices;

public class MessageProcessingHost(
    ILogger<MessageProcessingHost> logger,
    MessageQueue messageQueue,
    IServiceProvider serviceProvider,
    IConfiguration configuration)
    : BackgroundService
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("MessageProcessingHost started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _timer.WaitForNextTickAsync(stoppingToken);

                if (messageQueue.Count == 0) continue;

                if (_counter != messageQueue.Count)
                {
                    logger.LogDebug("Messages in queue: {Count}.", messageQueue.Count);
                    _counter = messageQueue.Count;
                }

                if (messageQueue.Count >= _buffer)
                {
                    using var scope = serviceProvider.CreateScope();
                    var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

                    var messagesToProcess = new List<OnMessageReceivedArgs>();
                    while (messageQueue.TryRead(out var message) && messagesToProcess.Count < _buffer)
                    {
                        messagesToProcess.Add(message);
                    }

                    if (messagesToProcess.Count > 0)
                    {
                        await messageRepository.WriteBatchAsync(messagesToProcess);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message queue.");
            }
        }

        logger.LogDebug("MessageProcessingHost stopped.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Finishing saving messages...");

        if (messageQueue.Count > 0)
        {
            using var scope = serviceProvider.CreateScope();
            var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            var messagesToProcess = new List<OnMessageReceivedArgs>();
            while (messageQueue.TryRead(out var message))
            {
                messagesToProcess.Add(message);
            }

            if (messagesToProcess.Count > 0)
            {
                await messageRepository.WriteBatchAsync(messagesToProcess);
            }
        }

        await base.StopAsync(cancellationToken);
    }
}