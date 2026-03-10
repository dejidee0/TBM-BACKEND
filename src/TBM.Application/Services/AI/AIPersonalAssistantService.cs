using System.Text.Json;
using Microsoft.Extensions.Logging;
using TBM.Application.DTOs.AI;
using TBM.Application.Interfaces;
using TBM.Core.Entities.AI;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AIPersonalAssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAssistantLlmClient _llmClient;
    private readonly RenovationEstimatorService _renovationEstimatorService;
    private readonly ILogger<AIPersonalAssistantService> _logger;

    public AIPersonalAssistantService(
        IUnitOfWork unitOfWork,
        IAssistantLlmClient llmClient,
        RenovationEstimatorService renovationEstimatorService,
        ILogger<AIPersonalAssistantService> logger)
    {
        _unitOfWork = unitOfWork;
        _llmClient = llmClient;
        _renovationEstimatorService = renovationEstimatorService;
        _logger = logger;
    }

    public async Task<AssistantMessageResponseDto> HandleMessageAsync(Guid userId, AssistantMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new InvalidOperationException("Message is required.");
        }

        var session = await ResolveSessionAsync(userId, request.SessionId, request.Message);
        var userMessage = new AIAssistantMessage
        {
            SessionId = session.Id,
            Role = AIAssistantMessageRole.User,
            Content = request.Message.Trim()
        };
        await _unitOfWork.AIAssistant.AddMessageAsync(userMessage);

        var response = await BuildAssistantResponseAsync(session, request.Message.Trim(), request.EnableToolPlanning);
        var assistantMessage = new AIAssistantMessage
        {
            SessionId = session.Id,
            Role = AIAssistantMessageRole.Assistant,
            Content = response.Reply,
            Intent = NormalizeIntent(response.Intent),
            LinksJson = JsonSerializer.Serialize(response.Links, JsonOptions)
        };
        await _unitOfWork.AIAssistant.AddMessageAsync(assistantMessage);

        var actions = new List<AIAssistantToolAction>();
        foreach (var plan in response.ToolActions)
        {
            var method = NormalizeMethod(plan.ActionMethod);
            var action = new AIAssistantToolAction
            {
                SessionId = session.Id,
                MessageId = assistantMessage.Id,
                Name = NormalizeActionName(plan.Name, method, plan.ActionUrl),
                Description = plan.Description,
                ActionUrl = NormalizeUrl(plan.ActionUrl),
                ActionMethod = method,
                PayloadJson = NormalizePayload(plan.PayloadJson),
                RequiresApproval = plan.RequiresApproval || IsWriteMethod(method),
                Status = plan.RequiresApproval || IsWriteMethod(method)
                    ? AIAssistantToolActionStatus.PendingApproval
                    : AIAssistantToolActionStatus.Ready
            };
            await _unitOfWork.AIAssistant.AddToolActionAsync(action);
            actions.Add(action);
        }

        var tasks = new List<AIAssistantTask>();
        foreach (var task in response.Tasks)
        {
            var action = actions.FirstOrDefault(x =>
                string.Equals(x.ActionUrl, NormalizeUrl(task.ActionUrl), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ActionMethod, NormalizeMethod(task.ActionMethod), StringComparison.OrdinalIgnoreCase));
            var requiresApproval = task.RequiresApproval || action?.RequiresApproval == true;
            var entity = new AIAssistantTask
            {
                SessionId = session.Id,
                Title = task.Title,
                Description = task.Description,
                Status = requiresApproval ? AIAssistantTaskStatus.AwaitingApproval : AIAssistantTaskStatus.Pending,
                ActionUrl = NormalizeUrl(task.ActionUrl),
                ActionMethod = NormalizeMethod(task.ActionMethod),
                RequiresApproval = requiresApproval,
                ToolActionId = action?.Id,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _unitOfWork.AIAssistant.AddTaskAsync(entity);
            tasks.Add(entity);
        }

        session.LastUpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return new AssistantMessageResponseDto
        {
            SessionId = session.Id,
            UserMessageId = userMessage.Id,
            AssistantMessageId = assistantMessage.Id,
            CreatedAtUtc = DateTime.UtcNow,
            Intent = NormalizeIntent(response.Intent),
            AssistantReply = response.Reply,
            Links = response.Links,
            SuggestedTasks = tasks.Select(MapTask).ToList(),
            ToolActions = actions.Select(MapAction).ToList()
        };
    }

    public async Task<List<AssistantSessionSummaryDto>> GetSessionsAsync(Guid userId)
    {
        var sessions = await _unitOfWork.AIAssistant.GetSessionsAsync(userId);
        return sessions.Select(s => new AssistantSessionSummaryDto
        {
            SessionId = s.Id,
            Title = s.Title,
            CreatedAtUtc = s.CreatedAt,
            LastUpdatedAtUtc = s.LastUpdatedAtUtc,
            MessageCount = s.Messages.Count(m => !m.IsDeleted),
            PendingTaskCount = s.Tasks.Count(t => !t.IsDeleted && t.Status != AIAssistantTaskStatus.Completed && t.Status != AIAssistantTaskStatus.Cancelled)
        }).ToList();
    }

    public async Task<AssistantSessionDetailsDto?> GetSessionAsync(Guid userId, Guid sessionId)
    {
        var session = await _unitOfWork.AIAssistant.GetSessionAsync(sessionId, userId);
        if (session == null)
        {
            return null;
        }

        return new AssistantSessionDetailsDto
        {
            SessionId = session.Id,
            Title = session.Title,
            CreatedAtUtc = session.CreatedAt,
            LastUpdatedAtUtc = session.LastUpdatedAtUtc,
            Messages = session.Messages.Where(m => !m.IsDeleted).OrderBy(m => m.CreatedAt).Select(MapMessage).ToList(),
            Tasks = session.Tasks.Where(t => !t.IsDeleted).OrderByDescending(t => t.CreatedAt).Select(MapTask).ToList(),
            ToolActions = session.ToolActions.Where(a => !a.IsDeleted).OrderByDescending(a => a.CreatedAt).Select(MapAction).ToList()
        };
    }

    public async Task<AssistantTaskDto?> UpdateTaskStatusAsync(Guid userId, Guid taskId, string status)
    {
        var task = await _unitOfWork.AIAssistant.GetTaskAsync(taskId, userId);
        if (task == null)
        {
            return null;
        }

        task.Status = ParseTaskStatus(status);
        task.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        return MapTask(task);
    }

    public async Task<AssistantToolActionDto?> GetToolActionAsync(Guid userId, Guid actionId)
    {
        var action = await _unitOfWork.AIAssistant.GetToolActionAsync(actionId, userId);
        return action == null ? null : MapAction(action);
    }

    public async Task<AssistantToolActionDto?> ApproveToolActionAsync(Guid userId, Guid actionId, bool approve, string? reason)
    {
        var action = await _unitOfWork.AIAssistant.GetToolActionAsync(actionId, userId);
        if (action == null)
        {
            return null;
        }

        var approval = new AIAssistantToolApproval
        {
            ToolActionId = action.Id,
            UserId = userId,
            Status = approve ? AIAssistantApprovalStatus.Approved : AIAssistantApprovalStatus.Rejected,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ReviewedAtUtc = DateTime.UtcNow
        };
        await _unitOfWork.AIAssistant.AddToolApprovalAsync(approval);

        action.Status = approve ? AIAssistantToolActionStatus.Approved : AIAssistantToolActionStatus.Rejected;
        foreach (var task in action.Session.Tasks.Where(t => !t.IsDeleted && t.ToolActionId == action.Id))
        {
            task.Status = approve ? AIAssistantTaskStatus.Pending : AIAssistantTaskStatus.Cancelled;
            task.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync();
        return MapAction(action);
    }

    public async Task<AssistantToolExecutionDto?> ExecuteToolActionAsync(Guid userId, Guid actionId, bool dryRun)
    {
        var action = await _unitOfWork.AIAssistant.GetToolActionAsync(actionId, userId);
        if (action == null)
        {
            return null;
        }

        if (action.RequiresApproval && !IsApproved(action, userId))
        {
            var blocked = new AIAssistantToolExecution
            {
                ToolActionId = action.Id,
                ExecutedByUserId = userId,
                Status = AIAssistantToolExecutionStatus.Blocked,
                ErrorMessage = "Approval is required before execution.",
                ExecutedAtUtc = DateTime.UtcNow
            };
            await _unitOfWork.AIAssistant.AddToolExecutionAsync(blocked);
            await _unitOfWork.SaveChangesAsync();
            return MapExecution(blocked);
        }

        if (dryRun)
        {
            return new AssistantToolExecutionDto
            {
                ExecutionId = Guid.Empty,
                ActionId = action.Id,
                Status = "dry_run",
                ResultJson = JsonSerializer.Serialize(new { action = action.Name, action.ActionMethod, action.ActionUrl }, JsonOptions),
                ExecutedAtUtc = DateTime.UtcNow
            };
        }

        var execution = new AIAssistantToolExecution
        {
            ToolActionId = action.Id,
            ExecutedByUserId = userId,
            ExecutedAtUtc = DateTime.UtcNow
        };

        try
        {
            var result = await ExecuteCoreAsync(userId, action);
            execution.Status = AIAssistantToolExecutionStatus.Succeeded;
            execution.ResultJson = JsonSerializer.Serialize(result, JsonOptions);
            action.Status = AIAssistantToolActionStatus.Executed;
            foreach (var task in action.Session.Tasks.Where(t => !t.IsDeleted && t.ToolActionId == action.Id))
            {
                task.Status = AIAssistantTaskStatus.Completed;
                task.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Assistant action execution failed. ActionId={ActionId}", action.Id);
            execution.Status = AIAssistantToolExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            action.Status = AIAssistantToolActionStatus.Failed;
        }

        await _unitOfWork.AIAssistant.AddToolExecutionAsync(execution);
        await _unitOfWork.SaveChangesAsync();
        return MapExecution(execution);
    }

    private async Task<AIAssistantSession> ResolveSessionAsync(Guid userId, Guid? sessionId, string message)
    {
        if (sessionId.HasValue)
        {
            var existing = await _unitOfWork.AIAssistant.GetSessionAsync(sessionId.Value, userId);
            if (existing == null) throw new InvalidOperationException("Session not found.");
            return existing;
        }

        var session = new AIAssistantSession
        {
            UserId = userId,
            Title = message.Trim().Length <= 60 ? message.Trim() : $"{message.Trim()[..60]}...",
            LastUpdatedAtUtc = DateTime.UtcNow
        };
        await _unitOfWork.AIAssistant.CreateSessionAsync(session);
        await _unitOfWork.SaveChangesAsync();
        return session;
    }

    private async Task<AssistantLlmResponseDto> BuildAssistantResponseAsync(AIAssistantSession session, string message, bool toolPlanningEnabled)
    {
        var history = session.Messages
            .Where(m => !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(8)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AssistantLlmMessageDto
            {
                Role = m.Role == AIAssistantMessageRole.Assistant ? "assistant" : "user",
                Content = m.Content
            })
            .ToList();
        history.Add(new AssistantLlmMessageDto { Role = "user", Content = message });

        var llmResponse = await _llmClient.GenerateAsync(new AssistantLlmRequestDto
        {
            SystemPrompt = "You are TBM Personal Assistant. Return strict JSON with intent, reply, links, tasks, toolActions. Keep action URLs relative to TBM.",
            Messages = history
        });

        if (llmResponse == null)
        {
            llmResponse = await BuildFallbackResponseAsync(session.UserId, message);
        }

        llmResponse.Intent = NormalizeIntent(llmResponse.Intent);
        llmResponse.Links = SanitizeLinks(llmResponse.Links);
        llmResponse.Tasks = SanitizeTasks(llmResponse.Tasks);
        llmResponse.ToolActions = toolPlanningEnabled ? SanitizeActions(llmResponse.ToolActions) : new List<AssistantLlmToolActionPlanDto>();
        return llmResponse;
    }

    private async Task<AssistantLlmResponseDto> BuildFallbackResponseAsync(Guid userId, string message)
    {
        var intent = NormalizeIntent(message.Contains("estimate", StringComparison.OrdinalIgnoreCase) ? "renovation_estimator" : message.Contains("track", StringComparison.OrdinalIgnoreCase) ? "order_tracking" : "general");
        if (intent == "renovation_estimator")
        {
            return new AssistantLlmResponseDto
            {
                Intent = intent,
                Reply = "I can create a renovation estimate once you provide room dimensions and finish level.",
                Links = new List<AssistantLinkDto> { new() { Label = "Create estimate", Url = "/api/v1/ai/renovation/estimate", Method = "POST" } },
                Tasks = new List<AssistantLlmTaskPlanDto> { new() { Title = "Run estimator", Description = "Create estimate", ActionUrl = "/api/v1/ai/renovation/estimate", ActionMethod = "POST", RequiresApproval = true } },
                ToolActions = new List<AssistantLlmToolActionPlanDto>
                {
                    new()
                    {
                        Name = "create_renovation_estimate",
                        Description = "Create renovation estimate",
                        ActionUrl = "/api/v1/ai/renovation/estimate",
                        ActionMethod = "POST",
                        RequiresApproval = true,
                        PayloadJson = JsonSerializer.Serialize(new CreateRenovationEstimateRequestDto
                        {
                            ProjectName = "Assistant Renovation Estimate",
                            RoomType = "general",
                            LengthMeters = 4m,
                            WidthMeters = 3m,
                            HeightMeters = 2.8m,
                            FinishLevel = "standard",
                            IncludeFlooring = true,
                            IncludePainting = true,
                            IncludeElectrical = false,
                            IncludePlumbing = false,
                            ContingencyPercent = 10m
                        }, JsonOptions)
                    }
                }
            };
        }

        if (intent == "order_tracking")
        {
            var latest = await GetLatestOrderAsync(userId);
            var trackingUrl = latest == null ? "/dashboard/orders" : $"/dashboard/orders/{latest.Value.Id}/tracking";
            return new AssistantLlmResponseDto
            {
                Intent = intent,
                Reply = "I can retrieve your tracking link.",
                Links = new List<AssistantLinkDto> { new() { Label = "Order tracking", Url = trackingUrl, Method = "GET" } },
                Tasks = new List<AssistantLlmTaskPlanDto> { new() { Title = "Get tracking link", Description = "Fetch tracking URL", ActionUrl = trackingUrl, ActionMethod = "GET" } },
                ToolActions = new List<AssistantLlmToolActionPlanDto> { new() { Name = "get_tracking_link", Description = "Get tracking link", ActionUrl = trackingUrl, ActionMethod = "GET" } }
            };
        }

        return new AssistantLlmResponseDto
        {
            Intent = "general",
            Reply = "I can help with estimates, materials, orders, invoices, and checkout guidance.",
            Links = new List<AssistantLinkDto> { new() { Label = "Materials", Url = "/materials", Method = "GET" }, new() { Label = "Orders", Url = "/dashboard/orders", Method = "GET" } }
        };
    }

    private async Task<object> ExecuteCoreAsync(Guid userId, AIAssistantToolAction action)
    {
        var name = NormalizeActionName(action.Name, action.ActionMethod, action.ActionUrl);
        if (name == "create_renovation_estimate")
        {
            var parsedPayload = ParsePayload<CreateRenovationEstimateRequestDto>(action.PayloadJson);
            if (parsedPayload == null)
            {
                _logger.LogInformation(
                    "Estimator action payload missing or invalid. ActionId={ActionId}. Applying safe defaults.",
                    action.Id);
            }

            var payload = NormalizeEstimatorPayload(parsedPayload);
            var estimate = await _renovationEstimatorService.CreateEstimateAsync(userId, payload);
            return new { success = true, estimateId = estimate.EstimateId, estimate.TotalEstimate, estimate.Currency };
        }
        if (name == "get_tracking_link")
        {
            var orderId = ParsePayload<OrderRefPayload>(action.PayloadJson)?.OrderId ?? (await GetLatestOrderAsync(userId))?.Id;
            if (orderId == null) throw new InvalidOperationException("No order found for tracking.");
            return new { success = true, trackingUrl = $"/api/dashboard/orders/{orderId}/tracking" };
        }
        if (name == "get_invoice_link")
        {
            var orderId = ParsePayload<OrderRefPayload>(action.PayloadJson)?.OrderId ?? (await GetLatestOrderAsync(userId))?.Id;
            if (orderId == null) throw new InvalidOperationException("No order found for invoice.");
            return new { success = true, url = $"/dashboard/orders/{orderId}/invoice" };
        }
        if (name == "list_materials")
        {
            var payload = ParsePayload<ListMaterialsPayload>(action.PayloadJson);
            var limit = payload?.Limit is > 0 and <= 12 ? payload.Limit.Value : 6;
            var (items, _) = await _unitOfWork.Products.GetPagedAsync(1, limit, searchTerm: payload?.Search, activeOnly: true);
            return new { count = items.Count(), items = items.Select(p => new { p.Id, p.Name, p.Price, category = p.Category?.Name, url = $"/materials/{p.Id}" }) };
        }

        if (IsWriteMethod(action.ActionMethod))
        {
            throw new InvalidOperationException($"Write action '{action.Name}' is not supported by safe executor.");
        }

        return new { success = true, message = "Action logged", action.ActionUrl, action.ActionMethod };
    }

    private async Task<(Guid Id, string OrderNumber)?> GetLatestOrderAsync(Guid userId)
    {
        var latest = (await _unitOfWork.Orders.GetUserOrdersAsync(userId))
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();
        return latest == null ? null : (latest.Id, latest.OrderNumber);
    }

    private static AssistantChatMessageDto MapMessage(AIAssistantMessage message) => new()
    {
        MessageId = message.Id,
        Role = message.Role.ToString().ToLowerInvariant(),
        Content = message.Content,
        CreatedAtUtc = message.CreatedAt,
        Intent = message.Intent,
        Links = ParsePayload<List<AssistantLinkDto>>(message.LinksJson) ?? new List<AssistantLinkDto>()
    };

    private static AssistantTaskDto MapTask(AIAssistantTask task) => new()
    {
        TaskId = task.Id,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status.ToString().ToLowerInvariant(),
        ActionUrl = task.ActionUrl,
        ActionMethod = task.ActionMethod,
        RequiresApproval = task.RequiresApproval,
        ToolActionId = task.ToolActionId,
        CreatedAtUtc = task.CreatedAt,
        UpdatedAtUtc = task.UpdatedAtUtc
    };

    private static AssistantToolActionDto MapAction(AIAssistantToolAction action)
    {
        var approval = action.Approvals.Where(a => !a.IsDeleted).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        var execution = action.Executions.Where(e => !e.IsDeleted).OrderByDescending(e => e.CreatedAt).FirstOrDefault();
        return new AssistantToolActionDto
        {
            ActionId = action.Id,
            Name = action.Name,
            Description = action.Description,
            ActionUrl = action.ActionUrl,
            ActionMethod = action.ActionMethod,
            RequiresApproval = action.RequiresApproval,
            Status = action.Status.ToString().ToLowerInvariant(),
            CreatedAtUtc = action.CreatedAt,
            LatestApproval = approval == null ? null : new AssistantToolApprovalDto { ApprovalId = approval.Id, ActionId = approval.ToolActionId, Status = approval.Status.ToString().ToLowerInvariant(), Reason = approval.Reason, CreatedAtUtc = approval.CreatedAt, ReviewedAtUtc = approval.ReviewedAtUtc },
            LatestExecution = execution == null ? null : MapExecution(execution)
        };
    }

    private static AssistantToolExecutionDto MapExecution(AIAssistantToolExecution execution) => new()
    {
        ExecutionId = execution.Id,
        ActionId = execution.ToolActionId,
        Status = execution.Status.ToString().ToLowerInvariant(),
        ResultJson = execution.ResultJson,
        ErrorMessage = execution.ErrorMessage,
        ExecutedAtUtc = execution.ExecutedAtUtc
    };

    private static bool IsApproved(AIAssistantToolAction action, Guid userId) =>
        action.Approvals.Where(a => !a.IsDeleted && a.UserId == userId).OrderByDescending(a => a.CreatedAt).FirstOrDefault()?.Status == AIAssistantApprovalStatus.Approved;

    private static CreateRenovationEstimateRequestDto NormalizeEstimatorPayload(CreateRenovationEstimateRequestDto? payload)
    {
        payload ??= new CreateRenovationEstimateRequestDto();
        payload.ProjectName = string.IsNullOrWhiteSpace(payload.ProjectName) ? "Assistant Renovation Estimate" : payload.ProjectName.Trim();
        payload.RoomType = string.IsNullOrWhiteSpace(payload.RoomType) ? "general" : payload.RoomType.Trim().ToLowerInvariant();
        payload.FinishLevel = string.IsNullOrWhiteSpace(payload.FinishLevel) ? "standard" : payload.FinishLevel.Trim().ToLowerInvariant();
        payload.LengthMeters = payload.LengthMeters > 0 ? payload.LengthMeters : 4m;
        payload.WidthMeters = payload.WidthMeters > 0 ? payload.WidthMeters : 3m;
        payload.HeightMeters = payload.HeightMeters > 0 ? payload.HeightMeters : 2.8m;
        payload.ContingencyPercent = payload.ContingencyPercent < 0 ? 0 : payload.ContingencyPercent > 30 ? 30 : payload.ContingencyPercent;
        return payload;
    }

    private static string NormalizeIntent(string? intent) => string.IsNullOrWhiteSpace(intent) ? "general" : intent.Trim().ToLowerInvariant();
    private static string NormalizeMethod(string? method) => method?.Trim().ToUpperInvariant() is "POST" or "PUT" or "PATCH" or "DELETE" ? method!.Trim().ToUpperInvariant() : "GET";
    private static bool IsWriteMethod(string method) => method is "POST" or "PUT" or "PATCH" or "DELETE";
    private static string NormalizeUrl(string? url) => string.IsNullOrWhiteSpace(url) ? "/" : (url!.Trim().StartsWith('/') || url.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url.Trim() : $"/{url.TrimStart('/')}");
    private static string? NormalizePayload(string? payload) { try { if (string.IsNullOrWhiteSpace(payload)) return null; using var d = JsonDocument.Parse(payload); return JsonSerializer.Serialize(d.RootElement, JsonOptions); } catch { return null; } }
    private static T? ParsePayload<T>(string? json) where T : class { try { return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, JsonOptions); } catch { return null; } }
    private static string NormalizeActionName(string? name, string method, string? url) { var n = (name ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_'); if (!string.IsNullOrWhiteSpace(n)) return n; var u = NormalizeUrl(url).ToLowerInvariant(); if (u.Contains("invoice")) return "get_invoice_link"; if (u.Contains("tracking")) return "get_tracking_link"; if (u.Contains("/materials") || u.Contains("/flooring")) return "list_materials"; if (u.Contains("/ai/renovation/estimate")) return "create_renovation_estimate"; if (method == "GET" && u.Contains("/orders")) return "get_latest_order"; return "custom_action"; }
    private static List<AssistantLinkDto> SanitizeLinks(List<AssistantLinkDto>? links) => links?.Where(l => !string.IsNullOrWhiteSpace(l.Label) && !string.IsNullOrWhiteSpace(l.Url)).Select(l => new AssistantLinkDto { Label = l.Label.Trim(), Url = NormalizeUrl(l.Url), Method = NormalizeMethod(l.Method), Description = string.IsNullOrWhiteSpace(l.Description) ? null : l.Description.Trim() }).Take(12).ToList() ?? new List<AssistantLinkDto>();
    private static List<AssistantLlmTaskPlanDto> SanitizeTasks(List<AssistantLlmTaskPlanDto>? tasks) => tasks?.Where(t => !string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Description)).Select(t => new AssistantLlmTaskPlanDto { Title = t.Title.Trim(), Description = t.Description.Trim(), ActionUrl = NormalizeUrl(t.ActionUrl), ActionMethod = NormalizeMethod(t.ActionMethod), RequiresApproval = t.RequiresApproval }).Take(12).ToList() ?? new List<AssistantLlmTaskPlanDto>();
    private static List<AssistantLlmToolActionPlanDto> SanitizeActions(List<AssistantLlmToolActionPlanDto>? actions) => actions?.Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.ActionUrl)).Select(a => new AssistantLlmToolActionPlanDto { Name = a.Name.Trim(), Description = string.IsNullOrWhiteSpace(a.Description) ? a.Name.Trim() : a.Description.Trim(), ActionUrl = NormalizeUrl(a.ActionUrl), ActionMethod = NormalizeMethod(a.ActionMethod), RequiresApproval = a.RequiresApproval, PayloadJson = NormalizePayload(a.PayloadJson) }).Take(12).ToList() ?? new List<AssistantLlmToolActionPlanDto>();
    private static AIAssistantTaskStatus ParseTaskStatus(string status) => status.Trim().ToLowerInvariant() switch { "pending" => AIAssistantTaskStatus.Pending, "in_progress" or "inprogress" => AIAssistantTaskStatus.InProgress, "awaiting_approval" or "awaitingapproval" => AIAssistantTaskStatus.AwaitingApproval, "completed" => AIAssistantTaskStatus.Completed, "cancelled" => AIAssistantTaskStatus.Cancelled, "failed" => AIAssistantTaskStatus.Failed, _ => throw new InvalidOperationException("Invalid task status.") };

    private sealed class OrderRefPayload { public Guid? OrderId { get; set; } }
    private sealed class ListMaterialsPayload { public string? Search { get; set; } public int? Limit { get; set; } }
}
