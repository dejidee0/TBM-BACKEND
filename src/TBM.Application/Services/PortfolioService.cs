using System.IO;
using Microsoft.AspNetCore.Http;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Portfolio;
using TBM.Core.Entities.Portfolio;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Services;

namespace TBM.Application.Services;

public class PortfolioService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageStorageService _imageStorage;

    public PortfolioService(IUnitOfWork unitOfWork, IImageStorageService imageStorage)
    {
        _unitOfWork = unitOfWork;
        _imageStorage = imageStorage;
    }

    // ── VENDOR: create ───────────────────────────────────────────────────────

    public async Task<ApiResponse<PortfolioProjectDto>> CreateAsync(
        Guid vendorId, string vendorName, CreatePortfolioProjectDto dto)
    {
        var project = new VendorPortfolioProject
        {
            VendorId = vendorId,
            VendorName = vendorName,
            Title = dto.Title.Trim(),
            Location = dto.Location.Trim(),
            Category = dto.Category.Trim(),
            BudgetMin = dto.BudgetMin,
            BudgetMax = dto.BudgetMax,
            DurationMinDays = dto.DurationMinDays,
            DurationMaxDays = dto.DurationMaxDays,
            Description = dto.Description.Trim(),
            ScopeOfWork = string.Join("\n", dto.ScopeOfWork.Where(s => !string.IsNullOrWhiteSpace(s))),
            Status = PortfolioStatus.Draft
        };

        await _unitOfWork.Portfolio.CreateAsync(project);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(
            MapToDto(project), "Portfolio project created. Upload images to complete it.");
    }

    // ── VENDOR: update ───────────────────────────────────────────────────────

    public async Task<ApiResponse<PortfolioProjectDto>> UpdateAsync(
        Guid projectId, Guid vendorId, UpdatePortfolioProjectDto dto)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null || project.VendorId != vendorId)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse("Project not found.");

        if (project.Status == PortfolioStatus.Published)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse(
                "Published projects cannot be edited. Unpublish first.");

        project.Title = dto.Title.Trim();
        project.Location = dto.Location.Trim();
        project.Category = dto.Category.Trim();
        project.BudgetMin = dto.BudgetMin;
        project.BudgetMax = dto.BudgetMax;
        project.DurationMinDays = dto.DurationMinDays;
        project.DurationMaxDays = dto.DurationMaxDays;
        project.Description = dto.Description.Trim();
        project.ScopeOfWork = string.Join("\n", dto.ScopeOfWork.Where(s => !string.IsNullOrWhiteSpace(s)));

        await _unitOfWork.Portfolio.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(MapToDto(project), "Updated successfully.");
    }

    // ── VENDOR: publish/unpublish ────────────────────────────────────────────

    public async Task<ApiResponse<PortfolioProjectDto>> PublishAsync(Guid projectId, Guid vendorId)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null || project.VendorId != vendorId)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse("Project not found.");

        if (!project.Images.Any(i => i.ImageType == PortfolioImageType.After))
            return ApiResponse<PortfolioProjectDto>.ErrorResponse(
                "At least one 'After' image is required before publishing.");

        project.Status = PortfolioStatus.Published;
        project.PublishedAt = DateTime.UtcNow;
        project.RejectionReason = null;

        await _unitOfWork.Portfolio.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(MapToDto(project), "Project published.");
    }

    public async Task<ApiResponse<PortfolioProjectDto>> UnpublishAsync(Guid projectId, Guid vendorId)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null || project.VendorId != vendorId)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse("Project not found.");

        project.Status = PortfolioStatus.Draft;
        project.PublishedAt = null;

        await _unitOfWork.Portfolio.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(MapToDto(project), "Project unpublished.");
    }

    // ── VENDOR: delete ───────────────────────────────────────────────────────

    public async Task<ApiResponse<bool>> DeleteAsync(Guid projectId, Guid vendorId)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null || project.VendorId != vendorId)
            return ApiResponse<bool>.ErrorResponse("Project not found.");

        await _unitOfWork.Portfolio.DeleteAsync(projectId);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.SuccessResponse(true, "Project deleted.");
    }

    // ── IMAGE UPLOAD ─────────────────────────────────────────────────────────

    public async Task<ApiResponse<PortfolioImageDto>> UploadImageAsync(
        Guid projectId,
        Guid vendorId,
        IFormFile file,
        PortfolioImageType imageType,
        string? caption = null)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null || project.VendorId != vendorId)
            return ApiResponse<PortfolioImageDto>.ErrorResponse("Project not found.");

        if (file.Length == 0)
            return ApiResponse<PortfolioImageDto>.ErrorResponse("File is empty.");

        if (!IsAllowedImageType(file.FileName, file.ContentType))
            return ApiResponse<PortfolioImageDto>.ErrorResponse(
                "Unsupported file type. Allowed: JPEG, PNG, WebP, GIF, BMP, HEIC, HEIF.");

        if (file.Length > 50 * 1024 * 1024)
            return ApiResponse<PortfolioImageDto>.ErrorResponse("File exceeds 50 MB limit.");

        await using var stream = file.OpenReadStream();
        var imageUrl = await _imageStorage.UploadGalleryImageAsync(
            stream, file.FileName, vendorId.ToString(), file.ContentType);

        var sortOrder = project.Images.Count(i => i.ImageType == imageType);

        var image = new PortfolioProjectImage
        {
            ProjectId = projectId,
            ImageUrl = imageUrl,
            ImageType = imageType,
            Caption = caption,
            SortOrder = sortOrder
        };

        await _unitOfWork.Portfolio.AddImageAsync(image);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioImageDto>.SuccessResponse(
            new PortfolioImageDto { Id = image.Id, ImageUrl = imageUrl, Caption = caption, SortOrder = sortOrder },
            "Image uploaded.");
    }

    public async Task<ApiResponse<bool>> DeleteImageAsync(Guid projectId, Guid imageId, Guid vendorId)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null || project.VendorId != vendorId)
            return ApiResponse<bool>.ErrorResponse("Project not found.");

        var image = await _unitOfWork.Portfolio.GetImageByIdAsync(imageId);
        if (image == null || image.ProjectId != projectId)
            return ApiResponse<bool>.ErrorResponse("Image not found.");

        await _unitOfWork.Portfolio.DeleteImageAsync(imageId);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.SuccessResponse(true, "Image deleted.");
    }

    // ── VENDOR: get own ──────────────────────────────────────────────────────

    public async Task<ApiResponse<List<PortfolioProjectDto>>> GetVendorProjectsAsync(Guid vendorId)
    {
        var projects = await _unitOfWork.Portfolio.GetByVendorAsync(vendorId);
        return ApiResponse<List<PortfolioProjectDto>>.SuccessResponse(
            projects.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<PortfolioProjectDto>> GetVendorProjectByIdAsync(Guid projectId, Guid vendorId)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null || project.VendorId != vendorId)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse("Project not found.");

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(MapToDto(project));
    }

    // ── PUBLIC: browse published showcase ────────────────────────────────────

    public async Task<ApiResponse<PortfolioPagedResultDto>> GetPublishedAsync(
        string? category = null, string? location = null, int page = 1, int pageSize = 12)
    {
        var (items, total) = await _unitOfWork.Portfolio.GetPublishedAsync(category, location, page, pageSize);
        return ApiResponse<PortfolioPagedResultDto>.SuccessResponse(new PortfolioPagedResultDto
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponse<PortfolioProjectDto>> GetPublishedByIdAsync(Guid id)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(id);
        if (project == null || project.Status != PortfolioStatus.Published)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse("Project not found.");

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(MapToDto(project));
    }

    // ── ADMIN: create + upload ────────────────────────────────────────────────

    /// <summary>
    /// Admin creates a portfolio project on behalf of a vendor.
    /// VendorId is set to a fixed admin sentinel (Guid.Empty) so the project
    /// is not tied to any real user account.
    /// </summary>
    public async Task<ApiResponse<PortfolioProjectDto>> AdminCreateAsync(AdminCreatePortfolioProjectDto dto)
    {
        var project = new VendorPortfolioProject
        {
            VendorId = Guid.Empty, // not linked to a user account
            VendorName = string.IsNullOrWhiteSpace(dto.VendorName) ? "Admin Upload" : dto.VendorName.Trim(),
            Title = dto.Title.Trim(),
            Location = dto.Location.Trim(),
            Category = dto.Category.Trim(),
            BudgetMin = dto.BudgetMin,
            BudgetMax = dto.BudgetMax,
            DurationMinDays = dto.DurationMinDays,
            DurationMaxDays = dto.DurationMaxDays,
            Description = dto.Description.Trim(),
            ScopeOfWork = string.Join("\n", dto.ScopeOfWork.Where(s => !string.IsNullOrWhiteSpace(s))),
            Status = dto.PublishImmediately ? PortfolioStatus.Published : PortfolioStatus.Draft,
            PublishedAt = dto.PublishImmediately ? DateTime.UtcNow : null
        };

        await _unitOfWork.Portfolio.CreateAsync(project);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(
            MapToDto(project),
            dto.PublishImmediately
                ? "Portfolio project created and published."
                : "Portfolio project created as Draft. Upload images then publish.");
    }

    /// <summary>Admin uploads an image to any portfolio project (no ownership check).</summary>
    public async Task<ApiResponse<PortfolioImageDto>> AdminUploadImageAsync(
        Guid projectId,
        IFormFile file,
        PortfolioImageType imageType,
        string? caption = null)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null)
            return ApiResponse<PortfolioImageDto>.ErrorResponse("Project not found.");

        if (file.Length == 0)
            return ApiResponse<PortfolioImageDto>.ErrorResponse("File is empty.");

        if (!IsAllowedImageType(file.FileName, file.ContentType))
            return ApiResponse<PortfolioImageDto>.ErrorResponse(
                "Unsupported file type. Allowed: JPEG, PNG, WebP, GIF, BMP, HEIC, HEIF.");

        if (file.Length > 50 * 1024 * 1024)
            return ApiResponse<PortfolioImageDto>.ErrorResponse("File exceeds 50 MB limit.");

        await using var stream = file.OpenReadStream();
        var imageUrl = await _imageStorage.UploadGalleryImageAsync(
            stream, file.FileName, $"portfolio/{projectId}", file.ContentType);

        var sortOrder = project.Images.Count(i => i.ImageType == imageType);

        var image = new PortfolioProjectImage
        {
            ProjectId = projectId,
            ImageUrl = imageUrl,
            ImageType = imageType,
            Caption = caption,
            SortOrder = sortOrder
        };

        await _unitOfWork.Portfolio.AddImageAsync(image);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioImageDto>.SuccessResponse(
            new PortfolioImageDto { Id = image.Id, ImageUrl = imageUrl, Caption = caption, SortOrder = sortOrder },
            "Image uploaded.");
    }

    /// <summary>Admin fetches any project by ID regardless of status.</summary>
    public async Task<ApiResponse<PortfolioProjectDto>> AdminGetByIdAsync(Guid projectId)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse("Project not found.");

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(MapToDto(project));
    }

    /// <summary>Admin publishes any project (no ownership check, no After-image requirement).</summary>
    public async Task<ApiResponse<PortfolioProjectDto>> AdminPublishAsync(Guid projectId)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse("Project not found.");

        project.Status = PortfolioStatus.Published;
        project.PublishedAt = DateTime.UtcNow;
        project.RejectionReason = null;

        await _unitOfWork.Portfolio.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(MapToDto(project), "Project published.");
    }

    // ── ADMIN ─────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PortfolioPagedResultDto>> AdminGetAllAsync(
        PortfolioStatus? status = null, int page = 1, int pageSize = 20)
    {
        var (items, total) = await _unitOfWork.Portfolio.GetAllAdminAsync(status, page, pageSize);
        return ApiResponse<PortfolioPagedResultDto>.SuccessResponse(new PortfolioPagedResultDto
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponse<PortfolioProjectDto>> AdminUpdateStatusAsync(
        Guid projectId, UpdatePortfolioStatusDto dto)
    {
        var project = await _unitOfWork.Portfolio.GetByIdAsync(projectId);
        if (project == null)
            return ApiResponse<PortfolioProjectDto>.ErrorResponse("Project not found.");

        project.Status = dto.Status;
        project.RejectionReason = dto.Status == PortfolioStatus.Rejected ? dto.RejectionReason : null;
        project.PublishedAt = dto.Status == PortfolioStatus.Published ? DateTime.UtcNow : project.PublishedAt;

        await _unitOfWork.Portfolio.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<PortfolioProjectDto>.SuccessResponse(MapToDto(project),
            $"Project status updated to {dto.Status}.");
    }

    // ── MAPPING ───────────────────────────────────────────────────────────────

    private static PortfolioProjectDto MapToDto(VendorPortfolioProject p)
    {
        var scope = string.IsNullOrWhiteSpace(p.ScopeOfWork)
            ? new List<string>()
            : p.ScopeOfWork.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

        var budgetDisplay = (p.BudgetMin.HasValue || p.BudgetMax.HasValue)
            ? $"₦{p.BudgetMin?.ToString("N0") ?? "?"} — ₦{p.BudgetMax?.ToString("N0") ?? "?"}"
            : "On Request";

        var durationDisplay = (p.DurationMinDays.HasValue || p.DurationMaxDays.HasValue)
            ? $"{p.DurationMinDays ?? p.DurationMaxDays} — {p.DurationMaxDays ?? p.DurationMinDays} days"
            : string.Empty;

        return new PortfolioProjectDto
        {
            Id = p.Id,
            VendorId = p.VendorId,
            VendorName = p.VendorName,
            Title = p.Title,
            Location = p.Location,
            Category = p.Category,
            BudgetMin = p.BudgetMin,
            BudgetMax = p.BudgetMax,
            BudgetDisplay = budgetDisplay,
            DurationMinDays = p.DurationMinDays,
            DurationMaxDays = p.DurationMaxDays,
            DurationDisplay = durationDisplay,
            Description = p.Description,
            ScopeOfWork = scope,
            Status = p.Status.ToString(),
            RejectionReason = p.RejectionReason,
            PublishedAt = p.PublishedAt,
            CreatedAt = p.CreatedAt,
            BeforeImages = p.Images
                .Where(i => i.ImageType == PortfolioImageType.Before)
                .OrderBy(i => i.SortOrder)
                .Select(MapImageToDto).ToList(),
            AfterImages = p.Images
                .Where(i => i.ImageType == PortfolioImageType.After)
                .OrderBy(i => i.SortOrder)
                .Select(MapImageToDto).ToList(),
            ReferenceImages = p.Images
                .Where(i => i.ImageType == PortfolioImageType.Reference)
                .OrderBy(i => i.SortOrder)
                .Select(MapImageToDto).ToList()
        };
    }

    private static PortfolioImageDto MapImageToDto(PortfolioProjectImage i) => new()
    {
        Id = i.Id,
        ImageUrl = i.ImageUrl,
        Caption = i.Caption,
        SortOrder = i.SortOrder
    };

    /// <summary>
    /// Validates that the uploaded file is an image type the platform supports,
    /// including HEIC/HEIF from iPhones.
    /// </summary>
    private static bool IsAllowedImageType(string fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var ct = contentType.ToLowerInvariant();
            if (ct is "image/jpeg" or "image/jpg" or "image/png" or
                     "image/webp" or "image/gif" or "image/bmp" or
                     "image/heic" or "image/heif" or
                     "image/heic-sequence" or "image/heif-sequence")
                return true;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp"
                   or ".gif" or ".bmp" or ".heic" or ".heif";
    }
}
