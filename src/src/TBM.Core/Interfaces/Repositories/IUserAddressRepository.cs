using TBM.Core.Entities.Users;

namespace TBM.Core.Interfaces.Repositories;

public interface IUserAddressRepository
{
    Task<List<UserAddress>> GetByUserIdAsync(Guid userId);
    Task<UserAddress?> GetByIdAsync(Guid id);
    Task<UserAddress> CreateAsync(UserAddress address);
    Task UpdateAsync(UserAddress address);
    Task DeleteAsync(UserAddress address);
}
