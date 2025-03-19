using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using UCSProductGallery.Models;
using Microsoft.Extensions.Logging;

namespace UCSProductGallery.Services
{
    public class ProductApiClient : IProductService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductApiClient> _logger;

        public ProductApiClient(HttpClient httpClient, ILogger<ProductApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            try 
            {
                _logger.LogInformation("正在从API获取所有产品");
                var response = await _httpClient.GetFromJsonAsync<ProductResponse>("https://dummyjson.com/products");
                if (response?.Products != null)
                {
                    _logger.LogInformation($"成功获取到{response.Products.Count}个产品");
                }
                return response?.Products ?? new List<Product>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取产品列表时出错");
                return new List<Product>();
            }
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation($"正在从API获取ID为{id}的产品");
                // dummyjson.com API单个产品不使用products数组，而是直接返回产品对象
                var product = await _httpClient.GetFromJsonAsync<Product>($"https://dummyjson.com/products/{id}");
                if (product != null)
                {
                    _logger.LogInformation($"成功获取到产品: {product.Title}");
                }
                return product ?? new Product { Id = id };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取ID为{id}的产品时出错");
                return new Product { Id = id };
            }
        }
    }
}
