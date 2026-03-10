using TBM.Core.Entities.Contact;

namespace TBM.Core.Interfaces.Repositories;

public interface IContactMessageRepository
{
    Task<ContactMessage> CreateAsync(ContactMessage message);
    Task<ContactMessage?> GetByIdAsync(Guid id);
    Task UpdateAsync(ContactMessage message);
}
