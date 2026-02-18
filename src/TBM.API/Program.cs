using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TBM.Application.Extensions;
using TBM.Application.Helpers;
using TBM.Application.Services;
using TBM.Infrastructure.Extensions;
using TBM.API.Swagger;
using TBM.Infrastructure.Data;
using TBM.Core.Interfaces.AI;
using TBM.Infrastructure.AI;
using TBM.Application.Interfaces.Security;
using TBM.Infrastructure.Security;
using TBM.Application.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TBM.Application.DTOs.Settings;


var builder = WebApplication.CreateBuilder(args); 

// Add services to the container    
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // ✅ FIX: Handle circular references in JSON serialization
        options.JsonSerializerOptions.ReferenceHandler = 
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        
        // Make JSON more readable (optional, can remove in production)
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TBM Digital Platform API", Version = "v1" });

    options.OperationFilter<FileUploadOperationFilter>();
    
    // Add JWT authentication to Swagger
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

var encryptionKey = builder.Configuration["Security:EncryptionKey"];

builder.Services.AddSingleton<IEncryptionService>(
    new AesEncryptionService(encryptionKey!)
);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("DynamicPolicy", context =>
    {
        var settingsManager = context.RequestServices.GetRequiredService<ISettingsManager>();
        var generalSettings = settingsManager.GetAsync<GeneralSettingsDto>("General").Result;

        var permitLimit = generalSettings?.ApiRateLimit ?? 1000;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});


builder.Services.AddMemoryCache();
builder.Services.AddScoped<ISettingsManager, SettingsManager>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<ISettingsManager, SettingsManager>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<AdminAnalyticsService>();




// Configure JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
var key = Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? throw new InvalidOperationException("JWT Secret Key not configured"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Changed to true for production
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Infrastructure Services (Database, Repositories)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Application Services (Business Logic)
builder.Services.AddApplicationServices();
builder.Services.AddHttpClient<IAIProvider, ReplicateAIProvider>();

builder.Services.AddScoped<AIService>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger in all environments (not just Development)
app.UseRateLimiter();
app.UseDeveloperExceptionPage();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TBM Digital Platform API v1");
    c.RoutePrefix = string.Empty; // Set Swagger at root URL
});

//app.UseHttpsRedirection();

app.UseCors("AllowAll"); 

app.UseAuthentication();
app.UseMiddleware<TBM.API.Middleware.MaintenanceMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Seed database
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