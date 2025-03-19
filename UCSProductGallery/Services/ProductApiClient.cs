using System.Net.Http.Json;
using UCSProductGallery.Models;

namespace UCSProductGallery.Services
{
    public class ProductApiClient
    {
        private readonly HttpClient _httpClient;

        public ProductApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<ProductResponse>("https://dummyjson.com/products");
            return response?.Products ?? new List<Product>();
        }
    }
}
