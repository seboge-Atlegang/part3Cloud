using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ABCRetailers.Data;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization; // Added for [AllowAnonymous]

namespace ABCRetailers.Controllers
{
    [AllowAnonymous] // Allow access to authentication actions without being logged in
    public class LoginController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        // Constructor updated to inject Identity services
        public LoginController(UserManager<User> userManager,
                               SignInManager<User> signInManager,
                               RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        // --- Registration Actions ---

        [HttpGet]
        public IActionResult Register()
        {
            // The view will check TempData for a success message upon redirection
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Create new User object (must inherit IdentityUser)
                var user = new User
                {
                    UserName = model.Username, // Identity uses UserName property for login/lookup
                    Email = model.Email,
                    // Identity handles password hashing internally
                };

                // Use UserManager to create the user and hash the password
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // 1. Determine the role (defaulting to "Customer" if not specified)
                    string role = string.IsNullOrEmpty(model.Role) ? "Customer" : model.Role;

                    // 2. Ensure the required role exists before assigning
                    if (!await _roleManager.RoleExistsAsync(role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(role));
                    }

                    // 3. Assign the user to the role
                    await _userManager.AddToRoleAsync(user, role);

                    // Set success message for display on the Login page/view
                    TempData["SuccessMessage"] = $"Registration successful! Your account has been created and registered as a {role}. Please log in.";

                    // Redirect to the login page after successful registration
                    return RedirectToAction("Login", "Login");
                }

                // If creation failed, add errors to ModelState
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            // If validation fails or creation fails, return the view with model to show errors
            return View(model);
        }


        // --- Login Actions ---

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            // TempData messages (like the registration success message) are read here.
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                // Use SignInManager to authenticate the user
                var result = await _signInManager.PasswordSignInAsync(
                    model.Username,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false); // Set to true if account lockout is configured

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByNameAsync(model.Username);
                    var roles = await _userManager.GetRolesAsync(user);

                    // Redirect based on role (using roles obtained from Identity)
                    if (roles.Contains("Admin"))
                    {
                        return RedirectToAction("AdminDashboard", "Home");
                    }

                    // Default redirect for Customer or other users
                    return RedirectToLocal(returnUrl);
                }

                // --- Enhanced Error Handling for Login Failure ---
                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Account locked out due to multiple failed login attempts.");
                }
                else if (result.IsNotAllowed)
                {
                    ModelState.AddModelError(string.Empty, "Login not allowed. You may need to confirm your email.");
                }
                else // Covers Failed, RequiresTwoFactor, etc.
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt. Check your username and password.");
                }
            }

            // If we got this far, something failed, redisplay form with error messages
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Use SignInManager to sign out
            await _signInManager.SignOutAsync();

            return RedirectToAction("Index", "Home"); // Redirect to home page after logout
        }

        // Helper method to safely redirect to a local URL after successful login
        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                // Default action for successful login (e.g., standard customer view)
                return RedirectToAction("CustomerDashboard", "Home");
            }
        }
    }
}