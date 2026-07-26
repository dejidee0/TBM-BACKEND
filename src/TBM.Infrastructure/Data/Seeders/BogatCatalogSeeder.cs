using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Products;
using TBM.Core.Enums;

namespace TBM.Infrastructure.Data.Seeders;

/// <summary>
/// One-off, idempotent seeder for the 120-product BOGAT Seven Signature
/// Collections catalogue (bathroom vanities with 600–1400 mm size variants).
///
/// Safe to re-run after a partial execution: products are matched by SKU and
/// skipped when present; for existing products the four size variants are
/// reconciled individually (missing ones are created, existing ones untouched).
///
/// Not part of DbInitializer (which is Development-only) — triggered from
/// Program.cs via the Database:SeedBogatCatalog configuration flag.
/// </summary>
public static class BogatCatalogSeeder
{
    private static readonly string[] VariantSizes = { "600mm", "800mm", "1000mm", "1200mm" };

    private static readonly string[] NameSuffixes =
    {
        "Amber", "Aria", "Aura", "Belle", "Cedar", "Celine", "Dawn", "Eden",
        "Ember", "Grace", "Haven", "Iris", "Ivory", "Jade", "Luna", "Nova",
        "Pearl", "Sage", "Sienna", "Sol", "Vale", "Willow", "Zara", "Aster",
        "Blair", "Cove", "Elara", "Flora", "Mila", "Nola", "Opal"
    };

    private const string OrderingNote =
        "Made to order. Lead time: 8–12 weeks after approved drawings and material selection.";

    private sealed record CollectionSpec(
        string Prefix,
        string Name,
        string Tagline,
        int Count,
        decimal[] Prices,
        string Description,
        string SignatureDetails);

