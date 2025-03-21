using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using UCSProductGallery.Data;
using UCSProductGallery.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Register services
builder.Services.AddHttpClient<ProductApiClient>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ProductSyncService>();
builder.Services.AddTransient<IProductService, ProductApiClient>();

var app = builder.Build();

// Initialize database and load initial data
InitializeDatabaseAsync(app).GetAwaiter().GetResult();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Product}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Database initialization method
async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var serviceProvider = scope.ServiceProvider;
    var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Check if database needs to be created or migrated
        bool isNewDatabase = !dbContext.Database.CanConnect();
        
        // Apply pending migrations and create database if needed
        logger.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");
        
        // Fetch initial data for new or empty database
        if (isNewDatabase || !await dbContext.Products.AnyAsync())
        {
            logger.LogInformation("New or empty database detected. Fetching initial product data from API...");
            
            var syncService = serviceProvider.GetRequiredService<ProductSyncService>();
            await syncService.SyncAllProductsAsync();
            
            logger.LogInformation("Initial product data has been successfully fetched and saved to database");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while setting up the database");
        // Only rethrow in development environment to prevent application crash in production
        if (app.Environment.IsDevelopment())
        {
            throw;
        }
    }
}
