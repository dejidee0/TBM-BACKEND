using TBM.Core.Interfaces.Repositories.AI;
using TBM.Core.Interfaces.Repositories.Subscriptions;

namespace TBM.Application.Tests.TestDoubles;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public FakeUnitOfWork(
        FakeProductRepository products,
        FakeBillOfMaterialsRepository billsOfMaterials,
        FakeDesignSessionRepository designSessions,
        FakeProjectRepository projects,
        FakeOrderRepository orders)
    {
        ProductStore = products;
        BillStore = billsOfMaterials;
        DesignSessionStore = designSessions;
        ProjectStore = projects;
        OrderStore = orders;

        Products = products;
        BillsOfMaterials = billsOfMaterials;
        DesignSessions = designSessions;
        Projects = projects;
        Orders = orders;
    }

    public FakeProductRepository ProductStore { get; }
    public FakeBillOfMaterialsRepository BillStore { get; }
    public FakeDesignSessionRepository DesignSessionStore { get; }
    public FakeProjectRepository ProjectStore { get; }
    public FakeOrderRepository OrderStore { get; }
    public FakeCartRepository CartStore { get; } = new();

    public IUserRepository Users => throw new NotSupportedException();
    public IUserAddressRepository UserAddresses => throw new NotSupportedException();
    public IRoleRepository Roles => throw new NotSupportedException();
    public IAIProjectRepository AIProjects => throw new NotSupportedException();
    public IAIDesignRepository AIDesigns => throw new NotSupportedException();
    public IAIUsageRepository AIUsages => throw new NotSupportedException();
    public IAIRenovationEstimateRepository AIRenovationEstimates => throw new NotSupportedException();
    public IAIAssistantRepository AIAssistant => throw new NotSupportedException();
    public IDesignSessionRepository DesignSessions { get; }
    public IBillOfMaterialsRepository BillsOfMaterials { get; }
    public IProjectRepository Projects { get; }
    public ISettingRepository Settings => throw new NotSupportedException();
    public IAuditLogRepository AuditLogs => throw new NotSupportedException();
    public ICategoryRepository Categories => throw new NotSupportedException();
    public IProductRepository Products { get; }
    public IProductImageRepository ProductImages => throw new NotSupportedException();
    public IProductVariantRepository ProductVariants => throw new NotSupportedException();
    public ICartRepository Carts => CartStore;
    public IOrderRepository Orders { get; }
    public IOrderStatusHistoryRepository OrderStatusHistories => throw new NotSupportedException();
    public IWebhookEventRepository WebhookEvents => throw new NotSupportedException();
    public ISubscriptionRepository Subscriptions => throw new NotSupportedException();
    public IPricingConfigRepository PricingConfigs => throw new NotSupportedException();
    public IDiscountRepository Discounts => throw new NotSupportedException();
    public IPortfolioRepository Portfolio => throw new NotSupportedException();

    public Task<int> SaveChangesAsync() => Task.FromResult(0);
    public void ClearChangeTracker() { }
    public Task BeginTransactionAsync() => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
    public Task ExecuteInTransactionAsync(Func<Task> operation) => operation();
    public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation) => operation();
    public void Dispose() { }
}

