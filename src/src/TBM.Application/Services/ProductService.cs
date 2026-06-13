using CsvHelper;
using CsvHelper.Configuration;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Products;
using TBM.Application.Helpers;
using TBM.Application.Interfaces;
using TBM.Core.Entities.Products;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using System.Globalization;

namespace TBM.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public ProductService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    #region Category Operations
    
    public async Task<ApiResponse<CategoryDto>> GetCategoryByIdAsync(Guid id)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        
        if (category == null)
        {
            return ApiResponse<CategoryDto>.ErrorResponse(\"Category not found\");
        }
        
        return ApiResponse<CategoryDto>.SuccessResponse(MapCategoryToDto(category));
    }
    
    public async Task<ApiResponse<CategoryDto>> GetCategoryBySlugAsync(string slug)
    {
        var category = await _unitOfWork.Categories.GetBySlugAsync(slug);
        
        if (category == null)
        {
            return ApiResponse<CategoryDto>.ErrorResponse(\"Category not found\");
        }
        
        return ApiResponse<CategoryDto>.SuccessResponse(MapCategoryToDto(category));
    }
    
    public async Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync()
    {
        var categories = await _unitOfWork.Categories.GetAllAsync();
        var categoryDtos = categories.Select(MapCategoryToDto).ToList();
        
        return ApiResponse<List<CategoryDto>>.SuccessResponse(categoryDtos);
    }
    
    public async Task<ApiResponse<List<CategoryDto>>> GetCategoriesByBrandAsync(int brandType)
    {
        if (!Enum.IsDefined(typeof(BrandType), brandType))
        {
            return ApiResponse<List<CategoryDto>>.ErrorResponse(\"Invalid brand type\");
        }
        
        var categories = await _unitOfWork.Categories.GetByBrandAsync((BrandType)brandType);
        var categoryDtos = categories.Select(MapCategoryToDto).ToList();
        
        return ApiResponse<List<CategoryDto>>.SuccessResponse(categoryDtos);
    }
    
    public async Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto)
    {
        // Validate brand type
        if (!Enum.IsDefined(typeof(BrandType), dto.BrandType))
        {
            return ApiResponse<CategoryDto>.ErrorResponse(\"Invalid brand type\");
        }
        
        // Generate slug
        var slug = SlugHelper.GenerateSlug(dto.Name);
        
        // Check if slug exists
        if (await _unitOfWork.Categories.SlugExistsAsync(slug))
        {
            slug = $\"{slug}-{Guid.NewGuid().ToString(\"N\")[..8]}\";
        }
        
        var category = new Category
        {
            Name = dto.Name,
            Description = dto.Description,
            Slug = slug,
            BrandType = (BrandType)dto.BrandType,
            ParentCategoryId = dto.ParentCategoryId,
            ImageUrl = dto.ImageUrl,
            DisplayOrder = dto.DisplayOrder,
            IsActive = true
        };
        
        await _unitOfWork.Categories.CreateAsync(category);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<CategoryDto>.SuccessResponse(
            MapCategoryToDto(category),
            \"Category created successfully\"
        );
    }
    
    public async Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        
        if (category == null)
        {
            return ApiResponse<CategoryDto>.ErrorResponse(\"Category not found\");
        }
        
        // Update slug if name changed
        var slug = SlugHelper.GenerateSlug(dto.Name);
        if (slug != category.Slug && await _unitOfWork.Categories.SlugExistsAsync(slug, id))
        {
            slug = $\"{slug}-{Guid.NewGuid().ToString(\"N\")[..8]}\";
        }
        
        category.Name = dto.Name;
        category.Description = dto.Description;
        category.Slug = slug;
        category.ParentCategoryId = dto.ParentCategoryId;
        category.ImageUrl = dto.ImageUrl;
        category.DisplayOrder = dto.DisplayOrder;
        category.IsActive = dto.IsActive;
        
        await _unitOfWork.Categories.UpdateAsync(category);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<CategoryDto>.SuccessResponse(
            MapCategoryToDto(category),
            \"Category updated successfully\"
        );
    }
    
    public async Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        
        if (category == null)
        {
            return ApiResponse<bool>.ErrorResponse(\"Category not found\");
        }
        
        // Check if category has products
        if (category.Products.Any())
        {
            return ApiResponse<bool>.ErrorResponse(\"Cannot delete category with products\");
        }
        
        await _unitOfWork.Categories.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, \"Category deleted successfully\");
    }
    
    #endregion
    
    #region Product Operations
    
    public async Task<ApiResponse<ProductDto>> GetProductByIdAsync(Guid id)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        
        if (product == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse(\"Product not found\");
        }
        
        return ApiResponse<ProductDto>.SuccessResponse(MapProductToDto(product));
    }
    
    public async Task<ApiResponse<ProductDto>> GetProductBySlugAsync(string slug)
    {
        var product = await _unitOfWork.Products.GetBySlugAsync(slug);
        
        if (product == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse(\"Product not found\");
        }
        
        return ApiResponse<ProductDto>.SuccessResponse(MapProductToDto(product));
    }
    
    public async Task<ApiResponse<PagedResultDto<ProductDto>>> GetProductsAsync(ProductFilterDto filter)
    {
        var (items, totalCount) = await _unitOfWork.Products.GetPagedAsync(
            filter.PageNumber,
            filter.PageSize,
            filter.BrandType.HasValue ? (BrandType)filter.BrandType.Value : null,
            filter.ProductType.HasValue ? (ProductType)filter.ProductType.Value : null,
            filter.CategoryId,
            filter.SearchTerm,
            filter.IsFeatured,
            filter.ActiveOnly
        );
        
        var result = new PagedResultDto<ProductDto>
        {
            Items = items.Select(MapProductToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        };
        
        return ApiResponse<PagedResultDto<ProductDto>>.SuccessResponse(result);
    }
    
    public async Task<ApiResponse<List<ProductDto>>> GetFeaturedProductsAsync(int? brandType = null, int limit = 10)
    {
        BrandType? brand = brandType.HasValue ? (BrandType)brandType.Value : null;
        var products = await _unitOfWork.Products.GetFeaturedAsync(brand, limit);
        var productDtos = products.Select(MapProductToDto).ToList();
        
        return ApiResponse<List<ProductDto>>.SuccessResponse(productDtos);
    }
    
    public async Task<ApiResponse<List<ProductDto>>> GetRelatedProductsAsync(Guid productId, int limit = 4)
    {
        var products = await _unitOfWork.Products.GetRelatedAsync(productId, limit);
        var productDtos = products.Select(MapProductToDto).ToList();
        
        return ApiResponse<List<ProductDto>>.SuccessResponse(productDtos);
    }
    
    public async Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto)
    {
        // Validate enums
        if (!Enum.IsDefined(typeof(BrandType), dto.BrandType))
        {
            return ApiResponse<ProductDto>.ErrorResponse(\"Invalid brand type\");
        }
        
        if (!Enum.IsDefined(typeof(ProductType), dto.ProductType))
        {
            return ApiResponse<ProductDto>.ErrorResponse(\"Invalid product type\");
        }
        
        // Validate category exists
        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
        if (category == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse(\"Category not found\");
        }
        
        // Generate slug
        var slug = SlugHelper.GenerateSlug(dto.Name);
        if (await _unitOfWork.Products.SlugExistsAsync(slug))
        {
            slug = $\"{slug}-{Guid.NewGuid().ToString(\"N\")[..8]}\";
        }
        
        // Check SKU uniqueness
        if (!string.IsNullOrWhiteSpace(dto.SKU) && await _unitOfWork.Products.SKUExistsAsync(dto.SKU))
        {
            return ApiResponse<ProductDto>.ErrorResponse(\"SKU already exists\");
        }
        
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            ShortDescription = dto.ShortDescription,
            Slug = slug,
            SKU = dto.SKU,
            BrandType = (BrandType)dto.BrandType,
            ProductType = (ProductType)dto.ProductType,
            CategoryId = dto.CategoryId,
            Price = dto.Price,
            CompareAtPrice = dto.CompareAtPrice,
            ShowPrice = dto.ShowPrice,
            StockQuantity = dto.StockQuantity,
            LowStockThreshold = dto.LowStockThreshold,
            TrackInventory = dto.TrackInventory,
            IsActive = true,
            IsFeatured = dto.IsFeatured,
            DisplayOrder = dto.DisplayOrder,
            MetaTitle = dto.MetaTitle,
            MetaDescription = dto.MetaDescription,
            Tags = dto.Tags
        };
        
        await _unitOfWork.Products.CreateAsync(product);
        await _unitOfWork.SaveChangesAsync();
        
        // Reload with navigation properties
        product = await _unitOfWork.Products.GetByIdAsync(product.Id);
        
        return ApiResponse<ProductDto>.SuccessResponse(
            MapProductToDto(product!),
            \"Product created successfully\"
        );
    }
    
    public async Task<ApiResponse<ProductDto>> UpdateProductAsync(Guid id, UpdateProductDto dto)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        
        if (product == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse(\"Product not found\");
        }
        
        // Validate category exists
        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
        if (category == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse(\"Category not found\");
        }
        
        // Update slug if name changed
        var slug = SlugHelper.GenerateSlug(dto.Name);
        if (slug != product.Slug && await _unitOfWork.Products.SlugExistsAsync(slug, id))
        {
            slug = $\"{slug}-{Guid.NewGuid().ToString(\"N\")[..8]}\";
        }
        
        // Check SKU uniqueness
        if (!string.IsNullOrWhiteSpace(dto.SKU) && dto.SKU != product.SKU)
        {
            if (await _unitOfWork.Products.SKUExistsAsync(dto.SKU, id))
            {
                return ApiResponse<ProductDto>.ErrorResponse(\"SKU already exists\");
            }
        }
        
        product.Name = dto.Name;
        product.Description = dto.Description;
        product.ShortDescription = dto.ShortDescription;
        product.Slug = slug;
        product.SKU = dto.SKU;
        product.CategoryId = dto.CategoryId;
        product.Price = dto.Price;
        product.CompareAtPrice = dto.CompareAtPrice;
        product.ShowPrice = dto.ShowPrice;
        product.StockQuantity = dto.StockQuantity;
        product.LowStockThreshold = dto.LowStockThreshold;
        product.TrackInventory = dto.TrackInventory;
        product.IsActive = dto.IsActive;
        product.IsFeatured = dto.IsFeatured;
        product.DisplayOrder = dto.DisplayOrder;
        product.MetaTitle = dto.MetaTitle;
        product.MetaDescription = dto.MetaDescription;
        product.Tags = dto.Tags;
        
        await _unitOfWork.Products.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();
        
        // Reload with navigation properties
        product = await _unitOfWork.Products.GetByIdAsync(id);
        
        return ApiResponse<ProductDto>.SuccessResponse(
            MapProductToDto(product!),
            \"Product updated successfully\"
        );
    }
    
    public async Task<ApiResponse<bool>> DeleteProductAsync(Guid id)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        
        if (product == null)
        {
            return ApiResponse<bool>.ErrorResponse(\"Product not found\");
        }
        
        await _unitOfWork.Products.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, \"Product deleted successfully\");
    }
    
    #endregion
    
    #region Product Image Operations
    
    public async Task<ApiResponse<ProductImageDto>> AddProductImageAsync(Guid productId, AddProductImageDto dto)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        
        if (product == null)
        {
            return ApiResponse<ProductImageDto>.ErrorResponse(\"Product not found\");
        }
        
        var image = new ProductImage
        {
            ProductId = productId,
            ImageUrl = dto.ImageUrl,
            AltText = dto.AltText,
            DisplayOrder = dto.DisplayOrder,
            IsPrimary = dto.IsPrimary
        };
        
        await _unitOfWork.ProductImages.CreateAsync(image);
        
        // If this is set as primary, update other images
        if (dto.IsPrimary)
        {
            await _unitOfWork.ProductImages.SetPrimaryImageAsync(productId, image.Id);
        }
        
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<ProductImageDto>.SuccessResponse(
            MapProductImageToDto(image),
            \"Image added successfully\"
        );
    }
    
    public async Task<ApiResponse<bool>> DeleteProductImageAsync(Guid imageId)
    {
        var image = await _unitOfWork.ProductImages.GetByIdAsync(imageId);
        
        if (image == null)
        {
            return ApiResponse<bool>.ErrorResponse(\"Image not found\");
        }
        
        await _unitOfWork.ProductImages.DeleteAsync(imageId);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, \"Image deleted successfully\");
    }
    
    public async Task<ApiResponse<bool>> SetPrimaryImageAsync(Guid productId, Guid imageId)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        
        if (product == null)
        {
            return ApiResponse<bool>.ErrorResponse(\"Product not found\");
        }
        
        await _unitOfWork.ProductImages.SetPrimaryImageAsync(productId, imageId);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, \"Primary image set successfully\");
    }
    
    #endregion
    
    #region Bulk Import Operations
    
    public async Task<ApiResponse<ImportResultDto>> ImportProductsAsync(Stream fileStream, string fileName)
    {
        var result = new ImportResultDto();

        // Validate file type
        if (!fileName.EndsWith(\".csv\", StringComparison.OrdinalIgnoreCase))
        {
            result.Success = false;
            result.Results.Add(new ImportResultRowDto
            {
                RowNumber = 0,
                Errors = new List<string> { \"Invalid file format. Only CSV files are allowed.\" }
            });
            return ApiResponse<ImportResultDto>.ErrorResponse(\"Invalid file format\", result.Results);
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant().Replace(\" \", \"\").Replace(\"_\", \"\").Replace(\"-\", \"\")
        };

        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, config);

        var records = csv.GetRecords<ImportProductDto>().ToList();
        result.TotalRows = records.Count;

        if (!records.Any())
        {
            result.Success = false;
            result.Results.Add(new ImportResultRowDto
            {
                RowNumber = 0,
                Errors = new List<string> { \"CSV file is empty or contains no data rows.\" }
            });
            return ApiResponse<ImportResultDto>.ErrorResponse(\"Empty file\", result.Results);
        }

        if (records.Count > 500)
        {
            result.Success = false;
            result.Results.Add(new ImportResultRowDto
            {
                RowNumber = 0,
                Errors = new List<string> { \"File contains more than 500 rows. Please split into multiple files.\" }
            });
            return ApiResponse<ImportResultDto>.ErrorResponse(\"File too large\", result.Results);
        }

        var categories = await _unitOfWork.Categories.GetAllAsync();
        var categoryNameMap = categories.ToDictionary(c => c.Name.Trim().ToLowerInvariant(), c => c);
        var categorySlugMap = categories.ToDictionary(c => c.Slug.Trim().ToLowerInvariant(), c => c);

        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var rowNumber = i + 2; // +2 because header is row 1 and index is 0-based
            var errors = new List<string>();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(record.Name))
            {
                errors.Add(\"Product name is required\");
            }

            // Validate BrandType
            BrandType brandType;
            if (!string.IsNullOrWhiteSpace(record.BrandType))
            {
                if (!Enum.TryParse(typeof(BrandType), record.BrandType.Trim(), true, out var parsedBrand))
                {
                    errors.Add($\"Invalid BrandType '{record.BrandType}'. Valid values: TBM, Bogat\");
                }
                else
                {
                    brandType = (BrandType)parsedBrand;
                }
            }
            else
            {
                errors.Add(\"BrandType is required\");
                brandType = default;
            }

            // Validate ProductType
            ProductType productType;
            if (!string.IsNullOrWhiteSpace(record.ProductType))
            {
                if (!Enum.TryParse(typeof(ProductType), record.ProductType.Trim(), true, out var parsedType))
                {
                    errors.Add($\"Invalid ProductType '{record.ProductType}'. Valid values: PhysicalProduct, Service\");
                }
                else
                {
                    productType = (ProductType)parsedType;
                }
            }
            else
            {
                errors.Add(\"ProductType is required\");
                productType = default;
            }

            // Validate and find category
            Category? category = null;
            if (!string.IsNullOrWhiteSpace(record.CategoryName))
            {
                var categoryKey = record.CategoryName.Trim().ToLowerInvariant();
                if (categoryNameMap.TryGetValue(categoryKey, out var foundCategory) ||
                    categorySlugMap.TryGetValue(categoryKey, out foundCategory))
                {
                    category = foundCategory;
                }
                else
                {
                    errors.Add($\"Category '{record.CategoryName}' not found\");
                }
            }
            else
            {
                errors.Add(\"CategoryName is required\");
            }

            // Skip row if there are validation errors
            if (errors.Count > 0)
            {
                result.Failed++;
                result.Results.Add(new ImportResultRowDto
                {
                    RowNumber = rowNumber,
                    Name = record.Name,
                    Success = false,
                    Errors = errors
                });
                continue;
            }

            // Check for duplicate SKU
            if (!string.IsNullOrWhiteSpace(record.SKU))
            {
                var skuExists = await _unitOfWork.Products.SKUExistsAsync(record.SKU.Trim());
                if (skuExists)
                {
                    errors.Add($\"SKU '{record.SKU}' already exists\");
                    result.Failed++;
                    result.Results.Add(new ImportResultRowDto
                    {
                        RowNumber = rowNumber,
                        Name = record.Name,
                        Success = false,
                        Errors = errors
                    });
                    continue;
                }
            }

            // Generate slug
            var slug = SlugHelper.GenerateSlug(record.Name);
            if (await _unitOfWork.Products.SlugExistsAsync(slug))
            {
                slug = $\"{slug}-{Guid.NewGuid().ToString(\"N\")[..8]}\";
            }

            // Create product
            var product = new Product
            {
                Name = record.Name.Trim(),
                Description = record.Description?.Trim() ?? string.Empty,
                ShortDescription = record.ShortDescription?.Trim() ?? string.Empty,
                Slug = slug,
                SKU = string.IsNullOrWhiteSpace(record.SKU) ? null : record.SKU.Trim(),
                BrandType = brandType,
                ProductType = productType,
                CategoryId = category!.Id,
                Price = record.Price,
                CompareAtPrice = record.CompareAtPrice,
                ShowPrice = record.ShowPrice,
                StockQuantity = record.StockQuantity,
                LowStockThreshold = record.LowStockThreshold,
                TrackInventory = record.TrackInventory,
                IsActive = record.IsActive,
                IsFeatured = record.IsFeatured,
                DisplayOrder = record.DisplayOrder,
                MetaTitle = record.MetaTitle?.Trim(),
                MetaDescription = record.MetaDescription?.Trim(),
                Tags = record.Tags?.Trim()
            };

            await _unitOfWork.Products.CreateAsync(product);
        }

        await _unitOfWork.SaveChangesAsync();

        // After save, count successful imports
        // All that passed validation were saved
        result.SuccessfullyImported = result.TotalRows - result.Failed;
        result.Success = result.Failed == 0;

        if (result.Failed > 0)
        {
            return ApiResponse<ImportResultDto>.ErrorResponse(
                $\"Import completed with {result.Failed} error(s)\", result.Results);
        }

        return ApiResponse<ImportResultDto>.SuccessResponse(
            result,
            $\"Successfully imported {result.SuccessfullyImported} products\"
        );
    }

    #endregion
    
    #region Helper Methods
    
    private CategoryDto MapCategoryToDto(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Slug = category.Slug,
            BrandType = (int)category.BrandType,
            BrandName = category.BrandType.ToString(),
            ParentCategoryId = category.ParentCategoryId,
            ParentCategoryName = category.ParentCategory?.Name,
            ImageUrl = category.ImageUrl,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            SubCategories = category.SubCategories.Select(MapCategoryToDto).ToList(),
            ProductCount = category.Products.Count
        };
    }
    
    private ProductDto MapProductToDto(Product product)
    {
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary) ?? product.Images.FirstOrDefault();
        
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ShortDescription = product.ShortDescription,
            Slug = product.Slug,
            SKU = product.SKU,
            BrandType = (int)product.BrandType,
            BrandName = product.BrandType.ToString(),
            ProductType = (int)product.ProductType,
            ProductTypeName = product.ProductType.ToString(),
            CategoryId = product.CategoryId,
            CategoryName = product.Category.Name,
            Price = product.Price,
            CompareAtPrice = product.CompareAtPrice,
            ShowPrice = product.ShowPrice,
            PriceDisplay = product.ShowPrice && product.Price.HasValue 
                ? $\"?{product.Price.Value:N2}\" 
                : \"Request Price\",
            StockQuantity = product.StockQuantity,
            InStock = !product.TrackInventory || (product.StockQuantity ?? 0) > 0,
            TrackInventory = product.TrackInventory,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            Tags = product.Tags,
            Images = product.Images.Select(MapProductImageToDto).ToList(),
            PrimaryImageUrl = primaryImage?.ImageUrl,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt ?? product.CreatedAt
        };
    }
    private ProductImageDto MapProductImageToDto(ProductImage image)
    {
        return new ProductImageDto
        {
            Id = image.Id,
            ProductId = image.ProductId,
            ImageUrl = image.ImageUrl,
            AltText = image.AltText,
            DisplayOrder = image.DisplayOrder,
            IsPrimary = image.IsPrimary
        };
    }
    
    #endregion
}
