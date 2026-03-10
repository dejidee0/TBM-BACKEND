using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Contact;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class ContactMessageRepository : IContactMessageRepository
{
    private readonly ApplicationDbContext _context;

    public ContactMessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContactMessage> CreateAsync(ContactMessage message)
    {
        await _context.ContactMessages.AddAsync(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public Task<ContactMessage?> GetByIdAsync(Guid id)
    {
        return _context.ContactMessages.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task UpdateAsync(ContactMessage message)
    {
        _context.ContactMessages.Update(message);
        await _context.SaveChangesAsync();
    }
}
