using Metro.Core.Entities;
using Metro.Core.Interfaces;
using Metro.Core.Services;
using Metro.Data;
using Metro.Data.Repositories;
using Metro.Data.Seed;
using Metro.Data.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

#region Services

// MVC
builder.Services.AddControllersWithViews();

// DbContext (Use Pooling for better performance under load)
builder.Services.AddDbContextPool<MetroDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Dependency Injection (Repository Registration Example)
// IMPORTANT: Web wires abstractions to implementations
builder.Services.AddScoped<IStationRepository, StationRepository>();
builder.Services.AddScoped<ILineRepository, LineRepository>();
builder.Services.AddScoped<IStationConnectionRepository, StationConnectionRepository>();
builder.Services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IGraphBuilder, GraphBuilder>();
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IPricingService , PricingService>();
builder.Services.AddScoped<ITravelTimeService, TravelTimeService>();
builder.Services.AddScoped<ITransferDetectionService , TransferDetectionService>();
builder.Services.AddScoped<IMetroService, MetroService>();
#endregion

var app = builder.Build();



#region Middleware Pipeline

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


//Seed the database with initial data (uncomment if needed)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MetroDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await MetroDataSeeder.SeedAsync(context, logger);
}

#endregion

app.Run();
