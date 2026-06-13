using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Core.Enums;

namespace TBM.API.Controllers.V1;

/// <summary>
/// Returns all enum/lookup values the frontend needs for dropdowns and mapping.
/// All endpoints are public — no authentication required.
/// </summary>
[ApiController]
[Route("api/v1/lookups")]
[EnableRateLimiting("DynamicPolicy")]
public class LookupsController : ControllerBase
{
    // ─── Curated material types used across TBM products ───────────────────
    private static readonly IReadOnlyList<LookupItemDto> MaterialTypes =
    [
        new("Hardwood",       "hardwood"),
        new("Softwood",       "softwood"),
        new("Engineered Wood","engineered-wood"),
        new("Plywood",        "plywood"),
        new("MDF",            "mdf"),
        new("Metal",          "metal"),
        new("Steel",          "steel"),
        new("Aluminium",      "aluminium"),
        new("Glass",          "glass"),
        new("Ceramic",        "ceramic"),
        new("Porcelain",      "porcelain"),
        new("Marble",         "marble"),
        new("Granite",        "granite"),
        new("Concrete",       "concrete"),
        new("Vinyl",          "vinyl"),
        new("Laminate",       "laminate"),
        new("Fabric",         "fabric"),
        new("Leather",        "leather"),
        new("Plastic",        "plastic"),
        new("PVC",            "pvc"),
        new("Rubber",         "rubber"),
        new("Foam",           "foam"),
        new("Brass",          "brass"),
        new("Copper",         "copper"),
        new("Stone",          "stone"),
        new("Terrazzo",       "terrazzo"),
    ];

    // ─── Quality tiers ───────────────────────────────────────────────────────
    private static readonly IReadOnlyList<LookupItemDto> QualityTiers =
    [
        new("Budget",    "budget"),
        new("Standard",  "standard"),
        new("Premium",   "premium"),
        new("Luxury",    "luxury"),
    ];

    /// <summary>
    /// Returns ALL lookup tables in a single call — recommended for the frontend to cache on app load.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new
        {
            success = true,
            data = new
            {
                brandTypes    = BuildEnumLookup<BrandType>(),
                productTypes  = BuildEnumLookup<ProductType>(),
                materialTypes = MaterialTypes,
                qualityTiers  = QualityTiers,
                orderStatuses       = BuildEnumLookup<OrderStatus>(),
                paymentStatuses     = BuildEnumLookup<PaymentStatus>(),
                paymentMethods      = BuildEnumLookup<PaymentMethod>(),
                designSessionTiers  = BuildEnumLookup<DesignSessionTier>(),
                projectStatuses     = BuildEnumLookup<ProjectStatus>(),
            }
        });
    }

    /// <summary>
    /// Brand types — e.g. TBM=1, Bogat=2
    /// </summary>
    [HttpGet("brand-types")]
    public IActionResult GetBrandTypes()
        => OkLookup(BuildEnumLookup<BrandType>());

    /// <summary>
    /// Product types — e.g. PhysicalProduct=1, Service=2
    /// </summary>
    [HttpGet("product-types")]
    public IActionResult GetProductTypes()
        => OkLookup(BuildEnumLookup<ProductType>());

    /// <summary>
    /// Suggested material type strings used when creating/editing products.
    /// MaterialType is a free-text field — use the 'value' string as the stored value.
    /// </summary>
    [HttpGet("material-types")]
    public IActionResult GetMaterialTypes()
        => OkLookup(MaterialTypes);

    /// <summary>
    /// Quality tier strings — Budget, Standard, Premium, Luxury
    /// </summary>
    [HttpGet("quality-tiers")]
    public IActionResult GetQualityTiers()
        => OkLookup(QualityTiers);

    /// <summary>
    /// Order status enum — name + integer value
    /// </summary>
    [HttpGet("order-statuses")]
    public IActionResult GetOrderStatuses()
        => OkLookup(BuildEnumLookup<OrderStatus>());

    /// <summary>
    /// Payment status enum — name + integer value
    /// </summary>
    [HttpGet("payment-statuses")]
    public IActionResult GetPaymentStatuses()
        => OkLookup(BuildEnumLookup<PaymentStatus>());

    /// <summary>
    /// Payment method enum — name + integer value
    /// </summary>
    [HttpGet("payment-methods")]
    public IActionResult GetPaymentMethods()
        => OkLookup(BuildEnumLookup<PaymentMethod>());

    /// <summary>
    /// Design session tier enum — Luxury=1, Economic=2
    /// </summary>
    [HttpGet("design-session-tiers")]
    public IActionResult GetDesignSessionTiers()
        => OkLookup(BuildEnumLookup<DesignSessionTier>());

    /// <summary>
    /// Project status enum
    /// </summary>
    [HttpGet("project-statuses")]
    public IActionResult GetProjectStatuses()
        => OkLookup(BuildEnumLookup<ProjectStatus>());

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static List<EnumLookupItemDto> BuildEnumLookup<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
               .Select(e => new EnumLookupItemDto(e.ToString(), Convert.ToInt32(e)))
               .ToList();

    private IActionResult OkLookup(object data)
        => Ok(new { success = true, data });

    // ─── DTOs ────────────────────────────────────────────────────────────────

    private sealed record EnumLookupItemDto(string Name, int Value);
    private sealed record LookupItemDto(string Label, string Value);
}
