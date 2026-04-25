using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using TwitchChatParser.Worker.Extensions;

// ReSharper disable once RedundantAssignment
bool isDebug = false;

#if DEBUG
isDebug = true;
#endif

try
{
    var host = Host.CreateDefaultBuilder()
        .UseSerilog((_, loggerConfig) =>
        {
            LoggerSetup.Configure(loggerConfig, isDebug);
        })
        .ConfigureAppConfiguration((_, config) => { config.AddJsonFile("appsettings.json"); })
        .ConfigureServices((hostContext, services) => { services.AddApplicationServices(hostContext); }).Build();

    // ReSharper disable once HeuristicUnreachableCode
    Log.Information("Configuration: {IsDebug}", isDebug ? "Debug" : "Production");
    Log.Information("Starting TwitchChatParser...");
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