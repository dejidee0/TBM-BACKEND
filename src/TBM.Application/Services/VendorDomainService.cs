using System.Text.Json;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Vendor;
using TBM.Application.Helpers;
using TBM.Core.Entities;
using TBM.Core.Entities.Orders;
using TBM.Core.Entities.Products;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;
using System.Text.RegularExpressions;
using TBM.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace TBM.Application.Services;

public class VendorDomainService
{
    private const string VendorProfileCategory = "VendorProfiles";
    private const string VendorOwnershipCategory = "VendorProductOwnership";
    private const string VendorAssignmentCategory = "VendorOrderAssignments";
    private const string VendorOrderNoteCategory = "VendorOrderNotes";
    private const string VendorMessageCategory = "VendorMessages";
    private const string VendorNotificationCategory = "VendorNotifications";
    private const string VendorActivityCategory = "VendorActivities";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;

    public VendorDomainService(IUnitOfWork unitOfWork, AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<VendorDashboardDto> GetDashboardAsync(Guid vendorId)
    {
        var ownedProductIds = await GetOwnedProductIdsAsync(vendorId);
        var assignments = await GetAssignmentsAsync(vendorId);

        var lowStockCount = 0;
        foreach (var productId in ownedProductIds)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null || !product.TrackInventory)
            {
                continue;
            }

            if ((product.StockQuantity ?? 0) <= product.LowStockThreshold)
            {
                lowStockCount++;
            }
        }

        var unreadNotifications = (await GetNotificationsAsync(vendorId, unreadOnly: true)).Count;
        var unreadMessages = (await GetMessagesAsync(vendorId, unreadOnly: true)).Count;
        var assignedOrderIds = assignments.Select(x => x.OrderId).Distinct().ToHashSet();

        var pendingOrders = 0;
        var inProgressOrders = 0;
        foreach (var orderId in assignedOrderIds)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
            {
                continue;
            }

