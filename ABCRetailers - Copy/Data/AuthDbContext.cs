using Microsoft.EntityFrameworkCore;
using ABCRetailers.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using static ABCRetailers.Models.Order;

namespace ABCRetailers.Data
{
    // CRITICAL FIX: Inherit from IdentityDbContext to ensure all Identity tables are created.
    public class AuthDbContext : IdentityDbContext<User, IdentityRole, string>
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        // --- Non-Identity Database Tables ---
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // CRITICAL: Must call the base method FIRST to configure default Identity tables.
            base.OnModelCreating(modelBuilder);

            // Explicitly setting keys for custom non-Identity tables if needed.
            modelBuilder.Entity<Order>().HasKey(o => o.Id);
            modelBuilder.Entity<OrderItem>().HasKey(oi => oi.Id);
        }
    }
}