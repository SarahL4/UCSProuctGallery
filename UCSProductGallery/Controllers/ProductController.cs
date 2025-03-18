using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UCSProductGallery.Services;
using UCSProductGallery.Models;

namespace UCSProductGallery.Controllers
{
    public class ProductController : Controller
    {
        private readonly ProductApiClient _productApiClient;
        private readonly HttpClient _httpClient;

        public ProductController(ProductApiClient productApiClient)
        {
            _productApiClient = productApiClient;
            _httpClient = new HttpClient();
        }

        public async Task<IActionResult> Index()
        {
            var products = await _productApiClient.GetProductsAsync();
            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var response = await _httpClient.GetAsync($"https://dummyjson.com/products/{id}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var product = JsonConvert.DeserializeObject<Product>(json);

            ViewBag.Product = product;
            return View();
        }
    }

    public class Product
    {
        public int id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public decimal price { get; set; }
        public double discountPercentage { get; set; }
        public double rating { get; set; }
        public int stock { get; set; }
        public string brand { get; set; }
        public string category { get; set; }
        public string thumbnail { get; set; }
        public string[] images { get; set; }
    }
}
