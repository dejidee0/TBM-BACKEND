using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBM.Application.Configuration;
using TBM.Application.DTOs.AI;
using TBM.Application.Interfaces;
using TBM.Application.Services.Subscriptions;
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
    private readonly IAIService _aiService;
    private readonly AICreditService _creditService;
    private readonly ILogger<AIPersonalAssistantService> _logger;
    private readonly AppSettings _appSettings;

    public AIPersonalAssistantService(
        IUnitOfWork unitOfWork,
        IAssistantLlmClient llmClient,
        RenovationEstimatorService renovationEstimatorService,
        IAIService aiService,
        AICreditService creditService,
        ILogger<AIPersonalAssistantService> logger,
        IOptions<AppSettings> appSettings)
    {
        _unitOfWork = unitOfWork;
        _llmClient = llmClient;
        _renovationEstimatorService = renovationEstimatorService;
        _aiService = aiService;
        _creditService = creditService;
        _logger = logger;
        _appSettings = appSettings.Value;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

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
                ActionUrl = ToFrontendUrl(plan.ActionUrl),
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
        if (session == null) return null;

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
        if (task == null) return null;

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
        if (action == null) return null;

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

    public async Task<bool> ClearSessionAsync(Guid userId, Guid sessionId)
    {
        var deleted = await _unitOfWork.AIAssistant.DeleteSessionAsync(sessionId, userId);
        if (deleted) await _unitOfWork.SaveChangesAsync();
        return deleted;
    }

    public async Task<AssistantToolExecutionDto?> ExecuteToolActionAsync(Guid userId, Guid actionId, bool dryRun)
    {
        var action = await _unitOfWork.AIAssistant.GetToolActionAsync(actionId, userId);
        if (action == null) return null;

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
            _logger.LogWarning(ex, "ZIORA action execution failed. ActionId={ActionId}", action.Id);
            execution.Status = AIAssistantToolExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            action.Status = AIAssistantToolActionStatus.Failed;
        }

        await _unitOfWork.AIAssistant.AddToolExecutionAsync(execution);
        await _unitOfWork.SaveChangesAsync();
        return MapExecution(execution);
    }

    // ─── Session ──────────────────────────────────────────────────────────────

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

    // ─── LLM Response Builder ─────────────────────────────────────────────────

    private async Task<AssistantLlmResponseDto> BuildAssistantResponseAsync(
        AIAssistantSession session, string message, bool toolPlanningEnabled)
    {
        // Last 20 messages (increased from 12 for richer context)
        var history = session.Messages
            .Where(m => !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AssistantLlmMessageDto
            {
                Role = m.Role == AIAssistantMessageRole.Assistant ? "assistant" : "user",
                Content = m.Content
            })
            .ToList();
        history.Add(new AssistantLlmMessageDto { Role = "user", Content = message });

        var systemPrompt = await BuildSystemPromptAsync(session.UserId);

        // ── Pass 1: Planning ──────────────────────────────────────────────────
        var planResponse = await _llmClient.GenerateAsync(new AssistantLlmRequestDto
        {
            SystemPrompt = systemPrompt,
            Messages = history
        });

        if (planResponse == null)
            return await BuildFallbackResponseAsync(session.UserId, message);

        planResponse.Intent = NormalizeIntent(planResponse.Intent);
        planResponse.Links = SanitizeLinks(planResponse.Links);
        planResponse.Tasks = SanitizeTasks(planResponse.Tasks);
        planResponse.ToolActions = toolPlanningEnabled
            ? SanitizeActions(planResponse.ToolActions)
            : new List<AssistantLlmToolActionPlanDto>();

        // Hard guard: capability questions must never surface WRITE tool actions (Approve buttons).
        // This runs regardless of what the LLM returned.
        if (IsCapabilityQuestion(message))
        {
            planResponse.ToolActions = planResponse.ToolActions
                .Where(a => !IsWriteMethod(NormalizeMethod(a.ActionMethod)))
                .ToList();
            planResponse.Tasks = planResponse.Tasks
                .Where(t => !t.RequiresApproval && !IsWriteMethod(NormalizeMethod(t.ActionMethod)))
                .ToList();
        }

        if (!toolPlanningEnabled)
            return planResponse;

        // ── Pass 2: Agentic Loop — auto-execute READ actions, ground reply ────
        var readActions = planResponse.ToolActions
            .Where(a => !a.RequiresApproval && !IsWriteMethod(NormalizeMethod(a.ActionMethod)))
            .Take(3) // cap at 3 to avoid excessive DB calls per turn
            .ToList();

        if (readActions.Count == 0)
            return planResponse;

        var resultBlock = new StringBuilder();
        resultBlock.AppendLine("[TOOL_RESULTS]");
        bool anySucceeded = false;

        foreach (var actionPlan in readActions)
        {
            var name = NormalizeActionName(actionPlan.Name, NormalizeMethod(actionPlan.ActionMethod), actionPlan.ActionUrl);
            try
            {
                var tempAction = new AIAssistantToolAction
                {
                    Name = name,
                    ActionMethod = NormalizeMethod(actionPlan.ActionMethod),
                    ActionUrl = NormalizeUrl(actionPlan.ActionUrl),
                    PayloadJson = NormalizePayload(actionPlan.PayloadJson)
                };
                var result = await ExecuteCoreAsync(session.UserId, tempAction);
                resultBlock.AppendLine($"### {name}");
                resultBlock.AppendLine(JsonSerializer.Serialize(result, JsonOptions));
                resultBlock.AppendLine();
                anySucceeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ZIORA agentic: failed to auto-execute {Action}", name);
                resultBlock.AppendLine($"### {name}: unavailable");
            }
        }

        resultBlock.AppendLine("[/TOOL_RESULTS]");
        resultBlock.AppendLine("Using the live data above, give your final response to the user. Be specific — reference actual names, numbers and prices. Return full JSON.");

        if (!anySucceeded)
            return planResponse;

        // Second LLM call — AI sees its own plan + real results → grounded reply
        var messagesWithResults = new List<AssistantLlmMessageDto>(history)
        {
            new() { Role = "assistant", Content = planResponse.Reply },
            new() { Role = "user",      Content = resultBlock.ToString() }
        };

        var groundedResponse = await _llmClient.GenerateAsync(new AssistantLlmRequestDto
        {
            SystemPrompt = systemPrompt,
            Messages = messagesWithResults
        });

        if (groundedResponse == null)
            return planResponse;

        // Merge: grounded reply + intent/links from second call, tool actions/tasks from plan
        groundedResponse.Intent = string.IsNullOrWhiteSpace(groundedResponse.Intent)
            ? planResponse.Intent
            : NormalizeIntent(groundedResponse.Intent);

        // Only keep WRITE tool actions (those need user approval in the UI).
        // READ actions already executed in the agentic loop — no point showing Approve/Execute buttons for them.
        groundedResponse.ToolActions = planResponse.ToolActions
            .Where(a => a.RequiresApproval || IsWriteMethod(NormalizeMethod(a.ActionMethod)))
            .ToList();

        // Drop tasks whose linked actions were auto-executed (read-only tasks)
        var keptActionUrls = groundedResponse.ToolActions
            .Select(a => NormalizeUrl(a.ActionUrl))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        groundedResponse.Tasks = planResponse.Tasks
            .Where(t => string.IsNullOrWhiteSpace(t.ActionUrl) ||
                        t.RequiresApproval ||
                        keptActionUrls.Contains(NormalizeUrl(t.ActionUrl)))
            .ToList();

        groundedResponse.Links = groundedResponse.Links.Count > 0
            ? SanitizeLinks(groundedResponse.Links)
            : planResponse.Links;

        return groundedResponse;
    }

    // ─── System Prompt + User Context ─────────────────────────────────────────

    private async Task<string> BuildSystemPromptAsync(Guid userId)
    {
        // Each fetch is isolated — a single DB failure degrades gracefully rather than crashing ZIORA
        var user = await SafeFetchAsync(() => _unitOfWork.Users.GetByIdAsync(userId));

        IEnumerable<TBM.Core.Entities.Orders.Order> rawOrders;
        try { rawOrders = await _unitOfWork.Orders.GetUserOrdersAsync(userId); }
        catch (Exception ex) { _logger.LogWarning(ex, "ZIORA: failed to fetch orders for context"); rawOrders = Enumerable.Empty<TBM.Core.Entities.Orders.Order>(); }
        var orders = rawOrders
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Take(3)
            .ToList();

        var cart = await SafeFetchAsync(() => _unitOfWork.Carts.GetByUserIdAsync(userId));

        List<AIProject> projects;
        try { projects = await _unitOfWork.AIProjects.GetByUserAsync(userId); }
        catch (Exception ex) { _logger.LogWarning(ex, "ZIORA: failed to fetch projects for context"); projects = new List<AIProject>(); }
        var designCount = projects.SelectMany(p => p.Designs).Count(d => !d.IsDeleted);

        List<AIRenovationEstimate> estimates;
        try { estimates = await _unitOfWork.AIRenovationEstimates.GetByUserAsync(userId); }
        catch (Exception ex) { _logger.LogWarning(ex, "ZIORA: failed to fetch estimates for context"); estimates = new List<AIRenovationEstimate>(); }
        var latestEstimate = estimates.OrderByDescending(e => e.CreatedAt).FirstOrDefault();

        var ctx = new StringBuilder();
        ctx.AppendLine("## LIVE USER CONTEXT");
        ctx.AppendLine($"- User ID: {userId}");

        if (user != null)
        {
            var displayName = string.Join(" ",
                new[] { user.FirstName, user.LastName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(displayName))
                ctx.AppendLine($"- Name: {displayName}");
            ctx.AppendLine($"- Member since: {user.CreatedAt:MMMM yyyy}");
        }

        if (orders.Any())
        {
            ctx.AppendLine("- Recent orders:");
            foreach (var o in orders)
            {
                ctx.AppendLine($"  • #{o.OrderNumber} — {o.Status} — NGN {o.Total:N0} — {o.CreatedAt:dd MMM yyyy} [orderId: {o.Id}]");
            }
        }
        else
        {
            ctx.AppendLine("- Recent orders: None yet");
        }

        if (cart != null && cart.Items.Any())
        {
            var cartTotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
            ctx.AppendLine($"- Cart: {cart.Items.Count} item(s), NGN {cartTotal:N0} [cartId: {cart.Id}]");
        }
        else
        {
            ctx.AppendLine("- Cart: Empty");
        }

        ctx.AppendLine($"- Designs in library: {designCount}");

        if (latestEstimate != null)
        {
            ctx.AppendLine($"- Latest renovation estimate: #{latestEstimate.EstimateNumber} — {latestEstimate.ProjectName} ({latestEstimate.RoomType}) — NGN {latestEstimate.TotalEstimate:N0} [{latestEstimate.CreatedAt:dd MMM yyyy}]");
        }
        else
        {
            ctx.AppendLine("- Renovation estimates: None yet");
        }

        try
        {
            var balance = await _creditService.GetBalanceAsync(userId);
            ctx.AppendLine($"- AI design credits: {balance.Balance}");

            var latestProject = projects.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            if (latestProject != null)
                ctx.AppendLine($"- Latest AI project source image: {latestProject.SourceImageUrl} [projectId: {latestProject.Id}]");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ZIORA: failed to fetch credit balance for context");
        }

        var catalogContext = await BuildProductCatalogContextAsync();

        return BuildBaseSystemPrompt(ctx.ToString(), catalogContext);
    }

    private async Task<string> BuildProductCatalogContextAsync()
    {
        try
        {
            var (products, total) = await _unitOfWork.Products.GetPagedAsync(1, 60, activeOnly: true);
            if (!products.Any()) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("## LIVE PRODUCT CATALOG");

            var categories = products
                .GroupBy(p => p.Category?.Name ?? "Other")
                .OrderByDescending(g => g.Count())
                .Take(10);

            foreach (var cat in categories)
            {
                var priced = cat.Where(p => p.Price.HasValue).ToList();
                var priceRange = priced.Any()
                    ? $"NGN {priced.Min(p => p.Price)!.Value:N0}–{priced.Max(p => p.Price)!.Value:N0}"
                    : "price on request";
                sb.AppendLine($"- **{cat.Key}** ({cat.Count()} products, {priceRange})");
            }

            var featured = products.Where(p => p.IsFeatured && p.Price.HasValue).Take(6).ToList();
            if (featured.Any())
            {
                sb.AppendLine("\n**Featured products right now:**");
                foreach (var p in featured)
                    sb.AppendLine($"- {p.Name} — NGN {p.Price!.Value:N0} [{p.Category?.Name}]");
            }

            var distinctCats = products.Select(p => p.Category?.Name).Distinct().Count();
            sb.AppendLine($"\n*{total} active products across {distinctCats} categories. Use list_materials or get_featured_products for live details.*");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ZIORA: product catalog context fetch failed");
            return string.Empty;
        }
    }

    private static string BuildBaseSystemPrompt(string userContextBlock, string catalogContext = "") => $$"""
You are ZIORA — the intelligent AI personal assistant for TBM (The Building Market), Nigeria's premier AI-powered building materials and renovation platform. You always respond in valid json format as specified below.

## YOUR IDENTITY
- Name: ZIORA
- Platform: TBM (The Building Market)
- Role: Help users discover materials, track orders, generate renovation cost estimates, explore AI design projects, and navigate TBM seamlessly.
- Tone: Warm, knowledgeable, confident, and direct. You speak like a trusted Nigerian building professional who genuinely wants to help.
- Currency: Always use NGN (Naira, ₦). Format with commas e.g. NGN 450,000.
- Never reveal you are powered by an external AI model. If asked who you are: "I'm ZIORA, TBM's AI assistant — here to help you build smarter."

## WHAT TBM OFFERS
**Materials & Products**: Tiles, flooring, marble, granite, porcelain, ceramic, hardwood, softwood, plywood, MDF, PVC, steel, aluminium, glass, paint, electrical fittings, plumbing fixtures, roofing materials, and more — sourced from vetted Nigerian vendors.
**AI Design Studio**: Upload a room photo → receive AI-generated renovation visualisations showing different materials, colours, and layouts.
**Renovation Estimator**: Instant NGN cost breakdowns for flooring, painting, electrical, and plumbing — based on room dimensions and finish level (basic / standard / premium).
**Order Management**: Browse, purchase, and track material orders with Paystack payment and live delivery tracking.
**Design Sessions**: Professional one-on-one consultations — Luxury and Economy tiers.
**Vendor Network**: Quality-guaranteed materials from vetted vendors across Lagos, Abuja, Port Harcourt, and nationwide.

## VALID INTENTS — return exactly ONE per response
- `general` — greetings, general help, platform questions
- `order_tracking` — order status, delivery updates, tracking, ETA
- `materials_browse` — browsing products, prices, categories, availability
- `renovation_estimator` — renovation quotes, cost estimates, room pricing
- `design_studio` — AI room visualisation, design ideas, uploading room photos
- `cart_management` — viewing cart, adding items, checkout
- `account_support` — profile, password, saved items, addresses
- `invoice_download` — invoice or receipt for a completed order
- `design_session` — booking a professional design consultation
- `project_management` — renovation or construction project tracking

## APPROVED TOOL ACTIONS — only suggest from this list

IMPORTANT: `toolActions[].actionUrl` must use the API paths below (used for backend execution).
`links[].url` must use the FRONTEND NAVIGATION URLs listed in the next section — NEVER put /api/ paths in links.

READ actions (requiresApproval: false — execute immediately):
- name: list_materials           method: GET  url: /api/v1/products                              — Browse available materials
- name: get_featured_products    method: GET  url: /api/v1/products/featured                     — Top featured materials
- name: get_cart_summary         method: GET  url: /api/v1/cart                                  — View current cart
- name: get_orders               method: GET  url: /api/v1/dashboard/orders                      — Full order history
- name: get_order_detail         method: GET  url: /api/v1/dashboard/orders/{orderId}            — Specific order detail
- name: get_tracking_link        method: GET  url: /api/v1/dashboard/orders/{orderId}/tracking   — Live delivery tracking
- name: get_invoice_link         method: GET  url: /api/v1/dashboard/orders/{orderId}/invoice    — Invoice download
- name: get_designs              method: GET  url: /api/v1/designs                               — User's AI design library
- name: get_renovation_estimates method: GET  url: /api/v1/ai/renovation/estimates               — Previous renovation estimates
- name: get_credit_balance       method: GET  url: /api/v1/ai/credits                            — Check AI design credits remaining

WRITE actions (requiresApproval: true — ALWAYS, no exceptions):
- name: create_renovation_estimate  method: POST  url: /api/v1/ai/renovation/estimate   — Generate a new renovation estimate
- name: start_design_session        method: POST  url: /api/v1/design-sessions           — Book a design consultation
- name: generate_ai_design          method: POST  url: /api/v1/ai/generate               — Generate AI before/after room visualisation (costs 5 credits); payloadJson must include: sourceImageUrl (string, required — if unknown use the user's latest project source image from context), prompt (string — describe desired style e.g. "modern living room with marble floors"), roomType (string e.g. "living_room")

## FRONTEND NAVIGATION URLs — use these in `links[].url` ONLY (never in toolActions)
- Browse materials / products  →  /materials
- View cart                    →  /cart
- My orders / order history    →  /orders
- Specific order detail        →  /orders/{orderId}
- AI Visualizer / design studio →  /ai-visualizer
- My AI designs / gallery      →  /ai-gallery
- Renovation estimator         →  /renovation-estimator
- Book design consultation     →  /design-sessions
- Pricing / subscription plans →  /pricing
- My profile / account         →  /profile
- Home                         →  /
- Contact / support            →  /contact

## WHEN YOU RECEIVE [TOOL_RESULTS]
When a message starts with [TOOL_RESULTS], it contains **live data just fetched from TBM's database**.
- Use this data to give a specific, data-driven answer — quote actual product names, prices, order numbers, quantities.
- Do NOT say "let me check" or "I'll look that up" — you already have the data.
- Do NOT repeat the raw JSON to the user — summarise it conversationally.
- After answering, suggest one useful next action.
- Still return full JSON in the required output format.

## AI DESIGN STUDIO GUIDE
When a user asks for a room visualisation, before/after design, or AI design generation:
1. Check the LIVE USER CONTEXT — if "Latest AI project source image" is present, use that URL as sourceImageUrl in the payload.
2. If no source image exists in context, tell the user they need to upload a room photo first via the AI Design Studio (/ai-studio), then come back. Do NOT create a generate_ai_design action without a valid sourceImageUrl.
3. Check "AI design credits" in context — if 0 or missing, warn the user they may need to subscribe or top up credits.
4. Collect the desired style/room description (prompt). If not provided, ask for it first.
5. Once all info is gathered, suggest the generate_ai_design tool action with requiresApproval: true.
6. Payload structure: { "sourceImageUrl": "<url>", "prompt": "<style description>", "roomType": "<room type>" }

## RENOVATION ESTIMATOR GUIDE
Collect before calling create_renovation_estimate:
1. Room type (bedroom, living room, kitchen, bathroom, etc.)
2. Length × Width in meters (ask only if missing)
3. Height in meters (default 2.8m)
4. Finish level: basic, standard, or premium
5. Scope: flooring / painting / electrical / plumbing

Typical Lagos/Abuja room sizes: bedroom ~3×4m | living room ~5×6m | kitchen ~3×3m | bathroom ~2×2m
Reference cost guide (standard finish): flooring NGN 23,000/sqm | painting NGN 4,200/sqm | electrical NGN 6,500/sqm | plumbing NGN 7,000/sqm
Prices vary by location and contractor — these are indicative TBM estimates.
If all details are in the user message, create the estimate immediately — do not ask again.

## NIGERIAN BUILDING MARKET KNOWLEDGE
- Common renovation projects: kitchen upgrade, bathroom remodel, floor tiling, exterior painting, roofing, perimeter fencing
- Popular finish choices: Italian marble and porcelain tiles are premium; local ceramic is budget-friendly
- Rainy season (April–October) affects delivery timelines and outdoor work scheduling
- Lagos prices are typically 10–15% higher than Abuja; PH is comparable to Abuja
- Standard contractor markup: 20–30% on top of material cost
- Typical project durations: bathroom remodel 2–3 weeks | kitchen 3–4 weeks | full house finishing 3–6 months

## CONVERSATION RULES
1. Keep replies concise — max 3 short paragraphs or a bullet list. Be warm, natural, and direct — like a knowledgeable friend, not a robot.
2. Reference the user's live context naturally: "Your last order #TBM-0042 is Shipped — want tracking?"
3. When you have live data from TOOL_RESULTS, answer specifically — no vague responses, no "let me check".
4. Greet warmly on first message — introduce yourself and list 3–4 things you can help with.
5. Never hallucinate product names, prices, or stock levels.
6. Suggest one useful follow-up at the end of each reply.
7. WRITE actions: requiresApproval must always be true. No exceptions.

## CRITICAL RULES FOR TOOL ACTIONS
These rules are absolute — breaking them causes a bad user experience:

**DO include a toolAction when:**
- User is ASKING YOU TO FETCH data ("show me my orders", "what's in my cart?", "list materials")
- User is ASKING YOU TO CREATE/DO something ("design my room", "create an estimate for my bedroom", "generate a design")

**DO NOT include any toolAction when:**
- User is asking ABOUT CAPABILITIES ("can you design?", "do you have AI?", "what can you do?", "how does it work?")
- User is asking a GENERAL QUESTION ("what is TBM?", "how much does tiling cost?", "what materials do you have?")
- User is just CHATTING or greeting ("hello", "thanks", "okay", "got it")
- You already have the answer in your knowledge or in LIVE USER CONTEXT
- You just ran [TOOL_RESULTS] and already have fresh data

**Special rules for generate_ai_design (WRITE):**
- ONLY suggest it when the user explicitly says "design my room", "generate a design", "create AI visualisation", "show me what it would look like" etc.
- NEVER suggest it for "can you design?" — that is a capability question; just answer YES and explain how.
- Before suggesting it, confirm you have a sourceImageUrl from context or from the user.
- If no sourceImageUrl exists, tell the user to upload a photo in AI Studio first. Do not create the action.

{{(string.IsNullOrWhiteSpace(catalogContext) ? "" : catalogContext)}}

{{userContextBlock}}

## OUTPUT FORMAT — return ONLY valid json (no markdown fences, no extra text outside the json object)
{
  "intent": "<one intent from the valid list>",
  "reply": "<your response — warm, helpful, direct, specific>",
  "links": [
    { "label": "<text>", "url": "<relative url>", "method": "GET", "description": "<optional>" }
  ],
  "tasks": [
    { "title": "<short title>", "description": "<what this accomplishes>", "actionUrl": "<url>", "actionMethod": "GET", "requiresApproval": false }
  ],
  "toolActions": [
    { "name": "<name from catalog>", "description": "<what it does>", "actionUrl": "<url>", "actionMethod": "GET", "requiresApproval": false, "payloadJson": null }
  ]
}
""";

    // ─── Fallback (when LLM is unavailable) ──────────────────────────────────

    private async Task<AssistantLlmResponseDto> BuildFallbackResponseAsync(Guid userId, string message)
    {
        var lower = message.ToLowerInvariant();

        if (ContainsAny(lower, "estimate", "cost", "quote", "renovation", "price my", "how much"))
        {
            return new AssistantLlmResponseDto
            {
                Intent = "renovation_estimator",
                Reply = "I can put together a renovation estimate for you! Just tell me the room type, dimensions (length × width), finish level (basic / standard / premium), and what work you want covered — flooring, painting, electrical, or plumbing.",
                Links = new List<AssistantLinkDto>
                {
                    new() { Label = "Create renovation estimate", Url = "/api/v1/ai/renovation/estimate", Method = "POST", Description = "Get a detailed NGN cost breakdown" }
                },
                Tasks = new List<AssistantLlmTaskPlanDto>
                {
                    new() { Title = "Create renovation estimate", Description = "Generate a detailed cost breakdown", ActionUrl = "/api/v1/ai/renovation/estimate", ActionMethod = "POST", RequiresApproval = true }
                },
                ToolActions = new List<AssistantLlmToolActionPlanDto>
                {
                    new()
                    {
                        Name = "create_renovation_estimate",
                        Description = "Generate renovation cost estimate",
                        ActionUrl = "/api/v1/ai/renovation/estimate",
                        ActionMethod = "POST",
                        RequiresApproval = true,
                        PayloadJson = JsonSerializer.Serialize(new CreateRenovationEstimateRequestDto
                        {
                            ProjectName = "Renovation Estimate",
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

        if (ContainsAny(lower, "track", "where is my order", "delivery", "shipped", "status"))
        {
            var latest = await GetLatestOrderAsync(userId);
            var orderId = latest?.Id.ToString() ?? string.Empty;
            var trackUrl = latest == null
                ? "/api/v1/dashboard/orders"
                : $"/api/v1/dashboard/orders/{orderId}/tracking";

            var reply = latest == null
                ? "I don't see any orders on your account yet. Once you've placed an order, I can pull up live tracking for you."
                : $"Your most recent order #{latest.Value.OrderNumber} is currently **{latest.Value.Status}**. Here's your tracking link.";

            return new AssistantLlmResponseDto
            {
                Intent = "order_tracking",
                Reply = reply,
                Links = new List<AssistantLinkDto> { new() { Label = "Track order", Url = trackUrl, Method = "GET" } },
                Tasks = new List<AssistantLlmTaskPlanDto> { new() { Title = "Get tracking link", Description = "Fetch live delivery tracking", ActionUrl = trackUrl, ActionMethod = "GET" } },
                ToolActions = new List<AssistantLlmToolActionPlanDto> { new() { Name = "get_tracking_link", Description = "Get order tracking link", ActionUrl = trackUrl, ActionMethod = "GET", RequiresApproval = false } }
            };
        }

        if (ContainsAny(lower, "invoice", "receipt", "payment proof"))
        {
            var latest = await GetLatestOrderAsync(userId);
            var invoiceUrl = latest == null
                ? "/api/v1/dashboard/orders"
                : $"/api/v1/dashboard/orders/{latest.Value.Id}/invoice";

            return new AssistantLlmResponseDto
            {
                Intent = "invoice_download",
                Reply = latest == null
                    ? "No orders found on your account yet. Invoices are generated after a successful purchase."
                    : $"Here's the invoice link for your order #{latest.Value.OrderNumber}.",
                Links = new List<AssistantLinkDto> { new() { Label = "Download invoice", Url = invoiceUrl, Method = "GET" } },
                ToolActions = new List<AssistantLlmToolActionPlanDto> { new() { Name = "get_invoice_link", Description = "Download order invoice", ActionUrl = invoiceUrl, ActionMethod = "GET", RequiresApproval = false } }
            };
        }

        if (ContainsAny(lower, "cart", "basket", "my items", "added to"))
        {
            return new AssistantLlmResponseDto
            {
                Intent = "cart_management",
                Reply = "Let me pull up your cart for you.",
                Links = new List<AssistantLinkDto> { new() { Label = "View cart", Url = "/api/v1/cart", Method = "GET" } },
                ToolActions = new List<AssistantLlmToolActionPlanDto> { new() { Name = "get_cart_summary", Description = "View cart contents and total", ActionUrl = "/api/v1/cart", ActionMethod = "GET", RequiresApproval = false } }
            };
        }

        if (ContainsAny(lower, "material", "product", "flooring", "tile", "marble", "wood", "steel", "buy"))
        {
            return new AssistantLlmResponseDto
            {
                Intent = "materials_browse",
                Reply = "I can help you find the right materials. Here's a look at what's available on TBM — you can filter by type, price, and category.",
                Links = new List<AssistantLinkDto>
                {
                    new() { Label = "Browse all materials", Url = "/api/v1/products", Method = "GET" },
                    new() { Label = "Featured materials", Url = "/api/v1/products/featured", Method = "GET" }
                },
                ToolActions = new List<AssistantLlmToolActionPlanDto>
                {
                    new() { Name = "get_featured_products", Description = "Browse featured materials", ActionUrl = "/api/v1/products/featured", ActionMethod = "GET", RequiresApproval = false }
                }
            };
        }

        if (ContainsAny(lower, "design", "visuali", "room", "ai", "photo"))
        {
            return new AssistantLlmResponseDto
            {
                Intent = "design_studio",
                Reply = "With TBM's AI Design Studio, you can upload a photo of any room and see it transformed with different materials and layouts. Your design library is ready whenever you want to explore.",
                Links = new List<AssistantLinkDto>
                {
                    new() { Label = "My design library", Url = "/api/v1/designs", Method = "GET" },
                    new() { Label = "Start AI design", Url = "/api/v1/ai/generate", Method = "POST" }
                },
                ToolActions = new List<AssistantLlmToolActionPlanDto>
                {
                    new() { Name = "get_designs", Description = "View AI design library", ActionUrl = "/api/v1/designs", ActionMethod = "GET", RequiresApproval = false }
                }
            };
        }

        // Default general fallback
        return new AssistantLlmResponseDto
        {
            Intent = "general",
            Reply = "Hi! I'm ZIORA, your TBM assistant. I can help you with:\n• **Renovation estimates** — get NGN cost breakdowns instantly\n• **Order tracking** — check delivery status and invoices\n• **Materials** — browse flooring, tiles, marble, steel, and more\n• **AI Design Studio** — visualise your room with different materials\n\nWhat would you like to do?",
            Links = new List<AssistantLinkDto>
            {
                new() { Label = "Browse materials", Url = "/api/v1/products", Method = "GET" },
                new() { Label = "My orders", Url = "/api/v1/dashboard/orders", Method = "GET" },
                new() { Label = "My designs", Url = "/api/v1/designs", Method = "GET" }
            }
        };
    }

    // ─── Tool Executor ────────────────────────────────────────────────────────

    private async Task<object> ExecuteCoreAsync(Guid userId, AIAssistantToolAction action)
    {
        var name = NormalizeActionName(action.Name, action.ActionMethod, action.ActionUrl);

        switch (name)
        {
            case "list_materials":
            {
                var payload = ParsePayload<ListMaterialsPayload>(action.PayloadJson);
                var limit = payload?.Limit is > 0 and <= 20 ? payload.Limit.Value : 8;
                var (items, total) = await _unitOfWork.Products.GetPagedAsync(
                    1, limit, searchTerm: payload?.Search, activeOnly: true);
                return new
                {
                    count = total,
                    items = items.Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        price = p.Price,
                        category = p.Category?.Name,
                        materialType = p.MaterialType,
                        url = $"/materials/{p.Slug ?? p.Id.ToString()}"
                    })
                };
            }

            case "get_featured_products":
            {
                var (items, _) = await _unitOfWork.Products.GetPagedAsync(
                    1, 8, activeOnly: true, isFeatured: true);
                return new
                {
                    items = items.Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        price = p.Price,
                        category = p.Category?.Name,
                        url = $"/materials/{p.Slug ?? p.Id.ToString()}"
                    })
                };
            }

            case "get_cart_summary":
            {
                var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
                if (cart == null || !cart.Items.Any())
                    return new { success = true, itemCount = 0, total = 0m, currency = "NGN", items = Array.Empty<object>() };

                var total = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
                return new
                {
                    success = true,
                    cartId = cart.Id,
                    itemCount = cart.Items.Count,
                    total,
                    currency = "NGN",
                    items = cart.Items.Select(i => new
                    {
                        productId = i.ProductId,
                        productName = i.Product?.Name,
                        quantity = i.Quantity,
                        unitPrice = i.UnitPrice,
                        lineTotal = i.UnitPrice * i.Quantity
                    })
                };
            }

            case "get_orders":
            {
                var orders = (await _unitOfWork.Orders.GetUserOrdersAsync(userId))
                    .Where(o => !o.IsDeleted)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(10)
                    .ToList();
                return new
                {
                    count = orders.Count,
                    orders = orders.Select(o => new
                    {
                        id = o.Id,
                        orderNumber = o.OrderNumber,
                        status = o.Status.ToString(),
                        total = o.Total,
                        currency = "NGN",
                        createdAt = o.CreatedAt
                    })
                };
            }

            case "get_order_detail":
            {
                var payload = ParsePayload<OrderRefPayload>(action.PayloadJson);
                var orderId = payload?.OrderId ?? (await GetLatestOrderAsync(userId))?.Id;
                if (orderId == null) throw new InvalidOperationException("No order found.");
                var order = await _unitOfWork.Orders.GetByIdAsync(orderId.Value);
                if (order == null || order.UserId != userId) throw new InvalidOperationException("Order not found.");
                return new
                {
                    id = order.Id,
                    orderNumber = order.OrderNumber,
                    status = order.Status.ToString(),
                    paymentStatus = order.PaymentStatus.ToString(),
                    total = order.Total,
                    currency = "NGN",
                    shippingCity = order.ShippingCity,
                    shippingState = order.ShippingState,
                    trackingNumber = order.TrackingNumber,
                    createdAt = order.CreatedAt,
                    itemCount = order.Items.Count
                };
            }

            case "get_tracking_link":
            {
                var payload = ParsePayload<OrderRefPayload>(action.PayloadJson);
                var orderId = payload?.OrderId ?? (await GetLatestOrderAsync(userId))?.Id;
                if (orderId == null) throw new InvalidOperationException("No order found for tracking.");
                return new { success = true, trackingUrl = $"/api/v1/dashboard/orders/{orderId}/tracking" };
            }

            case "get_invoice_link":
            {
                var payload = ParsePayload<OrderRefPayload>(action.PayloadJson);
                var orderId = payload?.OrderId ?? (await GetLatestOrderAsync(userId))?.Id;
                if (orderId == null) throw new InvalidOperationException("No order found for invoice.");
                return new { success = true, url = $"/api/v1/dashboard/orders/{orderId}/invoice" };
            }

            case "get_designs":
            {
                var projects = await _unitOfWork.AIProjects.GetByUserAsync(userId);
                var designs = projects
                    .SelectMany(p => p.Designs)
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(10)
                    .ToList();
                return new
                {
                    count = designs.Count,
                    designs = designs.Select(d => new
                    {
                        id = d.Id,
                        outputUrl = d.OutputUrl,
                        outputType = d.OutputType.ToString(),
                        createdAt = d.CreatedAt
                    })
                };
            }

            case "get_renovation_estimates":
            {
                var estimates = (await _unitOfWork.AIRenovationEstimates.GetByUserAsync(userId))
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(5)
                    .ToList();
                return new
                {
                    count = estimates.Count,
                    estimates = estimates.Select(e => new
                    {
                        id = e.Id,
                        estimateNumber = e.EstimateNumber,
                        projectName = e.ProjectName,
                        roomType = e.RoomType,
                        totalEstimate = e.TotalEstimate,
                        currency = e.Currency,
                        createdAt = e.CreatedAt
                    })
                };
            }

            case "create_renovation_estimate":
            {
                var parsedPayload = ParsePayload<CreateRenovationEstimateRequestDto>(action.PayloadJson);
                var payload = NormalizeEstimatorPayload(parsedPayload);
                var estimate = await _renovationEstimatorService.CreateEstimateAsync(userId, payload);
                return new
                {
                    success = true,
                    estimateId = estimate.EstimateId,
                    estimateNumber = estimate.EstimateId,
                    projectName = estimate.ProjectName,
                    totalEstimate = estimate.TotalEstimate,
                    currency = estimate.Currency,
                    summary = estimate.Summary
                };
            }

            case "get_credit_balance":
            {
                var balance = await _creditService.GetBalanceAsync(userId);
                return new
                {
                    success = true,
                    balance = balance.Balance,
                    currency = "credits",
                    note = "Each AI image generation costs 5 credits."
                };
            }

            case "generate_ai_design":
            {
                var payload = ParsePayload<GenerateAIDesignPayload>(action.PayloadJson);
                if (payload == null || string.IsNullOrWhiteSpace(payload.SourceImageUrl))
                    throw new InvalidOperationException("A source image URL is required to generate an AI design. Please upload a room photo in the AI Design Studio first.");

                var prompt = string.IsNullOrWhiteSpace(payload.Prompt)
                    ? "Modern interior renovation with high-quality finishes"
                    : payload.Prompt.Trim();

                try
                {
                    var project = await _aiService.CreateProjectAsync(userId, new CreateAIProjectDto
                    {
                        SourceImageUrl = payload.SourceImageUrl.Trim(),
                        OutputType = AIOutputType.Image,
                        Prompt = prompt,
                        ContextLabel = payload.RoomType ?? "room"
                    });

                    var design = await _aiService.GenerateImageAsync(userId, new GenerateImageDto
                    {
                        ProjectId = project.Id,
                        Prompt = prompt,
                        SourceImageUrl = payload.SourceImageUrl.Trim(),
                        ContextTags = string.IsNullOrWhiteSpace(payload.RoomType)
                            ? null
                            : new List<string> { payload.RoomType }
                    });

                    return new
                    {
                        success = true,
                        designId = design.Id,
                        outputUrl = design.OutputUrl,
                        outputType = design.OutputType.ToString(),
                        projectId = project.Id,
                        prompt,
                        message = "AI design generated successfully. View it in your design library."
                    };
                }
                catch (SubscriptionQuotaException ex)
                {
                    throw new InvalidOperationException($"Design generation blocked: {ex.Message} Visit {_appSettings.FrontendBaseUrl}/pricing to subscribe.");
                }
            }
        }

        if (IsWriteMethod(action.ActionMethod))
        {
            throw new InvalidOperationException($"Write action '{action.Name}' is not handled by ZIORA's executor.");
        }

        return new { success = true, message = "Action logged", action.ActionUrl, action.ActionMethod };
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid Id, string OrderNumber, OrderStatus Status)?> GetLatestOrderAsync(Guid userId)
    {
        var latest = (await _unitOfWork.Orders.GetUserOrdersAsync(userId))
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();
        return latest == null ? null : (latest.Id, latest.OrderNumber, latest.Status);
    }

    private async Task<T?> SafeFetchAsync<T>(Func<Task<T?>> fetch) where T : class
    {
        try { return await fetch(); }
        catch (Exception ex) { _logger.LogWarning(ex, "ZIORA context fetch failed ({Type})", typeof(T).Name); return null; }
    }

    private async Task<T> SafeFetchAsync<T>(Func<Task<T>> fetch, T fallback)
    {
        try { return await fetch(); }
        catch (Exception ex) { _logger.LogWarning(ex, "ZIORA context fetch failed ({Type})", typeof(T).Name); return fallback; }
    }

    private static bool IsCapabilityQuestion(string message)
    {
        var lower = message.Trim().ToLowerInvariant();
        return ContainsAny(lower,
            "can you", "do you have", "are you able", "is it possible", "do you support",
            "what can you", "what do you", "how do you", "tell me about yourself",
            "what is ziora", "who are you", "what are you", "how does", "how do i",
            "what features", "what services", "do you offer", "can i", "will you be able",
            "are you capable", "do you know how", "is there a way") &&
        !ContainsAny(lower,
            "design my", "design the", "generate a design", "create a design",
            "visualise", "visualize", "show me what", "make it look",
            "estimate my", "estimate for my", "calculate", "create an estimate",
            "show my orders", "show my cart", "show my designs", "fetch", "get my");
    }

    private static bool ContainsAny(string source, params string[] keywords)
        => keywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static CreateRenovationEstimateRequestDto NormalizeEstimatorPayload(CreateRenovationEstimateRequestDto? payload)
    {
        payload ??= new CreateRenovationEstimateRequestDto();
        payload.ProjectName = string.IsNullOrWhiteSpace(payload.ProjectName) ? "ZIORA Renovation Estimate" : payload.ProjectName.Trim();
        payload.RoomType = string.IsNullOrWhiteSpace(payload.RoomType) ? "general" : payload.RoomType.Trim().ToLowerInvariant();
        payload.FinishLevel = string.IsNullOrWhiteSpace(payload.FinishLevel) ? "standard" : payload.FinishLevel.Trim().ToLowerInvariant();
        payload.LengthMeters = payload.LengthMeters > 0 ? payload.LengthMeters : 4m;
        payload.WidthMeters = payload.WidthMeters > 0 ? payload.WidthMeters : 3m;
        payload.HeightMeters = payload.HeightMeters > 0 ? payload.HeightMeters : 2.8m;
        payload.ContingencyPercent = Math.Clamp(payload.ContingencyPercent, 0m, 30m);
        return payload;
    }

    // ─── Mappers ──────────────────────────────────────────────────────────────

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
            LatestApproval = approval == null ? null : new AssistantToolApprovalDto
            {
                ApprovalId = approval.Id,
                ActionId = approval.ToolActionId,
                Status = approval.Status.ToString().ToLowerInvariant(),
                Reason = approval.Reason,
                CreatedAtUtc = approval.CreatedAt,
                ReviewedAtUtc = approval.ReviewedAtUtc
            },
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

    // ─── Normalizers & Sanitizers ─────────────────────────────────────────────

    private static bool IsApproved(AIAssistantToolAction action, Guid userId) =>
        action.Approvals
            .Where(a => !a.IsDeleted && a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault()?.Status == AIAssistantApprovalStatus.Approved;

    private static string NormalizeIntent(string? intent) =>
        string.IsNullOrWhiteSpace(intent) ? "general" : intent.Trim().ToLowerInvariant();

    private static string NormalizeMethod(string? method) =>
        method?.Trim().ToUpperInvariant() is "POST" or "PUT" or "PATCH" or "DELETE"
            ? method!.Trim().ToUpperInvariant()
            : "GET";

    private static bool IsWriteMethod(string method) =>
        method is "POST" or "PUT" or "PATCH" or "DELETE";

    private static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "/";
        var t = url.Trim();
        return t.StartsWith('/') || t.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? t : $"/{t.TrimStart('/')}";
    }

    private static string? NormalizePayload(string? payload)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            using var d = JsonDocument.Parse(payload);
            return JsonSerializer.Serialize(d.RootElement, JsonOptions);
        }
        catch { return null; }
    }

    private static T? ParsePayload<T>(string? json) where T : class
    {
        try { return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch { return null; }
    }

    private static string NormalizeActionName(string? name, string method, string? url)
    {
        var n = (name ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
        if (!string.IsNullOrWhiteSpace(n)) return n;

        var u = NormalizeUrl(url).ToLowerInvariant();
        if (u.Contains("renovation/estimate") && method == "POST") return "create_renovation_estimate";
        if (u.Contains("/invoice")) return "get_invoice_link";
        if (u.Contains("/tracking")) return "get_tracking_link";
        if (u.Contains("/designs")) return "get_designs";
        if (u.Contains("/cart")) return "get_cart_summary";
        if (u.Contains("renovation/estimates")) return "get_renovation_estimates";
        if (u.Contains("/materials") || u.Contains("/flooring")) return "list_materials";
        if (u.Contains("/products/featured")) return "get_featured_products";
        if (u.Contains("/products")) return "list_materials";
        if (u.Contains("/orders") && u.Contains("/tracking")) return "get_tracking_link";
        if (u.Contains("/orders") && u.Contains("/invoice")) return "get_invoice_link";
        if (u.Contains("/orders/") && method == "GET") return "get_order_detail";
        if (u.Contains("/orders") && method == "GET") return "get_orders";
        if (u.Contains("/design-sessions") && method == "POST") return "start_design_session";
        if (u.Contains("/ai/credits") || u.Contains("credits")) return "get_credit_balance";
        if (u.Contains("/ai/generate") && method == "POST") return "generate_ai_design";
        return "custom_action";
    }

    private static List<AssistantLinkDto> SanitizeLinks(List<AssistantLinkDto>? links) =>
        links?
            .Where(l => !string.IsNullOrWhiteSpace(l.Label) && !string.IsNullOrWhiteSpace(l.Url))
            .Select(l => new AssistantLinkDto
            {
                Label = l.Label.Trim(),
                Url = ToFrontendUrl(l.Url),
                Method = "GET",
                Description = string.IsNullOrWhiteSpace(l.Description) ? null : l.Description.Trim()
            })
            .Take(12)
            .ToList() ?? new List<AssistantLinkDto>();

    private static string ToFrontendUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "/";
        var raw = url.Trim();

        // Already a frontend route — return as-is
        if (!raw.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return raw.StartsWith('/') ? raw : $"/{raw}";

        var u = raw.ToLowerInvariant();

        // Extract an orderId segment if present: /api/v1/dashboard/orders/{id}/...
        static string? ExtractSegmentAfter(string path, string segment)
        {
            var idx = path.IndexOf(segment, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var rest = path[(idx + segment.Length)..].TrimStart('/');
            var end = rest.IndexOf('/');
            return end > 0 ? rest[..end] : (rest.Length > 0 ? rest : null);
        }

        if (u.Contains("/api/v1/dashboard/orders"))
        {
            var orderId = ExtractSegmentAfter(raw, "/orders/");
            if (!string.IsNullOrWhiteSpace(orderId) && Guid.TryParse(orderId, out _))
                return $"/orders/{orderId}";
            return "/orders";
        }

        if (u.Contains("/api/v1/products")) return "/materials";
        if (u.Contains("/api/v1/cart"))     return "/cart";
        if (u.Contains("/api/v1/designs"))  return "/ai-gallery";
        if (u.Contains("/api/v1/ai/renovation")) return "/renovation-estimator";
        if (u.Contains("/api/v1/ai/generate"))   return "/ai-visualizer";
        if (u.Contains("/api/v1/ai/credits"))     return "/ai-visualizer";
        if (u.Contains("/api/v1/design-sessions")) return "/design-sessions";
        if (u.Contains("/api/v1/auth"))  return "/profile";
        if (u.Contains("/pricing"))      return "/pricing";

        // Fallback: strip /api/v1 prefix so at least it's not an API path
        if (u.StartsWith("/api/v1/"))
            return raw["/api/v1".Length..];

        return raw;
    }

    private static List<AssistantLlmTaskPlanDto> SanitizeTasks(List<AssistantLlmTaskPlanDto>? tasks) =>
        tasks?
            .Where(t => !string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Description))
            .Select(t => new AssistantLlmTaskPlanDto
            {
                Title = t.Title.Trim(),
                Description = t.Description.Trim(),
                ActionUrl = ToFrontendUrl(t.ActionUrl),
                ActionMethod = NormalizeMethod(t.ActionMethod),
                RequiresApproval = t.RequiresApproval
            })
            .Take(12)
            .ToList() ?? new List<AssistantLlmTaskPlanDto>();

    private static List<AssistantLlmToolActionPlanDto> SanitizeActions(List<AssistantLlmToolActionPlanDto>? actions) =>
        actions?
            .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.ActionUrl))
            .Select(a => new AssistantLlmToolActionPlanDto
            {
                Name = a.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(a.Description) ? a.Name.Trim() : a.Description.Trim(),
                ActionUrl = ToFrontendUrl(a.ActionUrl),
                ActionMethod = NormalizeMethod(a.ActionMethod),
                RequiresApproval = a.RequiresApproval || IsWriteMethod(NormalizeMethod(a.ActionMethod)),
                PayloadJson = NormalizePayload(a.PayloadJson)
            })
            .Take(12)
            .ToList() ?? new List<AssistantLlmToolActionPlanDto>();

    private static AIAssistantTaskStatus ParseTaskStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "pending" => AIAssistantTaskStatus.Pending,
            "in_progress" or "inprogress" => AIAssistantTaskStatus.InProgress,
            "awaiting_approval" or "awaitingapproval" => AIAssistantTaskStatus.AwaitingApproval,
            "completed" => AIAssistantTaskStatus.Completed,
            "cancelled" => AIAssistantTaskStatus.Cancelled,
            "failed" => AIAssistantTaskStatus.Failed,
            _ => throw new InvalidOperationException("Invalid task status.")
        };

    // ─── Private payload models ───────────────────────────────────────────────

    private sealed class OrderRefPayload { public Guid? OrderId { get; set; } }
    private sealed class ListMaterialsPayload { public string? Search { get; set; } public int? Limit { get; set; } }
    private sealed class GenerateAIDesignPayload
    {
        public string? SourceImageUrl { get; set; }
        public string? Prompt { get; set; }
        public string? RoomType { get; set; }
    }
}
