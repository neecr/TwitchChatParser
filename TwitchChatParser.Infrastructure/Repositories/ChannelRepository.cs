using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;
using TwitchChatParser.Infrastructure.Services;

namespace TwitchChatParser.Infrastructure.Repositories;

public class ChannelRepository(
    DataContext context,
    ILogger<ChannelRepository> logger,
    TwitchApiService twitchApiService) : BaseRepository<Channel, string>(context), IChannelRepository
{
    public async Task<List<string>> GetProcessedChannelsAsync(List<string> channelsNames, CancellationToken cancellationToken = default)
    {
        var existingChannelNames = await _dbSet
            .Where(c => channelsNames.Contains(c.Name))
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        var processedNames = new List<string>(existingChannelNames);
        var candidatesForApiLookup = channelsNames.Except(existingChannelNames).ToList();

        if (candidatesForApiLookup.Count == 0) return processedNames;

        var newChannelsUserData = await twitchApiService.GetUserDataByUsernameAsync(candidatesForApiLookup);

        var foundNamesByApi = newChannelsUserData
            .Select(userData => userData.DisplayName)
            .ToList();

        var nonExistentNames = candidatesForApiLookup
            .Except(foundNamesByApi, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (nonExistentNames.Count > 0)
            foreach (var name in nonExistentNames)
                logger.LogWarning("Channel {channel} doesn't exist.", name);

        var channelsToAdd = newChannelsUserData
            .Select(userData => new Channel
            {
                Id = userData.Id,
                Name = userData.Login
            })
            .ToList();

        if (channelsToAdd.Count > 0)
        {
            await _dbSet.AddRangeAsync(channelsToAdd, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            processedNames.AddRange(channelsToAdd.Select(c => c.Name));
        }

        return processedNames;
    }
}
