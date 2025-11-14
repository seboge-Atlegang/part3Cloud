using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ABCRetailers.Data;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services; // <-- REQUIRED for IFunctionsApi
using System.Security.Claims;

namespace ABCRetailers.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CartController : Controller
    {
        private readonly AuthDbContext _authContext;
        private readonly ILogger<CartController> _logger;
        private readonly IFunctionsApi _api; // <-- New dependency

        // --- CONSTRUCTOR UPDATED ---
        // We now inject IFunctionsApi instead of the two controllers
        public CartController(AuthDbContext authContext, ILogger<CartController> logger, IFunctionsApi api)
        {
            _authContext = authContext;
            _logger = logger;
            _api = api; // Assign the API service
        }

        private async Task<Cart> GetOrCreateUserCartAsync(int customerId)
        {
            var cart = await _authContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                cart = new Cart { CustomerId = customerId };
                _authContext.Carts.Add(cart);
                await _authContext.SaveChangesAsync();
            }
            return cart;
        }

        private int GetCurrentCustomerId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Safety check for null/invalid ID, although Authorize should handle it
            if (string.IsNullOrEmpty(userIdClaim))
                throw new InvalidOperationException("Customer ID not found.");

            return int.Parse(userIdClaim);
        }

        // --- View Cart ---
        public async Task<IActionResult> Index()
        {
            int customerId = GetCurrentCustomerId();
            var cart = await GetOrCreateUserCartAsync(customerId);

            var viewModel = cart.Items.Select(item => new CartItemViewModel
            {
                CartItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ImageUrl = item.ImageUrl,
                UnitPrice = item.PriceAtTime,
                Quantity = item.Quantity,
                TotalPrice = item.Total
            }).ToList();

            ViewBag.CartTotal = viewModel.Sum(i => i.TotalPrice);
            return View(viewModel);
        }

        // --- Add to Cart (Called from Product/Index or Details) ---
        [HttpPost]
        public async Task<IActionResult> AddToCart(string productId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(productId)) return BadRequest("Product ID is required.");
            if (quantity < 1) return BadRequest("Quantity must be at least 1.");

            int customerId = GetCurrentCustomerId();
            var cart = await GetOrCreateUserCartAsync(customerId);

            // --- FIX: Use IFunctionsApi to fetch product details ---
            var product = await _api.GetProductAsync(productId);

            if (product == null) return NotFound("Product not found.");
            if (product.StockAvailable < quantity) return BadRequest("Insufficient stock.");

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = productId,
                    ProductName = product.ProductName,
                    ImageUrl = product.ImageUrl,
                    PriceAtTime = product.Price,
                    Quantity = quantity,
                    CartId = cart.Id
                });
            }

            await _authContext.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{quantity} x {product.ProductName} added to cart.";
            return RedirectToAction("Index", "Cart");
        }

        // --- Update Quantity (AJAX/Form Post) ---
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int newQuantity)
        {
            var item = await _authContext.CartItems
                                         .Include(ci => ci.Cart)
                                         .FirstOrDefaultAsync(ci => ci.Id == cartItemId);

            if (item == null || item.Cart.CustomerId != GetCurrentCustomerId())
                return NotFound();

            if (newQuantity <= 0)
            {
                _authContext.CartItems.Remove(item);
                TempData["SuccessMessage"] = "Item removed from cart.";
            }
            else
            {
                // Basic stock check using IFunctionsApi
                var product = await _api.GetProductAsync(item.ProductId);
                if (product != null && product.StockAvailable < newQuantity)
                    return BadRequest("Insufficient stock for this quantity.");

                item.Quantity = newQuantity;
                TempData["SuccessMessage"] = "Cart updated.";
            }

            await _authContext.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // --- Remove Item ---
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            var item = await _authContext.CartItems
                                         .Include(ci => ci.Cart)
                                         .FirstOrDefaultAsync(ci => ci.Id == cartItemId);

            if (item == null || item.Cart.CustomerId != GetCurrentCustomerId())
                return NotFound();

            _authContext.CartItems.Remove(item);
            await _authContext.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Item removed from cart.";
            return RedirectToAction("Index");
        }

        // --- Checkout / Place Order ---
        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            int customerId = GetCurrentCustomerId();
            var cart = await GetOrCreateUserCartAsync(customerId);

            if (!cart.Items.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction("Index");
            }

            // 1. Convert CartItems to a list of DTOs needed by the API/Function
            var cartItemsToOrder = cart.Items.Select(ci => new CartItemToOrderDto
            {
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                PriceAtTime = ci.PriceAtTime,
                ProductName = ci.ProductName
            }).ToList();

            string newOrderId;
            try
            {
                // --- FIX: Use IFunctionsApi to create the order ---
                newOrderId = await _api.CreateOrderFromCartAsync(customerId, cartItemsToOrder);

                // 2. Clear the cart only if the order creation was successful
                _authContext.CartItems.RemoveRange(cart.Items);
                await _authContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // This handles stock errors, API errors, etc.
                _logger.LogError(ex, "Checkout failed for customer {CustomerId}", customerId);
                TempData["ErrorMessage"] = $"Order failed: {ex.Message}. Please check your items.";
                return RedirectToAction("Index");
            }

            // 3. Redirect to Confirmation
            return RedirectToAction("Confirmation", new { orderId = newOrderId });
        }

        // --- Confirmation View ---
        public IActionResult Confirmation(string orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }
    }
}