            if (order.Status == OrderStatus.Pending)
            {
                pendingOrders++;
            }
            else if (order.Status == OrderStatus.Processing || order.Status == OrderStatus.Shipped)
            {
                inProgressOrders++;
            }
        }

        return new VendorDashboardDto
        {
            OwnedProducts = ownedProductIds.Count,
            LowStockProducts = lowStockCount,
            AssignedOrders = assignedOrderIds.Count,
            PendingOrders = pendingOrders,
            InProgressOrders = inProgressOrders,
            UnreadNotifications = unreadNotifications,
            UnreadMessages = unreadMessages
        };
    }

    public async Task<List<VendorAlertDto>> GetAlertsAsync(Guid vendorId)
    {
        var alerts = new List<VendorAlertDto>();

        var ownedProductIds = await GetOwnedProductIdsAsync(vendorId);
        foreach (var productId in ownedProductIds)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null || !product.TrackInventory)
            {
                continue;
            }

            var stock = product.StockQuantity ?? 0;
            var threshold = product.LowStockThreshold;
            if (stock <= threshold)
            {
                alerts.Add(new VendorAlertDto
                {
                    Type = "inventory_low_stock",
                    Severity = stock == 0 ? "high" : "medium",
                    Message = $"Low stock for {product.Name} (stock: {stock})",
                    ProductId = product.Id,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        var assignments = await GetAssignmentsAsync(vendorId);
        foreach (var assignment in assignments)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(assignment.OrderId);
            if (order == null)
            {
                continue;
            }

            var orderAge = DateTime.UtcNow - order.CreatedAt;
            if ((order.Status == OrderStatus.Pending || order.Status == OrderStatus.Processing) &&
                orderAge.TotalHours > 24)
            {
                alerts.Add(new VendorAlertDto
                {
                    Type = "sla_overdue",
                    Severity = "high",
                    Message = $"Order {order.OrderNumber} has been pending for {Math.Floor(orderAge.TotalHours)} hours.",
                    OrderId = order.Id,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        return alerts
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToList();
    }

public async Task<VendorPagedResultDto<VendorActivityDto>> GetActivityAsync(Guid vendorId, int page, int pageSize, ActivityFilter? filter = null)
    {
        var activities = await GetActivitiesAsync(vendorId);

        // Apply filter
        if (filter == ActivityFilter.Orders)
        {
            activities = activities.Where(x => x.OrderId.HasValue).ToList();
        }
        else if (filter == ActivityFilter.Projects)
        {
            activities = activities.Where(x => x.ProductId.HasValue).ToList();
        }

        var total = activities.Count;

        var pagedActivities = activities
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Load customer names for orders
        var orderIds = pagedActivities.Where(x => x.OrderId.HasValue).Select(x => x.OrderId!.Value).Distinct().ToList();
        var orderDict = new Dictionary<Guid, string>();
        foreach (var oid in orderIds)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(oid);
            orderDict[oid] = order?.User != null ? $"{order.User.FirstName} {order.User.LastName}".Trim() : "";
        }

        // Map to DTOs
        var items = new List<VendorActivityDto>();
        foreach (var activity in pagedActivities)
        {
            var customer = activity.OrderId.HasValue && orderDict.TryGetValue(activity.OrderId.Value, out var c) ? c : "";
            var status = GetActivityStatus(activity.ActivityType, activity.Description);
            items.Add(new VendorActivityDto
            {
                Id = activity.Id,
                Customer = customer,
                ActivityType = activity.ActivityType,
                CreatedAtUtc = activity.CreatedAtUtc,
                Status = status
            });
        }

        return new VendorPagedResultDto<VendorActivityDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private string GetActivityStatus(string activityType, string description)
    {
        if (activityType.Contains("order_status_updated"))
        {
            // Extract status from description like "Order # status changed to Pending"
            var match = System.Text.RegularExpressions.Regex.Match(description, @"to\s+([A-Za-z\s]+)(?:\s|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        else if (activityType.Contains("inventory_created") || activityType.Contains("inventory_updated"))
        {
            return "Active";
        }
        else if (activityType == "inventory_deleted")
        {
            return "Deleted";
        }
        else if (activityType.Contains("low_stock"))
        {
            return "Low Stock";
        }
        return activityType;
    }

    public async Task<VendorPagedResultDto<VendorOrderListItemDto>> GetOrdersAsync(
        Guid vendorId,
        int page,
        int pageSize,
        OrderStatus? status,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        bool assignedOnly,
        string? type = null)
    {
        var ownedProductIds = await GetOwnedProductIdsAsync(vendorId);
        var assignments = await GetAssignmentsAsync(vendorId);
        var assignmentMap = assignments.ToDictionary(x => x.OrderId, x => x);
        var assignedOrderIds = assignments.Select(x => x.OrderId).ToHashSet();

        if (assignedOnly && !assignedOrderIds.Any())
        {
            return EmptyPaged<VendorOrderListItemDto>(page, pageSize);
        }

        // When vendor has no product ownership and no assignments, show all orders
        var noOwnership = !ownedProductIds.Any() && !assignedOrderIds.Any();

        var baseOrders = new List<Order>();
        const int fetchPageSize = 200;
        var fetchPage = 1;
        while (true)
        {
            var (chunk, totalCount) = await _unitOfWork.Orders.GetPagedAsync(
                fetchPage,
                fetchPageSize,
                null,
                status,
                null,
                fromDate,
                toDate,
                search);

            baseOrders.AddRange(chunk);
            if (baseOrders.Count >= totalCount || !chunk.Any())
            {
                break;
            }

            fetchPage++;
            if (fetchPage > 200)
            {
                break;
            }
        }

        var filteredOrders = baseOrders
            .Where(o => !o.IsDeleted)
            .Where(o => assignedOnly
                ? assignedOrderIds.Contains(o.Id)
                : noOwnership || o.Items.Any(i => ownedProductIds.Contains(i.ProductId)) || assignedOrderIds.Contains(o.Id))
            .ToList();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim().ToLowerInvariant();
            if (t == "e-commerce" || t == "ecommerce")
            {
                filteredOrders = filteredOrders.Where(o => o.Items.Any(i => i.Product != null && i.Product.ProductType == ProductType.PhysicalProduct)).ToList();
            }
            else if (t == "renovation" || t == "service")
            {
                filteredOrders = filteredOrders.Where(o => o.Items.Any(i => i.Product != null && i.Product.ProductType == ProductType.Service)).ToList();
            }
        }

        filteredOrders = filteredOrders
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        var total = filteredOrders.Count;
        var orders = filteredOrders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var items = orders.Select(order =>
        {
            assignmentMap.TryGetValue(order.Id, out var assignment);
            return new VendorOrderListItemDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = $"{order.User.FirstName} {order.User.LastName}".Trim(),
                Type = order.Items.Any(i => i.Product != null && i.Product.ProductType == ProductType.Service) ? "renovation" : "e-commerce",
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                Total = order.Total,
                CreatedAt = order.CreatedAt,
                MatchingItems = order.Items.Count(i => ownedProductIds.Contains(i.ProductId)),
                DeliveryAgentName = assignment?.DeliveryAgentName,
                DeliveryAgentPhone = assignment?.DeliveryAgentPhone
            };
        }).ToList();

        return new VendorPagedResultDto<VendorOrderListItemDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<VendorOrderDetailDto> GetOrderDetailAsync(Guid vendorId, Guid orderId)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException("Order not found");

        await EnsureVendorCanAccessOrderAsync(vendorId, order);

        var notes = await GetOrderNotesAsync(vendorId, orderId);
        var assignment = (await GetAssignmentsAsync(vendorId)).FirstOrDefault(x => x.OrderId == orderId);

        return new VendorOrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            Total = order.Total,
            CreatedAt = order.CreatedAt,
            CustomerName = $"{order.User.FirstName} {order.User.LastName}".Trim(),
            CustomerEmail = order.User.Email,
            ShippingAddress = order.ShippingAddress,
            ShippingCity = order.ShippingCity,
            ShippingState = order.ShippingState,
            ShippingPhone = order.ShippingPhone,
            DeliveryAgentName = assignment?.DeliveryAgentName,
            DeliveryAgentPhone = assignment?.DeliveryAgentPhone,
            Items = order.Items.Select(i => new VendorOrderItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                SubTotal = i.SubTotal
            }).ToList(),
            Notes = notes
        };
    }

    public async Task UpdateOrderStatusAsync(Guid vendorId, Guid orderId, OrderStatus newStatus, string? note)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException("Order not found");

        await EnsureVendorCanAccessOrderAsync(vendorId, order);

        if (!OrderStatusValidator.CanTransition(order.Status, newStatus))
        {
            throw new InvalidOperationException($"Invalid transition from {order.Status} to {newStatus}");
        }

        var oldStatus = order.Status;
        order.Status = newStatus;
        if (newStatus == OrderStatus.Cancelled)
        {
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = note;
        }
        if (newStatus == OrderStatus.Shipped)
        {
            order.ShippedAt = DateTime.UtcNow;
        }
        if (newStatus == OrderStatus.Delivered || newStatus == OrderStatus.Completed)
        {
            order.DeliveredAt = DateTime.UtcNow;
        }

        await _unitOfWork.OrderStatusHistories.AddAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            UpdatedBy = vendorId.ToString(),
            Note = note
        });

        if (!string.IsNullOrWhiteSpace(note))
        {
            await AddOrderNoteInternalAsync(vendorId, orderId, note);
        }

        await AddActivityInternalAsync(vendorId, "order_status_updated", $"Order {order.OrderNumber} status changed to {newStatus}", orderId, null);
        await AddNotificationInternalAsync(vendorId, "order", "Order status updated", $"Order {order.OrderNumber} moved to {newStatus}", orderId);

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(
            "Vendor.Order.Status.Update",
            "Vendor",
            new { orderId, oldStatus },
            new { orderId, newStatus, vendorId, note });
    }

    public async Task<VendorOrderNoteDto> AddOrderNoteAsync(Guid vendorId, Guid orderId, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new InvalidOperationException("Note is required.");
        }

        var order = await _unitOfWork.Orders.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException("Order not found");

        await EnsureVendorCanAccessOrderAsync(vendorId, order);

        var entity = await AddOrderNoteInternalAsync(vendorId, orderId, note.Trim());
        await AddActivityInternalAsync(vendorId, "order_note_added", $"Note added on order {order.OrderNumber}", orderId, null);
        await _unitOfWork.SaveChangesAsync();

        return new VendorOrderNoteDto
        {
            Id = entity.Id,
            OrderId = entity.OrderId,
            VendorId = entity.VendorId,
            Note = entity.Note,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task<VendorDeliveryAssignmentDto> AssignDeliveryAsync(Guid vendorId, Guid orderId, VendorOrderAssignmentRequest request)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException("Order not found");

        await EnsureVendorCanAccessOrderAsync(vendorId, order);

        var assignment = new VendorOrderAssignmentRecord
        {
            OrderId = orderId,
            VendorId = vendorId,
            AssignedBy = vendorId,
            AssignedAtUtc = DateTime.UtcNow,
            DeliveryAgentName = request.DeliveryAgentName,
            DeliveryAgentPhone = request.DeliveryAgentPhone,
            AssignmentNote = request.AssignmentNote,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var key = BuildOrderAssignmentKey(orderId);
        var existing = await _unitOfWork.Settings.GetByKeyAsync(VendorAssignmentCategory, key);
        if (existing == null)
        {
            await _unitOfWork.Settings.AddAsync(new Setting
            {
                Category = VendorAssignmentCategory,
                Key = key,
                Value = JsonSerializer.Serialize(assignment, JsonOptions),
                Description = "Vendor order delivery assignment"
            });
        }
        else
        {
            existing.Value = JsonSerializer.Serialize(assignment, JsonOptions);
            existing.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Settings.UpdateAsync(existing);
        }

        await AddActivityInternalAsync(vendorId, "delivery_assignment_updated", $"Delivery assignment updated for order {order.OrderNumber}", orderId, null);
        await AddNotificationInternalAsync(vendorId, "delivery", "Delivery assignment updated", $"Delivery details updated for {order.OrderNumber}", orderId);
        await _unitOfWork.SaveChangesAsync();

        return new VendorDeliveryAssignmentDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            TrackingNumber = order.TrackingNumber,
            DeliveryAgentName = assignment.DeliveryAgentName,
            DeliveryAgentPhone = assignment.DeliveryAgentPhone,
            AssignmentNote = assignment.AssignmentNote,
            UpdatedAtUtc = assignment.UpdatedAtUtc
        };
    }

    public async Task<VendorPagedResultDto<VendorInventoryItemDto>> GetInventoryAsync(
        Guid vendorId,
        int page,
        int pageSize,
        string? search,
        bool lowStockOnly)
    {
        var ownedProductIds = await GetOwnedProductIdsAsync(vendorId);
        var inventory = new List<VendorInventoryItemDto>();

        foreach (var productId in ownedProductIds)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
            {
                continue;
            }

            var stock = product.StockQuantity;
            var threshold = product.LowStockThreshold;
            var isLow = product.TrackInventory && (stock ?? 0) <= threshold;

            if (lowStockOnly && !isLow)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(search) &&
                !product.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !(product.SKU?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                continue;
            }

            inventory.Add(new VendorInventoryItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.SKU,
                StockQuantity = product.StockQuantity,
                LowStockThreshold = product.LowStockThreshold,
                IsLowStock = isLow,
                IsActive = product.IsActive,
                Price = product.Price
            });
        }

        var total = inventory.Count;
        var items = inventory
            .OrderBy(x => x.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new VendorPagedResultDto<VendorInventoryItemDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<VendorInventoryStatsDto> GetInventoryStatsAsync(Guid vendorId)
    {
        var ownedProductIds = await GetOwnedProductIdsAsync(vendorId);
        var stats = new VendorInventoryStatsDto();

        foreach (var productId in ownedProductIds)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
            {
                continue;
            }

            stats.TotalProducts++;
            if (product.IsActive)
            {
                stats.ActiveProducts++;
            }

            if (!product.TrackInventory)
            {
                continue;
            }

            var stock = Math.Max(0, product.StockQuantity ?? 0);
            var threshold = Math.Max(0, product.LowStockThreshold);

            stats.TotalStockUnits += stock;

            if (stock == 0)
            {
                stats.OutOfStockProducts++;
            }

            if (stock <= threshold)
            {
                stats.LowStockProducts++;
            }
        }

        return stats;
    }

    public async Task<VendorInventoryItemDto> CreateInventoryProductAsync(Guid vendorId, VendorInventoryCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Product name is required.");
        }

        var brandType = BrandType.Bogat;
        if (request.BrandType.HasValue)
        {
            if (!Enum.IsDefined(typeof(BrandType), request.BrandType.Value))
            {
                throw new InvalidOperationException("Invalid brand type.");
            }

            brandType = (BrandType)request.BrandType.Value;
        }

        var productType = ProductType.PhysicalProduct;
        if (request.ProductType.HasValue)
        {
            if (!Enum.IsDefined(typeof(ProductType), request.ProductType.Value))
            {
                throw new InvalidOperationException("Invalid product type.");
            }

            productType = (ProductType)request.ProductType.Value;
        }

        var categoryId = request.CategoryId;
        if (categoryId == Guid.Empty)
        {
            var fallbackCategory = (await _unitOfWork.Categories.GetAllAsync())
                .OrderBy(c => c.DisplayOrder)
                .FirstOrDefault(c => c.IsActive);

            if (fallbackCategory == null)
            {
                throw new InvalidOperationException("A valid category is required.");
            }

            categoryId = fallbackCategory.Id;
        }

        var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
        if (category == null)
        {
            throw new KeyNotFoundException("Category not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.SKU))
        {
            var skuExists = await _unitOfWork.Products.SKUExistsAsync(request.SKU.Trim());
            if (skuExists)
            {
                throw new InvalidOperationException("SKU already exists.");
            }
        }

        var slug = SlugHelper.GenerateSlug(request.Name.Trim());
        if (await _unitOfWork.Products.SlugExistsAsync(slug))
        {
            slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 80)];
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? request.Name.Trim()
            : request.Description.Trim();

        var shortDescription = string.IsNullOrWhiteSpace(request.ShortDescription)
            ? (description.Length <= 120 ? description : description[..120])
            : request.ShortDescription.Trim();

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = description,
            ShortDescription = shortDescription,
            Slug = slug,
            SKU = string.IsNullOrWhiteSpace(request.SKU) ? null : request.SKU.Trim(),
            BrandType = brandType,
            ProductType = productType,
            CategoryId = categoryId,
            Price = request.Price,
            ShowPrice = request.Price.HasValue,
            StockQuantity = request.StockQuantity,
            LowStockThreshold = request.LowStockThreshold ?? 5,
            TrackInventory = request.TrackInventory ?? true,
            IsActive = request.IsActive,
            IsFeatured = false,
            DisplayOrder = 0
        };

        await _unitOfWork.Products.CreateAsync(product);

        var profile = await GetVendorProfileAsync(vendorId)
            ?? new VendorProfileRecord
            {
                VendorId = vendorId,
                TenantId = vendorId.ToString("N"),
                DisplayName = $"vendor-{vendorId:N}",
                SlaHours = 24,
                IsActive = true,
                ActivatedAtUtc = DateTime.UtcNow
            };

        var ownership = new VendorProductOwnershipRecord
        {
            ProductId = product.Id,
            VendorUserId = vendorId,
            TenantId = profile.TenantId,
            AssignedByUserId = vendorId,
            AssignedAtUtc = DateTime.UtcNow
        };

        await UpsertSettingAsync(
            VendorOwnershipCategory,
            BuildProductOwnershipKey(product.Id),
            JsonSerializer.Serialize(ownership, JsonOptions),
            "Vendor product ownership");

        await AddActivityInternalAsync(vendorId, "inventory_created", $"Inventory product created: {product.Name}", null, product.Id);
        await AddNotificationInternalAsync(vendorId, "inventory", "Inventory product created", $"{product.Name} has been added to inventory.");
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(
            "Vendor.Inventory.Create",
            "Vendor",
            null,
            new { vendorId, productId = product.Id, product.Name, product.SKU });

        return new VendorInventoryItemDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            SKU = product.SKU,
            StockQuantity = product.StockQuantity,
            LowStockThreshold = product.LowStockThreshold,
            IsLowStock = product.TrackInventory && (product.StockQuantity ?? 0) <= product.LowStockThreshold,
            IsActive = product.IsActive,
            Price = product.Price
        };
    }

    public async Task<VendorInventoryItemDto> UpdateInventoryAsync(Guid vendorId, Guid productId, VendorInventoryUpdateRequest request)
    {
        var owner = await GetProductOwnershipAsync(productId);
        if (owner == null || owner.VendorUserId != vendorId)
        {
            throw new UnauthorizedAccessException("Product does not belong to this vendor.");
        }

        var product = await _unitOfWork.Products.GetByIdAsync(productId)
            ?? throw new KeyNotFoundException("Product not found");

        product.StockQuantity = request.StockQuantity;
        if (request.LowStockThreshold.HasValue)
        {
            product.LowStockThreshold = request.LowStockThreshold.Value;
        }
        if (request.IsActive.HasValue)
        {
            product.IsActive = request.IsActive.Value;
        }
        if (request.Price.HasValue)
        {
            product.Price = request.Price.Value;
        }

        await _unitOfWork.Products.UpdateAsync(product);
        await AddActivityInternalAsync(vendorId, "inventory_updated", $"Inventory updated for {product.Name}", null, productId);
        await _unitOfWork.SaveChangesAsync();

        return new VendorInventoryItemDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            SKU = product.SKU,
            StockQuantity = product.StockQuantity,
            LowStockThreshold = product.LowStockThreshold,
            IsLowStock = product.TrackInventory && (product.StockQuantity ?? 0) <= product.LowStockThreshold,
            IsActive = product.IsActive,
            Price = product.Price
        };
    }

    public async Task DeleteInventoryProductAsync(Guid vendorId, Guid productId)
    {
        var owner = await GetProductOwnershipAsync(productId);
        if (owner == null || owner.VendorUserId != vendorId)
        {
            throw new UnauthorizedAccessException("Product does not belong to this vendor.");
        }

        var product = await _unitOfWork.Products.GetByIdAsync(productId)
            ?? throw new KeyNotFoundException("Product not found");

        await _unitOfWork.Products.DeleteAsync(productId);

        var ownershipSetting = await _unitOfWork.Settings.GetByKeyAsync(VendorOwnershipCategory, BuildProductOwnershipKey(productId));
        if (ownershipSetting != null && !ownershipSetting.IsDeleted)
        {
            ownershipSetting.IsDeleted = true;
            ownershipSetting.DeletedAt = DateTime.UtcNow;
            ownershipSetting.DeletedBy = vendorId.ToString();
            await _unitOfWork.Settings.UpdateAsync(ownershipSetting);
        }

        await AddActivityInternalAsync(vendorId, "inventory_deleted", $"Inventory product removed: {product.Name}", null, productId);
        await AddNotificationInternalAsync(vendorId, "inventory", "Inventory product removed", $"{product.Name} has been removed from inventory.");
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(
            "Vendor.Inventory.Delete",
            "Vendor",
            new { vendorId, productId, product.Name },
            null);
    }

    public async Task<VendorPagedResultDto<VendorDeliveryAssignmentDto>> GetDeliveriesAsync(Guid vendorId, int page, int pageSize, OrderStatus? status)
    {
        var assignments = await GetAssignmentsAsync(vendorId);
        var assignmentMap = assignments.ToDictionary(x => x.OrderId);
        var list = new List<VendorDeliveryAssignmentDto>();

        if (assignments.Any())
        {
            // Vendor has explicit assignments — show only those
            foreach (var assignment in assignments.OrderByDescending(x => x.UpdatedAtUtc))
            {
                var order = await _unitOfWork.Orders.GetByIdAsync(assignment.OrderId);
                if (order == null)
                {
                    continue;
                }

                if (status.HasValue && order.Status != status.Value)
                {
                    continue;
                }

                list.Add(new VendorDeliveryAssignmentDto
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Status = order.Status,
                    TrackingNumber = order.TrackingNumber,
                    DeliveryAgentName = assignment.DeliveryAgentName,
                    DeliveryAgentPhone = assignment.DeliveryAgentPhone,
                    AssignmentNote = assignment.AssignmentNote,
                    UpdatedAtUtc = assignment.UpdatedAtUtc
                });
            }
        }
        else
        {
            // No explicit assignments — fall back to all orders requiring delivery
            var deliveryStatuses = new[]
            {
                OrderStatus.PaymentReceived,
                OrderStatus.Processing,
                OrderStatus.Shipped
            };

            var fetchPage = 1;
            const int fetchSize = 200;
            var allOrders = new List<Order>();
            while (true)
            {
                var (chunk, totalCount) = await _unitOfWork.Orders.GetPagedAsync(
                    fetchPage, fetchSize, null, status, null, null, null, null);
                allOrders.AddRange(chunk);
                if (allOrders.Count >= totalCount || !chunk.Any()) break;
                fetchPage++;
                if (fetchPage > 200) break;
            }

            list = allOrders
                .Where(o => !o.IsDeleted && (status.HasValue ? o.Status == status.Value : deliveryStatuses.Contains(o.Status)))
                .OrderByDescending(o => o.CreatedAt)
                .Select(o =>
                {
                    assignmentMap.TryGetValue(o.Id, out var a);
                    return new VendorDeliveryAssignmentDto
                    {
                        OrderId = o.Id,
                        OrderNumber = o.OrderNumber,
                        Status = o.Status,
                        TrackingNumber = o.TrackingNumber,
                        DeliveryAgentName = a?.DeliveryAgentName,
                        DeliveryAgentPhone = a?.DeliveryAgentPhone,
                        AssignmentNote = a?.AssignmentNote,
                        UpdatedAtUtc = a?.UpdatedAtUtc ?? o.UpdatedAt ?? o.CreatedAt
                    };
                })
                .ToList();
        }

        var total = list.Count;
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new VendorPagedResultDto<VendorDeliveryAssignmentDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<VendorPagedResultDto<VendorMessageDto>> GetMessagesAsync(Guid vendorId, int page, int pageSize, bool unreadOnly)
    {
        var rows = await GetMessagesAsync(vendorId, unreadOnly);
        var total = rows.Count;
        var items = rows
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapMessage)
            .ToList();

        return new VendorPagedResultDto<VendorMessageDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<VendorMessageDto> SendMessageAsync(Guid vendorId, VendorMessageCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
        {
            throw new InvalidOperationException("Subject and body are required.");
        }

        var row = new VendorMessageRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            VendorId = vendorId,
            Direction = "outbound",
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            From = $"vendor:{vendorId:N}",
            To = string.IsNullOrWhiteSpace(request.To) ? "admin" : request.To.Trim(),
            IsRead = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.Settings.AddAsync(new Setting
        {
            Category = VendorMessageCategory,
            Key = BuildMessageKey(vendorId, row.Id),
            Value = JsonSerializer.Serialize(row, JsonOptions),
            Description = "Vendor message"
        });

        await AddActivityInternalAsync(vendorId, "message_sent", $"Message sent: {row.Subject}", null, null);
        await _unitOfWork.SaveChangesAsync();
        return MapMessage(row);
    }

    public async Task<VendorPagedResultDto<VendorNotificationDto>> GetNotificationsPagedAsync(Guid vendorId, int page, int pageSize, bool unreadOnly)
    {
        var rows = await GetNotificationsAsync(vendorId, unreadOnly);
        var total = rows.Count;
        var items = rows
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapNotification)
            .ToList();

        return new VendorPagedResultDto<VendorNotificationDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task MarkNotificationReadAsync(Guid vendorId, string notificationId)
    {
        var key = BuildNotificationKey(vendorId, notificationId);
        var setting = await _unitOfWork.Settings.GetByKeyAsync(VendorNotificationCategory, key)
            ?? throw new KeyNotFoundException("Notification not found");

        var row = JsonSerializer.Deserialize<VendorNotificationRecord>(setting.Value, JsonOptions)
            ?? throw new InvalidOperationException("Notification payload is invalid.");

        if (row.VendorId != vendorId)
        {
            throw new UnauthorizedAccessException("Notification does not belong to this vendor.");
        }

        if (!row.IsRead)
        {
            row.IsRead = true;
            setting.Value = JsonSerializer.Serialize(row, JsonOptions);
            setting.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Settings.UpdateAsync(setting);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<int> MarkAllNotificationsReadAsync(Guid vendorId)
    {
        var rows = await _unitOfWork.Settings.GetByCategoryAsync(VendorNotificationCategory);
        var updated = 0;

        foreach (var setting in rows.Where(x => !x.IsDeleted))
        {
            var row = DeserializeOrDefault<VendorNotificationRecord>(setting.Value);
            if (row == null || row.VendorId != vendorId || row.IsRead)
            {
                continue;
            }

            row.IsRead = true;
            setting.Value = JsonSerializer.Serialize(row, JsonOptions);
            setting.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Settings.UpdateAsync(setting);
            updated++;
        }

        if (updated > 0)
        {
            await AddActivityInternalAsync(vendorId, "notifications_mark_all_read", $"Marked {updated} notifications as read.", null, null);
            await _unitOfWork.SaveChangesAsync();
            await _auditService.LogAsync(
                "Vendor.Notifications.MarkAllRead",
                "Vendor",
                null,
                new { vendorId, updated });
        }

        return updated;
    }

    public async Task ActivateVendorAsync(Guid adminUserId, Guid userId, ActivateVendorRequest request)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        var vendorRole = await _unitOfWork.Roles.GetByNameAsync(UserRoles.Vendor);
        if (vendorRole == null)
        {
            vendorRole = new Role
            {
                Name = UserRoles.Vendor,
                Description = "Vendor with isolated operations scope"
            };
            await _unitOfWork.Roles.CreateAsync(vendorRole);
            await _unitOfWork.SaveChangesAsync();
        }

        var hasVendorRole = user.UserRoles.Any(r => r.Role.Name.Equals(UserRoles.Vendor, StringComparison.OrdinalIgnoreCase));
        if (!hasVendorRole)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = vendorRole.Id
            });
        }

        var profile = new VendorProfileRecord
        {
            VendorId = userId,
            TenantId = string.IsNullOrWhiteSpace(request.TenantId) ? userId.ToString("N") : request.TenantId.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.FullName : request.DisplayName.Trim(),
            SlaHours = request.SlaHours.GetValueOrDefault(24),
            IsActive = true,
            ActivatedAtUtc = DateTime.UtcNow
        };

        await UpsertSettingAsync(
            VendorProfileCategory,
            BuildVendorProfileKey(userId),
            JsonSerializer.Serialize(profile, JsonOptions),
            "Vendor profile");

        await AddNotificationInternalAsync(userId, "vendor", "Vendor role activated", "Your vendor account is active.");
        await AddActivityInternalAsync(userId, "vendor_activated", "Vendor role enabled by admin.", null, null);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(
            "Vendor.Activate",
            "Vendor",
            null,
            new { adminUserId, userId, profile.TenantId, profile.SlaHours });
    }

    public async Task<VendorProductOwnershipDto> AssignProductOwnershipAsync(Guid adminUserId, Guid productId, Guid vendorUserId)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId)
            ?? throw new KeyNotFoundException("Product not found");

        var vendorUser = await _unitOfWork.Users.GetByIdAsync(vendorUserId)
            ?? throw new KeyNotFoundException("Vendor user not found");

        var hasVendorRole = vendorUser.UserRoles.Any(x => x.Role.Name.Equals(UserRoles.Vendor, StringComparison.OrdinalIgnoreCase));
        if (!hasVendorRole)
        {
            throw new InvalidOperationException("User does not have Vendor role.");
        }

        var profile = await GetVendorProfileAsync(vendorUserId)
            ?? new VendorProfileRecord
            {
                VendorId = vendorUserId,
                TenantId = vendorUserId.ToString("N"),
                DisplayName = vendorUser.FullName,
                SlaHours = 24,
                IsActive = true,
                ActivatedAtUtc = DateTime.UtcNow
            };

        var ownership = new VendorProductOwnershipRecord
        {
            ProductId = productId,
            VendorUserId = vendorUserId,
            TenantId = profile.TenantId,
            AssignedByUserId = adminUserId,
            AssignedAtUtc = DateTime.UtcNow
        };

        await UpsertSettingAsync(
            VendorOwnershipCategory,
            BuildProductOwnershipKey(productId),
            JsonSerializer.Serialize(ownership, JsonOptions),
            "Vendor product ownership");

        await AddNotificationInternalAsync(vendorUserId, "ownership", "Product assigned", $"You now manage product {product.Name}.", null);
        await AddActivityInternalAsync(vendorUserId, "product_assigned", $"Assigned ownership of {product.Name}.", null, productId);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(
            "Vendor.Product.Assign",
            "Vendor",
            null,
            new { adminUserId, productId, vendorUserId });

        return new VendorProductOwnershipDto
        {
            ProductId = ownership.ProductId,
            VendorUserId = ownership.VendorUserId,
            TenantId = ownership.TenantId,
            AssignedByUserId = ownership.AssignedByUserId,
            AssignedAtUtc = ownership.AssignedAtUtc
        };
    }

    public async Task RemoveProductOwnershipAsync(Guid adminUserId, Guid productId)
    {
        var key = BuildProductOwnershipKey(productId);
        var current = await _unitOfWork.Settings.GetByKeyAsync(VendorOwnershipCategory, key);
        if (current == null)
        {
            return;
        }

        var existing = JsonSerializer.Deserialize<VendorProductOwnershipRecord>(current.Value, JsonOptions);
        current.IsDeleted = true;
        current.DeletedAt = DateTime.UtcNow;
        current.DeletedBy = adminUserId.ToString();
        await _unitOfWork.Settings.UpdateAsync(current);
        await _unitOfWork.SaveChangesAsync();

        if (existing != null)
        {
            await AddNotificationInternalAsync(existing.VendorUserId, "ownership", "Product unassigned", $"Product {productId} was removed from your ownership.", null);
            await AddActivityInternalAsync(existing.VendorUserId, "product_unassigned", $"Ownership removed for product {productId}.", null, productId);
            await _unitOfWork.SaveChangesAsync();
        }

        await _auditService.LogAsync(
            "Vendor.Product.Unassign",
            "Vendor",
            new { productId, previousOwner = existing?.VendorUserId },
            new { productId, removedBy = adminUserId });
    }

    public async Task<List<VendorProductOwnershipDto>> GetVendorOwnershipAsync(Guid vendorUserId)
    {
        var all = await _unitOfWork.Settings.GetByCategoryAsync(VendorOwnershipCategory);
        return all
            .Where(x => !x.IsDeleted)
            .Select(x => DeserializeOrDefault<VendorProductOwnershipRecord>(x.Value))
            .Where(x => x != null && x.VendorUserId == vendorUserId)
            .Select(x => new VendorProductOwnershipDto
            {
                ProductId = x!.ProductId,
                VendorUserId = x.VendorUserId,
                TenantId = x.TenantId,
                AssignedByUserId = x.AssignedByUserId,
                AssignedAtUtc = x.AssignedAtUtc
            })
            .OrderByDescending(x => x.AssignedAtUtc)
            .ToList();
    }

    private async Task EnsureVendorCanAccessOrderAsync(Guid vendorId, Order order)
    {
        var ownedProductIds = await GetOwnedProductIdsAsync(vendorId);
        var assignment = (await GetAssignmentsAsync(vendorId)).Any(x => x.OrderId == order.Id);
        var ownsItem = order.Items.Any(i => ownedProductIds.Contains(i.ProductId));

        if (!assignment && !ownsItem)
        {
            throw new UnauthorizedAccessException("Order is outside this vendor scope.");
        }
    }

    private async Task<List<Guid>> GetOwnedProductIdsAsync(Guid vendorId)
    {
        var rows = await _unitOfWork.Settings.GetByCategoryAsync(VendorOwnershipCategory);
        return rows
            .Where(x => !x.IsDeleted)
            .Select(x => DeserializeOrDefault<VendorProductOwnershipRecord>(x.Value))
            .Where(x => x != null && x.VendorUserId == vendorId)
            .Select(x => x!.ProductId)
            .Distinct()
            .ToList();
    }

    private async Task<VendorProductOwnershipRecord?> GetProductOwnershipAsync(Guid productId)
    {
        var setting = await _unitOfWork.Settings.GetByKeyAsync(VendorOwnershipCategory, BuildProductOwnershipKey(productId));
        if (setting == null || setting.IsDeleted)
        {
            return null;
        }

        return DeserializeOrDefault<VendorProductOwnershipRecord>(setting.Value);
    }

    private async Task<VendorProfileRecord?> GetVendorProfileAsync(Guid vendorId)
    {
        var setting = await _unitOfWork.Settings.GetByKeyAsync(VendorProfileCategory, BuildVendorProfileKey(vendorId));
        if (setting == null || setting.IsDeleted)
        {
            return null;
        }

        return DeserializeOrDefault<VendorProfileRecord>(setting.Value);
    }

    private async Task<List<VendorOrderAssignmentRecord>> GetAssignmentsAsync(Guid vendorId)
    {
        var rows = await _unitOfWork.Settings.GetByCategoryAsync(VendorAssignmentCategory);
        return rows
            .Where(x => !x.IsDeleted)
            .Select(x => DeserializeOrDefault<VendorOrderAssignmentRecord>(x.Value))
            .Where(x => x != null && x.VendorId == vendorId)
            .Select(x => x!)
            .ToList();
    }

    private async Task<List<VendorOrderNoteDto>> GetOrderNotesAsync(Guid vendorId, Guid orderId)
    {
        var rows = await _unitOfWork.Settings.GetByCategoryAsync(VendorOrderNoteCategory);
        return rows
            .Where(x => !x.IsDeleted)
            .Select(x => DeserializeOrDefault<VendorOrderNoteRecord>(x.Value))
            .Where(x => x != null && x.VendorId == vendorId && x.OrderId == orderId)
            .Select(x => new VendorOrderNoteDto
            {
                Id = x!.Id,
                OrderId = x.OrderId,
                VendorId = x.VendorId,
                Note = x.Note,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToList();
    }

    private async Task<VendorOrderNoteRecord> AddOrderNoteInternalAsync(Guid vendorId, Guid orderId, string note)
    {
        var row = new VendorOrderNoteRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            OrderId = orderId,
            VendorId = vendorId,
            Note = note,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.Settings.AddAsync(new Setting
        {
            Category = VendorOrderNoteCategory,
            Key = BuildOrderNoteKey(orderId, row.Id),
            Value = JsonSerializer.Serialize(row, JsonOptions),
            Description = "Vendor order note"
        });

        return row;
    }

    private async Task<List<VendorMessageRecord>> GetMessagesAsync(Guid vendorId, bool unreadOnly)
    {
        var rows = await _unitOfWork.Settings.GetByCategoryAsync(VendorMessageCategory);
        var messages = rows
            .Where(x => !x.IsDeleted)
            .Select(x => DeserializeOrDefault<VendorMessageRecord>(x.Value))
            .Where(x => x != null && x.VendorId == vendorId)
            .Select(x => x!)
            .ToList();

        if (unreadOnly)
        {
            messages = messages.Where(x => !x.IsRead).ToList();
        }

        return messages;
    }

    private async Task<List<VendorNotificationRecord>> GetNotificationsAsync(Guid vendorId, bool unreadOnly)
    {
        var rows = await _unitOfWork.Settings.GetByCategoryAsync(VendorNotificationCategory);
        var notifications = rows
            .Where(x => !x.IsDeleted)
            .Select(x => DeserializeOrDefault<VendorNotificationRecord>(x.Value))
            .Where(x => x != null && x.VendorId == vendorId)
            .Select(x => x!)
            .ToList();

        if (unreadOnly)
        {
            notifications = notifications.Where(x => !x.IsRead).ToList();
        }

        return notifications;
    }

    private async Task<List<VendorActivityRecord>> GetActivitiesAsync(Guid vendorId)
    {
        var rows = await _unitOfWork.Settings.GetByCategoryAsync(VendorActivityCategory);
        return rows
            .Where(x => !x.IsDeleted)
            .Select(x => DeserializeOrDefault<VendorActivityRecord>(x.Value))
            .Where(x => x != null && x.VendorId == vendorId)
            .Select(x => x!)
            .ToList();
    }

    private async Task AddNotificationInternalAsync(Guid vendorId, string type, string title, string body, Guid? orderId = null)
    {
        var row = new VendorNotificationRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            VendorId = vendorId,
            Type = type,
            Title = title,
            Body = body,
            IsRead = false,
            OrderId = orderId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.Settings.AddAsync(new Setting
        {
            Category = VendorNotificationCategory,
            Key = BuildNotificationKey(vendorId, row.Id),
            Value = JsonSerializer.Serialize(row, JsonOptions),
            Description = "Vendor notification"
        });
    }

    private async Task AddActivityInternalAsync(Guid vendorId, string activityType, string description, Guid? orderId, Guid? productId)
    {
        var row = new VendorActivityRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            VendorId = vendorId,
            ActivityType = activityType,
            Description = description,
            OrderId = orderId,
            ProductId = productId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.Settings.AddAsync(new Setting
        {
            Category = VendorActivityCategory,
            Key = BuildActivityKey(vendorId, row.Id),
            Value = JsonSerializer.Serialize(row, JsonOptions),
            Description = "Vendor activity"
        });
    }

    private async Task UpsertSettingAsync(string category, string key, string value, string description)
    {
        var current = await _unitOfWork.Settings.GetByKeyAsync(category, key);
        if (current == null)
        {
            await _unitOfWork.Settings.AddAsync(new Setting
            {
                Category = category,
                Key = key,
                Value = value,
                Description = description
            });
            return;
        }

        current.Value = value;
        current.UpdatedAt = DateTime.UtcNow;
        current.Description = description;
        await _unitOfWork.Settings.UpdateAsync(current);
    }

    private static T? DeserializeOrDefault<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static VendorPagedResultDto<T> EmptyPaged<T>(int page, int pageSize)
    {
        return new VendorPagedResultDto<T>
        {
            Items = new List<T>(),
            Total = 0,
            Page = page,
            PageSize = pageSize
        };
    }

    private static VendorMessageDto MapMessage(VendorMessageRecord x) => new()
    {
        Id = x.Id,
        VendorId = x.VendorId,
        Direction = x.Direction,
        Subject = x.Subject,
        Body = x.Body,
        From = x.From,
        To = x.To,
        IsRead = x.IsRead,
        CreatedAtUtc = x.CreatedAtUtc
    };

    private static VendorNotificationDto MapNotification(VendorNotificationRecord x) => new()
    {
        Id = x.Id,
        VendorId = x.VendorId,
        Type = x.Type,
        Title = x.Title,
        Body = x.Body,
        IsRead = x.IsRead,
        OrderId = x.OrderId,
        CreatedAtUtc = x.CreatedAtUtc
    };

    public async Task<(byte[] Content, string FileName, string ContentType, long SizeBytes)> ExportOrdersAsync(
        Guid vendorId,
        OrderStatus? status,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        bool assignedOnly,
        string? type)
    {
        // reuse the GetOrders filtering approach but fetch all matching orders
        var ownedProductIds = await GetOwnedProductIdsAsync(vendorId);
        var assignments = await GetAssignmentsAsync(vendorId);
        var assignedOrderIds = assignments.Select(x => x.OrderId).ToHashSet();

        var baseOrders = new List<Order>();
        const int fetchPageSize = 200;
        var fetchPage = 1;
        while (true)
        {
            var (chunk, totalCount) = await _unitOfWork.Orders.GetPagedAsync(
                fetchPage,
                fetchPageSize,
                null,
                status,
                null,
                fromDate,
                toDate,
                search);

            baseOrders.AddRange(chunk);
            if (baseOrders.Count >= totalCount || !chunk.Any())
            {
                break;
            }

            fetchPage++;
            if (fetchPage > 200) break;
        }

        var filtered = baseOrders
            .Where(o => !o.IsDeleted)
            .Where(o => assignedOnly
                ? assignedOrderIds.Contains(o.Id)
                : o.Items.Any(i => ownedProductIds.Contains(i.ProductId)) || assignedOrderIds.Contains(o.Id))
            .ToList();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim().ToLowerInvariant();
            if (t == "e-commerce" || t == "ecommerce")
            {
                filtered = filtered.Where(o => o.Items.Any(i => i.Product != null && i.Product.ProductType == ProductType.PhysicalProduct)).ToList();
            }
            else if (t == "renovation" || t == "service")
            {
                filtered = filtered.Where(o => o.Items.Any(i => i.Product != null && i.Product.ProductType == ProductType.Service)).ToList();
            }
        }

        // Build CSV
        var sb = new StringBuilder();
        sb.AppendLine("OrderNumber,CustomerName,CustomerEmail,Type,Status,PaymentStatus,Total,CreatedAt,UpdatedAt,TrackingNumber,Carrier");

        foreach (var o in filtered.OrderByDescending(x => x.CreatedAt))
        {
            var customerName = $"{o.User.FirstName} {o.User.LastName}".Trim();
            var email = o.User?.Email ?? string.Empty;
            var orderType = o.Items.Any(i => i.Product != null && i.Product.ProductType == ProductType.Service) ? "renovation" : "e-commerce";
            string Escape(string? v) => v == null ? string.Empty : $"\"{v.Replace("\"", "\"\"")}\"";

            var cols = new[]
            {
                Escape(o.OrderNumber),
                Escape(customerName),
                Escape(email),
                Escape(orderType),
                Escape(o.Status.ToString()),
                Escape(o.PaymentStatus.ToString()),
                Escape(o.Total.ToString("F2")),
                Escape(o.CreatedAt.ToString("o")),
                Escape(o.UpdatedAt.HasValue ? o.UpdatedAt.Value.ToString("o") : string.Empty),
                Escape(o.TrackingNumber),
                Escape(o.AdminNotes)
            };

            sb.AppendLine(string.Join(',', cols));
        }

        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"vendor-orders-{vendorId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        await _auditService.LogAsync("Vendor.Orders.Export", "Vendor", null, new { vendorId, exported = filtered.Count });
        return (content, fileName, "text/csv", content.LongLength);
    }

    public async Task<VendorOrderImportResultDto> ImportOrdersAsync(Guid vendorId, IFormFile file)
    {
        var result = new VendorOrderImportResultDto();
        if (file == null)
        {
            result.Success = false;
            result.Errors.Add("No file provided.");
            return result;
        }

        using var sr = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var text = await sr.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            result.Success = false;
            result.Errors.Add("File is empty.");
            return result;
        }

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (lines.Count <= 1)
        {
            result.Success = false;
            result.Errors.Add("No data rows found in CSV.");
            return result;
        }

        if (lines.Count - 1 > 5000)
        {
            result.Success = false;
            result.Errors.Add("Row limit exceeded (max 5000).");
            return result;
        }

        var header = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToList();
        var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++) idx[header[i]] = i;

        string GetField(string[] parts, string name)
        {
            if (!idx.ContainsKey(name)) return string.Empty;
            var p = idx[name];
            if (p < 0 || p >= parts.Length) return string.Empty;
            return parts[p].Trim().Trim('"');
        }

        for (var r = 1; r < lines.Count; r++)
        {
            var raw = lines[r];
            var parts = raw.Split(',');
            try
            {
                var orderNumber = GetField(parts, "OrderNumber");
                if (string.IsNullOrWhiteSpace(orderNumber))
                {
                    result.Failed++;
                    result.Errors.Add($"Row {r + 1}: OrderNumber is required.");
                    continue;
                }

                var order = await _unitOfWork.Orders.GetByOrderNumberAsync(orderNumber);
                if (order == null)
                {
                    result.Failed++;
                    result.Errors.Add($"Row {r + 1}: Order {orderNumber} not found.");
                    continue;
                }

                try
                {
                    await EnsureVendorCanAccessOrderAsync(vendorId, order);
                }
                catch
                {
                    result.Failed++;
                    result.Errors.Add($"Row {r + 1}: Order {orderNumber} is outside vendor scope.");
                    continue;
                }

                var newStatusRaw = GetField(parts, "NewStatus");
                var tracking = GetField(parts, "TrackingNumber");
                var carrier = GetField(parts, "Carrier");
                var note = GetField(parts, "Note");

                if (!string.IsNullOrWhiteSpace(newStatusRaw))
                {
                    if (!Enum.TryParse<OrderStatus>(newStatusRaw, true, out var newStatus))
                    {
                        result.Failed++;
                        result.Errors.Add($"Row {r + 1}: Invalid status '{newStatusRaw}'.");
                        continue;
                    }

                    if (!OrderStatusValidator.CanTransition(order.Status, newStatus))
                    {
                        result.Failed++;
                        result.Errors.Add($"Row {r + 1}: Invalid transition from {order.Status} to {newStatus}.");
                        continue;
                    }

                    // apply status change similar to UpdateOrderStatusAsync but minimal to avoid duplicating side-effects
                    var oldStatus = order.Status;
                    order.Status = newStatus;
                    if (newStatus == OrderStatus.Cancelled)
                    {
                        order.CancelledAt = DateTime.UtcNow;
                        order.CancellationReason = note;
                    }
                    if (newStatus == OrderStatus.Shipped)
                    {
                        order.ShippedAt = DateTime.UtcNow;
                    }
                    if (newStatus == OrderStatus.Delivered || newStatus == OrderStatus.Completed)
                    {
                        order.DeliveredAt = DateTime.UtcNow;
                    }

                    await _unitOfWork.OrderStatusHistories.AddAsync(new OrderStatusHistory
                    {
                        OrderId = order.Id,
                        OldStatus = oldStatus.ToString(),
                        NewStatus = newStatus.ToString(),
                        UpdatedBy = vendorId.ToString(),
                        Note = note
                    });
                }

                if (!string.IsNullOrWhiteSpace(tracking))
                {
                    order.TrackingNumber = tracking;
                }

                if (!string.IsNullOrWhiteSpace(carrier))
                {
                    order.AdminNotes = string.IsNullOrWhiteSpace(order.AdminNotes)
                        ? $"Carrier:{carrier}"
                        : order.AdminNotes + $";Carrier:{carrier}";
                }

                if (!string.IsNullOrWhiteSpace(note))
                {
                    await AddOrderNoteInternalAsync(vendorId, order.Id, note);
                }

                await _unitOfWork.Orders.UpdateAsync(order);
                await _unitOfWork.SaveChangesAsync();
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Row {r + 1}: {ex.Message}");
            }
        }

        result.Success = result.Failed == 0;
        await _auditService.LogAsync("Vendor.Orders.Import", "Vendor", null, new { vendorId, imported = result.Imported, failed = result.Failed });
        return result;
    }

    private static string BuildVendorProfileKey(Guid vendorId) => $"vendor:{vendorId:N}";
    private static string BuildProductOwnershipKey(Guid productId) => $"product:{productId:N}";
    private static string BuildOrderAssignmentKey(Guid orderId) => $"order:{orderId:N}";
    private static string BuildOrderNoteKey(Guid orderId, string noteId) => $"note:{orderId:N}:{noteId}";
    private static string BuildMessageKey(Guid vendorId, string messageId) => $"message:{vendorId:N}:{messageId}";
    private static string BuildNotificationKey(Guid vendorId, string notificationId) => $"notification:{vendorId:N}:{notificationId}";
    private static string BuildActivityKey(Guid vendorId, string activityId) => $"activity:{vendorId:N}:{activityId}";

    private sealed class VendorProfileRecord
    {
        public Guid VendorId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int SlaHours { get; set; }
        public bool IsActive { get; set; }
        public DateTime ActivatedAtUtc { get; set; }
    }

    private sealed class VendorProductOwnershipRecord
    {
        public Guid ProductId { get; set; }
        public Guid VendorUserId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public Guid AssignedByUserId { get; set; }
        public DateTime AssignedAtUtc { get; set; }
    }

    private sealed class VendorOrderAssignmentRecord
    {
        public Guid OrderId { get; set; }
        public Guid VendorId { get; set; }
        public Guid AssignedBy { get; set; }
        public DateTime AssignedAtUtc { get; set; }
        public string? DeliveryAgentName { get; set; }
        public string? DeliveryAgentPhone { get; set; }
        public string? AssignmentNote { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class VendorOrderNoteRecord
    {
        public string Id { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
        public Guid VendorId { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class VendorMessageRecord
    {
        public string Id { get; set; } = string.Empty;
        public Guid VendorId { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? From { get; set; }
        public string? To { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class VendorNotificationRecord
    {
        public string Id { get; set; } = string.Empty;
        public Guid VendorId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public Guid? OrderId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class VendorActivityRecord
    {
        public string Id { get; set; } = string.Empty;
        public Guid VendorId { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid? OrderId { get; set; }
        public Guid? ProductId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
