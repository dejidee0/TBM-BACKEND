using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.AI;
using TBM.Application.Interfaces;
using TBM.Application.Services;
using TBM.Application.Services.Subscriptions;
using TBM.Core.Entities.AI;
using TBM.Core.Enums;

namespace TBM.API.Controllers.V1.AI;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
[EnableRateLimiting("DynamicPolicy")]
public class AIController : ControllerBase
{
    private static readonly List<AIStyleDto> AvailableStyles = new()
    {
        new() { Id = "modern", Name = "Modern" },
        new() { Id = "minimalist", Name = "Minimalism" },
        new() { Id = "wabi-sabi", Name = "Wabi-Sabi" },
        new() { Id = "tropical", Name = "Tropical" },
        new() { Id = "farmhouse", Name = "Farmhouse" },
        new() { Id = "memphis", Name = "Memphis" },
        new() { Id = "afro-minimalism", Name = "Afro-Minimalism" },
        new() { Id = "contemporary-african", Name = "Contemporary African" },
        new() { Id = "industrial", Name = "Industrial" },
        new() { Id = "bohemian", Name = "Bohemian" }
    };

    private readonly IAIService _aiService;
    private readonly AIUsageService _aiUsageService;
    private readonly AICreditService _aiCreditService;
    private readonly IProductService _productService;

    public AIController(
        IAIService aiService,
        AIUsageService aiUsageService,
        AICreditService aiCreditService,
        IProductService productService)
    {
        _aiService = aiService;
        _aiUsageService = aiUsageService;
        _aiCreditService = aiCreditService;
        _productService = productService;
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject([FromBody] CreateAIProjectDto dto)
    {
        return await ExecuteAIActionAsync(async () =>
        {
            var userId = GetCurrentUserId();
            var project = await _aiService.CreateProjectAsync(userId, dto);
            return ToProjectResponse(project);
        });
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetCurrentUserId();
        var projects = await _aiService.GetUserProjectsAsync(userId);
        return Ok(projects);
    }

    [HttpPost("generate/image")]
    public async Task<IActionResult> GenerateImage([FromBody] GenerateImageDto dto)
    {
        return await ExecuteAIActionAsync(async () =>
        {
            var design = await _aiService.GenerateImageAsync(GetCurrentUserId(), dto);
            var result = ToGenerationResult(design);
            result.MatchedProducts = await _productService.SearchByAIKeywordsAsync(
                dto.Prompt ?? string.Empty, dto.ContextTags);
            return result;
        });
    }

    [HttpPost("transform/image")]
    public async Task<IActionResult> TransformImage([FromBody] GenerateImageDto dto)
    {
        return await ExecuteAIActionAsync(async () =>
        {
            var design = await _aiService.GenerateImageAsync(GetCurrentUserId(), dto);
            var result = ToGenerationResult(design);
            result.MatchedProducts = await _productService.SearchByAIKeywordsAsync(
                dto.Prompt ?? string.Empty, dto.ContextTags);
            return result;
        });
    }

    [HttpPost("generate/video")]
    public async Task<IActionResult> GenerateVideo([FromBody] GenerateVideoDto dto)
    {
        return await ExecuteAIActionAsync(async () =>
        {
            var design = await _aiService.GenerateVideoAsync(GetCurrentUserId(), dto);
            var result = ToGenerationResult(design);
            result.MatchedProducts = await _productService.SearchByAIKeywordsAsync(
                dto.Prompt ?? string.Empty, dto.ContextTags);
            return result;
        });
    }

    [HttpGet("styles")]
    public IActionResult GetStyles()
    {
        return Ok(AvailableStyles);
    }

    [HttpGet("usage/summary")]
    public async Task<IActionResult> GetUsageSummary([FromQuery] int? year = null, [FromQuery] int? month = null)
    {
        var userId = GetCurrentUserId();
        var summary = await _aiUsageService.GetUserSummaryAsync(userId, year, month);
        return Ok(summary);
    }

    [HttpGet("credits/balance")]
    public async Task<IActionResult> GetCreditBalance()
    {
        var userId = GetCurrentUserId();
        var balance = await _aiCreditService.GetBalanceAsync(userId);
        return Ok(balance);
    }

    private async Task<IActionResult> ExecuteAIActionAsync<T>(Func<Task<T>> action)
    {
        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (SubscriptionQuotaException ex)
        {
            return StatusCode(403, new
            {
                success = false,
                code = ex.ErrorCode,
                message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    private static AIProjectResponseDto ToProjectResponse(AIProject project)
    {
        return new AIProjectResponseDto
        {
            Id = project.Id,
            Status = project.Status,
            GenerationType = project.GenerationType,
            OutputType = MapGenerationTypeToOutputType(project.GenerationType),
            SourceImageUrl = project.SourceImageUrl,
            Prompt = project.Prompt ?? string.Empty,
            ContextLabel = project.ContextLabel,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }

    private static AIGenerationResultDto ToGenerationResult(AIDesign design)
    {
        return new AIGenerationResultDto
        {
            DesignId = design.Id,
            ProjectId = design.AIProjectId,
            OutputType = design.OutputType,
            OutputUrl = design.OutputUrl,
            Width = design.Width,
            Height = design.Height,
            DurationSeconds = design.DurationSeconds,
            Provider = design.Provider,
            ProviderJobId = design.ProviderJobId,
            CreatedAt = design.CreatedAt
        };
    }

    private static AIOutputType MapGenerationTypeToOutputType(AIGenerationType generationType)
    {
        return generationType switch
        {
            AIGenerationType.ImageToImage => AIOutputType.Image,
            AIGenerationType.ImageToVideo => AIOutputType.Video,
            _ => throw new InvalidOperationException("Unsupported generationType.")
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID claim missing.");
        }

        return userId;
    }
}
