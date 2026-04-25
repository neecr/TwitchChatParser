using TwitchChatParser.Domain.Models;

namespace TwitchChatParser.Infrastructure.Repositories.Interfaces;

public interface ITokenInfoRepository : IRepository<TokenInfo, Guid>
{
    Task<TokenInfo?> GetLatestTokenAsync(CancellationToken cancellationToken = default);
}