internal sealed class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = new();

    public void Seed(Product product, bool asInventoryCandidate = true)
    {
        _products[product.Id] = product;
        if (asInventoryCandidate && !InventoryCandidates.Contains(product))
        {
            InventoryCandidates.Add(product);
        }
    }

    public List<Product> InventoryCandidates { get; } = new();

    public Task<Product?> GetByIdAsync(Guid id)
        => Task.FromResult(_products.TryGetValue(id, out var product) ? product : null);

    public Task<Product?> GetBySlugAsync(string slug) => throw new NotSupportedException();

    public Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        BrandType? brandType = null,
        ProductType? productType = null,
        Guid? categoryId = null,
        string? searchTerm = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? isFeatured = null,
        bool activeOnly = true) => throw new NotSupportedException();

    public Task<IEnumerable<Product>> GetFeaturedAsync(BrandType? brandType = null, int limit = 10) => throw new NotSupportedException();
    public Task<IEnumerable<Product>> GetRelatedAsync(Guid productId, int limit = 4) => throw new NotSupportedException();

    public Task<Product> CreateAsync(Product product)
    {
        _products[product.Id] = product;
        return Task.FromResult(product);
    }

    public Task UpdateAsync(Product product)
    {
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id) => throw new NotSupportedException();
    public Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null) => throw new NotSupportedException();
    public Task<bool> SKUExistsAsync(string sku, Guid? excludeId = null) => throw new NotSupportedException();
    public Task<Dictionary<Guid, int>> GetActiveProductCountsAsync(IEnumerable<Guid> categoryIds) => throw new NotSupportedException();

    public Task<List<Product>> GetInventoryCandidatesAsync()
        => Task.FromResult(InventoryCandidates.ToList());

    public Task UpdateStockAsync(Guid productId, int quantity) => throw new NotSupportedException();
    public Task<int> UpdateStockAtomicAsync(Guid productId, int quantityToSubtract) => throw new NotSupportedException();

    public Task<List<Product>> SearchByAIKeywordsAsync(IEnumerable<string> keywords, int limit = 8)
    {
        var terms = keywords
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        var matches = _products.Values
            .Where(product => terms.Count == 0 || terms.Any(term =>
                (product.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (product.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (product.AIKeywords?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)))
            .Take(limit)
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<List<Product>> BulkCreateAsync(IEnumerable<Product> products)
    {
        var created = products.ToList();
        foreach (var product in created)
        {
            _products[product.Id] = product;
        }

        return Task.FromResult(created);
    }
}

internal sealed class FakeBillOfMaterialsRepository : IBillOfMaterialsRepository
{
    private readonly Dictionary<Guid, BillOfMaterials> _boms = new();
    private int _counter = 1;

    public List<BillOfMaterials> CreatedBoms { get; } = new();

    public void Seed(BillOfMaterials bom)
    {
        _boms[bom.Id] = bom;
    }

    public Task<BillOfMaterials> CreateAsync(BillOfMaterials bom)
    {
        _boms[bom.Id] = bom;
        CreatedBoms.Add(bom);
        return Task.FromResult(bom);
    }

    public Task<BillOfMaterials?> GetByIdAsync(Guid id)
        => Task.FromResult(_boms.TryGetValue(id, out var bom) ? bom : null);

    public Task<BillOfMaterials?> GetByDesignSessionIdAsync(Guid designSessionId)
        => Task.FromResult(_boms.Values.FirstOrDefault(x => x.DesignSessionId == designSessionId));

    public Task UpdateAsync(BillOfMaterials bom)
    {
        _boms[bom.Id] = bom;
        return Task.CompletedTask;
    }

    public Task<string> GenerateBomNumberAsync()
        => Task.FromResult($"BOM-2026-{_counter++.ToString("D5")}");
}

internal sealed class FakeDesignSessionRepository : IDesignSessionRepository
{
    private readonly Dictionary<Guid, DesignSession> _sessions = new();
    private int _counter = 1;

    public List<DesignSession> CreatedSessions { get; } = new();

    public void Seed(DesignSession session)
    {
        _sessions[session.Id] = session;
    }

    public Task<DesignSession> CreateAsync(DesignSession session)
    {
        _sessions[session.Id] = session;
        CreatedSessions.Add(session);
        return Task.FromResult(session);
    }

    public Task<DesignSession?> GetByIdAsync(Guid id)
        => Task.FromResult(_sessions.TryGetValue(id, out var session) ? session : null);

    public Task<List<DesignSession>> GetByUserAsync(Guid userId)
        => Task.FromResult(_sessions.Values.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToList());

    public Task<DesignSession?> GetNextProcessingAsync()
        => Task.FromResult(_sessions.Values.FirstOrDefault(x => x.Status == DesignSessionStatus.Processing));

    public Task UpdateAsync(DesignSession session)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<string> GenerateSessionNumberAsync()
        => Task.FromResult($"DS-2026-{_counter++.ToString("D5")}");
}

internal sealed class FakeProjectRepository : IProjectRepository
{
    private readonly Dictionary<Guid, Project> _projects = new();
    private int _counter = 1;

    public List<Project> CreatedProjects { get; } = new();
    public List<ProjectTimeline> AddedTimelines { get; } = new();

    public void Seed(Project project)
    {
        _projects[project.Id] = project;
    }

    public Task<Project> CreateAsync(Project project)
    {
        _projects[project.Id] = project;
        CreatedProjects.Add(project);
        return Task.FromResult(project);
    }

    public Task<Project?> GetByIdAsync(Guid id)
        => Task.FromResult(_projects.TryGetValue(id, out var project) ? project : null);

    public Task<Project?> GetByOrderIdAsync(Guid orderId)
        => Task.FromResult(_projects.Values.FirstOrDefault(x => x.OrderId == orderId));

    public Task<Project?> GetByDesignSessionIdAsync(Guid designSessionId)
        => Task.FromResult(_projects.Values.FirstOrDefault(x => x.DesignSessionId == designSessionId));

    public Task<List<Project>> GetByUserAsync(Guid userId)
        => Task.FromResult(_projects.Values.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToList());

    public Task UpdateAsync(Project project)
    {
        _projects[project.Id] = project;
        return Task.CompletedTask;
    }

    public Task AddTimelineAsync(ProjectTimeline timeline)
    {
        AddedTimelines.Add(timeline);
        if (_projects.TryGetValue(timeline.ProjectId, out var project))
        {
            project.Timelines.Add(timeline);
        }

        return Task.CompletedTask;
    }

    public Task AddDocumentAsync(ProjectDocument document)
    {
        if (_projects.TryGetValue(document.ProjectId, out var project))
        {
            project.Documents.Add(document);
        }

        return Task.CompletedTask;
    }

    public Task AddGalleryImageAsync(SiteGalleryImage image)
    {
        if (_projects.TryGetValue(image.ProjectId, out var project))
        {
            project.GalleryImages.Add(image);
        }

        return Task.CompletedTask;
    }

    public Task<string> GenerateProjectNumberAsync()
        => Task.FromResult($"PRJ-2026-{_counter++.ToString("D5")}");
}

internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = new();
    private int _counter = 1;

    public void Seed(Order order)
    {
        _orders[order.Id] = order;
    }

    public Task<Order?> GetByIdAsync(Guid id)
        => Task.FromResult(_orders.TryGetValue(id, out var order) ? order : null);

    public Task<Order?> GetByOrderNumberAsync(string orderNumber)
        => Task.FromResult(_orders.Values.FirstOrDefault(x => x.OrderNumber == orderNumber));

    public Task<Order?> GetByPaymentReferenceAsync(string paymentReference, Guid? userId = null)
        => Task.FromResult(_orders.Values.FirstOrDefault(x => x.PaymentReference == paymentReference && (!userId.HasValue || x.UserId == userId.Value)));

    public Task<List<TBM.Core.DTOs.Admin.MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months) => throw new NotSupportedException();
    public Task<List<TBM.Core.DTOs.Admin.PaymentDistributionDto>> GetPaymentDistributionAsync() => throw new NotSupportedException();
    public Task<decimal> GetRevenueAsync(DateTime from, DateTime to) => throw new NotSupportedException();
    public Task<int> GetOrderCountAsync(DateTime from, DateTime to) => throw new NotSupportedException();
    public Task<List<TBM.Core.DTOs.Admin.RevenueByServiceDto>> GetRevenueByServiceAsync(DateTime? fromDate = null, DateTime? toDate = null) => throw new NotSupportedException();
    public Task<(IEnumerable<Order> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? userId = null, OrderStatus? status = null, PaymentStatus? paymentStatus = null, DateTime? fromDate = null, DateTime? toDate = null, string? searchTerm = null) => throw new NotSupportedException();

    public Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId)
        => Task.FromResult<IEnumerable<Order>>(_orders.Values.Where(x => x.UserId == userId).ToList());

    public Task<Order> CreateAsync(Order order)
    {
        _orders[order.Id] = order;
        return Task.FromResult(order);
    }

    public Task UpdateAsync(Order order)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id) => throw new NotSupportedException();
    public IQueryable<Order> GetQueryable() => _orders.Values.AsQueryable();

    public Task<string> GenerateOrderNumberAsync()
        => Task.FromResult($"ORD-2026-{_counter++.ToString("D5")}");

    public Task<decimal> GetTotalSalesAsync(DateTime? fromDate = null, DateTime? toDate = null) => throw new NotSupportedException();
    public Task<int> GetTotalOrdersCountAsync(OrderStatus? status = null) => throw new NotSupportedException();
    public Task<int> GetCountByPaymentStatusAsync(PaymentStatus paymentStatus, DateTime? fromDate = null, DateTime? toDate = null) => throw new NotSupportedException();
    public Task<decimal> GetRevenueByPaymentStatusAsync(PaymentStatus paymentStatus, DateTime? fromDate = null, DateTime? toDate = null) => throw new NotSupportedException();
}

