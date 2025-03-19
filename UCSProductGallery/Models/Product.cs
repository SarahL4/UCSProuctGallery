using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace UCSProductGallery.Models
{
    public class Product
    {
        [Key]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("category")]
        public string? CategoryName { get; set; }

        [JsonIgnore]
        [ForeignKey("Category")]
        public int? CategoryId { get; set; }

        [JsonIgnore]
        public virtual Category? Category { get; set; }

        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }

        [JsonPropertyName("images")]
        [NotMapped]
        public List<string>? ImageUrls { get; set; }

        [JsonIgnore]
        public virtual ICollection<ProductImage>? Images { get; set; }
    }

    public class Dimensions
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Depth { get; set; }
    }

    public class Review
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime Date { get; set; }
        public string? ReviewerName { get; set; }
        public string? ReviewerEmail { get; set; }
    }

    public class Meta
    {
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Barcode { get; set; }
        public string? QrCode { get; set; }
    }
}
