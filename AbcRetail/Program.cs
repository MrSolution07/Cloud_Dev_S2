using System.Globalization;
using AbcRetail.Models;
using AbcRetail.Options;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// DI: Azure Storage options + four storage services (Tables, Blobs, Queues, Files).
builder.Services.AddControllersWithViews();
builder.Services.Configure<AzureStorageOptions>(
    builder.Configuration.GetSection(AzureStorageOptions.SectionName));

builder.Services.AddSingleton<IAzureStorageGate, AzureStorageGate>();
builder.Services.AddSingleton<ITableStorageService, TableStorageService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IQueueStorageService, QueueStorageService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.Configure<AzureFunctionsOptions>(
    builder.Configuration.GetSection(AzureFunctionsOptions.SectionName));
builder.Services.AddHttpClient("AzureFunctions", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<AzureFunctionsOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
    {
        var baseUrl = opts.BaseUrl.TrimEnd('/') + "/";
        client.BaseAddress = new Uri(baseUrl);
    }

    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<IFunctionGateway, FunctionGateway>();
builder.Services.AddSingleton<ICartService, CartService>();

// Cookie authentication: Customer vs Admin role drives which pages/nav items are visible.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Show product prices in ZAR (R). Based on en-US (period decimal separator) rather than en-ZA
// (comma decimal separator) because en-ZA breaks numeric model binding: HTML number inputs
// always post "1999.99" with a period, which en-ZA's NumberFormat rejects as invalid.
var za = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
za.NumberFormat.CurrencySymbol = "R";
CultureInfo.DefaultThreadCurrentCulture = za;
CultureInfo.DefaultThreadCurrentUICulture = za;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Seed a default Admin login (local coursework use — see README for credentials).
var gate = app.Services.GetRequiredService<IAzureStorageGate>();
if (gate.IsConfigured)
{
    var tables = app.Services.GetRequiredService<ITableStorageService>();
    var existingAdmin = await tables.GetUserByEmailAsync("admin@abcretail.local");
    if (existingAdmin is null)
    {
        var admin = new CustomerEntity
        {
            FirstName = "ABC",
            LastName = "Admin",
            Email = "admin@abcretail.local",
            Role = CustomerEntity.RoleAdmin
        };
        admin.PasswordHash = new PasswordHasher<CustomerEntity>().HashPassword(admin, "Admin@12345");
        await tables.AddCustomerAsync(admin);
    }
}

app.Run();
