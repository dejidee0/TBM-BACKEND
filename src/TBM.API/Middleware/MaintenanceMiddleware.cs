using System.Text.Json;
using TBM.Application.DTOs.Settings;
using TBM.Application.Interfaces;

namespace TBM.API.Middleware;

public class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISettingsManager settingsManager)
    {
        // Allow admin & health endpoints always
        var path = context.Request.Path.Value?.ToLower();

        if (path != null &&
            (path.StartsWith("/api/admin") ||
             path.StartsWith("/health") ||
             path.StartsWith("/swagger")))
        {
            await _next(context);
            return;
        }

        var generalSettings = await settingsManager.GetAsync<GeneralSettingsDto>("General");

        if (generalSettings?.MaintenanceMode == true)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                error = new
                {
                    code = "MAINTENANCE_MODE",
                    message = "Platform is temporarily under maintenance."
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }

        await _next(context);
    }
}
