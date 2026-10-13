using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;

namespace PoultryOS.Services;

public sealed class AuthService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<AuthUser> passwordHasher = new();

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public AuthUser? ValidateCredentials(string username, string password)
    {
        var normalizedUsername = username.Trim().ToLowerInvariant();
        var user = _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.IsActive && candidate.Username.ToLower() == normalizedUsername)
            .GetAwaiter()
            .GetResult();

        if (user is null)
        {
            return null;
        }

        var authUser = new AuthUser(user.Username, user.Role, user.PasswordHash);
        var result = passwordHasher.VerifyHashedPassword(authUser, authUser.PasswordHash, password);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded
            ? authUser
            : null;
    }
}

public sealed record AuthUser(string Username, string Role, string PasswordHash);
