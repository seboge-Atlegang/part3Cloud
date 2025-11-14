using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq; // Required for .Select() in error logging
using ABCRetailers.Models; // Assuming your custom User class is in this namespace

namespace ABCRetailers.Services
{
    // Static class to encapsulate identity seeding logic
    public static class IdentitySeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            // Resolve required services using Dependency Injection
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            // We use the generic ILogger here, but resolve the correct type
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            const string defaultPassword = "P@sswOrd123!";
            const string adminEmail = "admin@abcretailers.com";
            const string customerEmail = "testuser@abcretailers.com";

            logger.LogInformation("Starting Identity database seeding...");

            // 1. Ensure Roles Exist
            await EnsureRoleAsync(roleManager, "Admin", logger);
            await EnsureRoleAsync(roleManager, "Customer", logger);

            // 2. Ensure Default Users Exist and are Assigned Roles
            // Create Admin user
            await EnsureUserAndRoleAsync(userManager, adminEmail, "Admin", defaultPassword, logger);

            // Create Customer user
            await EnsureUserAndRoleAsync(userManager, customerEmail, "Customer", defaultPassword, logger);

            logger.LogInformation("Identity database seeding completed.");
        }

        // Helper function to create a role if it doesn't exist
        private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName, ILogger logger)
        {
            if (await roleManager.FindByNameAsync(roleName) == null)
            {
                logger.LogInformation("Creating role: {RoleName}", roleName);
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    logger.LogError("Failed to create role {RoleName}: {Errors}", roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // Helper function to create a user and assign them to a role
        private static async Task EnsureUserAndRoleAsync(UserManager<User> userManager, string email, string roleName, string password, ILogger logger)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                logger.LogInformation("Creating user: {Email}", email);
                user = new User
                {
                    UserName = email, // Set UserName to Email for simple login
                    Email = email,
                    EmailConfirmed = true,
                };

                // Securely hash the password and create the user
                var result = await userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    // Assign the user to the role using official Identity method
                    logger.LogInformation("User created. Assigning role {RoleName} to {Email}", roleName, email);
                    await userManager.AddToRoleAsync(user, roleName);
                }
                else
                {
                    logger.LogError("Failed to create user {Email}: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                // Ensure existing user has the correct role
                if (!await userManager.IsInRoleAsync(user, roleName))
                {
                    logger.LogInformation("User {Email} exists but is missing role {RoleName}. Assigning role.", email, roleName);
                    await userManager.AddToRoleAsync(user, roleName);
                }
            }
        }
    }
}