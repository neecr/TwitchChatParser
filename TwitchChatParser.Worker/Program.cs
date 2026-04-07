using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using TwitchChatParser.Application.Utils;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Services;
using TwitchChatParser.Worker.HostedServices;

namespace TwitchChatParser.Worker;

internal class Program
{
    private static async Task Main()
    {
        try
        {
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
                .WriteTo.Async(wt =>
                    wt.Console(
                        outputTemplate: "[{Timestamp:dd-MM-yy HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .WriteTo.File($"log/launch_{DateTime.Now:dd-MM-yy-HH-mm-ss}.txt",
                    outputTemplate: "[Timestamp:dd-MM-yy HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

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
#if DEBUG
                    var connectionString = hostContext.Configuration["ConnectionStrings:Debug"] ??
                                           throw new InvalidOperationException(
                                               "Debug ConnectionString is missing in configuration.");
                    Log.Information("Configuration: DEBUG");
#else
                    var connectionString = hostContext.Configuration["ConnectionStrings:Release"] ??
                                           throw new InvalidOperationException(
                                               "Release ConnectionString is missing in configuration.");
                    Log.Information("Configuration: RELEASE");
#endif

                    services.AddDbContext<DataContext>(options => options.UseNpgsql(connectionString));

                    services.AddSingleton<FollowersQueue>();
                    services.AddSingleton<MessageQueue>();

                    services.AddScoped<DatabaseService>();
                    services.AddScoped<TwitchTokenService>();
                    services.AddScoped<TwitchApiService>();

                    services.AddHttpClient<TwitchTokenService>();
                    services.AddHttpClient<TwitchApiService>();

                    services.AddHostedService<MessageProcessingHost>();
                    services.AddHostedService<FollowersUpdateHostedService>();
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