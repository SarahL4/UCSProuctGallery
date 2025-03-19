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
                // 先从数据库获取产品
                var dbProducts = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Images)
                    .ToListAsync();

                // 如果数据库中没有产品，则从API获取并保存
                if (!dbProducts.Any())
                {
                    await _syncService.SyncAllProductsAsync();
                    dbProducts = await _context.Products
                        .Include(p => p.Category)
                        .Include(p => p.Images)
                        .ToListAsync();
                }

                return View(dbProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取产品列表时出错");
                TempData["Error"] = $"获取产品列表时出错: {ex.Message}";
                return View(new List<Product>());
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            // 添加日志以便调试
            _logger.LogInformation($"尝试获取ID为{id}的产品");
            
            // 确保ID有效
            if (id <= 0)
            {
                return NotFound();
            }
            
            try
            {
                // 先从数据库中获取产品
                var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == id);

                // 如果数据库中没有找到，则从API获取并保存
                if (product == null)
                {
                    _logger.LogInformation($"数据库中未找到ID为{id}的产品，将从API获取");
                    await _syncService.SyncProductByIdAsync(id);
                    
                    // 再次尝试从数据库获取
                    product = await _context.Products
                        .Include(p => p.Category)
                        .Include(p => p.Images)
                        .FirstOrDefaultAsync(p => p.Id == id);
                        
                    if (product == null)
                    {
                        _logger.LogWarning($"未找到ID为{id}的产品");
                        return NotFound();
                    }
                }
                
                // 准备视图数据
                if (product.Images != null && product.Images.Any())
                {
                    // 如果ImageUrls为null，初始化它
                    if (product.ImageUrls == null)
                    {
                        product.ImageUrls = new List<string>();
                    }
                    
                    // 将数据库中的图片URL添加到ImageUrls中，供视图使用
                    foreach (var image in product.Images)
                    {
                        if (!string.IsNullOrEmpty(image.ImageUrl) && !product.ImageUrls.Contains(image.ImageUrl))
                        {
                            product.ImageUrls.Add(image.ImageUrl);
                        }
                    }
                }

                _logger.LogInformation($"已找到产品: {product.Title}, 价格: {product.Price}, 类别: {product.CategoryName}");
                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取ID为{id}的产品详情时出错");
                TempData["Error"] = $"获取产品详情时出错: {ex.Message}";
                return View(new Product { Id = id });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SyncProducts()
        {
            try
            {
                _logger.LogInformation("开始同步所有产品数据");
                await _syncService.SyncAllProductsAsync();
                TempData["Message"] = "产品数据已成功同步";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步产品数据时出错");
                TempData["Error"] = "同步产品数据时出错: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> FetchProducts()
        {
            try
            {
                _logger.LogInformation("开始获取并同步所有产品数据");
                await _syncService.SyncAllProductsAsync();
                TempData["Message"] = "产品数据已成功获取并保存";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取产品数据时出错");
                TempData["Error"] = "获取产品数据时出错: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
