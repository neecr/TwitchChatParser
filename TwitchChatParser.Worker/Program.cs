using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using TwitchChatParser.Worker.Extensions;

try
{
    var host = Host.CreateDefaultBuilder()
        .UseSerilog((hostContext, loggerConfig) =>
        {
            LoggerSetup.Configure(loggerConfig, hostContext.HostingEnvironment.IsDevelopment());
        })
        .ConfigureAppConfiguration((_, config) => { config.AddJsonFile("appsettings.json"); })
        .ConfigureServices((hostContext, services) =>
        {
            services.AddApplicationServices(hostContext);
        }).Build();

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