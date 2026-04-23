using TwitchChatParser.Domain.Models;

namespace TwitchChatParser.Infrastructure.Repositories.Interfaces;

public interface IBanRepository : IRepository<Ban, Guid>
{
}