    private static readonly CollectionSpec[] Collections =
    {
        new(
            "BGT-EAT", "Eclat Atelier", "Complete Luxury Vanity Systems", 31,
            new[] { 850_000m, 1_100_000m, 1_400_000m, 1_780_000m },
            "A complete, room-defining vanity composition combining an integrated stone basin, " +
            "furniture-grade storage, illuminated mirror and coordinated architectural lighting. " +
            "Designed as a complete bathroom focal point, Eclat Atelier balances expressive natural " +
            "stone with warm timber, precision cabinetry and layered illumination. Each piece is " +
            "made to order and adapted to the selected wall width.",
            "Integrated stone basin; single-level divided cabinet; soft-close storage; " +
            "illuminated mirror; under-cabinet LED; concealed wall support"),
        new(
            "BGT-JOA", "Joaillerie Stone", "Stone & Cabinet Editions", 26,
            new[] { 750_000m, 950_000m, 1_200_000m, 1_450_000m },
            "Refined stone-and-cabinet vanities combining a substantial integrated basin with warm " +
            "timber, coloured cabinetry and soft architectural lighting. Joaillerie Stone is composed " +
            "like a piece of jewellery: a distinctive natural-stone basin above practical floating " +
            "storage, finished with calm proportions and premium detailing.",
            "Rare natural stone; optional internal illumination; integrated basin; sculptural " +
            "floating shelf; wall-mounted tap compatibility; individually selected slab"),
        new(
            "BGT-MON", "Monolithe Prive", "Illuminated Stone Vanities", 13,
            new[] { 700_000m, 900_000m, 1_150_000m, 1_500_000m },
            "Illuminated stone vanities that use warm backlighting, glowing stone and halo mirrors " +
            "to create a dramatic but welcoming focal point. Monolithe Prive celebrates the colour, " +
            "weight and natural movement of stone. Optional illumination reveals depth in translucent " +
            "materials while the floating form keeps the composition elegant.",
            "Thick stone profile; integrated basin; concealed drainage; floating cabinet; " +
            "soft-close fronts; shadow-gap construction"),
        new(
            "BGT-LEV", "Levitation Royale", "Light Floating Basins", 8,
            new[] { 650_000m, 820_000m, 1_050_000m, 1_300_000m },
            "A minimal floating stone washbasin with an open underside, precision wall fixing and an " +
            "exposed designer bottle trap as part of the composition. Levitation Royale removes " +
            "everything unnecessary. Stone, water, light and metal remain, producing an airy " +
            "architectural basin ideal for powder rooms, guest suites and design-led bathrooms.",
            "Cantilevered slab; integrated basin; open underside; exposed decorative trap; " +
            "recessed LED option; wall-mounted tap compatibility"),
        new(
            "BGT-SYM", "Symphonie Deux", "Statement Stone Collection", 7,
            new[] { 950_000m, 1_250_000m, 1_650_000m, 2_100_000m },
            "A confident collection of statement stone basins, deep profiles and furniture-like " +
            "floating bases developed as individual bathroom centrepieces. Symphonie Deux focuses on " +
            "balanced proportions, memorable material combinations and strong silhouettes. Each piece " +
            "is designed to carry the visual identity of the room without sacrificing useful storage.",
            "Twin basins; double tap positions; shared or divided storage; optional lower display " +
            "shelf; illuminated mirror; custom stone selection"),
        new(
            "BGT-MSC", "Maison Sculptee", "Vessel & Double Vanity Collection", 20,
            new[] { 680_000m, 880_000m, 1_150_000m, 1_450_000m },
            "Vessel and wider vanity compositions created for generous bathrooms, including selected " +
            "twin-basin and open-shelf arrangements. Maison Sculptee brings symmetry and ease to " +
            "larger vanity spaces. Vessel, integrated and twin-basin options are paired with " +
            "practical counter space and coordinated floating storage.",
            "Carved vessel bowl; floating stone counter; organic mirror option; open towel shelf; " +
            "wall or deck-mounted tap compatibility; concealed support"),
        new(
            "BGT-TER", "Terra Sculpte", "Organic & Artisan Stone Forms", 15,
            new[] { 720_000m, 950_000m, 1_250_000m, 1_650_000m },
            "An artisanal series of tactile basins with natural, chiseled or irregular edges, " +
            "grounded by warm timber and understated architectural detailing. Terra Sculpte preserves " +
            "the character of the quarry. Hand-finished surfaces and imperfect edges make every piece " +
            "visibly unique, suited to serene resort-style and nature-led interiors.",
            "Hand-finished edge; integrated stone basin; natural textural variation; timber cabinet " +
            "option; concealed mounting; warm ambient lighting")
    };

    public sealed class SeedReport
    {
        public int ProductsCreated { get; set; }
        public int ProductsSkipped { get; set; }
        public int VariantsCreated { get; set; }
        public int VariantsAddedToExisting { get; set; }
        public int CategoriesCreated { get; set; }
        public List<string> SkippedSkus { get; } = new();

        public string Summary() =>
            $"Bogat catalogue seed: {ProductsCreated} products created, {ProductsSkipped} skipped " +
            $"(already existed), {VariantsCreated} variants created on new products, " +
            $"{VariantsAddedToExisting} missing variants added to existing products, " +
            $"{CategoriesCreated} categories created." +
            (SkippedSkus.Count > 0 ? $" Skipped SKUs: {string.Join(", ", SkippedSkus)}" : string.Empty);
    }

