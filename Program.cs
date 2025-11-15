using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.Async(wt => wt.Console())
                .WriteTo.File($"log/launch_{DateTime.Now:dd-MM-yy-HH-mm-ss}.txt");

#if DEBUG
            loggerConfig.MinimumLevel.Debug();
#else
            loggerConfig.MinimumLevel.Information();
#endif
            Log.Logger = loggerConfig.CreateLogger();

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

                    services.AddHostedService<TwitchHost>();
                }).Build();


            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly.");
        }
        finally
        {
            Log.Information("Application stopped.");
            await Log.CloseAndFlushAsync();
        }
    }
}