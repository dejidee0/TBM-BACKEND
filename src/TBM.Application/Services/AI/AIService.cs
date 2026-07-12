using System.Text.Json;
using TBM.Application.DTOs.AI;
using TBM.Application.Interfaces;
using TBM.Application.Services.Subscriptions;
using TBM.Core.Entities;
using TBM.Core.Entities.AI;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.AI;
using TBM.Core.Models.AI;

namespace TBM.Application.Services;

public class AIService : IAIService
{
    private const string ProviderMetadataCategory = "AIProviderMetadata";

    private readonly IUnitOfWork _uow;
    private readonly IAIProvider _provider;
    private readonly AIGeneratedMediaService _generatedMediaService;
    private readonly AICreditService _creditService;
    private readonly SubscriptionService _subscriptionService;

    public AIService(
        IUnitOfWork uow,
        IAIProvider provider,
        AIGeneratedMediaService generatedMediaService,
        AICreditService creditService,
        SubscriptionService subscriptionService)
    {
        _uow = uow;
        _provider = provider;
        _generatedMediaService = generatedMediaService;
        _creditService = creditService;
        _subscriptionService = subscriptionService;
    }

    public async Task<AIProject> CreateProjectAsync(Guid userId, CreateAIProjectDto dto)
    {
        var sourceImageUrl = RequireText(dto.SourceImageUrl, "sourceImageUrl is required when creating an AI project.");
        var prompt = RequireText(dto.Prompt, "prompt is required when creating an AI project.");
        var generationType = ResolveGenerationType(dto);

        var project = new AIProject
        {
            UserId = userId,
            SourceImageUrl = sourceImageUrl,
            GenerationType = generationType,
            Prompt = prompt,
            ContextLabel = NormalizeText(dto.ContextLabel),
            Status = AIProjectStatus.Pending
        };

        await _uow.AIProjects.CreateAsync(project);
        await _uow.SaveChangesAsync();
        return project;
    }

    public async Task<List<AIProjectListItemDto>> GetUserProjectsAsync(Guid userId)
    {
        var projects = await _uow.AIProjects.GetByUserAsync(userId);

        return projects
            .OrderByDescending(x => x.CreatedAt)
            .Select(project =>
            {
                var activeDesigns = project.Designs
                    .Where(x => !x.IsDeleted)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList();

                return new AIProjectListItemDto
                {
                    Id = project.Id,
                    Status = project.Status,
                    GenerationType = project.GenerationType,
                    OutputType = MapGenerationTypeToOutputType(project.GenerationType),
                    ContextLabel = project.ContextLabel,
                    CreatedAt = project.CreatedAt,
                    LatestDesignUrl = activeDesigns.FirstOrDefault()?.OutputUrl,
                    DesignCount = activeDesigns.Count
                };
            })
            .ToList();
    }

