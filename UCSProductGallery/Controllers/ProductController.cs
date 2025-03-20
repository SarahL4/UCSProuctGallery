using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UCSProductGallery.Data;
using UCSProductGallery.Models;
using UCSProductGallery.Services;

namespace UCSProductGallery.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IProductService _productService;
        private readonly ProductSyncService _syncService;
        private readonly ILogger<ProductController> _logger;

        public ProductController(
            ApplicationDbContext context,
            IProductService productService,
            ProductSyncService syncService,
            ILogger<ProductController> logger)
        {
            _context = context;
            _productService = productService;
            _syncService = syncService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Check if we are coming from SyncProducts action (after button click)
                bool isAfterSync = TempData["AfterSync"] != null && (bool)TempData["AfterSync"];
                
                if (!isAfterSync)
                {
                    // Initial page load - show empty products list with message
                    ViewBag.InitialLoad = true;
                    return View(new List<Product>());
                }
                
                // After button click - try from database first
                try
                {
                    var dbProducts = await _context.Products
                        .Include(p => p.Category)
                        .Include(p => p.Images)
                        .ToListAsync();

                    if (dbProducts.Any())
                    {
                        return View(dbProducts);
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Database error when retrieving products: {0}", dbEx.Message);
                    // Continue to API retrieval
                }
                
                // Either no products in database or database error - get from API
                try
                {
                    var apiProducts = await _productService.GetProductsAsync();
                    return View(apiProducts);
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "Error getting products from API: {0}", apiEx.Message);
                    TempData["Error"] = $"Failed to get data from API: {apiEx.Message}";
                    return View(new List<Product>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Index action: {0}", ex.Message);
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return View(new List<Product>());
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            // Add logs for debugging
            _logger.LogInformation($"Attempting to retrieve product with ID {id}");
            
            // Make sure ID is valid
            if (id <= 0)
            {
                return NotFound();
            }
            
            try
            {
                // First get product from database
                var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == id);

                // If not found in database, get from API and save
                if (product == null)
                {
                    _logger.LogInformation($"Product with ID {id} not found in database, will fetch from API");
                    await _syncService.SyncProductByIdAsync(id);
                    
                    // Try to get from database again
                    product = await _context.Products
                        .Include(p => p.Category)
                        .Include(p => p.Images)
                        .FirstOrDefaultAsync(p => p.Id == id);
                        
                    if (product == null)
                    {
                        _logger.LogWarning($"Product with ID {id} not found");
                        return NotFound();
                    }
                }
                
                // Prepare view data
                if (product.Images != null && product.Images.Any())
                {
                    // If ImageUrls is null, initialize it
                    if (product.ImageUrls == null)
                    {
                        product.ImageUrls = new List<string>();
                    }
                    
                    // Add image URLs from database to ImageUrls for view to use
                    foreach (var image in product.Images)
                    {
                        if (!string.IsNullOrEmpty(image.ImageUrl) && !product.ImageUrls.Contains(image.ImageUrl))
                        {
                            product.ImageUrls.Add(image.ImageUrl);
                        }
                    }
                }

                _logger.LogInformation($"Product found: {product.Title}, Price: {product.Price}, Category: {product.CategoryName}");
                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while retrieving product details for ID {id}");
                TempData["Error"] = $"Error occurred while retrieving product details: {ex.Message}";
                return View(new Product { Id = id });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SyncProducts()
        {
            try
            {
                _logger.LogInformation("Starting synchronization of all products");
                
                try
                {
                    // First try to save to database
                    await _syncService.SyncAllProductsAsync();
                    TempData["Message"] = "Products successfully synchronized to database";
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Error saving to database: {0}", dbEx.Message);
                    TempData["Error"] = $"Unable to save to database, will get data directly from API: {dbEx.Message}";
                    
                    try
                    {
                        // Database error, get products directly from API
                        var apiProducts = await _productService.GetProductsAsync();
                        if (apiProducts != null && apiProducts.Any())
                        {
                            TempData["Message"] = "Successfully retrieved data from API, but not saved to database";
                        }
                        else
                        {
                            TempData["Error"] = "API returned empty data";
                        }
                    }
                    catch (Exception apiEx)
                    {
                        _logger.LogError(apiEx, "Error getting products from API: {0}", apiEx.Message);
                        TempData["Error"] = $"Unable to get data from API: {apiEx.Message}";
                    }
                }
                
                // Set flag to indicate we're coming from SyncProducts
                TempData["AfterSync"] = true;
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while synchronizing products: {0}", ex.Message);
                TempData["Error"] = "Error occurred while synchronizing products: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> FetchProducts()
        {
            try
            {
                _logger.LogInformation("Starting fetch and synchronization of all products");
                await _syncService.SyncAllProductsAsync();
                TempData["Message"] = "Products successfully fetched and saved";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching products");
                TempData["Error"] = "Error occurred while fetching products: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
