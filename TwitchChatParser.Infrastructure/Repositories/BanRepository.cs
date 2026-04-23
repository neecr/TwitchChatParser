using TwitchChatParser.Domain.Models;
using TwitchChatParser.Infrastructure.Data;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;

namespace TwitchChatParser.Infrastructure.Repositories;

public class BanRepository(DataContext context, IUserRepository userRepository)
    : BaseRepository<Ban, Guid>(context), IBanRepository
{
    public async Task<Ban> AddAsync(Ban entity, string username, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(entity.UserId, cancellationToken);
    
        if (user == null)
        {
            user = new User
            {
                Id = entity.UserId,
                Username = username,
                CreationTime = DateTime.UtcNow
            };
        
            await userRepository.AddAsync(user, cancellationToken);
            await userRepository.SaveChangesAsync(cancellationToken);
        }

        return await base.AddAsync(entity, cancellationToken);
    }
}