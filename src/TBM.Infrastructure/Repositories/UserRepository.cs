using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Users;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;
    
    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.Addresses)
            .FirstOrDefaultAsync(u => u.Id == id);
    }
public async Task<int> GetUserCountAsync(DateTime from, DateTime to)
{
    return await _context.Users
        .Where(u => u.CreatedAt >= from &&
                    u.CreatedAt < to)
        .CountAsync();
}

    public IQueryable<User> GetQueryable()
{
    return _context.Users.AsQueryable();
}

    
    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }
    
    public async Task<User?> GetByEmailWithRolesAsync(string email)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }
    
    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken 
                && u.RefreshTokenExpiry > DateTime.UtcNow);
    }
    
    public async Task<User?> GetByPasswordResetTokenAsync(string token)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.PasswordResetToken == token 
                && u.PasswordResetTokenExpiry > DateTime.UtcNow);
    }
    
    public async Task<User?> GetByEmailVerificationTokenAsync(string token)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == token 
                && u.EmailVerificationTokenExpiry > DateTime.UtcNow);
    }
    
    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }
    
    public async Task<User> CreateAsync(User user)
    {
        await _context.Users.AddAsync(user);
        return user;
    }
    
    public Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }
    
    public async Task<IEnumerable<string>> GetUserRolesAsync(Guid userId)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();
    }
}
