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
                bool isAfterSync = TempData["AfterSync"] != null && Convert.ToBoolean(TempData["AfterSync"]);
                
                // Try to get products from database first
                try
                {
                    var dbProducts = await _context.Products
                        .Include(p => p.Category)
                        .Include(p => p.Images)
                        .ToListAsync();

                    if (dbProducts.Any())
                    {
                        // Database works fine, return data
                        ViewBag.InitialLoad = false;
                        return View(dbProducts);
                    }
                    else if (!isAfterSync)
                    {
                        // Database works but no data and not after sync - show empty state
                        ViewBag.InitialLoad = true;
                        return View(new List<Product>());
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Database error when retrieving products: {0}", dbEx.Message);
                    ViewBag.DatabaseError = true;
                    
                    // Only show initialLoad message if not coming from SyncProducts
                    if (!isAfterSync)
                    {
                        ViewBag.InitialLoad = true;
                        return View(new List<Product>());
                    }
                }
                
                // Either database is empty after sync or database error - get from API
                if (isAfterSync)
                {
                    try
                    {
                        var apiProducts = await _productService.GetProductsAsync();
                        return View(apiProducts);
                    }
                    catch (Exception apiEx)
                    {
                        _logger.LogError(apiEx, "Error getting products from API: {0}", apiEx.Message);
                        TempData["Error"] = $"Failed to get data from API.";
                        return View(new List<Product>());
                    }
                }
                
                // Default fallback
                return View(new List<Product>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Index action: {0}", ex.Message);
                TempData["Error"] = $"An error occurred.";
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
                // Check if we came from the index where API data was used
                bool useApiDirectly = TempData["AfterSync"] != null && Convert.ToBoolean(TempData["AfterSync"]) 
                                     && TempData["Error"] != null && TempData["Error"]?.ToString()?.Contains("database") == true;
                
                if (!useApiDirectly)
                {
                    // Try to get product from database first
                    try
                    {
                        var product = await _context.Products
                            .Include(p => p.Category)
                            .Include(p => p.Images)
                            .FirstOrDefaultAsync(p => p.Id == id);

                        // If found in database, prepare and return
                        if (product != null)
                        {
                            PrepareProductForView(product);
                            return View(product);
                        }
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogError(dbEx, "Database error when retrieving product details for ID {0}: {1}", id, dbEx.Message);
                        // Continue to API retrieval
                    }
                }
                
                // If useApiDirectly or database failed - try to get directly from API
                try
                {
                    _logger.LogInformation($"Getting product with ID {id} directly from API");
                    var apiProduct = await _productService.GetProductByIdAsync(id);
                    
                    if (apiProduct != null)
                    {
                        // Try to enrich with category info if available without using database
                        PrepareProductForView(apiProduct);
                        _logger.LogInformation($"Product found from API: {apiProduct.Title}, Price: {apiProduct.Price}, Category: {apiProduct.CategoryName}");
                        return View(apiProduct);
                    }
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "Error getting product from API for ID {0}: {1}", id, apiEx.Message);
                    TempData["Error"] = $"Failed to get product data from API.";
                }

                // If we reach here, product was not found in database or API
                _logger.LogWarning($"Product with ID {id} not found in database or API");
                return View(new Product { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error occurred while retrieving product details for ID {id}: {ex.Message}");
                TempData["Error"] = $"Error occurred while retrieving product details.";
                return View(new Product { Id = id });
            }
        }

        // Helper method to prepare product for view
        private void PrepareProductForView(Product product)
        {
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
                    
                    try
                    {
                        // Database error, get products directly from API
                        var apiProducts = await _productService.GetProductsAsync();
                        if (apiProducts != null && apiProducts.Any())
                        {
                            TempData["Message"] = "Successfully retrieved data from API, but can not saved to database.";
                        }
                        else
                        {
                            TempData["Error"] = "API returned empty data";
                        }
                    }
                    catch (Exception apiEx)
                    {
                        _logger.LogError(apiEx, "Error getting products from API: {0}", apiEx.Message);
                        TempData["Error"] = $"Unable to get data from API.";
                    }
                }
                
                // Set flag to indicate we're coming from SyncProducts
                TempData["AfterSync"] = true;
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while synchronizing products: {0}", ex.Message);
                TempData["Error"] = "Error occurred while synchronizing products.";
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
                TempData["Error"] = "Error occurred while fetching products." ;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> FetchProductFromApi(int id)
        {
            try
            {
                _logger.LogInformation($"Attempting to fetch product with ID {id} directly from API");
                
                // Get product from API
                var apiProduct = await _productService.GetProductByIdAsync(id);
                
                if (apiProduct != null)
                {
                    TempData["Message"] = "Product successfully retrieved from API";
                    
                    // Try to save to database if possible
                    try
                    {
                        await _syncService.SyncProductByIdAsync(id);
                        TempData["Message"] = "Product successfully retrieved from API and saved to database";
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogWarning(dbEx, "Could not save product to database: {0}", dbEx.Message);
                        // Continue without database save
                    }
                    
                    return RedirectToAction(nameof(Details), new { id = id });
                }
                else
                {
                    TempData["Error"] = "Product not found in API";
                    return RedirectToAction(nameof(Details), new { id = id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching product from API: {0}", ex.Message);
                TempData["Error"] = $"Error occurred while fetching product from API";
                return RedirectToAction(nameof(Details), new { id = id });
            }
        }
    }
}
