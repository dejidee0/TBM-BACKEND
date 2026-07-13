using TBM.Application.DTOs.Inspiration;
using TBM.Core.Interfaces.Repositories;

namespace TBM.Application.Services;

public class InspirationService
{
    private readonly IInspirationDesignRepository _inspirationDesigns;

    public InspirationService(IInspirationDesignRepository inspirationDesigns)
    {
        _inspirationDesigns = inspirationDesigns;
    }

    public async Task<List<InspirationDesignDto>> GetAsync(string? category, string? style)
    {
        var items = await _inspirationDesigns.GetActiveAsync(category, style);

        return items.Select(x => new InspirationDesignDto
        {
            Id = x.Id,
            Title = x.Title,
            Style = x.Style,
            ImageUrl = x.ImageUrl,
            Description = x.Description,
            Category = x.Category,
            DisplayOrder = x.DisplayOrder
        }).ToList();
    }
}
