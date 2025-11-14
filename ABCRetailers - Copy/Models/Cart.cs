using System.ComponentModel.DataAnnotations;

namespace ABCRetailers.Models
{
    public class Cart
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; } // Foreign key to the User table

        public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public User Customer { get; set; }
        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}


