using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TwitchChatParser.Application.Utils;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Services;
using TwitchChatParser.Worker.HostedServices;

namespace TwitchChatParser.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        HostBuilderContext hostContext)
    {
        string connectionString;
        if (hostContext.HostingEnvironment.IsDevelopment())
        {
            connectionString = hostContext.Configuration["ConnectionStrings:Debug"] ??
                               throw new InvalidOperationException(
                                   "Debug ConnectionString is missing in configuration.");
            Log.Information("Configuration: DEBUG");
        }
        else
        {
            connectionString = hostContext.Configuration["ConnectionStrings:Release"] ??
                               throw new InvalidOperationException(
                                   "Release ConnectionString is missing in configuration.");
            Log.Information("Configuration: RELEASE");
        }

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

        return services;
    }
}