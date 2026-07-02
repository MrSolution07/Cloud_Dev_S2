using System.Globalization;
using AbcRetail.Options;
using AbcRetail.Services;

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

var app = builder.Build();

// Show product prices in South African Rand (R / ZAR).
var za = new CultureInfo("en-ZA");
CultureInfo.DefaultThreadCurrentCulture = za;
CultureInfo.DefaultThreadCurrentUICulture = za;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
