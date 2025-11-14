using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Linq;

namespace ABCRetailers.Controllers
{
    [Authorize] // All order actions require login
    public class OrderController : Controller
    {
        private readonly IFunctionsApi _api;
        public OrderController(IFunctionsApi api) => _api = api;

        // --- LIST (Admin View: All Orders) ---
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var orders = await _api.GetOrdersAsync();
            return View(orders.OrderByDescending(o => o.OrderDateUtc).ToList());
        }

        // --- NEW: MyOrders (Customer View: Only Customer's Orders) ---
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> MyOrders()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out int customerId))
            {
                return Forbid();
            }

            var orders = await _api.GetOrdersByCustomerIdAsync(customerId);
            return View(orders.OrderByDescending(o => o.OrderDateUtc).ToList());
        }

        // --- CREATE (GET) - Admin/Staff Order Placement ---
        [Authorize(Roles = "Admin, Staff")]
        public async Task<IActionResult> Create()
        {
            var customers = await _api.GetCustomersAsync();
            var products = await _api.GetProductsAsync();

            var vm = new OrderCreateViewModel
            {
                Customers = customers,
                Products = products
            };
            return View(vm);
        }

        // --- CREATE (POST) - Admin/Staff Order Placement ---
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Staff")]
        public async Task<IActionResult> Create(OrderCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            try
            {
                // Assuming CustomerId is string in OrderCreateViewModel but API expects string ID
                var saved = await _api.CreateOrderAsync(model.CustomerId.ToString(), model.ProductId, model.Quantity);

                TempData["Success"] = $"Order {saved.Id} created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error creating order: {ex.Message}");
                await PopulateDropdowns(model);
                return View(model);
            }
        }

        // --- DETAILS (Shared View: Admin/Customer) ---
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var order = await _api.GetOrderAsync(id);

            if (order is null) return NotFound();

            // Security check: Only Admin or the Customer who owns the order can view it
            if (!User.IsInRole("Admin"))
            {
                var currentUserIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // --- ROBUST ID CHECK ---
                // 1. Get current user ID as INT
                if (!int.TryParse(currentUserIdString, out int currentUserId))
                {
                    return Forbid();
                }

                // 2. Get order owner ID as INT (Assuming order.CustomerId is int, but ToString() for safety)
                if (!int.TryParse(order.CustomerId.ToString(), out int orderCustomerId))
                {
                    return Forbid();
                }

                // 3. Compare the two INT values
                if (currentUserId != orderCustomerId)
                {
                    return Forbid();
                }
            }

            return View(order);
        }

        // --- EDIT (GET) - Admin only ---
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var order = await _api.GetOrderAsync(id);
            return order is null ? NotFound() : View(order);
        }

        // --- EDIT (POST) - Admin only ---
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Order posted)
        {
            if (posted is null || string.IsNullOrWhiteSpace(posted.Id))
            {
                ModelState.AddModelError(string.Empty, "Invalid order data.");
                return View(posted);
            }

            try
            {
                // Update only the status via the API
                await _api.UpdateOrderStatusAsync(posted.Id, posted.Status.ToString());
                TempData["Success"] = $"Order {posted.Id} status updated to {posted.Status.ToString()} successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error updating order: {ex.Message}");
                var order = await _api.GetOrderAsync(posted.Id);
                return View(order);
            }
        }

        // --- DELETE (POST) - Admin only ---
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _api.DeleteOrderAsync(id);
                TempData["Success"] = $"Order {id} deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting order: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- AJAX helper for status update (Admin only) ---
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(string id, string newStatus)
        {
            try
            {
                await _api.UpdateOrderStatusAsync(id, newStatus);
                return Json(new { success = true, message = $"Order status updated to {newStatus}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // --- Helper for dropdowns in Create View ---
        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            model.Customers = await _api.GetCustomersAsync();
            model.Products = await _api.GetProductsAsync();
        }
    }
}