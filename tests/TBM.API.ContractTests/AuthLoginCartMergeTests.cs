using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TBM.Application.DTOs.Auth;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.Helpers;
using TBM.Application.Interfaces;
using TBM.Application.Services;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Core.Interfaces.Services;

namespace TBM.API.ContractTests;

public sealed class AuthLoginCartMergeTests
{
    [Fact]
    public async Task LoginAsync_WithGuestCartItems_MergesAndReturnsPersistentCart()
    {
        var user = BuildUser("customer@test.com", "Pass123$");
        var mergedCart = BuildCart(quantity: 3);
        var cartService = new StubCartService
        {
            MergeResponse = ApiResponse<MergeCartResultDto>.SuccessResponse(
                new MergeCartResultDto
                {
                    Cart = mergedCart,
                    Warnings = new List<MergeCartWarningDto>()
                },
                "Guest cart merged successfully.")
        };

        var service = BuildAuthService(user, cartService);

        var result = await service.LoginAsync(new LoginDto
        {
            Email = user.Email,
            Password = "Pass123$",
            GuestCartItems =
            [
                new MergeCartItemDto
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 3
                }
            ]
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.CartMergeAttempted);
        Assert.True(result.Data.CartMergeSucceeded);
        Assert.NotNull(result.Data.Cart);
        Assert.Equal(3, result.Data.Cart!.TotalItems);
        Assert.Equal(user.Id, cartService.LastMergeUserId);
        Assert.Single(cartService.LastMergeItems);
        Assert.Equal(1, cartService.MergeCallCount);
        Assert.Equal(0, cartService.GetCartCallCount);
    }

    [Fact]
    public async Task LoginAsync_WhenMergeFails_ReturnsExistingPersistentCart()
    {
        var user = BuildUser("fallback@test.com", "Pass123$");
        var existingCart = BuildCart(quantity: 2);
        var cartService = new StubCartService
        {
            MergeResponse = ApiResponse<MergeCartResultDto>.ErrorResponse("Merge failed"),
            GetCartResponse = ApiResponse<CartDto>.SuccessResponse(existingCart)
        };

        var service = BuildAuthService(user, cartService);

        var result = await service.LoginAsync(new LoginDto
        {
            Email = user.Email,
            Password = "Pass123$",
            GuestCartItems =
            [
                new MergeCartItemDto
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 2
                }
            ]
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.CartMergeAttempted);
        Assert.False(result.Data.CartMergeSucceeded);
        Assert.Equal("Merge failed", result.Data.CartMergeMessage);
        Assert.NotNull(result.Data.Cart);
        Assert.Equal(2, result.Data.Cart!.TotalItems);
        Assert.Equal(1, cartService.MergeCallCount);
        Assert.Equal(1, cartService.GetCartCallCount);
    }

    [Fact]
    public async Task LoginAsync_WithoutGuestCartItems_ReturnsExistingPersistentCart()
    {
        var user = BuildUser("persisted@test.com", "Pass123$");
        var existingCart = BuildCart(quantity: 4);
        var cartService = new StubCartService
        {
            GetCartResponse = ApiResponse<CartDto>.SuccessResponse(existingCart)
        };

        var service = BuildAuthService(user, cartService);

        var result = await service.LoginAsync(new LoginDto
        {
            Email = user.Email,
            Password = "Pass123$"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.CartMergeAttempted);
        Assert.False(result.Data.CartMergeSucceeded);
        Assert.NotNull(result.Data.Cart);
        Assert.Equal(4, result.Data.Cart!.TotalItems);
        Assert.Equal(0, cartService.MergeCallCount);
        Assert.Equal(1, cartService.GetCartCallCount);
    }

    private static AuthService BuildAuthService(User user, StubCartService cartService)
    {
        var jwtHelper = new JwtHelper(Options.Create(new JwtSettings
        {
            SecretKey = "test-secret-key-with-at-least-32-characters",
            Issuer = "TBM.Tests",
            Audience = "TBM.Tests",
            ExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7
        }));

        var unitOfWork = new AuthLoginTestUnitOfWork(new StubUserRepository(user));
        return new AuthService(unitOfWork, jwtHelper, new StubEmailService(), cartService, NullLogger<AuthService>.Instance);
    }

    private static User BuildUser(string email, string password)
    {
        var role = new Role
        {
            Name = UserRoles.Customer,
            Description = "Customer"
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = PasswordHasher.HashPassword(password),
            IsActive = true
        };

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role
        });

