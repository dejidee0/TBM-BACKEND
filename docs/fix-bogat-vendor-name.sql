-- ============================================================
-- RESTORE: Re-seed BOGAT portfolio projects (all 6)
-- Run against: db_ac81d5_tbmbuilding1
-- ============================================================

DECLARE @VendorId   UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @VendorName NVARCHAR(200)    = 'BOGAT';
DECLARE @Now        DATETIME2        = GETUTCDATE();

-- PROJECT 1: Luxury Guest Toilet Upgrade
DECLARE @P1 UNIQUEIDENTIFIER = NEWID();
INSERT INTO VendorPortfolioProjects
    (Id, VendorId, VendorName, Title, Location, Category,
     BudgetMin, BudgetMax, DurationMinDays, DurationMaxDays,
     Description, ScopeOfWork, Status, PublishedAt, IsDeleted, CreatedAt)
VALUES (
    @P1, @VendorId, @VendorName,
    'Luxury Guest Toilet Upgrade', 'Lagos', 'Guest Toilet Renovation',
    3500000, 6000000, 6, 9,
    'This project involved transforming a small, outdated guest toilet into a clean, modern, and visually appealing space. The focus was on maximizing limited space while achieving a premium, minimalist finish.',
    'Demolition of existing finishes
Wall treatment and repainting
Installation of modern WC
Compact floating sink installation
Mirror and accessory styling
Lighting upgrade',
    1, @Now, 0, @Now
);
INSERT INTO PortfolioProjectImages (Id, ProjectId, ImageUrl, ImageType, Caption, SortOrder, CreatedAt) VALUES
    (NEWID(), @P1, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776599275/IMG_4846_unnpbv.jpg', 0, 'Before: Outdated guest toilet', 0, @Now),
    (NEWID(), @P1, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776599275/IMG_4847_xxcbf4.jpg', 1, 'After: Clean modern guest toilet', 0, @Now);

-- PROJECT 2: Luxury Modern Residence
DECLARE @P2 UNIQUEIDENTIFIER = NEWID();
INSERT INTO VendorPortfolioProjects
    (Id, VendorId, VendorName, Title, Location, Category,
     BudgetMin, BudgetMax, DurationMinDays, DurationMaxDays,
     Description, ScopeOfWork, Status, PublishedAt, IsDeleted, CreatedAt)
VALUES (
    @P2, @VendorId, @VendorName,
    'Luxury Modern Residence — From Structure to Premium Finish', 'Abuja', 'Construction (Shell to Finish)',
    NULL, NULL, NULL, NULL,
    'This project involved the complete construction and finishing of a modern residential property, transforming a raw structural frame into a fully finished luxury home. The focus was on architectural precision, premium materials, and a clean, contemporary aesthetic.',
    'Structural construction
Blockwork & plastering
Exterior finishing
Architectural detailing
Glass & facade installation
Painting & final finishing',
    1, @Now, 0, @Now
);
INSERT INTO PortfolioProjectImages (Id, ProjectId, ImageUrl, ImageType, Caption, SortOrder, CreatedAt) VALUES
    (NEWID(), @P2, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776599698/IMG_4888_supjna.jpg', 0, 'Before: Raw structural frame', 0, @Now),
    (NEWID(), @P2, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776599697/IMG_4889_lszzxz.jpg', 1, 'After: Fully finished luxury residence', 0, @Now);

-- PROJECT 3: Luxury Outdoor Lounge Transformation
DECLARE @P3 UNIQUEIDENTIFIER = NEWID();
INSERT INTO VendorPortfolioProjects
    (Id, VendorId, VendorName, Title, Location, Category,
     BudgetMin, BudgetMax, DurationMinDays, DurationMaxDays,
     Description, ScopeOfWork, Status, PublishedAt, IsDeleted, CreatedAt)
VALUES (
    @P3, @VendorId, @VendorName,
    'Luxury Outdoor Lounge Transformation (Courtyard Upgrade)', 'Abuja', 'Outdoor / Exterior Design',
    NULL, NULL, NULL, NULL,
    'This project transformed an unused outdoor space into a warm, inviting, and functional lounge area. The design focused on combining greenery, lighting, and natural materials to create a relaxing and visually appealing environment.',
    'Surface cleaning and preparation
Wall finishing and cladding
Installation of vertical greenery
Lighting design and installation
Furniture and decor styling
Space planning',
    1, @Now, 0, @Now
);
INSERT INTO PortfolioProjectImages (Id, ProjectId, ImageUrl, ImageType, Caption, SortOrder, CreatedAt) VALUES
    (NEWID(), @P3, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776600043/IMG_4871_ohfsa3.jpg', 0, 'Before: Unused outdoor space', 0, @Now),
    (NEWID(), @P3, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776600044/IMG_4870_k0jcpa.jpg', 1, 'After: Inviting outdoor lounge', 0, @Now);

-- PROJECT 4: Modern Luxury Bathroom Transformation
DECLARE @P4 UNIQUEIDENTIFIER = NEWID();
INSERT INTO VendorPortfolioProjects
    (Id, VendorId, VendorName, Title, Location, Category,
     BudgetMin, BudgetMax, DurationMinDays, DurationMaxDays,
     Description, ScopeOfWork, Status, PublishedAt, IsDeleted, CreatedAt)
VALUES (
    @P4, @VendorId, @VendorName,
    'Modern Luxury Bathroom Transformation (Full Upgrade)', 'Lagos', 'Bathroom Renovation',
    NULL, NULL, NULL, NULL,
    'This project involved the complete transformation of an outdated bathroom into a modern, luxurious space. The design focused on functionality, premium finishes, and a clean contemporary aesthetic.',
    'Full demolition of existing finishes
Plumbing system upgrade
Wall and floor tiling
Installation of modern WC
Vanity and cabinet installation
Shower system installation
Lighting and electrical works',
    1, @Now, 0, @Now
);
INSERT INTO PortfolioProjectImages (Id, ProjectId, ImageUrl, ImageType, Caption, SortOrder, CreatedAt) VALUES
    (NEWID(), @P4, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776600275/IMG_4858_bx3x7b.jpg', 0, 'Before: Outdated bathroom', 0, @Now),
    (NEWID(), @P4, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776600282/IMG_4857_hj4grw.jpg', 1, 'After: Premium luxury bathroom', 0, @Now);

-- PROJECT 5: Interior Layout & Corridor Transformation
DECLARE @P5 UNIQUEIDENTIFIER = NEWID();
INSERT INTO VendorPortfolioProjects
    (Id, VendorId, VendorName, Title, Location, Category,
     BudgetMin, BudgetMax, DurationMinDays, DurationMaxDays,
     Description, ScopeOfWork, Status, PublishedAt, IsDeleted, CreatedAt)
VALUES (
    @P5, @VendorId, @VendorName,
    'Interior Layout & Corridor Transformation', 'Lagos', 'Interior Finishing',
    NULL, NULL, NULL, NULL,
    'This project transformed a confined corridor into a bright, modern transition space through improved layout, refined finishes, and enhanced lighting. The result delivers a clean and premium interior experience.',
    'Demolition / surface prep
Floor replacement (large format tiles)
Wall finishing + painting
Door/frame refinishing or replacement
Lighting improvement',
    1, @Now, 0, @Now
);
INSERT INTO PortfolioProjectImages (Id, ProjectId, ImageUrl, ImageType, Caption, SortOrder, CreatedAt) VALUES
    (NEWID(), @P5, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776601168/IMG_4854_nkknit.jpg', 0, 'Before: Confined corridor', 0, @Now),
    (NEWID(), @P5, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776601169/IMG_4853_mwgqjc.jpg', 1, 'After: Bright modern corridor', 0, @Now);

-- PROJECT 6: Modern Kitchen Transformation
DECLARE @P6 UNIQUEIDENTIFIER = NEWID();
INSERT INTO VendorPortfolioProjects
    (Id, VendorId, VendorName, Title, Location, Category,
     BudgetMin, BudgetMax, DurationMinDays, DurationMaxDays,
     Description, ScopeOfWork, Status, PublishedAt, IsDeleted, CreatedAt)
VALUES (
    @P6, @VendorId, @VendorName,
    'Modern Kitchen Transformation', 'Lagos', 'Interior Renovation',
    NULL, NULL, NULL, NULL,
    'This project involved a complete transformation of an outdated kitchen into a modern, functional, and visually refined space. The design integrates custom cabinetry, built-in appliances, and premium finishes to enhance both usability and aesthetics.',
    'Demolition + surface prep
Electrical + plumbing adjustments
Custom cabinet fabrication
Countertop + backsplash installation
Appliance integration
Finishing + painting',
    1, @Now, 0, @Now
);
INSERT INTO PortfolioProjectImages (Id, ProjectId, ImageUrl, ImageType, Caption, SortOrder, CreatedAt) VALUES
    (NEWID(), @P6, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776601293/IMG_4851_ltkjqt.jpg', 0, 'Before: Outdated kitchen', 0, @Now),
    (NEWID(), @P6, 'https://res.cloudinary.com/dympqafol/image/upload/q_auto/f_auto/v1776601292/IMG_4852_l9pexo.jpg', 1, 'After: Modern luxury kitchen', 0, @Now);

-- Verify — should show 6 BOGAT projects, 2 images each
SELECT p.VendorName, p.Title, p.Location, p.Status, COUNT(i.Id) AS ImageCount
FROM VendorPortfolioProjects p
LEFT JOIN PortfolioProjectImages i ON i.ProjectId = p.Id
WHERE p.IsDeleted = 0
GROUP BY p.VendorName, p.Title, p.Location, p.Status
ORDER BY p.Title;