internal sealed class FakeCartRepository : ICartRepository
{
    private readonly Dictionary<Guid, Cart> _carts = new();

    public int ClearCartCallCount { get; private set; }

    public void Seed(Cart cart) => _carts[cart.Id] = cart;

    public Task<Cart?> GetByUserIdAsync(Guid userId)
        => Task.FromResult(_carts.Values.FirstOrDefault(c => c.UserId == userId));

    public Task<Cart?> GetByGuestSessionIdAsync(string guestSessionId)
        => Task.FromResult(_carts.Values.FirstOrDefault(c => c.GuestSessionId == guestSessionId));

    public Task<Cart?> GetByIdAsync(Guid id)
        => Task.FromResult(_carts.TryGetValue(id, out var cart) ? cart : null);

    public Task<Cart> CreateAsync(Cart cart)
    {
        _carts[cart.Id] = cart;
        return Task.FromResult(cart);
    }

    public Task UpdateAsync(Cart cart)
    {
        _carts[cart.Id] = cart;
        return Task.CompletedTask;
    }

    public Task ClearCartAsync(Guid cartId)
    {
        ClearCartCallCount++;
        if (_carts.TryGetValue(cartId, out var cart))
        {
            cart.Items.Clear();
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id) => throw new NotSupportedException();
    public Task<CartItem?> GetCartItemAsync(Guid cartId, Guid productId) => throw new NotSupportedException();
    public Task AddItemAsync(CartItem item) => throw new NotSupportedException();
    public Task UpdateItemAsync(CartItem item) => throw new NotSupportedException();
    public Task RemoveItemAsync(Guid itemId) => throw new NotSupportedException();
}

internal sealed class NoopImageStorageService : IImageStorageService
{
    public Task<string> UploadRoomImageAsync(Stream stream, string fileName, string userId)
        => Task.FromResult($"https://storage.local/{fileName}");

    public Task<string> UploadGeneratedMediaAsync(Stream stream, string fileName, string userId, string? contentType = null)
        => Task.FromResult($"https://storage.local/{fileName}");

    public Task<string> UploadDocumentAsync(Stream stream, string fileName, string userId, string? contentType = null)
        => Task.FromResult($"https://storage.local/{fileName}");

    public Task<string> UploadGalleryImageAsync(Stream stream, string fileName, string userId, string? contentType = null)
        => Task.FromResult($"https://storage.local/{fileName}");
}

internal sealed class CapturingBomGenerationClient : IBomGenerationClient
{
    public List<BomGenerationRequestDto> Requests { get; } = new();
    public BomGenerationResultDto? Response { get; set; }

    public Task<BomGenerationResultDto?> GenerateAsync(BomGenerationRequestDto request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(Response);
    }
}

internal sealed class NoopLogger<T> : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