        return user;
    }

    private static CartDto BuildCart(int quantity)
    {
        return new CartDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TotalItems = quantity,
            SubTotal = 1000m * quantity,
            Items =
            [
                new CartItemDto
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    ProductName = "Test Product",
                    Quantity = quantity,
                    UnitPrice = 1000m,
                    SubTotal = 1000m * quantity,
                    AddedAt = DateTime.UtcNow
                }
            ],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private sealed class StubCartService : ICartService
    {
        public ApiResponse<MergeCartResultDto> MergeResponse { get; set; } =
            ApiResponse<MergeCartResultDto>.SuccessResponse(new MergeCartResultDto
            {
                Cart = new CartDto()
            });

        public ApiResponse<CartDto> GetCartResponse { get; set; } =
            ApiResponse<CartDto>.SuccessResponse(new CartDto());

        public int MergeCallCount { get; private set; }
        public int GetCartCallCount { get; private set; }
        public Guid? LastMergeUserId { get; private set; }
        public List<MergeCartItemDto> LastMergeItems { get; private set; } = new();

        public Task<ApiResponse<CartDto>> GetCartAsync(Guid userId)
        {
            GetCartCallCount++;
            return Task.FromResult(GetCartResponse);
        }

        public Task<ApiResponse<CartDto>> AddToCartAsync(Guid userId, AddToCartDto dto) => throw new NotImplementedException();
        public Task<ApiResponse<CartDto>> UpdateCartItemAsync(Guid userId, Guid itemId, UpdateCartItemDto dto) => throw new NotImplementedException();
        public Task<ApiResponse<bool>> RemoveCartItemAsync(Guid userId, Guid itemId) => throw new NotImplementedException();
        public Task<ApiResponse<bool>> ClearCartAsync(Guid userId) => throw new NotImplementedException();

        public Task<ApiResponse<MergeCartResultDto>> MergeGuestCartAsync(Guid userId, MergeCartRequestDto dto)
        {
            MergeCallCount++;
            LastMergeUserId = userId;
            LastMergeItems = dto.Items;
            return Task.FromResult(MergeResponse);
        }
    }

    private sealed class StubEmailService : IEmailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string htmlBody) => Task.CompletedTask;
        public Task SendVerificationEmailAsync(string toEmail, string fullName, string verificationLink) => Task.CompletedTask;
        public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink) => Task.CompletedTask;
        public Task SendPasswordResetConfirmationAsync(string toEmail, string fullName) => Task.CompletedTask;
        public Task SendPasswordChangeOTPAsync(string toEmail, string fullName, string otp) => Task.CompletedTask;
        public Task SendWelcomeEmailAsync(string toEmail, string fullName) => Task.CompletedTask;
        public Task SendOrderConfirmationAsync(string toEmail, string fullName, string orderNumber, decimal totalAmount) => Task.CompletedTask;
    }

    private sealed class StubUserRepository : IUserRepository
    {
        private readonly User _user;

        public StubUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(id == _user.Id ? _user : null);
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult(string.Equals(email, _user.Email, StringComparison.OrdinalIgnoreCase) ? _user : null);
        public Task<User?> GetByEmailWithRolesAsync(string email) => Task.FromResult(string.Equals(email, _user.Email, StringComparison.OrdinalIgnoreCase) ? _user : null);
        public IQueryable<User> GetQueryable() => new List<User> { _user }.AsQueryable();
        public Task<User?> GetByRefreshTokenAsync(string refreshToken) => Task.FromResult(_user.RefreshToken == refreshToken ? _user : null);
        public Task<User?> GetByPasswordResetTokenAsync(string token) => Task.FromResult(_user.PasswordResetToken == token ? _user : null);
        public Task<User?> GetByEmailVerificationTokenAsync(string token) => Task.FromResult(_user.EmailVerificationToken == token ? _user : null);
        public Task<bool> EmailExistsAsync(string email) => Task.FromResult(string.Equals(email, _user.Email, StringComparison.OrdinalIgnoreCase));
        public Task<User> CreateAsync(User user) => throw new NotImplementedException();
        public Task UpdateAsync(User user) => Task.CompletedTask;
        public Task<int> GetUserCountAsync(DateTime from, DateTime to) => throw new NotImplementedException();
        public Task<IEnumerable<string>> GetUserRolesAsync(Guid userId) => Task.FromResult<IEnumerable<string>>(_user.UserRoles.Select(x => x.Role.Name).ToList());
    }

    private sealed class AuthLoginTestUnitOfWork : IUnitOfWork
    {
        public AuthLoginTestUnitOfWork(IUserRepository users)
        {
            Users = users;
        }

        public IUserRepository Users { get; }
        public IUserAddressRepository UserAddresses => throw new NotImplementedException();
        public IRoleRepository Roles => throw new NotImplementedException();
        public IAIProjectRepository AIProjects => throw new NotImplementedException();
        public IAIDesignRepository AIDesigns => throw new NotImplementedException();
        public IAIUsageRepository AIUsages => throw new NotImplementedException();
        public IAIRenovationEstimateRepository AIRenovationEstimates => throw new NotImplementedException();
        public IAIAssistantRepository AIAssistant => throw new NotImplementedException();
        public ISettingRepository Settings => throw new NotImplementedException();
        public IAuditLogRepository AuditLogs => throw new NotImplementedException();
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
