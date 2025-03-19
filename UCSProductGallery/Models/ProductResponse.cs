using System.Text.Json.Serialization;

namespace UCSProductGallery.Models
{
    public class ProductResponse
    {
        [JsonPropertyName("products")]
        public List<Product>? Products { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("skip")]
        public int Skip { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }
    }
}
