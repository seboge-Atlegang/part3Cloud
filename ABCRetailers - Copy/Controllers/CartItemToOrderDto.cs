using System.ComponentModel.DataAnnotations.Schema;

namespace ABCRetailers.Controllers
{
    public class CartItemToOrderDto
    {


        public string ProductId { get; set; }
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18, 2)")] // Optional: ensures type consistency
        public decimal PriceAtTime { get; set; }

        public string ProductName { get; set; }
        
    }
}