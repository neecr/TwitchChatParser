using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Infrastructure.Services;

namespace TwitchChatParser.Worker.HostedServices;

public class TokenUpdateHostedService(
    ILogger<TokenUpdateHostedService> logger,
    TwitchTokenService tokenService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TokenUpdateHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await tokenService.GetAccessTokenAsync();

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during periodic token update.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }

        logger.LogInformation("TokenUpdateHostedService stopped.");
    }
}