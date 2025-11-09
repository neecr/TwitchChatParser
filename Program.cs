using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using TwitchChatParser.EfCore.Data;
using TwitchChatParser.HostedServices;
using TwitchChatParser.Services;
using TwitchChatParser.Utils;

namespace TwitchChatParser;

internal class Program
{
    private static async Task Main()
    {
        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.Async(wt => wt.Console())
                .WriteTo.File($"log/launch_{DateTime.Now:dd-MM-yy-HH-mm-ss}.txt")
                .CreateLogger();

            Log.Information("Starting TwitchChatParser...");

            var host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureAppConfiguration((_, config) => { config.AddJsonFile("appsettings.json"); })
                .ConfigureServices((hostContext, services) =>
                {
                    var connectionString = hostContext.Configuration["ConnectionString"] ??
                                           throw new InvalidOperationException(
                                               "ConnectionString is missing in configuration.");

                    services.AddDbContext<DataContext>(options => options.UseNpgsql(connectionString));


                    services.AddSingleton<QueueProvider>();

                    services.AddScoped<DatabaseService>();
                    services.AddScoped<TokenService>();

                    services.AddHostedService<MessageProcessingHost>();

                    services.AddHostedService(sp =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var tokenService = sp.GetRequiredService<TokenService>();
                        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                        var logger = sp.GetRequiredService<ILogger<TwitchHost>>();
                        var queueProvider = sp.GetRequiredService<QueueProvider>();

                        return new TwitchHost(config, tokenService, scopeFactory, logger, queueProvider);
                    });
                })
                .Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly.");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}