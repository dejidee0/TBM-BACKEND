using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.DesignFlow;
using TBM.Core.Entities.DesignFlow;
using TBM.Core.Entities.Orders;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.AI;
using TBM.Core.Interfaces.Services;
using TBM.Core.Models.AI;
using TBM.Application.Services;

namespace TBM.Application.Services.DesignFlow;

public class DesignSessionService
{
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageStorageService _imageStorageService;
    private readonly IAIProvider _aiProvider;
    private readonly AIGeneratedMediaService _generatedMediaService;
    private readonly BOMGenerationService _bomGenerationService;
    private readonly ILogger<DesignSessionService> _logger;

    public DesignSessionService(
        IUnitOfWork unitOfWork,
        IImageStorageService imageStorageService,
        IAIProvider aiProvider,
        AIGeneratedMediaService generatedMediaService,
        BOMGenerationService bomGenerationService,
        ILogger<DesignSessionService> logger)
    {
        _unitOfWork = unitOfWork;
        _imageStorageService = imageStorageService;
        _aiProvider = aiProvider;
        _generatedMediaService = generatedMediaService;
        _bomGenerationService = bomGenerationService;
        _logger = logger;
    }

    public async Task<ApiResponse<CreateDesignSessionResponseDto>> CreateSessionAsync(
        Guid userId,
        CreateDesignSessionRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProjectName) ||
            string.IsNullOrWhiteSpace(dto.RoomType) ||
            string.IsNullOrWhiteSpace(dto.VisionText))
        {
            return ApiResponse<CreateDesignSessionResponseDto>.ErrorResponse("Project name, room type, and vision text are required");
        }

        if (dto.RoomDimensions.Length <= 0 ||
            dto.RoomDimensions.Width <= 0 ||
            dto.RoomDimensions.Height <= 0)
        {
            return ApiResponse<CreateDesignSessionResponseDto>.ErrorResponse("Room dimensions must be greater than zero");
        }

        var session = new DesignSession
        {
            UserId = userId,
            SessionNumber = await _unitOfWork.DesignSessions.GenerateSessionNumberAsync(),
            ProjectName = dto.ProjectName.Trim(),
            RoomType = dto.RoomType.Trim(),
            VisionText = dto.VisionText.Trim(),
            Tier = dto.Tier,
            RoomLength = dto.RoomDimensions.Length,
            RoomWidth = dto.RoomDimensions.Width,
            RoomHeight = dto.RoomDimensions.Height,
            Status = DesignSessionStatus.Draft,
            Progress = 0,
            CurrentStep = "Draft"
        };

        await _unitOfWork.DesignSessions.CreateAsync(session);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CreateDesignSessionResponseDto>.SuccessResponse(new CreateDesignSessionResponseDto
        {
            SessionId = session.Id,
            SessionNumber = session.SessionNumber,
            Status = session.Status.ToString(),
            UploadUrl = $"/api/v1/designs/sessions/{session.Id}/upload"
        });
    }

    public async Task<ApiResponse<UploadDesignSessionPhotoResponseDto>> UploadPhotoAsync(
        Guid userId,
        Guid sessionId,
        IFormFile image)
    {
        if (image == null || image.Length == 0)
        {
            return ApiResponse<UploadDesignSessionPhotoResponseDto>.ErrorResponse("Image file is required");
        }

        if (image.Length > MaxImageSizeBytes)
        {
            return ApiResponse<UploadDesignSessionPhotoResponseDto>.ErrorResponse("Image exceeds the 10MB limit");
        }

        if (!IsImageContentType(image.ContentType))
        {
            return ApiResponse<UploadDesignSessionPhotoResponseDto>.ErrorResponse("Only image files are allowed");
        }

        var session = await GetOwnedSessionAsync(userId, sessionId);
        if (session == null)
        {
            return ApiResponse<UploadDesignSessionPhotoResponseDto>.ErrorResponse("Design session not found");
        }

        if (session.Status is DesignSessionStatus.Processing or
            DesignSessionStatus.Generated or
            DesignSessionStatus.CartCreated or
            DesignSessionStatus.Ordered or
            DesignSessionStatus.ConvertedToProject)
        {
            return ApiResponse<UploadDesignSessionPhotoResponseDto>.ErrorResponse("Design session cannot accept a new photo at this stage");
        }

        await using var stream = image.OpenReadStream();
        var fileName = BuildFileName("design-session", image.FileName, ".png");
        var url = await _imageStorageService.UploadRoomImageAsync(stream, fileName, userId.ToString("N"));

        session.OriginalImageUrl = url;
        session.Status = DesignSessionStatus.PhotoUploaded;
        session.Progress = Math.Max(session.Progress, 20);
        session.CurrentStep = "Photo uploaded";
        session.ErrorMessage = null;

        await _unitOfWork.DesignSessions.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<UploadDesignSessionPhotoResponseDto>.SuccessResponse(new UploadDesignSessionPhotoResponseDto
        {
            OriginalImageUrl = url,
            Status = session.Status.ToString()
        });
    }

    public async Task<ApiResponse<GenerateDesignSessionResponseDto>> RequestGenerationAsync(
        Guid userId,
        Guid sessionId)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId);
        if (session == null)
        {
            return ApiResponse<GenerateDesignSessionResponseDto>.ErrorResponse("Design session not found");
        }

        if (session.Status is DesignSessionStatus.Generated or DesignSessionStatus.CartCreated or DesignSessionStatus.Ordered or DesignSessionStatus.ConvertedToProject)
        {
            return ApiResponse<GenerateDesignSessionResponseDto>.ErrorResponse("This design session has already progressed beyond generation");
        }

        if (string.IsNullOrWhiteSpace(session.OriginalImageUrl))
        {
            return ApiResponse<GenerateDesignSessionResponseDto>.ErrorResponse("Upload a photo before generating the design");
        }

        session.Status = DesignSessionStatus.Processing;
        session.Progress = 0;
        session.CurrentStep = "Queued for generation";
        session.ErrorMessage = null;

        await _unitOfWork.DesignSessions.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<GenerateDesignSessionResponseDto>.SuccessResponse(new GenerateDesignSessionResponseDto
        {
            Status = session.Status.ToString(),
            EstimatedTime = 45,
            StatusUrl = $"/api/v1/designs/sessions/{session.Id}/status"
        });
    }

    public async Task<ApiResponse<DesignSessionStatusDto>> GetSessionStatusAsync(Guid userId, Guid sessionId)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId);
        if (session == null)
        {
            return ApiResponse<DesignSessionStatusDto>.ErrorResponse("Design session not found");
        }

        var bom = await GetBomAsync(session);
        var status = new DesignSessionStatusDto
        {
            Status = session.Status.ToString(),
            Progress = session.Progress,
            CurrentStep = session.CurrentStep,
            ImageUrl = session.GeneratedImageUrl ?? session.OriginalImageUrl,
            BomGenerated = bom != null,
            ErrorMessage = session.ErrorMessage
        };

        return ApiResponse<DesignSessionStatusDto>.SuccessResponse(status);
    }

    public async Task<ApiResponse<DesignSessionDetailDto>> GetSessionAsync(Guid userId, Guid sessionId)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId);
        if (session == null)
        {
            return ApiResponse<DesignSessionDetailDto>.ErrorResponse("Design session not found");
        }

        var bom = await GetBomAsync(session);
        var detail = new DesignSessionDetailDto
        {
            Session = MapSessionDto(session),
            BillOfMaterials = bom == null ? null : MapBomDto(bom),
            AllItemsInStock = bom?.Items.All(x => x.InStock) ?? false
        };

        return ApiResponse<DesignSessionDetailDto>.SuccessResponse(detail);
    }

    public async Task<ApiResponse<DesignSessionListDto>> GetSessionsAsync(Guid userId)
    {
        var sessions = await _unitOfWork.DesignSessions.GetByUserAsync(userId);
        var result = new DesignSessionListDto
        {
            Sessions = sessions
                .OrderByDescending(x => x.CreatedAt)
                .Select(MapSummaryDto)
                .ToList()
        };

        return ApiResponse<DesignSessionListDto>.SuccessResponse(result);
    }

    public async Task<ApiResponse<AddDesignSessionToCartResponseDto>> AddToCartAsync(
        Guid userId,
        Guid sessionId,
        AddDesignSessionToCartRequestDto dto)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId);
        if (session == null)
        {
            return ApiResponse<AddDesignSessionToCartResponseDto>.ErrorResponse("Design session not found");
        }

        var bom = await GetBomAsync(session);
        if (bom == null || !bom.Items.Any())
        {
            return ApiResponse<AddDesignSessionToCartResponseDto>.ErrorResponse("No bill of materials exists for this session");
        }

        var itemsToAdd = dto.AddAll
            ? bom.Items.ToList()
            : bom.Items.Where(x => dto.ItemIds.Contains(x.Id)).ToList();

        if (!itemsToAdd.Any())
        {
            return ApiResponse<AddDesignSessionToCartResponseDto>.ErrorResponse("No BOM items matched the request");
        }

        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            await _unitOfWork.Carts.CreateAsync(cart);
            await _unitOfWork.SaveChangesAsync();
            cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        }

        if (cart == null)
        {
            return ApiResponse<AddDesignSessionToCartResponseDto>.ErrorResponse("Unable to initialize cart");
        }

        decimal totalAmount = 0m;
        var itemsAdded = 0;

        foreach (var bomItem in itemsToAdd)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(bomItem.ProductId);
            if (product == null || !product.IsActive)
            {
                continue;
            }

            var quantity = (int)Math.Ceiling(bomItem.Quantity);
            if (quantity <= 0)
            {
                quantity = 1;
            }

            if (product.TrackInventory && product.StockQuantity.HasValue && product.StockQuantity.Value < quantity)
            {
                quantity = product.StockQuantity.Value;
            }

            if (quantity <= 0)
            {
                continue;
            }

            var existing = await _unitOfWork.Carts.GetCartItemAsync(cart.Id, product.Id);
            if (existing != null)
            {
                var updatedQuantity = existing.Quantity + quantity;
                if (product.TrackInventory && product.StockQuantity.HasValue && product.StockQuantity.Value < updatedQuantity)
                {
                    updatedQuantity = product.StockQuantity.Value;
                }

                existing.Quantity = updatedQuantity;
                await _unitOfWork.Carts.UpdateItemAsync(existing);
                totalAmount += existing.UnitPrice * quantity;
            }
            else
            {
                await _unitOfWork.Carts.AddItemAsync(new CartItem
                {
                    CartId = cart.Id,
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.Price ?? 0m,
                    AddedAt = DateTime.UtcNow
                });

                totalAmount += (product.Price ?? 0m) * quantity;
            }

            itemsAdded++;
        }

        if (itemsAdded == 0)
        {
            return ApiResponse<AddDesignSessionToCartResponseDto>.ErrorResponse("No BOM items could be added to the cart");
        }

        session.Status = DesignSessionStatus.CartCreated;
        session.CurrentStep = "BOM added to cart";
        session.Progress = Math.Max(session.Progress, 80);
        await _unitOfWork.DesignSessions.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<AddDesignSessionToCartResponseDto>.SuccessResponse(new AddDesignSessionToCartResponseDto
        {
            CartId = cart.Id,
            ItemsAdded = itemsAdded,
            TotalAmount = totalAmount
        });
    }

    public async Task ProcessNextQueuedSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await _unitOfWork.DesignSessions.GetNextProcessingAsync();
        if (session == null)
        {
            return;
        }

        await ProcessGenerationAsync(session.Id, cancellationToken);
    }

    public async Task ProcessGenerationAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _unitOfWork.DesignSessions.GetByIdAsync(sessionId);
        if (session == null || session.Status != DesignSessionStatus.Processing)
        {
            return;
        }

        try
        {
            session.Progress = 10;
            session.CurrentStep = "Preparing design prompt";
            await _unitOfWork.DesignSessions.UpdateAsync(session);
            await _unitOfWork.SaveChangesAsync();

            if (string.IsNullOrWhiteSpace(session.OriginalImageUrl))
            {
                throw new InvalidOperationException("Original image is missing");
            }

            var providerResult = await _aiProvider.GenerateImageAsync(new AIImageRequest
            {
                Prompt = BuildDesignPrompt(session),
                ImageUrl = session.OriginalImageUrl,
                Width = 1024,
                Height = 1024
            });

            if (!providerResult.Success || string.IsNullOrWhiteSpace(providerResult.OutputUrl))
            {
                throw new InvalidOperationException(providerResult.ErrorMessage ?? "AI design generation failed");
            }

            session.Progress = 30;
            session.CurrentStep = "Persisting design preview";
            await _unitOfWork.DesignSessions.UpdateAsync(session);
            await _unitOfWork.SaveChangesAsync();

            var persisted = await _generatedMediaService.PersistAsync(
                session.UserId,
                providerResult.OutputUrl,
                AIOutputType.Image);

            session.GeneratedImageUrl = persisted.CloudinaryUrl;
            session.Progress = 60;
            session.CurrentStep = "Generating BOM";
            await _unitOfWork.DesignSessions.UpdateAsync(session);
            await _unitOfWork.SaveChangesAsync();

            var bom = await _bomGenerationService.GenerateBOMFromInventoryAsync(session, cancellationToken);
            if (bom == null)
            {
                throw new InvalidOperationException("Unable to generate a bill of materials from inventory");
            }

            session.BOMId = bom.Id;
            session.Progress = 100;
            session.Status = DesignSessionStatus.Generated;
            session.CurrentStep = "Generation complete";
            session.ErrorMessage = null;

            await _unitOfWork.DesignSessions.UpdateAsync(session);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            session.Status = DesignSessionStatus.Failed;
            session.Progress = Math.Max(session.Progress, 0);
            session.CurrentStep = "Generation failed";
            session.ErrorMessage = ex.Message;
            await _unitOfWork.DesignSessions.UpdateAsync(session);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogError(ex, "Failed to process design session {SessionId}", sessionId);
        }
    }

    private async Task<DesignSession?> GetOwnedSessionAsync(Guid userId, Guid sessionId)
    {
        var session = await _unitOfWork.DesignSessions.GetByIdAsync(sessionId);
        return session != null && session.UserId == userId ? session : null;
    }

    private async Task<BillOfMaterials?> GetBomAsync(DesignSession session)
    {
        if (session.BOMId.HasValue)
        {
            return await _unitOfWork.BillsOfMaterials.GetByIdAsync(session.BOMId.Value);
        }

        return await _unitOfWork.BillsOfMaterials.GetByDesignSessionIdAsync(session.Id);
    }

    private static string BuildDesignPrompt(DesignSession session)
    {
        return
            $"Create a polished interior design visualization for a {session.RoomType} project named '{session.ProjectName}'. " +
            $"Vision: {session.VisionText}. " +
            $"Room dimensions: {session.RoomLength}m x {session.RoomWidth}m x {session.RoomHeight}m. " +
            $"Style target: {session.Tier} finish. " +
            "Preserve architectural proportions, use realistic lighting, and produce a clean presentation-ready concept.";
    }

    private static DesignSessionSummaryDto MapSummaryDto(DesignSession session)
    {
        return new DesignSessionSummaryDto
        {
            SessionId = session.Id,
            SessionNumber = session.SessionNumber,
            ProjectName = session.ProjectName,
            RoomType = session.RoomType,
            Tier = session.Tier,
            Status = session.Status.ToString(),
            Progress = session.Progress,
            ImageUrl = session.GeneratedImageUrl ?? session.OriginalImageUrl,
            CreatedAt = session.CreatedAt
        };
    }

    private static DesignSessionDto MapSessionDto(DesignSession session)
    {
        return new DesignSessionDto
        {
            SessionId = session.Id,
            SessionNumber = session.SessionNumber,
            ProjectName = session.ProjectName,
            RoomType = session.RoomType,
            VisionText = session.VisionText,
            Tier = session.Tier,
            Status = session.Status.ToString(),
            Progress = session.Progress,
            CurrentStep = session.CurrentStep,
            ErrorMessage = session.ErrorMessage,
            OriginalImageUrl = session.OriginalImageUrl,
            GeneratedImageUrl = session.GeneratedImageUrl,
            RoomLength = session.RoomLength,
            RoomWidth = session.RoomWidth,
            RoomHeight = session.RoomHeight,
            BOMId = session.BOMId,
            OrderId = session.OrderId,
            ProjectId = session.ProjectId,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt ?? session.CreatedAt
        };
    }

    private static BillOfMaterialsDto MapBomDto(BillOfMaterials bom)
    {
        return new BillOfMaterialsDto
        {
            Id = bom.Id,
            DesignSessionId = bom.DesignSessionId,
            BomNumber = bom.BomNumber,
            TotalEstimatedCost = bom.TotalEstimatedCost,
            ItemCount = bom.ItemCount,
            Status = bom.Status.ToString(),
            Items = bom.Items.Select(MapBomItemDto).ToList(),
            CreatedAt = bom.CreatedAt,
            UpdatedAt = bom.UpdatedAt ?? bom.CreatedAt
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
}
