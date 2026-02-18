using TBM.Core.Entities.AI;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.AI;
using TBM.Application.DTOs.AI;
using TBM.Core.Enums;
using TBM.Core.Models.AI; // ✅ ADD THIS

namespace TBM.Application.Services;

public class AIService
{
    private readonly IUnitOfWork _uow;
    private readonly IAIProvider _provider;
    
    public AIService(IUnitOfWork uow, IAIProvider provider)
    {
        _uow = uow;
        _provider = provider;
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
        var project = await _uow.AIProjects.GetByIdAsync(dto.ProjectId);
        if (project == null || project.UserId != userId)
            throw new UnauthorizedAccessException("Invalid AI project");
        
        var result = await _provider.GenerateImageAsync(new AIImageRequest
        {
            Prompt = dto.Prompt,
            ImageUrl = dto.SourceImageUrl
        });
        
        if (!result.Success)
            throw new InvalidOperationException("AI generation failed");
        
        var design = new AIDesign
        {
            AIProjectId = project.Id,
            OutputUrl = result.OutputUrl!,
            OutputType = AIOutputType.Image,
            Provider = _provider.ProviderName
        };
        
        await _uow.AIDesigns.CreateAsync(design);
        await _uow.SaveChangesAsync();
        return design;
    }
    
    public async Task<AIDesign> GenerateVideoAsync(Guid userId, GenerateVideoDto dto)
    {
        // Validate project belongs to user
        var project = await _uow.AIProjects.GetByIdAsync(dto.ProjectId); // ✅ FIXED: _uow not _unitOfWork
        if (project == null || project.UserId != userId)
        {
            throw new UnauthorizedAccessException("Project not found or access denied");
        }
        
        // Prepare AI request
        var videoRequest = new AIVideoRequest
        {
            Prompt = dto.Prompt,
            ImageUrl = dto.SourceImageUrl,
            DurationSeconds = dto.DurationSeconds
        };
        
        // Generate video
        var result = await _provider.GenerateVideoAsync(videoRequest); // ✅ FIXED: _provider not _aiProvider
        
        if (!result.Success)
        {
            throw new Exception($"Video generation failed: {result.ErrorMessage}");
        }
        
        // Save to database
        var design = new AIDesign
        {
            AIProjectId = dto.ProjectId,
            OutputUrl = result.OutputUrl,
            OutputType = AIOutputType.Video,
            Provider = _provider.ProviderName, // ✅ FIXED: _provider not _aiProvider
            DurationSeconds = dto.DurationSeconds,
            Width = 1280,
            Height = 720
        };
        
        await _uow.AIDesigns.CreateAsync(design); // ✅ FIXED: _uow not _unitOfWork
        await _uow.SaveChangesAsync(); // ✅ FIXED: SaveChangesAsync not CompleteAsync
        
        return design;
    }
}