using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;

namespace TwitchChatParser.Infrastructure.Repositories;

public class UserRepository(DataContext context) : BaseRepository<User, string>(context), IUserRepository;
