using TBM.Core.Entities.Users;

namespace TBM.Core.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByEmailWithRolesAsync(string email);
    IQueryable<User> GetQueryable();

    Task<User?> GetByRefreshTokenAsync(string refreshToken);
    Task<User?> GetByPasswordResetTokenAsync(string token);
    Task<User?> GetByEmailVerificationTokenAsync(string token);
    Task<bool> EmailExistsAsync(string email);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task<int> GetUserCountAsync(DateTime from, DateTime to);
    Task<IEnumerable<string>> GetUserRolesAsync(Guid userId);
}