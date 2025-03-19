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
                _logger.LogInformation("Fetching all products from API");
                var response = await _httpClient.GetFromJsonAsync<ProductResponse>("https://dummyjson.com/products");
                if (response?.Products != null)
                {
                    _logger.LogInformation($"Successfully retrieved {response.Products.Count} products");
                }
                return response?.Products ?? new List<Product>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving product list");
                return new List<Product>();
            }
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation($"Fetching product with ID {id} from API");
                // dummyjson.com API returns the product object directly, not in a products array
                var product = await _httpClient.GetFromJsonAsync<Product>($"https://dummyjson.com/products/{id}");
                if (product != null)
                {
                    _logger.LogInformation($"Successfully retrieved product: {product.Title}");
                }
                return product ?? new Product { Id = id };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while retrieving product with ID {id}");
                return new Product { Id = id };
            }
        }
    }
}
