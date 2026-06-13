using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.DesignFlow;
using TBM.Core.Entities.DesignFlow;
using TBM.Core.Entities.Orders;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Services;

namespace TBM.Application.Services.DesignFlow;

public class ProjectService
{
    private const long MaxDocumentSizeBytes = 25 * 1024 * 1024;
    private const long MaxGalleryImageSizeBytes = 10 * 1024 * 1024;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageStorageService _imageStorageService;
    private readonly ProjectTimelineService _timelineService;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IUnitOfWork unitOfWork,
        IImageStorageService imageStorageService,
        ProjectTimelineService timelineService,
        ILogger<ProjectService> logger)
    {
        _unitOfWork = unitOfWork;
        _imageStorageService = imageStorageService;
        _timelineService = timelineService;
        _logger = logger;
    }

    public async Task<Project?> CreateProjectFromOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null || order.PaymentStatus != PaymentStatus.Paid)
        {
            return null;
        }

        var existingProject = await _unitOfWork.Projects.GetByOrderIdAsync(order.Id)
            ?? (order.DesignSessionId.HasValue
                ? await _unitOfWork.Projects.GetByDesignSessionIdAsync(order.DesignSessionId.Value)
                : null);

        if (existingProject != null)
        {
            await EnsureDesignSessionLinkAsync(order, existingProject);
            return existingProject;
        }

        if (!order.DesignSessionId.HasValue)
        {
            return null;
        }

        var session = await _unitOfWork.DesignSessions.GetByIdAsync(order.DesignSessionId.Value);
        if (session == null)
        {
            _logger.LogWarning("Paid order {OrderId} referenced a missing design session {SessionId}", order.Id, order.DesignSessionId);
            return null;
        }

        if (!order.UserId.HasValue || session.UserId != order.UserId.Value)
        {
            _logger.LogWarning("Design session {SessionId} does not belong to order user {UserId}", session.Id, order.UserId);
            return null;
        }

        var bom = session.BOMId.HasValue
            ? await _unitOfWork.BillsOfMaterials.GetByIdAsync(session.BOMId.Value)
            : await _unitOfWork.BillsOfMaterials.GetByDesignSessionIdAsync(session.Id);

        var projectNumber = await _unitOfWork.Projects.GenerateProjectNumberAsync();
        var totalBudget = bom?.TotalEstimatedCost > 0 ? bom.TotalEstimatedCost : order.Total;
        var amountPaid = order.Total;
        var amountPending = Math.Max(0m, totalBudget - amountPaid);

        var project = new Project
        {
            ProjectNumber = projectNumber,
            UserId = order.UserId.Value,
            DesignSessionId = session.Id,
            OrderId = order.Id,
            BOMId = bom?.Id,
            VendorId = null,
            Name = session.ProjectName,
            Description = $"Auto-created from design session {session.SessionNumber} for {session.RoomType}.",
            RoomType = session.RoomType,
            Status = ProjectStatus.InProgress,
            StartDate = DateTime.UtcNow,
            TotalBudget = totalBudget,
            AmountPaid = amountPaid,
            AmountPending = amountPending
        };

        await _unitOfWork.Projects.CreateAsync(project);
        await _unitOfWork.SaveChangesAsync();

        var timelines = await _timelineService.GenerateTimelineFromBOMAsync(project, bom, cancellationToken);
        if (timelines.Any())
        {
            project.ExpectedCompletionDate = timelines
                .Where(x => x.PlannedDate.HasValue)
                .Max(x => x.PlannedDate);
        }

        await _unitOfWork.SaveChangesAsync();

        session.ProjectId = project.Id;
        session.Status = DesignSessionStatus.ConvertedToProject;
        session.CurrentStep = "Project created";
        session.Progress = 100;
        session.ErrorMessage = null;
        if (!session.BOMId.HasValue && bom != null)
        {
            session.BOMId = bom.Id;
        }

        await _unitOfWork.DesignSessions.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync();

        var reloaded = await _unitOfWork.Projects.GetByIdAsync(project.Id);
        return reloaded ?? project;
    }

    public async Task<ApiResponse<ProjectListDto>> GetUserProjectsAsync(Guid userId)
    {
        var projects = await _unitOfWork.Projects.GetByUserAsync(userId);
        var result = new ProjectListDto
        {
            Projects = projects.Select(MapProjectDto).ToList()
        };

        return ApiResponse<ProjectListDto>.SuccessResponse(result);
    }

    public async Task<ApiResponse<ProjectDetailDto>> GetProjectDetailsAsync(Guid projectId, Guid userId)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        if (project == null || project.UserId != userId)
        {
            return ApiResponse<ProjectDetailDto>.ErrorResponse("Project not found");
        }

        var bom = project.BOMId.HasValue
            ? await _unitOfWork.BillsOfMaterials.GetByIdAsync(project.BOMId.Value)
            : null;

        var detail = new ProjectDetailDto
        {
            Project = MapProjectDto(project),
            Timeline = project.Timelines
                .OrderBy(x => x.SortOrder)
                .Select(MapTimelineDto)
                .ToList(),
            Materials = bom?.Items.Select(MapBomItemDto).ToList() ?? new List<BomItemDto>(),
            Financial = new ProjectFinancialDto
            {
                TotalBudget = project.TotalBudget,
                AmountPaid = project.AmountPaid,
                AmountPending = project.AmountPending
            },
            Vendor = null
        };

        return ApiResponse<ProjectDetailDto>.SuccessResponse(detail);
    }

    public async Task<ApiResponse<List<ProjectTimelineDto>>> GetProjectTimelineAsync(Guid projectId, Guid userId)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        if (project == null || project.UserId != userId)
        {
            return ApiResponse<List<ProjectTimelineDto>>.ErrorResponse("Project not found");
        }

        var timelines = project.Timelines
            .OrderBy(x => x.SortOrder)
            .Select(MapTimelineDto)
            .ToList();

        return ApiResponse<List<ProjectTimelineDto>>.SuccessResponse(timelines);
    }

    public async Task<ApiResponse<List<ProjectDocumentDto>>> GetProjectDocumentsAsync(Guid projectId, Guid userId)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        if (project == null || project.UserId != userId)
        {
            return ApiResponse<List<ProjectDocumentDto>>.ErrorResponse("Project not found");
        }

        var documents = project.Documents
            .OrderByDescending(x => x.UploadedAt)
            .Select(MapDocumentDto)
            .ToList();

        return ApiResponse<List<ProjectDocumentDto>>.SuccessResponse(documents);
    }

    public async Task<ApiResponse<UploadProjectDocumentResponseDto>> UploadDocumentAsync(
        Guid projectId,
        Guid userId,
        UploadProjectDocumentRequestDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
        {
            return ApiResponse<UploadProjectDocumentResponseDto>.ErrorResponse("File is required");
        }

        if (dto.File.Length > MaxDocumentSizeBytes)
        {
            return ApiResponse<UploadProjectDocumentResponseDto>.ErrorResponse("File exceeds the 25MB limit");
        }

        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        if (project == null || project.UserId != userId)
        {
            return ApiResponse<UploadProjectDocumentResponseDto>.ErrorResponse("Project not found");
        }

        await using var stream = dto.File.OpenReadStream();
        var fileName = BuildFileName("project-document", dto.File.FileName, ".pdf");
        var url = await _imageStorageService.UploadDocumentAsync(
            stream,
            fileName,
            userId.ToString("N"),
            dto.File.ContentType);

        var document = new ProjectDocument
        {
            ProjectId = project.Id,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? dto.File.FileName : dto.Name.Trim(),
            Type = string.IsNullOrWhiteSpace(dto.Type) ? InferDocumentType(dto.File.ContentType, dto.File.FileName) : dto.Type.Trim(),
            FileUrl = url,
            FileSize = dto.File.Length,
            MimeType = string.IsNullOrWhiteSpace(dto.File.ContentType) ? "application/octet-stream" : dto.File.ContentType,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.Projects.AddDocumentAsync(document);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<UploadProjectDocumentResponseDto>.SuccessResponse(new UploadProjectDocumentResponseDto
        {
            DocumentId = document.Id,
            FileUrl = document.FileUrl
        });
    }

    public async Task<ApiResponse<List<ProjectGalleryImageDto>>> GetProjectGalleryAsync(Guid projectId, Guid userId)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        if (project == null || project.UserId != userId)
        {
            return ApiResponse<List<ProjectGalleryImageDto>>.ErrorResponse("Project not found");
        }

        var images = project.GalleryImages
            .OrderBy(x => x.SortOrder)
            .Select(MapGalleryDto)
            .ToList();

        return ApiResponse<List<ProjectGalleryImageDto>>.SuccessResponse(images);
    }

    public async Task<ApiResponse<UploadProjectGalleryImageResponseDto>> UploadGalleryImageAsync(
        Guid projectId,
        Guid userId,
        UploadProjectGalleryImageRequestDto dto)
    {
        if (dto.Image == null || dto.Image.Length == 0)
        {
            return ApiResponse<UploadProjectGalleryImageResponseDto>.ErrorResponse("Image is required");
        }

        if (dto.Image.Length > MaxGalleryImageSizeBytes)
        {
            return ApiResponse<UploadProjectGalleryImageResponseDto>.ErrorResponse("Image exceeds the 10MB limit");
        }

        if (!IsImageContentType(dto.Image.ContentType))
        {
            return ApiResponse<UploadProjectGalleryImageResponseDto>.ErrorResponse("Only image files are allowed");
        }

        var project = await _unitOfWork.Projects.GetByIdAsync(projectId);
        if (project == null || project.UserId != userId)
        {
            return ApiResponse<UploadProjectGalleryImageResponseDto>.ErrorResponse("Project not found");
        }

        await using var stream = dto.Image.OpenReadStream();
        var fileName = BuildFileName("project-gallery", dto.Image.FileName, ".png");
        var url = await _imageStorageService.UploadGalleryImageAsync(
            stream,
            fileName,
            userId.ToString("N"),
            dto.Image.ContentType);

        var nextSortOrder = project.GalleryImages.Count == 0
            ? 1
            : project.GalleryImages.Max(x => x.SortOrder) + 1;

        var image = new SiteGalleryImage
        {
            ProjectId = project.Id,
            ImageUrl = url,
            Thumbnail = url,
            Caption = string.IsNullOrWhiteSpace(dto.Caption) ? null : dto.Caption.Trim(),
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
            SortOrder = nextSortOrder
        };

        await _unitOfWork.Projects.AddGalleryImageAsync(image);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<UploadProjectGalleryImageResponseDto>.SuccessResponse(new UploadProjectGalleryImageResponseDto
        {
            ImageId = image.Id,
            ImageUrl = image.ImageUrl
        });
    }

    public async Task<ApiResponse<bool>> EnsureDesignSessionLinkAsync(Order order, Project project)
    {
        var sessionId = order.DesignSessionId;
        if (!sessionId.HasValue)
        {
            return ApiResponse<bool>.SuccessResponse(true);
        }

        var session = await _unitOfWork.DesignSessions.GetByIdAsync(sessionId.Value);
        if (session == null)
        {
            return ApiResponse<bool>.ErrorResponse("Design session not found");
        }

        session.OrderId = order.Id;
        session.ProjectId = project.Id;
        session.Status = DesignSessionStatus.ConvertedToProject;
        session.Progress = 100;
        session.CurrentStep = "Project created";
        await _unitOfWork.DesignSessions.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<bool>.SuccessResponse(true);
    }

    private static ProjectDto MapProjectDto(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            ProjectNumber = project.ProjectNumber,
            UserId = project.UserId,
            DesignSessionId = project.DesignSessionId,
            OrderId = project.OrderId,
            BOMId = project.BOMId,
            VendorId = project.VendorId,
            Name = project.Name,
            Description = project.Description,
            RoomType = project.RoomType,
            Status = project.Status.ToString(),
            StartDate = project.StartDate,
            ExpectedCompletionDate = project.ExpectedCompletionDate,
            ActualCompletionDate = project.ActualCompletionDate,
            Financial = new ProjectFinancialDto
            {
                TotalBudget = project.TotalBudget,
                AmountPaid = project.AmountPaid,
                AmountPending = project.AmountPending
            },
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt ?? project.CreatedAt
        };
    }

    private static ProjectTimelineDto MapTimelineDto(ProjectTimeline timeline)
    {
        return new ProjectTimelineDto
        {
            Id = timeline.Id,
            ProjectId = timeline.ProjectId,
            MilestoneName = timeline.MilestoneName,
            Description = timeline.Description,
            PlannedDate = timeline.PlannedDate,
            ActualDate = timeline.ActualDate,
            Status = timeline.Status.ToString(),
            SortOrder = timeline.SortOrder
        };
    }

    private static ProjectDocumentDto MapDocumentDto(ProjectDocument document)
    {
        return new ProjectDocumentDto
        {
            Id = document.Id,
            ProjectId = document.ProjectId,
            Name = document.Name,
            Type = document.Type,
            FileUrl = document.FileUrl,
            FileSize = document.FileSize,
            MimeType = document.MimeType,
            UploadedBy = document.UploadedBy,
            UploadedAt = document.UploadedAt
        };
    }

    private static ProjectGalleryImageDto MapGalleryDto(SiteGalleryImage image)
    {
        return new ProjectGalleryImageDto
        {
            Id = image.Id,
            ProjectId = image.ProjectId,
            ImageUrl = image.ImageUrl,
            Thumbnail = image.Thumbnail,
            Caption = image.Caption,
            UploadedBy = image.UploadedBy,
            UploadedAt = image.UploadedAt,
            SortOrder = image.SortOrder
        };
    }

    private static BomItemDto MapBomItemDto(BOMItem item)
    {
        return new BomItemDto
        {
            Id = item.Id,
            ProductId = item.ProductId,
            SKU = item.SKU,
            Name = item.Name,
            Description = item.Description,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            InStock = item.InStock,
            VendorId = item.VendorId,
            LeadTimeDays = item.LeadTimeDays,
            Reason = item.Reason
        };
    }

    private static bool IsImageContentType(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) &&
               contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFileName(string prefix, string originalFileName, string extensionFallback)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = extensionFallback;
        }

        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
    }

    private static string InferDocumentType(string? contentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            {
                return "PDF";
            }

            if (contentType.Contains("word", StringComparison.OrdinalIgnoreCase))
            {
                return "Word";
            }

            if (contentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase))
            {
                return "Spreadsheet";
            }
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "PDF",
            ".doc" or ".docx" => "Word",
            ".xls" or ".xlsx" => "Spreadsheet",
            ".ppt" or ".pptx" => "Presentation",
            _ => "Document"
        };
    }
}
