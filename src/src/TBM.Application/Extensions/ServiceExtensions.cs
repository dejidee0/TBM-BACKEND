using Microsoft.Extensions.DependencyInjection;
using TBM.Application.Helpers;
using TBM.Application.Interfaces;
using TBM.Application.Services;

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
        services.AddScoped<IPromoService, PromoService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<ImageUploadService>();
        services.AddScoped<UserDataStoreService>();
        services.AddScoped<AIGeneratedMediaService>();
        services.AddScoped<AIUsageService>();
        services.AddScoped<AICreditService>();
        services.AddScoped<VendorDomainService>();

        
        return services;
    }
}
