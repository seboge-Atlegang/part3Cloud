using ABCRetailers.Controllers;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels; // Required for CartItemToOrderDto

namespace ABCRetailers.Services;

public interface IFunctionsApi
{
    // Customers
    Task<List<Customer>> GetCustomersAsync();
    Task<Customer?> GetCustomerAsync(string id);
    Task<Customer> CreateCustomerAsync(Customer c);
    Task<Customer> UpdateCustomerAsync(string id, Customer c);
    Task DeleteCustomerAsync(string id);

    // Products
    Task<List<Product>> GetProductsAsync();
    Task<Product?> GetProductAsync(string id);
    Task<Product> CreateProductAsync(Product p, IFormFile? imageFile);
    Task<Product> UpdateProductAsync(string id, Product p, IFormFile? imageFile);
    Task DeleteProductAsync(string id);

    // Orders
    Task<List<Order>> GetOrdersAsync();
    Task<Order?> GetOrderAsync(string id);
    Task<Order> CreateOrderAsync(string customerId, string productId, int quantity);
    Task UpdateOrderStatusAsync(string id, string newStatus);
    Task DeleteOrderAsync(string id);

    // --- NEW METHODS FOR CART/CHECKOUT ---

    // 1. Used by CartController's Checkout action to convert the cart into an order
    Task<string> CreateOrderFromCartAsync(int customerId, List<CartItemToOrderDto> cartItems);

    // 2. Used by OrderController's MyOrders action to fetch customer-specific orders
    Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(int customerId);

    // Uploads
    Task<string> UploadProofOfPaymentAsync(IFormFile file, string? orderId, string? customerName);
}