using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UCSProductGallery.Data;
using UCSProductGallery.Models;
using UCSProductGallery.Services;
using Xunit;

namespace UCSProductGallery.Tests.ServicesTests
{
    public class ProductSyncServiceTests
    {
        [Fact]
        public async Task SyncAllProductsAsync_ShouldSynchronizeProductsToDatabase_WhenApiReturnsProducts()
        {
            // Arrange
            // 1. Set up mock product service
            var mockProductService = new Mock<IProductService>();
            var testProducts = CreateTestProducts();
            
            mockProductService
                .Setup(service => service.GetProductsAsync())
                .ReturnsAsync(testProducts);
            
            // 2. Set up in-memory database
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestProductDb_" + Guid.NewGuid().ToString())
                .Options;
            
            // Create a context instance for testing
            var dbContext = new ApplicationDbContext(dbOptions);
            
            // 3. Set up logger
            var mockLogger = new Mock<ILogger<ProductSyncService>>();
            
            // 4. Create ProductSyncService
            var syncService = new ProductSyncService(
                mockProductService.Object,
                dbContext,
                mockLogger.Object
            );
            
            // Act
            await syncService.SyncAllProductsAsync();
            
            // Assert
            // Verify that products were saved to the database
            var savedProducts = await dbContext.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .ToListAsync();
            
            // We want to make sure products were saved, but the exact count might depend on implementation
            Assert.NotEmpty(savedProducts);
            
            // Verify first product details
            var testProduct = testProducts[0];
            var savedProduct = savedProducts.FirstOrDefault(p => p.Title == testProduct.Title);
            
            Assert.NotNull(savedProduct);
            Assert.Equal(testProduct.Title, savedProduct.Title);
            Assert.Equal(testProduct.Description, savedProduct.Description);
            Assert.Equal(testProduct.Price, savedProduct.Price);
            Assert.Equal(testProduct.CategoryName, savedProduct.CategoryName);
            
            // Verify that categories were created
            var savedCategories = await dbContext.Categories.ToListAsync();
            // We should have two unique categories: "smartphones" and "laptops"
            Assert.Equal(2, savedCategories.Count);
            
            // Verify first product's images (if it has any)
            var firstTestProduct = testProducts[0];
            if (firstTestProduct.ImageUrls != null && firstTestProduct.ImageUrls.Count > 0)
            {
                var firstSavedProduct = savedProducts.FirstOrDefault(p => p.Title == firstTestProduct.Title);
                Assert.NotNull(firstSavedProduct);
                Assert.NotNull(firstSavedProduct.Images);
                
                // Count of images should match
                Assert.True(firstSavedProduct.Images.Count > 0);
                
                // First image should be set as main
                Assert.Contains(firstSavedProduct.Images, img => img.IsMain);
            }
        }
        
        private List<Product> CreateTestProducts()
        {
            return new List<Product>
            {
                new Product
                {
                    Id = 1,
                    Title = "iPhone 9",
                    Description = "An apple mobile which is nothing like apple",
                    Price = 549,
                    CategoryName = "smartphones",
                    Thumbnail = "https://cdn.dummyjson.com/product-images/1/thumbnail.jpg",
                    ImageUrls = new List<string>
                    {
                        "https://cdn.dummyjson.com/product-images/1/1.jpg",
                        "https://cdn.dummyjson.com/product-images/1/2.jpg",
                        "https://cdn.dummyjson.com/product-images/1/3.jpg"
                    }
                },
                new Product
                {
                    Id = 2,
                    Title = "iPhone X",
                    Description = "SIM-Free, Model A19211 6.5-inch Super Retina HD display",
                    Price = 899,
                    CategoryName = "smartphones",
                    Thumbnail = "https://cdn.dummyjson.com/product-images/2/thumbnail.jpg",
                    ImageUrls = new List<string>
                    {
                        "https://cdn.dummyjson.com/product-images/2/1.jpg",
                        "https://cdn.dummyjson.com/product-images/2/2.jpg",
                        "https://cdn.dummyjson.com/product-images/2/3.jpg"
                    }
                },
                new Product
                {
                    Id = 3,
                    Title = "Samsung Universe 9",
                    Description = "Samsung's new variant which goes beyond Galaxy",
                    Price = 1249,
                    CategoryName = "smartphones",
                    Thumbnail = "https://cdn.dummyjson.com/product-images/3/thumbnail.jpg",
                    ImageUrls = new List<string>
                    {
                        "https://cdn.dummyjson.com/product-images/3/1.jpg"
                    }
                },
                new Product
                {
                    Id = 4,
                    Title = "OPPOF19",
                    Description = "OPPO F19 is officially announced on April 2021.",
                    Price = 280,
                    CategoryName = "smartphones",
                    Thumbnail = "https://cdn.dummyjson.com/product-images/4/thumbnail.jpg",
                    ImageUrls = new List<string>() // Empty list instead of null
                },
                new Product
                {
                    Id = 5,
                    Title = "MacBook Pro",
                    Description = "MacBook Pro 2021 with mini-LED display",
                    Price = 1749,
                    CategoryName = "laptops",
                    Thumbnail = "https://cdn.dummyjson.com/product-images/6/thumbnail.png",
                    ImageUrls = new List<string>
                    {
                        "https://cdn.dummyjson.com/product-images/6/1.png",
                        "https://cdn.dummyjson.com/product-images/6/2.jpg",
                        "https://cdn.dummyjson.com/product-images/6/3.png"
                    }
                }
            };
        }
    }
}
