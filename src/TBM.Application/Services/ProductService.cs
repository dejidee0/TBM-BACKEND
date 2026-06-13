using TBM.Application.DTOs.AI;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text.Json;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Products;
using TBM.Application.Helpers;
using TBM.Application.Interfaces;
using TBM.Core.Entities.Products;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

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
            return ApiResponse<CategoryDto>.ErrorResponse("Category not found");
        }
        
        return ApiResponse<CategoryDto>.SuccessResponse(MapCategoryToDto(category));
    }
    
    public async Task<ApiResponse<CategoryDto>> GetCategoryBySlugAsync(string slug)
    {
        var category = await _unitOfWork.Categories.GetBySlugAsync(slug);
        
        if (category == null)
        {
            return ApiResponse<CategoryDto>.ErrorResponse("Category not found");
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
            return ApiResponse<List<CategoryDto>>.ErrorResponse("Invalid brand type");
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
            return ApiResponse<CategoryDto>.ErrorResponse("Invalid brand type");
        }
        
        // Generate slug
        var slug = SlugHelper.GenerateSlug(dto.Name);
        
        // Check if slug exists
        if (await _unitOfWork.Categories.SlugExistsAsync(slug))
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
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
            "Category created successfully"
        );
    }
    
    public async Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        
        if (category == null)
        {
            return ApiResponse<CategoryDto>.ErrorResponse("Category not found");
        }
        
        // Update slug if name changed
        var slug = SlugHelper.GenerateSlug(dto.Name);
        if (slug != category.Slug && await _unitOfWork.Categories.SlugExistsAsync(slug, id))
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
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
            "Category updated successfully"
        );
    }
    
    public async Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id);
        
        if (category == null)
        {
            return ApiResponse<bool>.ErrorResponse("Category not found");
        }
        
        // Check if category has products
        if (category.Products.Any())
        {
            return ApiResponse<bool>.ErrorResponse("Cannot delete category with products");
        }
        
        await _unitOfWork.Categories.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Category deleted successfully");
    }
    
    #endregion
    
    #region Product Operations
    
    public async Task<ApiResponse<ProductDto>> GetProductByIdAsync(Guid id)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        
        if (product == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse("Product not found");
        }

        var dto = MapProductToDto(product);
        dto.SimilarProducts = await BuildSimilarProductsAsync(product.Id);
        
        return ApiResponse<ProductDto>.SuccessResponse(dto);
    }
    
    public async Task<ApiResponse<ProductDto>> GetProductBySlugAsync(string slug)
    {
        var product = await _unitOfWork.Products.GetBySlugAsync(slug);
        
        if (product == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse("Product not found");
        }

        var dto = MapProductToDto(product);
        dto.SimilarProducts = await BuildSimilarProductsAsync(product.Id);
        
        return ApiResponse<ProductDto>.SuccessResponse(dto);
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
            filter.MinPrice,
            filter.MaxPrice,
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
            return ApiResponse<ProductDto>.ErrorResponse("Invalid brand type");
        }
        
        if (!Enum.IsDefined(typeof(ProductType), dto.ProductType))
        {
            return ApiResponse<ProductDto>.ErrorResponse("Invalid product type");
        }
        
        // Validate category exists
        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
        if (category == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse("Category not found");
        }
        
        // Generate slug
        var slug = SlugHelper.GenerateSlug(dto.Name);
        if (await _unitOfWork.Products.SlugExistsAsync(slug))
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
        }
        
        // Check SKU uniqueness
        if (!string.IsNullOrWhiteSpace(dto.SKU) && await _unitOfWork.Products.SKUExistsAsync(dto.SKU))
        {
            return ApiResponse<ProductDto>.ErrorResponse("SKU already exists");
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
            Tags = dto.Tags,
            AIKeywords = dto.AIKeywords,
            MaterialType = dto.MaterialType,
            QualityTier = dto.QualityTier,
            RecommendedFor = dto.RecommendedFor,
            Specifications = SerializeJson(dto.Specifications),
            KeyFeatures = SerializeJson(dto.KeyFeatures),
            WhatsIncluded = SerializeJson(dto.WhatsIncluded),
            WhatsNotIncluded = SerializeJson(dto.WhatsNotIncluded),
            Dimensions = dto.Dimensions,
            Warranty = dto.Warranty,
            FinishType = dto.FinishType,
            InstallationType = dto.InstallationType,
            Material = dto.Material,
            Color = dto.Color
        };

        await _unitOfWork.Products.CreateAsync(product);
        await _unitOfWork.SaveChangesAsync();
        
        // Reload with navigation properties
        product = await _unitOfWork.Products.GetByIdAsync(product.Id);
        
        return ApiResponse<ProductDto>.SuccessResponse(
            MapProductToDto(product!),
            "Product created successfully"
        );
    }
    
    public async Task<ApiResponse<ProductDto>> UpdateProductAsync(Guid id, UpdateProductDto dto)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        
        if (product == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse("Product not found");
        }
        
        // Validate category exists
        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
        if (category == null)
        {
            return ApiResponse<ProductDto>.ErrorResponse("Category not found");
        }
        
        // Update slug if name changed
        var slug = SlugHelper.GenerateSlug(dto.Name);
        if (slug != product.Slug && await _unitOfWork.Products.SlugExistsAsync(slug, id))
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
        }
        
        // Check SKU uniqueness
        if (!string.IsNullOrWhiteSpace(dto.SKU) && dto.SKU != product.SKU)
        {
            if (await _unitOfWork.Products.SKUExistsAsync(dto.SKU, id))
            {
                return ApiResponse<ProductDto>.ErrorResponse("SKU already exists");
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
        product.AIKeywords = dto.AIKeywords;
        product.MaterialType = dto.MaterialType;
        product.QualityTier = dto.QualityTier;
        product.RecommendedFor = dto.RecommendedFor;
        product.Specifications = SerializeJson(dto.Specifications);
        product.KeyFeatures = SerializeJson(dto.KeyFeatures);
        product.WhatsIncluded = SerializeJson(dto.WhatsIncluded);
        product.WhatsNotIncluded = SerializeJson(dto.WhatsNotIncluded);
        product.Dimensions = dto.Dimensions;
        product.Warranty = dto.Warranty;
        product.FinishType = dto.FinishType;
        product.InstallationType = dto.InstallationType;
        product.Material = dto.Material;
        product.Color = dto.Color;
        
        await _unitOfWork.Products.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();
        
        // Reload with navigation properties
        product = await _unitOfWork.Products.GetByIdAsync(id);
        
        return ApiResponse<ProductDto>.SuccessResponse(
            MapProductToDto(product!),
            "Product updated successfully"
        );
    }
    
    public async Task<ApiResponse<bool>> DeleteProductAsync(Guid id)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        
        if (product == null)
        {
            return ApiResponse<bool>.ErrorResponse("Product not found");
        }
        
        await _unitOfWork.Products.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Product deleted successfully");
    }
    
    #endregion
    
    #region Product Image Operations
    
    public async Task<ApiResponse<ProductImageDto>> AddProductImageAsync(Guid productId, AddProductImageDto dto)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        
        if (product == null)
        {
            return ApiResponse<ProductImageDto>.ErrorResponse("Product not found");
        }
        
        var image = new ProductImage
        {
            ProductId = productId,
            ImageUrl = dto.ImageUrl,
            AltText = dto.AltText,
            ViewType = dto.ViewType,
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
            "Image added successfully"
        );
    }
    
    public async Task<ApiResponse<bool>> DeleteProductImageAsync(Guid imageId)
    {
        var image = await _unitOfWork.ProductImages.GetByIdAsync(imageId);
        
        if (image == null)
        {
            return ApiResponse<bool>.ErrorResponse("Image not found");
        }
        
        await _unitOfWork.ProductImages.DeleteAsync(imageId);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Image deleted successfully");
    }
    
    public async Task<ApiResponse<bool>> SetPrimaryImageAsync(Guid productId, Guid imageId)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        
        if (product == null)
        {
            return ApiResponse<bool>.ErrorResponse("Product not found");
        }
        
        await _unitOfWork.ProductImages.SetPrimaryImageAsync(productId, imageId);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Primary image set successfully");
    }
    
    #endregion
    
    #region AI Keyword Matching

    // Common English stop-words to exclude from keyword extraction so we don't
    // match every product that mentions "the" or "with".
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","and","or","but","in","on","at","to","for","of","with",
        "by","from","as","is","was","are","were","be","been","has","have","had",
        "do","does","did","will","would","could","should","may","might","that",
        "this","it","its","i","you","we","they","he","she","my","your","our",
        "room","rooms","interior","design","style","modern","very","also","into",
        "show","make","create","generate","high","quality","look","like","want","need"
    };

    public async Task<List<MatchedProductDto>> SearchByAIKeywordsAsync(
        string prompt,
        List<string>? contextTags,
        int limit = 8)
    {
        // Extract meaningful words from the prompt (>3 chars, not stop-words)
        var promptKeywords = prompt
            .Split(new[] { ' ', ',', '.', '!', '?', '\n', '\r', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !StopWords.Contains(w))
            .Select(w => w.ToLower())
            .Distinct();

        // Context tags are added as-is (e.g. "oak flooring", "marble", "kitchen")
        var tagKeywords = contextTags?
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.Length > 2)
            .Select(w => w.ToLower()) ?? Enumerable.Empty<string>();

        var allKeywords = promptKeywords.Concat(tagKeywords).Distinct().ToList();

        var products = await _unitOfWork.Products.SearchByAIKeywordsAsync(allKeywords, limit);

        return products.Select(p =>
        {
            var primaryImage = p.Images.FirstOrDefault(i => i.IsPrimary) ?? p.Images.FirstOrDefault();
            return new MatchedProductDto
            {
                ProductId = p.Id,
                Name = p.Name,
                MaterialType = p.MaterialType,
                Category = p.Category?.Name,
                Price = p.Price,
                PriceDisplay = p.ShowPrice && p.Price.HasValue ? $"₦{p.Price.Value:N2}" : "Request Price",
                ImageUrl = primaryImage?.ImageUrl,
                Slug = p.Slug,
                InStock = !p.TrackInventory || (p.StockQuantity ?? 0) > 0
            };
        }).ToList();
    }

    #endregion

    #region Bulk Operations

    public async Task<ApiResponse<BulkCreateProductResultDto>> BulkCreateProductsAsync(
        List<CreateProductDto> dtos)
    {
        var result = new BulkCreateProductResultDto { TotalSubmitted = dtos.Count };
        var toInsert = new List<Product>();

        for (var i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];

            if (!Enum.IsDefined(typeof(BrandType), dto.BrandType))
            {
                result.Failures.Add(new BulkProductFailureDto { Index = i, ProductName = dto.Name, Reason = "Invalid BrandType" });
                continue;
            }

            if (!Enum.IsDefined(typeof(ProductType), dto.ProductType))
            {
                result.Failures.Add(new BulkProductFailureDto { Index = i, ProductName = dto.Name, Reason = "Invalid ProductType" });
                continue;
            }

            var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
            if (category == null)
            {
                result.Failures.Add(new BulkProductFailureDto { Index = i, ProductName = dto.Name, Reason = $"Category {dto.CategoryId} not found" });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(dto.SKU) && await _unitOfWork.Products.SKUExistsAsync(dto.SKU))
            {
                result.Failures.Add(new BulkProductFailureDto { Index = i, ProductName = dto.Name, Reason = $"SKU '{dto.SKU}' already exists" });
                continue;
            }

            var slug = SlugHelper.GenerateSlug(dto.Name);
            if (await _unitOfWork.Products.SlugExistsAsync(slug))
                slug = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";

            toInsert.Add(new Product
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
                Tags = dto.Tags,
                AIKeywords = dto.AIKeywords,
                MaterialType = dto.MaterialType,
                QualityTier = dto.QualityTier,
                RecommendedFor = dto.RecommendedFor
            });
        }

        if (toInsert.Count > 0)
        {
            var created = await _unitOfWork.Products.BulkCreateAsync(toInsert);
            await _unitOfWork.SaveChangesAsync();
            result.Created = created.Count;
            result.CreatedProducts = created.Select(MapProductToDto).ToList();
        }

        result.Failed = result.Failures.Count;

        return ApiResponse<BulkCreateProductResultDto>.SuccessResponse(
            result,
            $"{result.Created} product(s) created, {result.Failed} failed.");
    }

    #endregion


    #region Bulk Import Operations
    
    public async Task<ApiResponse<ImportResultDto>> ImportProductsAsync(Stream fileStream, string fileName)
    {
        var result = new ImportResultDto();

        // Validate file type
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            result.Success = false;
            result.Results.Add(new ImportResultRowDto
            {
                RowNumber = 0,
                Errors = new List<string> { "Invalid file format. Only CSV files are allowed." }
            });
            return new ApiResponse<ImportResultDto> { Success = false, Message = "Invalid file format", Data = result };
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "")
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
                Errors = new List<string> { "CSV file is empty or contains no data rows." }
            });
            return new ApiResponse<ImportResultDto> { Success = false, Message = "Empty file", Data = result };
        }

        if (records.Count > 500)
        {
            result.Success = false;
            result.Results.Add(new ImportResultRowDto
            {
                RowNumber = 0,
                Errors = new List<string> { "File contains more than 500 rows. Please split into multiple files." }
            });
            return new ApiResponse<ImportResultDto> { Success = false, Message = "File too large", Data = result };
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
                errors.Add("Product name is required");
            }

            // Validate BrandType
            BrandType brandType;
            if (!string.IsNullOrWhiteSpace(record.BrandType))
            {
                if (!Enum.TryParse(typeof(BrandType), record.BrandType.Trim(), true, out var parsedBrand))
                {
                    errors.Add($"Invalid BrandType '{record.BrandType}'. Valid values: TBM, Bogat");
                    brandType = default;
                }
                else
                {
                    brandType = (BrandType)parsedBrand;
                }
            }
            else
            {
                errors.Add("BrandType is required");
                brandType = default;
            }

            // Validate ProductType
            ProductType productType;
            if (!string.IsNullOrWhiteSpace(record.ProductType))
            {
                if (!Enum.TryParse(typeof(ProductType), record.ProductType.Trim(), true, out var parsedType))
                {
                    errors.Add($"Invalid ProductType '{record.ProductType}'. Valid values: PhysicalProduct, Service");
                    productType = default;
                }
                else
                {
                    productType = (ProductType)parsedType;
                }
            }
            else
            {
                errors.Add("ProductType is required");
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
                    errors.Add($"Category '{record.CategoryName}' not found");
                }
            }
            else
            {
                errors.Add("CategoryName is required");
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
                    errors.Add($"SKU '{record.SKU}' already exists");
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
                slug = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
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
                Tags = record.Tags?.Trim(),
                AIKeywords = record.AIKeywords?.Trim(),
                MaterialType = record.MaterialType?.Trim(),
                QualityTier = record.QualityTier?.Trim(),
                RecommendedFor = record.RecommendedFor?.Trim(),
                Specifications = ParseSpecifications(record.Specifications),
                KeyFeatures = ParsePipeList(record.KeyFeatures),
                WhatsIncluded = ParsePipeList(record.WhatsIncluded),
                WhatsNotIncluded = ParsePipeList(record.WhatsNotIncluded),
                Dimensions = record.Dimensions?.Trim(),
                Warranty = record.Warranty?.Trim(),
                FinishType = record.FinishType?.Trim(),
                InstallationType = record.InstallationType?.Trim(),
                Material = record.Material?.Trim(),
                Color = record.Color?.Trim()
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
            return new ApiResponse<ImportResultDto> { Success = false, Message = $"Import completed with {result.Failed} error(s)", Data = result };
        }

        return ApiResponse<ImportResultDto>.SuccessResponse(
            result,
            $"Successfully imported {result.SuccessfullyImported} products"
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
            ? $"₦{product.Price.Value:N2}" 
            : "Request Price",
            StockQuantity = product.StockQuantity,
            InStock = !product.TrackInventory || (product.StockQuantity ?? 0) > 0,
            TrackInventory = product.TrackInventory,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            Tags = product.Tags,
            AIKeywords = product.AIKeywords,
            MaterialType = product.MaterialType,
            QualityTier = product.QualityTier,
            RecommendedFor = product.RecommendedFor,
            Specifications = DeserializeJson<SpecificationItemDto>(product.Specifications),
            KeyFeatures = DeserializeJson<string>(product.KeyFeatures),
            WhatsIncluded = DeserializeJson<string>(product.WhatsIncluded),
            WhatsNotIncluded = DeserializeJson<string>(product.WhatsNotIncluded),
            Dimensions = product.Dimensions,
            Warranty = product.Warranty,
            FinishType = product.FinishType,
            InstallationType = product.InstallationType,
            Material = product.Material,
            Color = product.Color,
            Images = product.Images.Select(MapProductImageToDto).ToList(),
            PrimaryImageUrl = primaryImage?.ImageUrl,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt ?? product.CreatedAt  // FIX: Handle nullable UpdatedAt
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
            ViewType = image.ViewType,
            DisplayOrder = image.DisplayOrder,
            IsPrimary = image.IsPrimary
        };
    }

    private static string? SerializeJson<T>(IEnumerable<T>? value) =>
        value == null ? null : JsonSerializer.Serialize(value);

    private static List<T>? DeserializeJson<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<List<T>>(json);

    private static string? ParsePipeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var items = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return items.Count == 0 ? null : JsonSerializer.Serialize(items);
    }

    private static string? ParseSpecifications(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var specs = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair =>
            {
                var idx = pair.IndexOf(':');
                return idx > 0
                    ? new SpecificationItemDto { Key = pair[..idx].Trim(), Value = pair[(idx + 1)..].Trim() }
                    : new SpecificationItemDto { Key = pair.Trim(), Value = string.Empty };
            })
            .ToList();
        return specs.Count == 0 ? null : JsonSerializer.Serialize(specs);
    }

    private async Task<List<ProductCardDto>> BuildSimilarProductsAsync(Guid productId)
    {
        const int defaultLimit = 4;
        const int maxLimit = 12;

        var safeLimit = Math.Clamp(defaultLimit, 1, maxLimit);
        var related = await _unitOfWork.Products.GetRelatedAsync(productId, safeLimit);

        return related
            .Select(MapProductToCardDto)
            .ToList();
    }

    private ProductCardDto MapProductToCardDto(Product product)
    {
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary) ?? product.Images.FirstOrDefault();

        return new ProductCardDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Price = product.Price,
            Image = primaryImage?.ImageUrl,
            Category = product.Category?.Name ?? string.Empty,
            InStock = !product.TrackInventory || (product.StockQuantity ?? 0) > 0
        };
    }
    
    #endregion
}


