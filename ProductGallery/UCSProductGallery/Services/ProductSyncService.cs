using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            _productService = productService;
            _dbContext = dbContext;
            _logger = logger;
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

                // Synchronize product categories
                await SyncCategoriesAsync(apiProducts);

                // Synchronize product data
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

                // Synchronize product categories
                await SyncCategoriesAsync(apiProducts);

                // Synchronize product data
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
            // Get all unique category names from API products
            var categoryNames = apiProducts
                .Where(p => !string.IsNullOrEmpty(p.CategoryName))
                .Select(p => p.CategoryName)
                .Distinct()
                .ToList();

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
                foreach (var categoryName in newCategoryNames)
                {
                    var newCategory = new Category
                    {
                        Name = categoryName
                    };
                    _dbContext.Categories.Add(newCategory);
                }
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Added {newCategoryNames.Count} new categories");
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

            var existingProductDict = existingProducts.ToDictionary(p => p.Title?.ToLower() ?? string.Empty, p => p);

            // Process each API product
            foreach (var apiProduct in apiProducts)
            {
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
                    string productTitle = apiProduct.Title?.ToLower() ?? string.Empty;
                    Product? dbProduct = null;

                    if (!string.IsNullOrEmpty(productTitle) && existingProductDict.ContainsKey(productTitle))
                    {
                        // Found existing product, update it
                        dbProduct = existingProductDict[productTitle];

                        // Don't update ID, keep database ID
                        int dbProductId = dbProduct.Id;

                        // Copy API product properties to database product
                        _dbContext.Entry(dbProduct).CurrentValues.SetValues(apiProduct);

                        // Restore database ID
                        dbProduct.Id = dbProductId;

                        _logger.LogInformation($"Updated existing product: {dbProduct.Title} (API ID: {apiProductId}, DB ID: {dbProductId})");
                    }
                    else
                    {
                        // New product, need to create a new instance and clear ID, let database generate
                        dbProduct = new Product
                        {
                            Title = apiProduct.Title,
                            Description = apiProduct.Description,
                            Price = apiProduct.Price,
                            CategoryId = apiProduct.CategoryId,
                            CategoryName = apiProduct.CategoryName,
                            Thumbnail = apiProduct.Thumbnail,
                            ImageUrls = apiProduct.ImageUrls
                        };

                        _dbContext.Products.Add(dbProduct);
                        _logger.LogInformation($"Added new product: {dbProduct.Title} (API ID: {apiProductId})");
                    }

                    // Save changes to get database-generated ID (if new product)
                    await _dbContext.SaveChangesAsync();

                    // Synchronize product images (using database product ID)
                    await SyncProductImagesAsync(dbProduct, apiProduct.ImageUrls ?? new List<string>(), apiProduct.Thumbnail ?? string.Empty);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error occurred while synchronizing product {apiProduct.Id}");
                    // Continue processing the next product
                }
            }
        }

        private async Task SyncProductImagesAsync(Product dbProduct, List<string> imageUrls, string thumbnail)
        {
            // Skip if product has no image URLs
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
                foreach (var imageUrl in imageUrls)
                {
                    if (!string.IsNullOrEmpty(imageUrl))
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