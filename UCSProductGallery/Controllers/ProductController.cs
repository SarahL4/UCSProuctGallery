using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using UCSProductGallery.Data;
using UCSProductGallery.Models;

namespace UCSProductGallery.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
            _httpClient = new HttpClient();
        }

        public async Task<IActionResult> Index()
        {
            var products = _context.Products.ToList();
            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        public async Task<IActionResult> FetchProducts()
        {
            try
            {
                var response = await _httpClient.GetStringAsync("https://dummyjson.com/products");
                var productResponse = JsonConvert.DeserializeObject<ProductResponse>(response);

                if (productResponse?.Products != null)
                {
                    foreach (var product in productResponse.Products)
                    {
                        // Check if product already exists
                        var existingProduct = await _context.Products.FindAsync(product.Id);
                        if (existingProduct == null)
                        {
                            var category = await _context.Categories
                                .FirstOrDefaultAsync(c => c.Name == product.CategoryName);

                            if (category == null)
                            {
                                category = new Category { Name = product.CategoryName };
                                _context.Categories.Add(category);
                            }

                            product.Category = category;
                            _context.Products.Add(product);

                            // Add images
                            if (product.ImageUrls != null)
                            {
                                foreach (var imageUrl in product.ImageUrls)
                                {
                                    if (product.Images == null)
                                    {
                                        product.Images = new List<ProductImage>();
                                    }
                                    product.Images.Add(new ProductImage { ImageUrl = imageUrl });
                                }
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["Message"] = "Products fetched and saved successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error fetching products: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
