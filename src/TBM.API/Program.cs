using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using TBM.API.Swagger;
using TBM.Application.DTOs.Settings;
using TBM.Application.Helpers;
using TBM.Application.Interfaces;
using TBM.Application.Interfaces.Security;
using TBM.Application.Services;
using TBM.Core.Interfaces.AI;
using TBM.Infrastructure.AI;
using TBM.Infrastructure.Data;
using TBM.Infrastructure.Extensions;
using TBM.Application.Extensions;
using TBM.Infrastructure.Security;

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
        Title = "TBM Digital Platform API",
        Version = "v1"
    });

    options.OperationFilter<FileUploadOperationFilter>();

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

// ==============================
// External Services
// ==============================
builder.Services.AddHttpClient<PaystackService>();
builder.Services.AddHttpClient<IAIProvider, ReplicateAIProvider>();

builder.Services.AddScoped<AIService>();

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

// ==============================
// Rate Limiting
// ==============================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("DynamicPolicy", context =>
    {
        var settingsManager = context.RequestServices.GetRequiredService<ISettingsManager>();
        var generalSettings = settingsManager
            .GetAsync<GeneralSettingsDto>("General")
            .GetAwaiter().GetResult();

        var permitLimit = generalSettings?.ApiRateLimit ?? 1000;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ??
                          context.Connection.RemoteIpAddress?.ToString() ??
                          "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ==============================
// Middleware Pipeline
// ==============================
app.UseDeveloperExceptionPage();

app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TBM Digital Platform API v1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseMiddleware<TBM.API.Middleware.MaintenanceMiddleware>();
app.UseAuthorization();

app.MapControllers();

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

app.Run();