    public async Task<AIDesign> GenerateImageAsync(Guid userId, GenerateImageDto dto)
    {
        var project = await GetAuthorizedProjectAsync(userId, dto.ProjectId);
        if (project.GenerationType != AIGenerationType.ImageToImage)
        {
            throw new InvalidOperationException("Project generation type does not support image generation.");
        }

        var prompt = ResolvePrompt(dto.Prompt, project, "image generation");
        var enrichedPrompt = EnrichPromptWithTags(prompt, dto.ContextTags);
        var sourceImageUrl = ResolveSourceImageUrl(dto.SourceImageUrl, project, "image generation");

        // Subscription gate — throws SubscriptionQuotaException if user has no active plan or quota exceeded
        await _subscriptionService.CheckAndIncrementQuotaAsync(userId);

        var designCommitted = false;
        try
        {
            project.Prompt = prompt;
            project.SourceImageUrl = sourceImageUrl;
            project.Status = AIProjectStatus.Processing;
            await _uow.SaveChangesAsync();

            var providerResult = await _provider.GenerateImageAsync(new AIImageRequest
            {
                Prompt = enrichedPrompt,
                ImageUrl = sourceImageUrl,
                Style = dto.Style
            });

            if (!providerResult.Success || string.IsNullOrWhiteSpace(providerResult.OutputUrl))
            {
                throw new InvalidOperationException(providerResult.ErrorMessage ?? "AI image generation failed.");
            }

            var persisted = await _generatedMediaService.PersistAsync(
                userId,
                providerResult.OutputUrl,
                AIOutputType.Image);

            var design = new AIDesign
            {
                AIProjectId = project.Id,
                OutputUrl = persisted.CloudinaryUrl,
                OutputType = AIOutputType.Image,
                Provider = _provider.ProviderName,
                ProviderJobId = providerResult.ProviderJobId,
                Width = 1024,
                Height = 1024
            };

            await _uow.AIDesigns.CreateAsync(design);
            await _uow.Settings.AddAsync(BuildProviderMetadataSetting(
                design.Id,
                userId,
                project.Id,
                AIGenerationType.ImageToImage,
                AIOutputType.Image,
                providerResult,
                persisted));

            await _uow.AIUsages.CreateAsync(new AIUsage
            {
                UserId = userId,
                AIProjectId = project.Id,
                GenerationType = AIGenerationType.ImageToImage,
                CreditsUsed = _creditService.GetCreditsRequired(AIGenerationType.ImageToImage),
                EstimatedCost = providerResult.Cost,
                Provider = _provider.ProviderName
            });

            project.Status = AIProjectStatus.Completed;
            await _uow.SaveChangesAsync();
            designCommitted = true;
            return design;
        }
        catch (Exception ex)
        {
            if (!designCommitted)
            {
                await TryRefundCreditsAsync(userId, AIGenerationType.ImageToImage, project.Id, "Image generation failed");
                // Note: subscription quota increment was already done optimistically above; no rollback needed
                await MarkProjectFailedAsync(project);
                await TryRecordFailedUsageAsync(userId, project.Id, AIGenerationType.ImageToImage);
            }

            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public async Task<AIDesign> GenerateVideoAsync(Guid userId, GenerateVideoDto dto)
    {
        var project = await GetAuthorizedProjectAsync(userId, dto.ProjectId);
        if (project.GenerationType != AIGenerationType.ImageToVideo)
        {
            throw new InvalidOperationException("Project generation type does not support video generation.");
        }

        var prompt = ResolvePrompt(dto.Prompt, project, "video generation");
        var enrichedPrompt = EnrichPromptWithTags(prompt, dto.ContextTags);
        var sourceImageUrl = ResolveSourceImageUrl(dto.SourceImageUrl, project, "video generation");
        var durationSeconds = ResolveDurationSeconds(dto.DurationSeconds);

        // Subscription gate — throws SubscriptionQuotaException if user has no active plan or quota exceeded
        await _subscriptionService.CheckAndIncrementQuotaAsync(userId);

        var designCommitted = false;
        try
        {
            project.Prompt = prompt;
            project.SourceImageUrl = sourceImageUrl;
            project.Status = AIProjectStatus.Processing;
            await _uow.SaveChangesAsync();

            var providerResult = await _provider.GenerateVideoAsync(new AIVideoRequest
            {
                Prompt = enrichedPrompt,
                ImageUrl = sourceImageUrl,
                DurationSeconds = durationSeconds
            });

            if (!providerResult.Success || string.IsNullOrWhiteSpace(providerResult.OutputUrl))
            {
                throw new InvalidOperationException(providerResult.ErrorMessage ?? "AI video generation failed.");
            }

            var persisted = await _generatedMediaService.PersistAsync(
                userId,
                providerResult.OutputUrl,
                AIOutputType.Video);

            var design = new AIDesign
            {
                AIProjectId = project.Id,
                OutputUrl = persisted.CloudinaryUrl,
                OutputType = AIOutputType.Video,
                Provider = _provider.ProviderName,
                ProviderJobId = providerResult.ProviderJobId,
                DurationSeconds = durationSeconds,
                Width = 1280,
                Height = 720
            };

            await _uow.AIDesigns.CreateAsync(design);
            await _uow.Settings.AddAsync(BuildProviderMetadataSetting(
                design.Id,
                userId,
                project.Id,
                AIGenerationType.ImageToVideo,
                AIOutputType.Video,
                providerResult,
                persisted));

            await _uow.AIUsages.CreateAsync(new AIUsage
            {
                UserId = userId,
                AIProjectId = project.Id,
                GenerationType = AIGenerationType.ImageToVideo,
                CreditsUsed = _creditService.GetCreditsRequired(AIGenerationType.ImageToVideo),
                EstimatedCost = providerResult.Cost,
                Provider = _provider.ProviderName
            });

            project.Status = AIProjectStatus.Completed;
            await _uow.SaveChangesAsync();
            designCommitted = true;
            return design;
        }
        catch (Exception ex)
        {
            if (!designCommitted)
            {
                await TryRefundCreditsAsync(userId, AIGenerationType.ImageToVideo, project.Id, "Video generation failed");
                await MarkProjectFailedAsync(project);
                await TryRecordFailedUsageAsync(userId, project.Id, AIGenerationType.ImageToVideo);
            }

            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private async Task<AIProject> GetAuthorizedProjectAsync(Guid userId, Guid projectId)
    {
        var project = await _uow.AIProjects.GetByIdAsync(projectId);
        if (project == null || project.UserId != userId)
        {
            throw new UnauthorizedAccessException("Invalid AI project.");
        }

        return project;
    }

    private static AIGenerationType ResolveGenerationType(CreateAIProjectDto dto)
    {
        if (dto.OutputType.HasValue)
        {
            var resolvedFromOutputType = MapOutputTypeToGenerationType(dto.OutputType.Value);
            if (dto.GenerationType.HasValue && dto.GenerationType.Value != resolvedFromOutputType)
            {
                throw new InvalidOperationException("outputType and generationType do not match.");
            }

            return resolvedFromOutputType;
        }

        if (!dto.GenerationType.HasValue)
        {
            throw new InvalidOperationException("outputType is required when creating an AI project.");
        }

        return dto.GenerationType.Value;
    }

    private static string ResolvePrompt(string? requestedPrompt, AIProject project, string operationName)
    {
        var prompt = NormalizeText(requestedPrompt) ?? NormalizeText(project.Prompt);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException($"prompt is required for {operationName}.");
        }

        return prompt;
    }

    private static string ResolveSourceImageUrl(string? requestedSourceImageUrl, AIProject project, string operationName)
    {
        var sourceImageUrl = NormalizeText(requestedSourceImageUrl) ?? NormalizeText(project.SourceImageUrl);
        if (string.IsNullOrWhiteSpace(sourceImageUrl))
        {
            throw new InvalidOperationException($"sourceImageUrl is required for {operationName}.");
        }

        return sourceImageUrl;
    }

    private static int ResolveDurationSeconds(int durationSeconds)
    {
        if (durationSeconds <= 0)
        {
            throw new InvalidOperationException("durationSeconds must be greater than zero.");
        }

        return durationSeconds;
    }

    private static string RequireText(string? value, string message)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(message);
        }

        return normalized;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string EnrichPromptWithTags(string prompt, List<string>? contextTags)
    {
        if (contextTags == null || contextTags.Count == 0)
        {
            return prompt;
        }

        var tags = contextTags
            .Select(t => t?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tags.Count == 0)
        {
            return prompt;
        }

        return $"{prompt}. Style context: {string.Join(", ", tags)}.";
    }

    private static AIGenerationType MapOutputTypeToGenerationType(AIOutputType outputType)
    {
        return outputType switch
        {
            AIOutputType.Image => AIGenerationType.ImageToImage,
            AIOutputType.Video => AIGenerationType.ImageToVideo,
            _ => throw new InvalidOperationException("Unsupported outputType.")
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

    private Setting BuildProviderMetadataSetting(
        Guid designId,
        Guid userId,
        Guid projectId,
        AIGenerationType generationType,
        AIOutputType outputType,
        AIProviderResult providerResult,
        GeneratedMediaPersistenceResultDto persisted)
    {
        var metadata = new
        {
            designId,
            projectId,
            userId,
            generationType,
            outputType,
            provider = _provider.ProviderName,
            providerJobId = providerResult.ProviderJobId,
            temporaryProviderUrl = persisted.TemporaryProviderUrl,
            cloudinaryUrl = persisted.CloudinaryUrl,
            fileName = persisted.FileName,
            contentType = persisted.ContentType,
            providerRawResponse = providerResult.RawResponse,
            estimatedProviderCost = providerResult.Cost,
            persistedAtUtc = DateTime.UtcNow
        };

        return new Setting
        {
            Category = ProviderMetadataCategory,
            Key = $"design:{designId:N}",
            Value = JsonSerializer.Serialize(metadata),
            Description = "AI provider metadata and temporary URL"
        };
    }

    private async Task TryRefundCreditsAsync(Guid userId, AIGenerationType generationType, Guid projectId, string reason)
    {
        try
        {
            await _creditService.RefundGenerationAsync(userId, generationType, projectId, reason);
        }
        catch
        {
            // best-effort refund in failure path
        }
    }

    private async Task MarkProjectFailedAsync(AIProject project)
    {
        try
        {
            project.Status = AIProjectStatus.Failed;
            await _uow.SaveChangesAsync();
        }
        catch
        {
            // best-effort status update in failure path
        }
    }

    private async Task TryRecordFailedUsageAsync(Guid userId, Guid projectId, AIGenerationType generationType)
    {
        try
        {
            await _uow.AIUsages.CreateAsync(new AIUsage
            {
                UserId = userId,
                AIProjectId = projectId,
                GenerationType = generationType,
                CreditsUsed = 0,
                EstimatedCost = 0m,
                Provider = $"{_provider.ProviderName}:failed"
            });
            await _uow.SaveChangesAsync();
        }
        catch
        {
            // best-effort failure usage in failure path
        }
    }
}
