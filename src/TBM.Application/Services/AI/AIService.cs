using System.Text.Json;
using TBM.Application.DTOs.AI;
using TBM.Core.Entities;
using TBM.Core.Entities.AI;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.AI;
using TBM.Core.Models.AI;

namespace TBM.Application.Services;

public class AIService
{
    private const string ProviderMetadataCategory = "AIProviderMetadata";

    private readonly IUnitOfWork _uow;
    private readonly IAIProvider _provider;
    private readonly AIGeneratedMediaService _generatedMediaService;
    private readonly AICreditService _creditService;

    public AIService(
        IUnitOfWork uow,
        IAIProvider provider,
        AIGeneratedMediaService generatedMediaService,
        AICreditService creditService)
    {
        _uow = uow;
        _provider = provider;
        _generatedMediaService = generatedMediaService;
        _creditService = creditService;
    }

    public async Task<AIProject> CreateProjectAsync(Guid userId, CreateAIProjectDto dto)
    {
        var project = new AIProject
        {
            UserId = userId,
            SourceImageUrl = dto.SourceImageUrl,
            GenerationType = dto.GenerationType,
            Prompt = dto.Prompt,
            ContextLabel = dto.ContextLabel,
            Status = AIProjectStatus.Pending
        };

        await _uow.AIProjects.CreateAsync(project);
        await _uow.SaveChangesAsync();
        return project;
    }

    public async Task<AIDesign> GenerateImageAsync(Guid userId, GenerateImageDto dto)
    {
        var project = await GetAuthorizedProjectAsync(userId, dto.ProjectId);
        if (project.GenerationType != AIGenerationType.ImageToImage)
        {
            throw new InvalidOperationException("Project generation type does not support image generation.");
        }

        var sourceImageUrl = string.IsNullOrWhiteSpace(dto.SourceImageUrl)
            ? project.SourceImageUrl
            : dto.SourceImageUrl;

        await _creditService.DeductForGenerationAsync(userId, AIGenerationType.ImageToImage, project.Id);

        var designCommitted = false;
        try
        {
            project.Status = AIProjectStatus.Processing;
            await _uow.SaveChangesAsync();

            var providerResult = await _provider.GenerateImageAsync(new AIImageRequest
            {
                Prompt = dto.Prompt,
                ImageUrl = sourceImageUrl
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

        await _creditService.DeductForGenerationAsync(userId, AIGenerationType.ImageToVideo, project.Id);

        var designCommitted = false;
        try
        {
            project.Status = AIProjectStatus.Processing;
            await _uow.SaveChangesAsync();

            var providerResult = await _provider.GenerateVideoAsync(new AIVideoRequest
            {
                Prompt = dto.Prompt,
                ImageUrl = dto.SourceImageUrl ?? project.SourceImageUrl,
                DurationSeconds = dto.DurationSeconds
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
                DurationSeconds = dto.DurationSeconds,
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
