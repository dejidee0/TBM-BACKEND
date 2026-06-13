using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Users;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class UserAddressRepository : IUserAddressRepository
{
    private readonly ApplicationDbContext _context;

    public UserAddressRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<UserAddress>> GetByUserIdAsync(Guid userId)
    {
        return _context.UserAddresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public Task<UserAddress?> GetByIdAsync(Guid id)
    {
        return _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<UserAddress> CreateAsync(UserAddress address)
    {
        await _context.UserAddresses.AddAsync(address);
        return address;
    }

    public Task UpdateAsync(UserAddress address)
    {
        _context.UserAddresses.Update(address);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(UserAddress address)
    {
        _context.UserAddresses.Remove(address);
        return Task.CompletedTask;
    }
}
