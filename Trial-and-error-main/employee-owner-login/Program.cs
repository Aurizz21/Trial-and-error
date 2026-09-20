using Microsoft.AspNetCore.Authentication.Cookies;
using PoultryOS.Services;

var builder = WebApplication.CreateBuilder(args);

// Register MVC and the single in-memory authentication service.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<AuthService>();

// Dashboard: in-memory demo data + the 7-day weighted moving average forecast.
// Replace InMemoryInventoryStore with a SQL Server-backed IInventoryStore when the database is ready.
builder.Services.AddSingleton<IInventoryStore, InMemoryInventoryStore>();
builder.Services.AddSingleton<ForecastService>();
builder.Services.AddSingleton<DashboardService>();

// Use a secure cookie for the authenticated session.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "PoultryOS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

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
