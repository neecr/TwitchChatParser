using Microsoft.EntityFrameworkCore;
using TwitchChatParser.Models;

namespace TwitchChatParser.Data;

public class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
    }

    public DataContext()
    {
    }

    public DbSet<Message> Messages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ChannelUser> ChannelUsers { get; set; }
    public DbSet<TokenInfo> TokenInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .IsRequired();
        });

        modelBuilder.Entity<ChannelUser>(entity =>
        {
            entity.HasKey(channelUser => channelUser.Id);

            entity.HasOne(channelUser => channelUser.User)
                .WithMany()
                .HasForeignKey(channelUser => channelUser.UserId)
                .IsRequired();
        });

        modelBuilder.Entity<TokenInfo>(entity => { entity.HasKey(tokenInfo => tokenInfo.Id); });
    }
}