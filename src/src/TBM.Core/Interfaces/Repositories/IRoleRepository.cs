using TBM.Core.Entities.Users;

namespace TBM.Core.Interfaces.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name);
    Task<Role?> GetByIdAsync(Guid id);
    Task<IEnumerable<Role>> GetAllAsync();
    Task<Role> CreateAsync(Role role);
}