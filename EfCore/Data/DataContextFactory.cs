using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TwitchChatParser.EfCore.Data;

[UsedImplicitly]
public class DataContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
        IConfiguration config = builder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();

        optionsBuilder.UseNpgsql(config["ConnectionString"]);
        return new DataContext(optionsBuilder.Options);
    }
}