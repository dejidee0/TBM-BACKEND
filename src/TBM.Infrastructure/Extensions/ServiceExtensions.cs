using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Services;
using TBM.Infrastructure.Configuration;
using TBM.Infrastructure.Data;
using TBM.Infrastructure.Repositories;
using TBM.Infrastructure.Services;
using TBM.Infrastructure.Storage;



        using TBM.Core.Interfaces.Repositories.AI;
using TBM.Infrastructure.Repositories.AI;
using TBM.Application.Services;

namespace TBM.Infrastructure.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // SMTP Email
        services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));
        services.AddScoped<IEmailService, SmtpEmailService>();

        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null
                )
            )
        );

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ISettingRepository, SettingRepository>();


        // UnitOfWork
        services.AddScoped<IUnitOfWork, UnitOfWork>();


// AI repositories
services.AddScoped<IAIProjectRepository, AIProjectRepository>();
services.AddScoped<IAIDesignRepository, AIDesignRepository>();

// Cloudinary
services.Configure<CloudinarySettings>(configuration.GetSection("Cloudinary"));
services.AddScoped<IImageStorageService, CloudinaryStorageService>();


// AI Services
services.Configure<ReplicateSettings>(
    configuration.GetSection("AI:Replicate"));

    



        return services;
    }
}
