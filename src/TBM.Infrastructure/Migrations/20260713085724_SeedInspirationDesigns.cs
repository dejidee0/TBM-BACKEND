using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedInspirationDesigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Static reference data — seeded here (not DbInitializer) so it lands in
            // every environment, including Production, where DbInitializer.SeedAsync
            // never runs (it's gated behind IsDevelopment() in Program.cs).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM InspirationDesigns)
BEGIN
    INSERT INTO InspirationDesigns
        (Id, Title, Style, ImageUrl, Description, Category, IsActive, DisplayOrder, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, DeletedAt, DeletedBy, IsDeleted)
    VALUES
        (NEWID(), N'Lagos Penthouse', N'afro-minimalism', N'https://images.unsplash.com/photo-1600210492486-724fe5c67fb0?w=800', N'Aso-Oke accents with marble floors', N'Living Room', 1, 1, SYSUTCDATETIME(), NULL, NULL, NULL, NULL, NULL, 0),
        (NEWID(), N'Modern Kitchen', N'modern', N'https://images.unsplash.com/photo-1556909114-f6e7ad7d3136?w=800', N'Built-in marble island with warm lighting', N'Kitchen', 1, 2, SYSUTCDATETIME(), NULL, NULL, NULL, NULL, NULL, 0),
        (NEWID(), N'Zen Bedroom', N'minimalist', N'https://images.unsplash.com/photo-1631049307264-da0ec9d70304?w=800', N'Microcement floors, floor-to-ceiling windows', N'Bedroom', 1, 3, SYSUTCDATETIME(), NULL, NULL, NULL, NULL, NULL, 0),
        (NEWID(), N'Tropical Retreat', N'tropical', N'https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?w=800', N'Teak beams with lush indoor palms', N'Living Room', 1, 4, SYSUTCDATETIME(), NULL, NULL, NULL, NULL, NULL, 0),
        (NEWID(), N'Artisan Bathroom', N'wabi-sabi', N'https://images.unsplash.com/photo-1552321554-5fefe8c9ef14?w=800', N'Mineral plaster walls with stone basin', N'Bathroom', 1, 5, SYSUTCDATETIME(), NULL, NULL, NULL, NULL, NULL, 0),
        (NEWID(), N'Industrial Loft', N'industrial', N'https://images.unsplash.com/photo-1600585154340-be6161a56a0c?w=800', N'Exposed concrete with Edison lighting', N'Living Room', 1, 6, SYSUTCDATETIME(), NULL, NULL, NULL, NULL, NULL, 0);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM InspirationDesigns
WHERE Title IN (N'Lagos Penthouse', N'Modern Kitchen', N'Zen Bedroom', N'Tropical Retreat', N'Artisan Bathroom', N'Industrial Loft');
");
        }
    }
}
