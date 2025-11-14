using ABCRetailers.Data;
using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity; // Required for IdentityRole
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

Console.WriteLine($"Functions:BaseUrl = '{cfg["Functions:BaseUrl"]}'");
Console.WriteLine($"Grpc:Address = '{cfg["Grpc:Address"]}'");

// 1. --- DATABASE CONTEXT (Azure SQL) ---
// Add your DbContext using the connection string from appsettings.json
var connectionString = cfg.GetConnectionString("AuthDbConnection")
    ?? throw new InvalidOperationException("Connection string 'AuthDbConnection' not found.");

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. --- IDENTITY/AUTHENTICATION (CRITICAL FIX) ---
// Use AddIdentity to explicitly define User and Role types for maximum control.
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    // Set up password requirements 
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AuthDbContext>() // Link Identity to the DbContext
.AddDefaultTokenProviders(); // Required for generating tokens (e.g., password reset, email confirmation)


// 3. --- SESSION/CACHING FOR CART ---
builder.Services.AddDistributedMemoryCache();

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


// 4. --- MVC & CONTROLLERS & API CLIENTS ---
builder.Services.AddControllersWithViews();

// Typed HttpClient for your Azure Functions
builder.Services.AddHttpClient("Functions", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Functions:BaseUrl"] ?? throw new InvalidOperationException("Functions:BaseUrl missing");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/api/");
    client.Timeout = TimeSpan.FromSeconds(100);
});

// Register the API client
builder.Services.AddScoped<IFunctionsApi, FunctionsApiClient>();

// Allow larger multipart uploads (e.g., product images)
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});

builder.Services.AddLogging();


// --- BUILD APP ---
var app = builder.Build();

// ** ADD THIS BLOCK FOR SECURE IDENTITY SEEDING **
// This block runs once on startup to ensure default Admin/Customer roles and users exist.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Call the InitializeAsync method to create default roles and users
        await IdentitySeedData.InitializeAsync(services);
        Console.WriteLine("Default Identity users and roles seeded successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the Identity database.");
    }
}
// ** END IDENTITY SEEDING BLOCK **


// Culture
var culture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;


// --- REQUEST PIPELINE ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// MIDDLEWARE ORDER IS CRITICAL (Authentication must come before Authorization):
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();