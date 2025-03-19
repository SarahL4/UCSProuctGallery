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
        /// 从API同步所有产品到数据库
        /// </summary>
        public async Task SyncAllProductsAsync()
        {
            try
            {
                // 从API获取产品
                var apiProducts = await _productService.GetProductsAsync();
                if (apiProducts == null || !apiProducts.Any())
                {
                    _logger.LogWarning("API没有返回任何产品数据");
                    return;
                }

                _logger.LogInformation($"从API获取到{apiProducts.Count}个产品");

                // 同步产品类别
                await SyncCategoriesAsync(apiProducts);

                // 同步产品数据
                await SyncProductsAsync(apiProducts);

                _logger.LogInformation("产品同步完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步产品时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 从API同步单个产品到数据库
        /// </summary>
        public async Task SyncProductByIdAsync(int id)
        {
            try
            {
                // 从API获取产品
                var apiProduct = await _productService.GetProductByIdAsync(id);
                if (apiProduct == null || apiProduct.Id == 0)
                {
                    _logger.LogWarning($"API没有返回ID为{id}的产品数据");
                    return;
                }

                // 将单个产品添加到列表中，以便复用同步逻辑
                var apiProducts = new List<Product> { apiProduct };

                // 同步产品类别
                await SyncCategoriesAsync(apiProducts);

                // 同步产品数据
                await SyncProductsAsync(apiProducts);

                _logger.LogInformation($"产品 {id} 同步完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"同步产品 {id} 时发生错误");
                throw;
            }
        }

        private async Task SyncCategoriesAsync(List<Product> apiProducts)
        {
            // 获取API产品中的所有唯一类别名称
            var categoryNames = apiProducts
                .Where(p => !string.IsNullOrEmpty(p.CategoryName))
                .Select(p => p.CategoryName)
                .Distinct()
                .ToList();

            // 获取数据库中已存在的类别
            var existingCategories = await _dbContext.Categories
                .Where(c => categoryNames.Contains(c.Name))
                .ToListAsync();

            // 找出需要新添加的类别
            var existingCategoryNames = existingCategories.Select(c => c.Name).ToList();
            var newCategoryNames = categoryNames
                .Where(name => !existingCategoryNames.Contains(name))
                .ToList();

            // 添加新类别到数据库
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
                _logger.LogInformation($"添加了{newCategoryNames.Count}个新类别");
            }
        }

        private async Task SyncProductsAsync(List<Product> apiProducts)
        {
            // 获取数据库中的所有类别（用于后续的外键映射）
            var allCategories = await _dbContext.Categories.ToListAsync();
            var categoryDict = allCategories.ToDictionary(c => c.Name, c => c.Id);

            // 获取已存在的产品列表（根据标题匹配）
            var existingProducts = await _dbContext.Products
                .Include(p => p.Images)
                .ToListAsync();
            
            var existingProductDict = existingProducts.ToDictionary(p => p.Title?.ToLower() ?? string.Empty, p => p);

            // 处理每个API产品
            foreach (var apiProduct in apiProducts)
            {
                try
                {
                    // 设置类别ID（外键）
                    if (!string.IsNullOrEmpty(apiProduct.CategoryName) && 
                        categoryDict.ContainsKey(apiProduct.CategoryName))
                    {
                        apiProduct.CategoryId = categoryDict[apiProduct.CategoryName];
                    }

                    // 保存原始API产品ID用于日志记录
                    int apiProductId = apiProduct.Id;

                    // 检查产品是否已存在（通过标题匹配）
                    string productTitle = apiProduct.Title?.ToLower() ?? string.Empty;
                    Product dbProduct = null;
                    
                    if (!string.IsNullOrEmpty(productTitle) && existingProductDict.ContainsKey(productTitle))
                    {
                        // 找到现有产品，更新它
                        dbProduct = existingProductDict[productTitle];
                        
                        // 不更新ID，保留数据库ID
                        int dbProductId = dbProduct.Id;
                        
                        // 复制API产品属性到数据库产品
                        _dbContext.Entry(dbProduct).CurrentValues.SetValues(apiProduct);
                        
                        // 恢复数据库ID
                        dbProduct.Id = dbProductId;
                        
                        _logger.LogInformation($"更新现有产品: {dbProduct.Title} (API ID: {apiProductId}, DB ID: {dbProductId})");
                    }
                    else
                    {
                        // 新产品，需要创建一个新的实例并清除ID，让数据库生成
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
                        _logger.LogInformation($"添加新产品: {dbProduct.Title} (API ID: {apiProductId})");
                    }

                    // 保存更改以获取数据库生成的ID（如果是新产品）
                    await _dbContext.SaveChangesAsync();

                    // 同步产品图片（使用数据库产品ID）
                    await SyncProductImagesAsync(dbProduct, apiProduct.ImageUrls, apiProduct.Thumbnail);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"同步产品 {apiProduct.Id} 时发生错误");
                    // 继续处理下一个产品
                }
            }
        }

        private async Task SyncProductImagesAsync(Product dbProduct, List<string> imageUrls, string thumbnail)
        {
            // 如果产品没有图片URL，则跳过
            if ((imageUrls == null || !imageUrls.Any()) && string.IsNullOrEmpty(thumbnail))
            {
                return;
            }

            // 获取现有的产品图片
            var existingImages = await _dbContext.ProductImages
                .Where(pi => pi.ProductId == dbProduct.Id)
                .ToListAsync();

            // 删除现有图片
            if (existingImages.Any())
            {
                _dbContext.ProductImages.RemoveRange(existingImages);
            }

            // 添加新图片
            var newImages = new List<ProductImage>();
            bool isFirstImage = true;

            // 添加来自API的图片URL
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
                            IsMain = isFirstImage // 第一张图片设为主图
                        });
                        isFirstImage = false;
                    }
                }
            }

            // 如果有缩略图但不在ImageUrls中，也添加
            if (!string.IsNullOrEmpty(thumbnail) && 
                (imageUrls == null || !imageUrls.Contains(thumbnail)))
            {
                newImages.Add(new ProductImage
                {
                    ProductId = dbProduct.Id,
                    ImageUrl = thumbnail,
                    IsMain = !newImages.Any() // 如果没有其他图片，则设置为主图
                });
            }

            if (newImages.Any())
            {
                await _dbContext.ProductImages.AddRangeAsync(newImages);
                _logger.LogInformation($"为产品 {dbProduct.Id} 添加了 {newImages.Count} 张图片");
                await _dbContext.SaveChangesAsync();
            }
        }
    }
} 