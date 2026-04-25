using Microsoft.EntityFrameworkCore;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;

namespace TwitchChatParser.Infrastructure.Repositories;

public class TokenInfoRepository(DataContext context) : BaseRepository<TokenInfo, Guid>(context), ITokenInfoRepository
{
    public async Task<TokenInfo?> GetLatestTokenAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .OrderByDescending(t => t.CreationTime)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
