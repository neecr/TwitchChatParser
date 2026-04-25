using Microsoft.EntityFrameworkCore;
using TwitchChatParser.Domain.Models;

namespace TwitchChatParser.Infrastructure.Data;

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
    public DbSet<ChannelUserRelation> ChannelUserRelations { get; set; }
    public DbSet<Ban> Bans { get; set; }
    public DbSet<Channel> Channels { get; set; }
    public DbSet<TokenInfo> TokenInfos { get; set; }
    public DbSet<FollowersInfo> FollowersInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(30);
            entity.Property(e => e.Username).HasMaxLength(25);
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(25);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.MessageText).HasMaxLength(500);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .IsRequired();

            entity.HasOne(m => m.Channel)
                .WithMany()
                .HasForeignKey(m => m.ChannelId)
                .IsRequired();
        });

        modelBuilder.Entity<ChannelUserRelation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .IsRequired();

            entity.HasOne(e => e.Channel)
                .WithMany()
                .HasForeignKey(e => e.ChannelId)
                .IsRequired();
        });

        modelBuilder.Entity<Ban>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .IsRequired();

            entity.HasOne(e => e.Channel)
                .WithMany()
                .HasForeignKey(e => e.ChannelId)
                .IsRequired();
        });

        modelBuilder.Entity<FollowersInfo>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .IsRequired();
        });
    }
}