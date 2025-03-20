using UCSProductGallery.Models;

namespace UCSProductGallery.Services
{
    public interface IProductService
    {
        Task<List<Product>> GetProductsAsync();
        Task<Product> GetProductByIdAsync(int id);
    }
}