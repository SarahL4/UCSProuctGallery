using Microsoft.EntityFrameworkCore;
using UCSProductGallery.Data;
using UCSProductGallery.Models;

namespace UCSProductGallery.Services
{
    public class ProductSyncService
    {
        private readonly IProductService _productService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ProductSyncService> _logger;

        public ProductSyncService(
            IProductService productService,
            ApplicationDbContext dbContext,
            ILogger<ProductSyncService> logger)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Synchronize all products from API to database
        /// </summary>
        public async Task SyncAllProductsAsync()
        {
            try
            {
                // Get products from API
                var apiProducts = await _productService.GetProductsAsync();
                if (apiProducts == null || !apiProducts.Any())
                {
                    _logger.LogWarning("API did not return any product data");
                    return;
                }

                _logger.LogInformation($"Retrieved {apiProducts.Count} products from API");

                // Synchronize product categories and then products
                await SyncCategoriesAsync(apiProducts);
                await SyncProductsAsync(apiProducts);

                _logger.LogInformation("Product synchronization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during product synchronization");
                throw;
            }
        }

        /// <summary>
        /// Synchronize a single product from API to database
        /// </summary>
        public async Task SyncProductByIdAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("Product ID must be greater than zero", nameof(id));
            }

            try
            {
                // Get product from API
                var apiProduct = await _productService.GetProductByIdAsync(id);
                if (apiProduct == null || apiProduct.Id == 0)
                {
                    _logger.LogWarning($"API did not return product data for ID {id}");
                    return;
                }

                // Add single product to the list to reuse synchronization logic
                var apiProducts = new List<Product> { apiProduct };

                // Synchronize product categories and then the product
                await SyncCategoriesAsync(apiProducts);
                await SyncProductsAsync(apiProducts);

                _logger.LogInformation($"Product {id} synchronization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while synchronizing product {id}");
                throw;
            }
        }

        private async Task SyncCategoriesAsync(List<Product> apiProducts)
        {
            // Get all unique non-empty category names from API products
            var categoryNames = apiProducts
                .Where(p => !string.IsNullOrEmpty(p.CategoryName))
                .Select(p => p.CategoryName)
                .Distinct()
                .ToList();

            if (!categoryNames.Any())
            {
                _logger.LogInformation("No categories to synchronize");
                return;
            }

            // Get existing categories from database
            var existingCategories = await _dbContext.Categories
                .Where(c => categoryNames.Contains(c.Name))
                .ToListAsync();

            // Find categories that need to be added
            var existingCategoryNames = existingCategories.Select(c => c.Name).ToList();
            var newCategoryNames = categoryNames
                .Where(name => !existingCategoryNames.Contains(name))
                .ToList();

            // Add new categories to database
            if (newCategoryNames.Any())
            {
                var newCategories = newCategoryNames.Select(name => new Category { Name = name });
                await _dbContext.Categories.AddRangeAsync(newCategories);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Added {newCategoryNames.Count} new categories");
            }
            else
            {
                _logger.LogInformation("All categories already exist in the database");
            }
        }

        private async Task SyncProductsAsync(List<Product> apiProducts)
        {
            // Get all categories from the database (for subsequent foreign key mapping)
            var allCategories = await _dbContext.Categories.ToListAsync();
            var categoryDict = allCategories.ToDictionary(c => c.Name ?? string.Empty, c => c.Id);

            // Get existing product list (match by title)
            var existingProducts = await _dbContext.Products
                .Include(p => p.Images)
                .ToListAsync();

            var existingProductDict = existingProducts
                .Where(p => !string.IsNullOrEmpty(p.Title))
                .ToDictionary(p => (p.Title ?? string.Empty).ToLower(), p => p);

            // Process each API product
            foreach (var apiProduct in apiProducts)
            {
                await ProcessProductAsync(apiProduct, categoryDict, existingProductDict);
            }
        }

        private async Task ProcessProductAsync(
            Product apiProduct, 
            Dictionary<string, int> categoryDict, 
            Dictionary<string, Product> existingProductDict)
        {
            if (apiProduct == null)
            {
                _logger.LogWarning("Skipping null product");
                return;
            }

            try
            {
                // Set category ID (foreign key)
                if (!string.IsNullOrEmpty(apiProduct.CategoryName) &&
                    categoryDict.ContainsKey(apiProduct.CategoryName))
                {
                    apiProduct.CategoryId = categoryDict[apiProduct.CategoryName];
                }

                // Save original API product ID for logging
                int apiProductId = apiProduct.Id;

                // Check if product already exists (match by title)
                Product? dbProduct = null;
                string? productTitle = apiProduct.Title?.ToLower();
                
                if (!string.IsNullOrEmpty(productTitle) && existingProductDict.ContainsKey(productTitle))
                {
                    // Update existing product
                    dbProduct = existingProductDict[productTitle];
                    int dbProductId = dbProduct.Id;

                    // Copy API product properties to database product
                    _dbContext.Entry(dbProduct).CurrentValues.SetValues(apiProduct);
                    dbProduct.Id = dbProductId; // Restore database ID

                    _logger.LogInformation($"Updated existing product: {dbProduct.Title} (API ID: {apiProductId}, DB ID: {dbProductId})");
                }
                else
                {
                    // Create new product
                    dbProduct = new Product
                    {
                        Title = apiProduct.Title ?? string.Empty,
                        Description = apiProduct.Description ?? string.Empty,
                        Price = apiProduct.Price,
                        CategoryId = apiProduct.CategoryId,
                        CategoryName = apiProduct.CategoryName ?? string.Empty,
                        Thumbnail = apiProduct.Thumbnail ?? string.Empty,
                        ImageUrls = apiProduct.ImageUrls ?? new List<string>()
                    };

                    await _dbContext.Products.AddAsync(dbProduct);
                    _logger.LogInformation($"Added new product: {dbProduct.Title} (API ID: {apiProductId})");
                }

                // Save changes to get database-generated ID (if new product)
                await _dbContext.SaveChangesAsync();

                // Synchronize product images
                await SyncProductImagesAsync(
                    dbProduct, 
                    apiProduct.ImageUrls ?? new List<string>(), 
                    apiProduct.Thumbnail ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while synchronizing product {apiProduct.Id} ({apiProduct.Title})");
                // Continue processing the next product
            }
        }

        private async Task SyncProductImagesAsync(Product dbProduct, List<string> imageUrls, string thumbnail)
        {
            // Skip if product has no image URLs and no thumbnail
            if ((imageUrls == null || !imageUrls.Any()) && string.IsNullOrEmpty(thumbnail))
            {
                return;
            }

            // Get existing product images
            var existingImages = await _dbContext.ProductImages
                .Where(pi => pi.ProductId == dbProduct.Id)
                .ToListAsync();

            // Delete existing images
            if (existingImages.Any())
            {
                _dbContext.ProductImages.RemoveRange(existingImages);
            }

            // Add new images
            var newImages = new List<ProductImage>();
            bool isFirstImage = true;

            // Add image URLs from API
            if (imageUrls != null)
            {
                foreach (var imageUrl in imageUrls.Where(url => !string.IsNullOrEmpty(url)))
                {
                    newImages.Add(new ProductImage
                    {
                        ProductId = dbProduct.Id,
                        ImageUrl = imageUrl,
                        IsMain = isFirstImage // Set first image as main image
                    });
                    isFirstImage = false;
                }
            }

            // If there is a thumbnail but not in ImageUrls, add it too
            if (!string.IsNullOrEmpty(thumbnail) &&
                (imageUrls == null || !imageUrls.Contains(thumbnail)))
            {
                newImages.Add(new ProductImage
                {
                    ProductId = dbProduct.Id,
                    ImageUrl = thumbnail,
                    IsMain = !newImages.Any() // Set as main image if there are no other images
                });
            }

            if (newImages.Any())
            {
                await _dbContext.ProductImages.AddRangeAsync(newImages);
                _logger.LogInformation($"Added {newImages.Count} images for product {dbProduct.Id}");
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}