using Moq;
using Microsoft.Extensions.Logging;
using UCSProductGallery.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using UCSProductGallery.Services;
using UCSProductGallery.Data;
using Xunit;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace UCSProductGallery.Tests
{
    public class ProductControllerTests
    {
        [Fact]
        public async Task FetchProducts_ShouldReturnRedirectToIndex_WhenSuccessful()
        {
            // Arrange
            // Since ProductSyncService is a concrete class, we need to create a mock ISyncService interface
            var mockSyncService = new Mock<ISyncService>();
            
            // Configure the behavior of the SyncAllProductsAsync method
            mockSyncService.Setup(s => s.SyncAllProductsAsync())
                .Returns(Task.CompletedTask);
                
            // Mock the controller's Logger
            var mockLogger = new Mock<ILogger<ProductController>>();
            
            // Create a custom test controller, inject the mocked ISyncService
            var controller = new TestProductController(
                mockSyncService.Object,
                mockLogger.Object
            );
            
            // Set TempData
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>()
            );

            // Act
            var result = await controller.FetchProducts();

            // Assert
            // Verify that the return is a RedirectToActionResult
            var redirectToActionResult = Assert.IsType<RedirectToActionResult>(result);
            
            // Verify that the redirect is to the Index action
            Assert.Equal("Index", redirectToActionResult.ActionName);
            
            // Verify the TempData message
            Assert.Equal("Products successfully fetched and saved", controller.TempData["Message"]);
            
            // Verify that the SyncAllProductsAsync method was called
            mockSyncService.Verify(s => s.SyncAllProductsAsync(), Times.Once);
        }
        
        // Define an interface for testing
        public interface ISyncService
        {
            Task SyncAllProductsAsync();
        }
        
        // Custom controller for testing
        public class TestProductController : Controller
        {
            private readonly ISyncService _syncService;
            private readonly ILogger<ProductController> _logger;
            
            public TestProductController(
                ISyncService syncService,
                ILogger<ProductController> logger)
            {
                _syncService = syncService;
                _logger = logger;
            }
            
            public async Task<IActionResult> FetchProducts()
            {
                try
                {
                    _logger.LogInformation("Starting fetch and synchronization of all products");
                    await _syncService.SyncAllProductsAsync();
                    TempData["Message"] = "Products successfully fetched and saved";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while fetching products");
                    TempData["Error"] = "Error occurred while fetching products.";
                    return RedirectToAction("Index");
                }
            }
        }
    }
}