    public static async Task<SeedReport> SeedAsync(ApplicationDbContext context)
    {
        var report = new SeedReport();

        var bathroom = await EnsureCategoryAsync(context, report,
            name: "Bathroom",
            slug: "bathroom",
            description: "Luxury bathroom collections by Bogat",
            parentId: null,
            displayOrder: 0);

        // IgnoreQueryFilters: a soft-deleted product still owns its unique slug,
        // so it must count as "exists" or re-inserting would violate the index.
        var existingBySku = await context.Products
            .IgnoreQueryFilters()
            .Include(p => p.Variants)
            .Where(p => p.SKU != null && p.SKU.StartsWith("BGT-"))
            .ToDictionaryAsync(p => p.SKU!, StringComparer.OrdinalIgnoreCase);

        var categoryOrder = 0;
        foreach (var spec in Collections)
        {
            categoryOrder++;
            var category = await EnsureCategoryAsync(context, report,
                name: spec.Name,
                slug: Slugify(spec.Name),
                description: $"{spec.Tagline} — {spec.Name} collection by Bogat",
                parentId: bathroom.Id,
                displayOrder: categoryOrder);

            for (var i = 1; i <= spec.Count; i++)
            {
                var sku = $"{spec.Prefix}-{i:D3}";
                var name = $"{spec.Name} {NameSuffixes[i - 1]}";

                if (existingBySku.TryGetValue(sku, out var existing))
                {
                    report.ProductsSkipped++;
                    report.SkippedSkus.Add(sku);
                    report.VariantsAddedToExisting += EnsureVariants(existing, spec.Prices);
                    continue;
                }

                var product = new Product
                {
                    Name = name,
                    Description = $"{spec.Description}\n\nSignature details: {spec.SignatureDetails}\n\n{OrderingNote}",
                    ShortDescription = spec.Tagline,
                    Slug = Slugify(name),
                    SKU = sku,
                    BrandType = BrandType.Bogat,
                    ProductType = ProductType.PhysicalProduct,
                    CategoryId = category.Id,
                    Price = spec.Prices[0],
                    CompareAtPrice = spec.Prices[3],
                    ShowPrice = true,
                    StockQuantity = 20,
                    LowStockThreshold = 5,
                    TrackInventory = true,
                    IsActive = true,
                    IsFeatured = false,
                    DisplayOrder = i,
                    Size = VariantSizes[0],
                    QualityTier = "Luxury",
                    MetaTitle = $"{name} | Bogat by TBM",
                    MetaDescription = spec.Tagline,
                    Tags = $"bathroom,vanity,luxury,{Slugify(spec.Name)}",
                    AIKeywords = $"bathroom vanity stone basin luxury {spec.Name.ToLowerInvariant()}",
                    KeyFeatures = JsonSerializer.Serialize(
                        spec.SignatureDetails.Split("; ", StringSplitOptions.RemoveEmptyEntries))
                };

                EnsureVariants(product, spec.Prices);
                report.VariantsCreated += product.Variants.Count;
                report.ProductsCreated++;

                await context.Products.AddAsync(product);
            }
        }

        await context.SaveChangesAsync();
        return report;
    }

    /// <summary>
    /// Adds any of the four size variants the product is missing.
    /// Returns the number of variants added; existing variants are untouched.
    /// </summary>
    private static int EnsureVariants(Product product, decimal[] prices)
    {
        var added = 0;
        for (var order = 0; order < VariantSizes.Length; order++)
        {
            var size = VariantSizes[order];
            if (product.Variants.Any(v => string.Equals(v.Size, size, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            product.Variants.Add(new ProductVariant
            {
                Size = size,
                Price = prices[order],
                StockQuantity = 20,
                IsActive = true,
                DisplayOrder = order
            });
            added++;
        }

        return added;
    }

    private static async Task<Category> EnsureCategoryAsync(
        ApplicationDbContext context,
        SeedReport report,
        string name,
        string slug,
        string description,
        Guid? parentId,
        int displayOrder)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
        if (category != null)
        {
            return category;
        }

        category = new Category
        {
            Name = name,
            Description = description,
            Slug = slug,
            BrandType = BrandType.Bogat,
            ParentCategoryId = parentId,
            DisplayOrder = displayOrder,
            IsActive = true
        };

        await context.Categories.AddAsync(category);
        await context.SaveChangesAsync();
        report.CategoriesCreated++;
        return category;
    }

    private static string Slugify(string value) =>
        value.Trim().ToLowerInvariant().Replace(" ", "-");
}
