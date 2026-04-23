using Serilog;
using Serilog.Events;

namespace TwitchChatParser.Worker.Extensions;

public static class LoggerSetup
{
    public static void Configure(LoggerConfiguration loggerConfig, bool isDevelopment)
    {
        loggerConfig
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .WriteTo.Async(wt =>
                wt.Console(
                    outputTemplate: "[{Timestamp:dd-MM-yy HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .WriteTo.File($"log/launch_{DateTime.Now:dd-MM-yy-HH-mm-ss}.txt",
                outputTemplate: "[Timestamp:dd-MM-yy HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        if (isDevelopment) loggerConfig.MinimumLevel.Debug();
        else loggerConfig.MinimumLevel.Information();
    }
}