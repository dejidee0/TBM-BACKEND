using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class SettingRepository : ISettingRepository
{
    private readonly ApplicationDbContext _context;

    public SettingRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Setting?> GetByKeyAsync(string category, string key)
    {
        return await _context.Settings
            .FirstOrDefaultAsync(s => s.Category == category && s.Key == key);
    }

    public async Task<IEnumerable<Setting>> GetByCategoryAsync(string category)
    {
        return await _context.Settings
            .Where(s => s.Category == category)
            .ToListAsync();
    }

    public async Task<IEnumerable<Setting>> GetAllAsync()
{
    return await _context.Settings.ToListAsync();
}


    public async Task AddAsync(Setting setting)
    {
        await _context.Settings.AddAsync(setting);
    }

    public async Task UpdateAsync(Setting setting)
    {
        _context.Settings.Update(setting);
        await Task.CompletedTask;
    }
}
