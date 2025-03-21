using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace UCSProductGallery.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public virtual ICollection<Product>? Products { get; set; }
    }
}
