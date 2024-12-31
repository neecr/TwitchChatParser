using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.FullName)
            .AddJsonFile("appsettings.json")
            .Build();

        if (!optionsBuilder.IsConfigured) optionsBuilder.UseNpgsql(configuration["ConnectionStrings:DefaultDB"]);
    }

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
    }
}