using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ABCRetailers.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        [Required]
        public int CartId { get; set; }

        // Product information
        [Required]
        [StringLength(50)]
        public string ProductId { get; set; } // GUID/String ID of the Product

        [StringLength(150)]
        public string ProductName { get; set; }

        [StringLength(250)]
        public string ImageUrl { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal PriceAtTime { get; set; } // Price when added to cart

        // Navigation properties
        public Cart Cart { get; set; }

        // Helper property (Not mapped to DB)
        [NotMapped]
        public decimal Total => Quantity * PriceAtTime;
    }
}


