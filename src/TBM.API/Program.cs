using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using TBM.API.Middleware;
using TBM.API.Observability;
using TBM.API.Swagger;
using TBM.Application.DTOs.Settings;
using TBM.Application.Helpers;
using TBM.Application.Interfaces;
using TBM.Application.Interfaces.Security;
using TBM.Application.Services;
using TBM.Application.Services.DesignFlow;
using TBM.Application.Services.Subscriptions;
using TBM.Core.Interfaces.AI;
using TBM.Infrastructure.AI;
using TBM.Infrastructure.Data;
using TBM.Infrastructure.Extensions;
using TBM.Application.Extensions;
using TBM.Infrastructure.Security;
using TBM.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ==============================
// Controllers + JSON
// ==============================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();

// ==============================
// Swagger
// ==============================
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TBM BUILDING AI VISUALIZER API",
        Version = "v1",
        Description = "AI design and construction platform endpoints for TBM."
    });

    options.OperationFilter<FileUploadOperationFilter>();

    // Tell Swashbuckle how to represent IFormFile — without this, any DTO that has
    // an IFormFile property (e.g. UploadAvatarRequest) causes a schema-generation 500.
    options.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });

    // Guard against duplicate schema IDs (e.g., same class names in different namespaces)
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    
    // If multiple actions resolve to the same route/method, pick the first to avoid swagger generation 500s
    options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ==============================
// Infrastructure (Db + Repos)
// ==============================
builder.Services.AddInfrastructureServices(builder.Configuration);

// ==============================
// Application Layer
// ==============================
builder.Services.AddApplicationServices();

// ==============================
// Admin Services
// ==============================
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<AdminOrderService>();
builder.Services.AddScoped<AdminAnalyticsService>();
builder.Services.AddScoped<AdminSettingsService>();
builder.Services.AddScoped<AdminDashboardService>();
builder.Services.AddScoped<AdminSystemLogService>();
builder.Services.AddScoped<AdminFinancialService>();

// ==============================
// External Services
// ==============================
builder.Services.AddHttpClient<PaystackService>();
builder.Services.AddHttpClient<IAIProvider, ReplicateAIProvider>();
builder.Services.Configure<AssistantLlmSettings>(builder.Configuration.GetSection("AI:Assistant"));
builder.Services.Configure<TBM.Application.Configuration.AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.AddHttpClient<IAssistantLlmClient, OpenAIAssistantLlmClient>();
builder.Services.AddHostedService<AIGenerationBackgroundService>();
builder.Services.AddHostedService<SubscriptionExpiryJob>();
builder.Services.AddHostedService<DiscountCleanupJob>();

// ==============================
// Security
// ==============================
var encryptionKey = builder.Configuration["Security:EncryptionKey"]
    ?? throw new InvalidOperationException("Encryption key not configured");

builder.Services.AddSingleton<IEncryptionService>(
    new AesEncryptionService(encryptionKey)
);

// ==============================
// Settings + Cache
// ==============================
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ISettingsManager, SettingsManager>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddSingleton<IEndpointMetricsStore, InMemoryEndpointMetricsStore>();

// ==============================
// Rate Limiting
// ==============================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var requestId = context.HttpContext.TraceIdentifier;
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiter");

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.HttpContext.Response.Headers["Retry-After"] =
                seconds.ToString(CultureInfo.InvariantCulture);
        }

        logger.LogWarning(
            "Rate limit exceeded. RequestId={RequestId} Method={Method} Path={Path} Client={Client}",
            requestId,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path.Value,
            ResolveClientIdentifier(context.HttpContext));

        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                JsonSerializer.Serialize(new
                {
                    success = false,
                    error = new
                    {
                        code = "RATE_LIMIT_EXCEEDED",
                        message = "Too many requests. Please retry later.",
                        requestId
                    }
                }),
                token);
        }
    };

    options.AddPolicy("DynamicPolicy", context =>
    {
        var defaultLimit = 600;
        try
        {
            var settingsManager = context.RequestServices.GetRequiredService<ISettingsManager>();
            var generalSettings = settingsManager
                .GetAsync<GeneralSettingsDto>("General")
                .GetAwaiter().GetResult();
            defaultLimit = Math.Clamp(generalSettings?.ApiRateLimit ?? defaultLimit, 60, 5000);
        }
        catch
        {
            defaultLimit = 600;
        }

        var profile = ResolveRateLimitProfile(context.Request.Path, defaultLimit);
        var partitionIdentity = profile.Scope switch
        {
            RateLimitPartitionScope.Client => ResolveClientIdentifier(context),
            _ => ResolveUserOrClientIdentifier(context)
        };

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{profile.Name}:{partitionIdentity}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = profile.PermitLimit,
                Window = profile.Window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.AddPolicy("WebhookPolicy", context =>
    {
        var client = ResolveClientIdentifier(context);
        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: $"webhook:{client}",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 240,
                TokensPerPeriod = 240,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// ==============================
// JWT Configuration
// ==============================
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings not configured");

var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ==============================
// CORS
// ==============================
const string CorsPolicyName = "AllowConfiguredOrigins";

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? Array.Empty<string>();

if (allowedCorsOrigins.Length == 0)
{
    throw new InvalidOperationException("CORS origins are not configured. Set Cors:AllowedOrigins.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ==============================
// Middleware Pipeline
// ==============================
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exFeature = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = exFeature?.Error;

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        object responseBody = app.Environment.IsDevelopment()
            ? new
            {
                success = false,
                message = "Internal server error.",
                error = ex?.GetType().Name,
                detail = ex?.Message,
                inner = ex?.InnerException?.Message,
                stackTrace = ex?.StackTrace
            }
            : (object)new
            {
                success = false,
                message = "An unexpected error occurred. Please try again.",
                error = ex?.GetType().Name,
                detail = ex?.Message
            };

        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(responseBody));
    });
});

app.UseMiddleware<RequestContextMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseRateLimiter();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TBM BUILDING AI VISUALIZER API v1");
    c.DocumentTitle = "TBM BUILDING AI VISUALIZER API";
    c.InjectStylesheet("/swagger-ui/custom.css");
    c.InjectJavascript("/swagger-ui/custom.js");
    c.DisplayRequestDuration();
    c.DefaultModelsExpandDepth(-1);
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    c.RoutePrefix = string.Empty;
});

