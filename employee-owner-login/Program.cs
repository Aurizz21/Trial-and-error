using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;                    // ← NEW
using PoultryOS.Data;                                   // ← NEW
using PoultryOS.Services;

var builder = WebApplication.CreateBuilder(args);

// Register MVC and the in-memory authentication and inventory services.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<IInventoryService, InventoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISalesHistoryService, SalesHistoryService>();
builder.Services.AddScoped<IStockAlertsService, StockAlertsService>();
builder.Services.AddScoped<INotificationsService, NotificationsService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISalesEntryService, SalesEntryService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IForecastService, ForecastService>();

// Register the EF Core DbContext against SQL Server.   // ← NEW
builder.Services.AddDbContext<AppDbContext>(options =>  // ← NEW
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));  // ← NEW

// Use a secure cookie for the authenticated session.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "PoultryOS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    SeedData.Initialize(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Account/Login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Account/Login is the default entry point for the application.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();