using System.Security.Claims;

namespace PoultryOS.Models;

public static class RolePermissions
{
    public const string Owner = "Owner";
    public const string Employee = "Employee";

    public const string ModuleDashboard = "Dashboard";
    public const string ModuleSalesEntry = "SalesEntry";
    public const string ModuleStockAlerts = "StockAlerts";
    public const string ModuleForecast = "Forecast";
    public const string ModuleProductManagement = "ProductManagement";
    public const string ModuleSalesHistory = "SalesHistory";
    public const string ModuleAuditTrail = "AuditTrail";
    public const string ModuleNotifications = "Notifications";

    private static readonly HashSet<string> OwnerModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ModuleDashboard,
        ModuleSalesEntry,
        ModuleStockAlerts,
        ModuleForecast,
        ModuleProductManagement,
        ModuleSalesHistory,
        ModuleAuditTrail,
        ModuleNotifications
    };

    private static readonly HashSet<string> EmployeeModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ModuleDashboard,
        ModuleSalesEntry,
        ModuleForecast,
        ModuleNotifications
    };

    public static bool CanAccess(string role, string module)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return role.Equals(Owner, StringComparison.OrdinalIgnoreCase)
            ? OwnerModules.Contains(module)
            : role.Equals(Employee, StringComparison.OrdinalIgnoreCase)
                ? EmployeeModules.Contains(module)
                : false;
    }

    public static string GetCurrentRole(ClaimsPrincipal user)
    {
        if (user.IsInRole(Owner))
        {
            return Owner;
        }

        if (user.IsInRole(Employee))
        {
            return Employee;
        }

        return user.FindFirstValue(ClaimTypes.Role) ?? Employee;
    }

    public static IReadOnlyCollection<string> GetModulesForRole(string role)
    {
        return role.Equals(Owner, StringComparison.OrdinalIgnoreCase)
            ? OwnerModules
            : role.Equals(Employee, StringComparison.OrdinalIgnoreCase)
                ? EmployeeModules
                : Array.Empty<string>();
    }
}
