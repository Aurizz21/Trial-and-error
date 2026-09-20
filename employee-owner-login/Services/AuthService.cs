using Microsoft.AspNetCore.Identity;

namespace PoultryOS.Services;

public sealed class AuthService
{
    private readonly PasswordHasher<AuthUser> passwordHasher = new();

    // The only two accounts supported by this no-database demo.
    private static readonly IReadOnlyList<AuthUser> Users = new List<AuthUser>
    {
        new("owner", "Owner", "AQAAAAEAAYagAAAAENBVJptoK2HVEbw0IGMWsZDsSvQa0rVhb9pUBKduNqEC1b+9B3SoTSDFB9vBoCuc3w=="),
        new("employee", "Employee", "AQAAAAEAAYagAAAAEEvy1hUK0cXXRnsY69v7OHrid8VsnyBCkjksWPax32ggkOIZtHoKp5QWayjjLJL+bw==")
    };

    public AuthUser? ValidateCredentials(string username, string password)
    {
        var user = Users.FirstOrDefault(candidate =>
            string.Equals(candidate.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded
            ? user
            : null;
    }
}

public sealed record AuthUser(string Username, string Role, string PasswordHash);
