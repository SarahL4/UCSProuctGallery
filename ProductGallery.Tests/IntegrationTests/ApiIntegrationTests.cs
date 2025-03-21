using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UCSProductGallery.Services;
using UCSProductGallery.Models;

namespace UCSProductGallery.Tests.IntegrationTests
{
    public class ApiIntegrationTests
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductApiClient> _logger;
        private readonly ProductApiClient _apiClient;

        public ApiIntegrationTests()
        {
            // Create a real HTTP client for integration testing
            _httpClient = new HttpClient();
            
            // Mock the logger
            var mockLogger = new Mock<ILogger<ProductApiClient>>();
            _logger = mockLogger.Object;
            
            // Create the real API client with real HTTP client
            _apiClient = new ProductApiClient(_httpClient, _logger);
        }
        
        [Fact]
        public async Task GetProducts_ShouldReturnProductList_WhenApiIsAvailable()
        {
            // Act
            var products = await _apiClient.GetProductsAsync();
            
            // Assert
            Assert.NotNull(products);
            Assert.NotEmpty(products);
            Assert.True(products.Count > 0, "Should return at least one product");
            
            // Verify product properties
            var firstProduct = products[0];
            Assert.NotEqual(0, firstProduct.Id);
            Assert.NotNull(firstProduct.Title);
            Assert.NotEmpty(firstProduct.Title);
        }
        
        [Fact]
        public async Task GetProductById_ShouldReturnProduct_WhenValidIdProvided()
        {
            // Arrange
            int productId = 1; // Use a known valid product ID from dummyjson.com
            
            // Act
            var product = await _apiClient.GetProductByIdAsync(productId);
            
            // Assert
            Assert.NotNull(product);
            Assert.Equal(productId, product.Id);
            Assert.NotNull(product.Title);
            Assert.NotEmpty(product.Title);
            Assert.NotNull(product.Description);
        }
        
        [Fact]
        public async Task GetProductById_ShouldHandleErrors_WhenInvalidIdProvided()
        {
            // Arrange
            int invalidProductId = 10000; // Use an ID that likely doesn't exist
            
            // Act
            var product = await _apiClient.GetProductByIdAsync(invalidProductId);
            
            // Assert
            // Note: our API client returns a new Product with the given ID when the API call fails
            Assert.NotNull(product);
            Assert.Equal(invalidProductId, product.Id);
            Assert.Null(product.Title); // The title should be null for a product that doesn't exist
        }
    }
}
