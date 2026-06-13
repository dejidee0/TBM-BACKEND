using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Portfolio;
using TBM.Core.Enums;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class PortfolioRepository : IPortfolioRepository
{
    private readonly ApplicationDbContext _context;

    public PortfolioRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VendorPortfolioProject> CreateAsync(VendorPortfolioProject project)
    {
        await _context.VendorPortfolioProjects.AddAsync(project);
        return project;
    }

    public async Task<VendorPortfolioProject?> GetByIdAsync(Guid id)
    {
        return await _context.VendorPortfolioProjects
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public async Task<List<VendorPortfolioProject>> GetByVendorAsync(Guid vendorId)
    {
        return await _context.VendorPortfolioProjects
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .Where(x => x.VendorId == vendorId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<VendorPortfolioProject> Items, int TotalCount)> GetPublishedAsync(
        string? category = null, string? location = null, int page = 1, int pageSize = 12)
    {
        var query = _context.VendorPortfolioProjects
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .Where(x => x.Status == PortfolioStatus.Published && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category.Contains(category));

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(x => x.Location.Contains(location));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<VendorPortfolioProject> Items, int TotalCount)> GetAllAdminAsync(
        PortfolioStatus? status = null, int page = 1, int pageSize = 20)
    {
        var query = _context.VendorPortfolioProjects
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .Where(x => !x.IsDeleted);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task UpdateAsync(VendorPortfolioProject project)
    {
        _context.VendorPortfolioProjects.Update(project);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var project = await _context.VendorPortfolioProjects.FindAsync(id);
        if (project != null)
        {
            project.IsDeleted = true;
            project.DeletedAt = DateTime.UtcNow;
            _context.VendorPortfolioProjects.Update(project);
        }
    }

    public async Task AddImageAsync(PortfolioProjectImage image)
    {
        await _context.PortfolioProjectImages.AddAsync(image);
    }

    public async Task DeleteImageAsync(Guid imageId)
    {
        var image = await _context.PortfolioProjectImages.FindAsync(imageId);
        if (image != null)
            _context.PortfolioProjectImages.Remove(image);
    }

    public async Task<PortfolioProjectImage?> GetImageByIdAsync(Guid imageId)
    {
        return await _context.PortfolioProjectImages.FindAsync(imageId);
    }
}
