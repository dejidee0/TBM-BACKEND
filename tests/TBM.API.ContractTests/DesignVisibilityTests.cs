using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TBM.API.Controllers.V1;
using TBM.Application.DTOs.AI;
using TBM.Application.Services;
using TBM.Core.Entities.AI;
using TBM.Core.Entities.Audit;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Repositories.AI;

namespace TBM.API.ContractTests;

public sealed class DesignVisibilityTests
{
    [Fact]
    public async Task UpdateVisibility_Owner_IsAllowed()
    {
        var ownerId = Guid.NewGuid();
        var design = BuildDesign(ownerId);

        var uow = new VisibilityTestUnitOfWork(
            new StubDesignRepository(design),
            new StubAuditLogRepository());

        var controller = BuildController(uow, ownerId);

        var result = await controller.UpdateVisibility(design.Id, new UpdateDesignVisibilityDto
        {
            IsPublic = true
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode ?? StatusCodes.Status200OK);
        Assert.True(design.IsPublic);
        Assert.NotNull(design.PublishedAt);
    }

    [Fact]
    public async Task UpdateVisibility_NonOwner_IsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var nonOwnerId = Guid.NewGuid();
        var design = BuildDesign(ownerId);

        var uow = new VisibilityTestUnitOfWork(
            new StubDesignRepository(design),
            new StubAuditLogRepository());

        var controller = BuildController(uow, nonOwnerId);

        var result = await controller.UpdateVisibility(design.Id, new UpdateDesignVisibilityDto
        {
            IsPublic = true
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.False(design.IsPublic);
    }

    private static DesignsController BuildController(IUnitOfWork uow, Guid userId)
    {
        var accessor = new HttpContextAccessor();
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Test");
        httpContext.User = new ClaimsPrincipal(identity);
        accessor.HttpContext = httpContext;

        var controller = new DesignsController(
            uow,
            new UserDataStoreService(uow),
            new AuditService(uow, accessor));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static AIDesign BuildDesign(Guid ownerId)
    {
        return new AIDesign
        {
            Id = Guid.NewGuid(),
            AIProjectId = Guid.NewGuid(),
            OutputUrl = "https://example.com/output.jpg",
            OutputType = TBM.Core.Enums.AIOutputType.Image,
            AIProject = new AIProject
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                SourceImageUrl = "https://example.com/source.jpg",
                GenerationType = TBM.Core.Enums.AIGenerationType.ImageToImage,
                Status = TBM.Core.Enums.AIProjectStatus.Completed
            }
        };
    }

    private sealed class StubDesignRepository : IAIDesignRepository
    {
        private readonly AIDesign _design;

        public StubDesignRepository(AIDesign design)
        {
            _design = design;
        }

        public Task CreateAsync(AIDesign design) => Task.CompletedTask;

        public Task<AIDesign?> GetByIdAsync(Guid id)
        {
            return Task.FromResult(id == _design.Id ? _design : null);
        }

        public Task<List<AIDesign>> GetByProjectAsync(Guid projectId) => Task.FromResult(new List<AIDesign>());

        public Task<(IReadOnlyList<AIDesign> Items, int TotalCount)> GetPublicPagedAsync(
            int page,
            int limit,
            string? roomType,
            string? search,
            string? sort)
        {
            return Task.FromResult(((IReadOnlyList<AIDesign>)new List<AIDesign>(), 0));
        }
    }

    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        public Task AddAsync(AuditLog log) => Task.CompletedTask;

        public Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
            int page,
            int pageSize,
            string? search = null,
            string? severity = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null)
        {
            return Task.FromResult((Enumerable.Empty<AuditLog>(), 0));
        }

        public Task<(int TotalLogs, int ErrorCount, int WarningCount, int InfoCount, DateTime? LastLogAt)> GetStatsAsync(
            DateTime? fromUtc = null,
            DateTime? toUtc = null)
        {
            return Task.FromResult((0, 0, 0, 0, (DateTime?)null));
        }
    }

    private sealed class VisibilityTestUnitOfWork : IUnitOfWork
    {
        public VisibilityTestUnitOfWork(IAIDesignRepository designs, IAuditLogRepository auditLogs)
        {
            AIDesigns = designs;
            AuditLogs = auditLogs;
        }

        public IUserRepository Users => throw new NotImplementedException();
        public IUserAddressRepository UserAddresses => throw new NotImplementedException();
        public IRoleRepository Roles => throw new NotImplementedException();
        public IAIProjectRepository AIProjects => throw new NotImplementedException();
        public IAIDesignRepository AIDesigns { get; }
        public IAIUsageRepository AIUsages => throw new NotImplementedException();
        public IAIRenovationEstimateRepository AIRenovationEstimates => throw new NotImplementedException();
        public IAIAssistantRepository AIAssistant => throw new NotImplementedException();
        public ISettingRepository Settings => throw new NotImplementedException();
        public IAuditLogRepository AuditLogs { get; }
        public ICategoryRepository Categories => throw new NotImplementedException();
        public IProductRepository Products => throw new NotImplementedException();
        public IProductImageRepository ProductImages => throw new NotImplementedException();
        public ICartRepository Carts => throw new NotImplementedException();
        public IOrderRepository Orders => throw new NotImplementedException();
        public IOrderStatusHistoryRepository OrderStatusHistories => throw new NotImplementedException();
        public IWebhookEventRepository WebhookEvents => throw new NotImplementedException();

        public Task<int> SaveChangesAsync() => Task.FromResult(1);
        public Task BeginTransactionAsync() => Task.CompletedTask;
        public Task CommitTransactionAsync() => Task.CompletedTask;
        public Task RollbackTransactionAsync() => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<Task> operation) => operation();
        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation) => operation();
        public void Dispose() { }
    }
}
