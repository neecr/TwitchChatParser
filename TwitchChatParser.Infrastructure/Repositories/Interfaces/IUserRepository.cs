using TwitchChatParser.Domain.Models;

namespace TwitchChatParser.Infrastructure.Repositories.Interfaces;

public interface IUserRepository : IRepository<User, string>
{
}
