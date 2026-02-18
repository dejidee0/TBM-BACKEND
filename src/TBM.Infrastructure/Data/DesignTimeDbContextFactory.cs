using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TBM.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var env = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Determine TBM.API project folder by using the startup assembly base directory if available.
        string? basePath = null;
        try
        {
            var baseDir = AppContext.BaseDirectory;
            // baseDir typically: ...\src\TBM.API\bin\Debug\net9.0\
            var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            if (File.Exists(Path.Combine(projectDir, "appsettings.json")))
            {
                basePath = projectDir;
            }
        }
        catch { }

        if (basePath == null)
        {
            // fallback to current directory
            basePath = Directory.GetCurrentDirectory();
        }

        // Load configuration from the resolved appsettings.json path if present
        IConfiguration configuration;
        var appsettingsPath = Path.Combine(basePath, "appsettings.json");
        if (File.Exists(appsettingsPath))
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile(appsettingsPath, optional: false)
                .AddJsonFile(Path.Combine(basePath, $"appsettings.{env}.json"), optional: true)
                .AddEnvironmentVariables();
            configuration = builder.Build();
        }
        else
        {
            // fallback to environment-only configuration
            configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Could not find a connection string named 'DefaultConnection'.");

        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
