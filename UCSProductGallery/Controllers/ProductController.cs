using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using UCSProductGallery.Services;
using UCSProductGallery.Models;

namespace UCSProductGallery.Controllers
{
    public class ProductController : Controller
    {
        private readonly ProductApiClient _productApiClient;

        public ProductController(ProductApiClient productApiClient)
        {
            _productApiClient = productApiClient;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _productApiClient.GetProductsAsync();
            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var products = await _productApiClient.GetProductsAsync();
            var product = products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }
    }
}
