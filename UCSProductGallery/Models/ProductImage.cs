using System.ComponentModel.DataAnnotations;

namespace UCSProductGallery.Models
{
    public class ProductImage
    {
        [Key]
        public int Id { get; set; }
        public string? ImageUrl { get; set; }
        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }
    }
}