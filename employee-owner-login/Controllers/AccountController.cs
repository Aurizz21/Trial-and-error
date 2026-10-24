using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;
using PoultryOS.Models.Entities;
using PoultryOS.Services;

namespace PoultryOS.Controllers;

public class AccountController : Controller
{
    private readonly AuthService authService;
    private readonly AppDbContext _db;

    public AccountController(AuthService authService, AppDbContext db)
    {
        this.authService = authService;
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = authService.ValidateCredentials(model.Username, model.Password);
        if (user is null)
        {
            try
            {
                var matchedUser = await FindUserAsync(model.Username);
                var description = matchedUser is not null
                    ? "Failed sign-in attempt."
                    : $"Failed sign-in attempt for unknown username '{SanitizeUsername(model.Username)}'.";
                await WriteAuditAsync("Login", matchedUser?.Id, description);
            }
            catch (Exception)
            {
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        try
        {
            var databaseUser = await FindUserAsync(user.Username);
            await WriteAuditAsync(
                "Login",
                databaseUser?.Id,
                model.RememberMe ? "Signed in. (remember me)" : "Signed in.");
        }
        catch (Exception)
        {
        }

        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name))
        {
            try
            {
                var user = await FindUserAsync(User.Identity.Name);
                await WriteAuditAsync("Logout", user?.Id, "Signed out.");
            }
            catch (Exception)
            {
            }
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task<User?> FindUserAsync(string username)
    {
        var normalizedUsername = username.Trim().ToLowerInvariant();
        return await _db.Users.FirstOrDefaultAsync(user =>
            user.Username.ToLower() == normalizedUsername);
    }

    private async Task WriteAuditAsync(string actionType, int? userId, string description)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.Now,
            UserId = userId,
            ActionType = actionType[..Math.Min(actionType.Length, 30)],
            Description = description[..Math.Min(description.Length, 500)]
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception)
        {
        }
    }

    private static string SanitizeUsername(string username)
    {
        var sanitized = new string(username.Trim().Where(character => !char.IsControl(character)).ToArray());
        return sanitized[..Math.Min(sanitized.Length, 50)];
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");
    }
}
