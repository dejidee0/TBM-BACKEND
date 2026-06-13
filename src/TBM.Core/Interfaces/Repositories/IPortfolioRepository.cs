using TBM.Core.Entities.Portfolio;
using TBM.Core.Enums;

namespace TBM.Core.Interfaces.Repositories;

public interface IPortfolioRepository
{
    Task<VendorPortfolioProject> CreateAsync(VendorPortfolioProject project);
    Task<VendorPortfolioProject?> GetByIdAsync(Guid id);
    Task<List<VendorPortfolioProject>> GetByVendorAsync(Guid vendorId);
    Task<(List<VendorPortfolioProject> Items, int TotalCount)> GetPublishedAsync(
        string? category = null, string? location = null, int page = 1, int pageSize = 12);
    Task<(List<VendorPortfolioProject> Items, int TotalCount)> GetAllAdminAsync(
        PortfolioStatus? status = null, int page = 1, int pageSize = 20);
    Task UpdateAsync(VendorPortfolioProject project);
    Task DeleteAsync(Guid id);
    Task AddImageAsync(PortfolioProjectImage image);
    Task DeleteImageAsync(Guid imageId);
    Task<PortfolioProjectImage?> GetImageByIdAsync(Guid imageId);
}
