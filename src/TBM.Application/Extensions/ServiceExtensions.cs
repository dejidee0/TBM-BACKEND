using Microsoft.Extensions.DependencyInjection;
using TBM.Application.Helpers;
using TBM.Application.Interfaces;
using TBM.Application.Services;
using TBM.Application.Services.DesignFlow;
using TBM.Application.Services.Subscriptions;

namespace TBM.Application.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Helpers
        services.AddScoped<JwtHelper>();
        
        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IPromoService, PromoService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<PaystackWebhookService>();
        services.AddScoped<ImageUploadService>();
        services.AddScoped<UserDataStoreService>();
        services.AddScoped<AIGeneratedMediaService>();
        services.AddScoped<IAIService, AIService>();
        services.AddScoped<AIUsageService>();
        services.AddScoped<AICreditService>();
        services.AddScoped<RenovationEstimatorService>();
        services.AddScoped<AIPersonalAssistantService>();
        services.AddScoped<VendorDomainService>();
        services.AddScoped<DesignSessionService>();
        services.AddScoped<BOMGenerationService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectTimelineService>();
        services.AddScoped<PortfolioService>();

        // Subscription services
        services.AddScoped<DiscountEngine>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<PricingConfigService>();
        services.AddScoped<DiscountAdminService>();

        return services;
    }
}
