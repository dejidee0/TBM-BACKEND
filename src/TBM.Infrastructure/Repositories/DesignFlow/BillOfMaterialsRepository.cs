using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.DesignFlow;
using TBM.Core.Interfaces.Repositories.DesignFlow;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.DesignFlow;

public class BillOfMaterialsRepository : IBillOfMaterialsRepository
{
    private readonly ApplicationDbContext _context;

    public BillOfMaterialsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BillOfMaterials> CreateAsync(BillOfMaterials bom)
    {
        await _context.BillsOfMaterials.AddAsync(bom);
        return bom;
    }

    public Task<BillOfMaterials?> GetByIdAsync(Guid id)
    {
        return _context.BillsOfMaterials
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public Task<BillOfMaterials?> GetByDesignSessionIdAsync(Guid designSessionId)
    {
        return _context.BillsOfMaterials
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.DesignSessionId == designSessionId && !x.IsDeleted);
    }

    public Task UpdateAsync(BillOfMaterials bom)
    {
        _context.BillsOfMaterials.Update(bom);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateBomNumberAsync()
    {
        var prefix = $"BOM-{DateTime.UtcNow:yyyy}-";
        var lastNumber = await _context.BillsOfMaterials
            .Where(x => x.BomNumber.StartsWith(prefix))
            .OrderByDescending(x => x.BomNumber)
            .Select(x => x.BomNumber)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(lastNumber))
        {
            return $"{prefix}00001";
        }

        var seqText = lastNumber[(prefix.Length)..];
        return int.TryParse(seqText, out var seq)
            ? $"{prefix}{(seq + 1):D5}"
            : $"{prefix}00001";
    }
}
