using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TBM.Core.Entities.Audit;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AuditService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _http;

    public AuditService(IUnitOfWork unitOfWork,
                        IHttpContextAccessor http)
    {
        _unitOfWork = unitOfWork;
        _http = http;
    }

    public async Task LogAsync(
        string action,
        string category,
        object? oldValue,
        object? newValue)
    {
        var userId = _http.HttpContext?
            .User?
            .FindFirst("sub")?.Value ?? "system";

        var ip = _http.HttpContext?
            .Connection?
            .RemoteIpAddress?
            .ToString();

        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            Category = category,
            OldValue = oldValue == null ? null :
                JsonSerializer.Serialize(oldValue),
            NewValue = newValue == null ? null :
                JsonSerializer.Serialize(newValue),
            IpAddress = ip,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.AuditLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();
    }
}
