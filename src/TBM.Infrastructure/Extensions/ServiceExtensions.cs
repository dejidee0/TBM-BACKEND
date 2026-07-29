using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Repositories.DesignFlow;
using TBM.Core.Interfaces.Repositories.Subscriptions;
using TBM.Core.Interfaces.Services;
using TBM.Infrastructure.Configuration;
using TBM.Infrastructure.Data;
using TBM.Infrastructure.Repositories;
using TBM.Infrastructure.Repositories.DesignFlow;
using TBM.Infrastructure.Repositories.Subscriptions;
using TBM.Infrastructure.Services;
using TBM.Infrastructure.Storage;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Infrastructure.Repositories.AI;
using TBM.Application.Services;
using TBM.Application.Interfaces;
using TBM.Infrastructure.AI;

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
        services.AddScoped<IUserAddressRepository, UserAddressRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<IProductVariantRepository, ProductVariantRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOrderStatusHistoryRepository, OrderStatusHistoryRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IContactMessageRepository, ContactMessageRepository>();
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.AddScoped<IDesignSessionRepository, DesignSessionRepository>();
        services.AddScoped<IBillOfMaterialsRepository, BillOfMaterialsRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IInspectionRequestRepository, InspectionRequestRepository>();
        services.AddScoped<IProjectRequestRepository, ProjectRequestRepository>();
        services.AddScoped<IInspirationDesignRepository, InspirationDesignRepository>();


        // Subscription repositories
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IPricingConfigRepository, PricingConfigRepository>();
        services.AddScoped<IDiscountRepository, DiscountRepository>();

        // Portfolio repository
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();

        // UnitOfWork
        services.AddScoped<IUnitOfWork, UnitOfWork>();


// AI repositories
services.AddScoped<IAIProjectRepository, AIProjectRepository>();
services.AddScoped<IAIDesignRepository, AIDesignRepository>();
services.AddScoped<IAIUsageRepository, AIUsageRepository>();
services.AddScoped<IAIRenovationEstimateRepository, AIRenovationEstimateRepository>();
services.AddScoped<IAIAssistantRepository, AIAssistantRepository>();
        services.AddHttpClient<IBomGenerationClient, OpenAiBomGenerationClient>();

// Cloudinary
services.Configure<CloudinarySettings>(configuration.GetSection("Cloudinary"));
services.AddScoped<IImageStorageService, CloudinaryStorageService>();

services.Configure<ReplicateSettings>(
    configuration.GetSection("AI:Replicate"));

    



        return services;
    }
}