app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseMiddleware<TBM.API.Middleware.MaintenanceMiddleware>();
app.UseAuthorization();

app.MapControllers();

await ApplyDatabaseMigrationsAsync(app);

// ==============================
// Seed DB (Dev only)
// ==============================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    try
    {
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await DbInitializer.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database seeding failed");
    }
}

// Seed default pricing configs (all environments)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var pricingService = scope.ServiceProvider.GetRequiredService<PricingConfigService>();
        await pricingService.SeedDefaultsAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Pricing config seeding failed");
    }
}

app.Run();

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    if (app.Environment.IsEnvironment("Testing"))
    {
        return;
    }

    var applyMigrations = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);
    if (!applyMigrations)
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

        if (pendingMigrations.Any())
        {
            logger.LogInformation(
                "Applying {Count} pending database migration(s): {Migrations}",
                pendingMigrations.Count(),
                string.Join(", ", pendingMigrations));

            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply database migrations on startup.");
        throw;
    }
}

static RateLimitProfile ResolveRateLimitProfile(PathString path, int defaultLimit)
{
    var value = path.Value?.ToLowerInvariant() ?? string.Empty;

    if (value.Contains("/auth/login") ||
        value.Contains("/auth/register") ||
        value.Contains("/auth/forgot-password") ||
        value.Contains("/auth/reset-password") ||
        value.Contains("/auth/verify-email") ||
        value.Contains("/auth/resend-verification") ||
        value.Contains("/auth/refresh") ||
        value.Contains("/auth/google") ||
        value.Contains("/auth/apple"))
    {
        return new RateLimitProfile(
            Name: "auth-sensitive",
            PermitLimit: 12,
            Window: TimeSpan.FromMinutes(1),
            Scope: RateLimitPartitionScope.Client);
    }

    if (value.Contains("/checkout/payment"))
    {
        return new RateLimitProfile(
            Name: "checkout-payment",
            PermitLimit: 20,
            Window: TimeSpan.FromMinutes(5),
            Scope: RateLimitPartitionScope.UserOrClient);
    }

    if (value.Contains("/api/v1/ai/generate") ||
        value.Contains("/api/v1/ai/transform") ||
        value.Contains("/api/v1/ai/upload-room"))
    {
        return new RateLimitProfile(
            Name: "ai-generation",
            PermitLimit: 15,
            Window: TimeSpan.FromMinutes(5),
            Scope: RateLimitPartitionScope.UserOrClient);
    }

    if (value.StartsWith("/api/admin", StringComparison.Ordinal))
    {
        return new RateLimitProfile(
            Name: "admin",
            PermitLimit: Math.Min(defaultLimit, 300),
            Window: TimeSpan.FromMinutes(1),
            Scope: RateLimitPartitionScope.UserOrClient);
    }

    return new RateLimitProfile(
        Name: "default",
        PermitLimit: defaultLimit,
        Window: TimeSpan.FromMinutes(1),
        Scope: RateLimitPartitionScope.UserOrClient);
}

static string ResolveUserOrClientIdentifier(HttpContext context)
{
    var userId =
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.User.Identity?.Name;

    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId.Trim()}";
    }

    return $"client:{ResolveClientIdentifier(context)}";
}

static string ResolveClientIdentifier(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        var first = forwardedFor.Split(',')[0].Trim();
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}

internal enum RateLimitPartitionScope
{
    UserOrClient = 0,
    Client = 1
}

internal sealed record RateLimitProfile(
    string Name,
    int PermitLimit,
    TimeSpan Window,
    RateLimitPartitionScope Scope);

public partial class Program { }
