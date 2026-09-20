namespace PoultryOS.Models;

// ---- Domain records (what the store keeps) -------------------------------------------------
// These mirror the ERD: PoultryProduct, SalesTransaction and AuditTrail.

public sealed record Product(
    int Id,
    string Name,
    string Category,
    string Unit,             // plural label shown next to quantities: "bags", "vials", "kg"
    int Stock,
    int ReorderThreshold,    // set by the owner in the Admin Panel
    DateOnly TrackedSince);  // first day the product was tracked; < 7 days means "insufficient data"

public sealed record SaleRecord(int ProductId, DateOnly Date, int Quantity, string User, DateTimeOffset Timestamp);

public sealed record AuditEntry(DateTimeOffset Timestamp, string User, string Action, string Description);

public sealed record InventoryData(
    IReadOnlyList<Product> Products,
    IReadOnlyList<SaleRecord> Sales,
    IReadOnlyList<AuditEntry> Audit);

public sealed record OperationResult(bool Ok, string Message);

public static class StockStatus
{
    public const string Critical = "critical";
    public const string Warning = "warning";
    public const string Healthy = "healthy";

    // Lower number = more urgent. Used for sorting and for detecting "got worse" transitions.
    public static int Severity(string status) => status switch
    {
        Critical => 0,
        Warning => 1,
        _ => 2
    };
}

public static class UnitText
{
    // "1 bags" reads badly, so singularise plural unit labels when the quantity is exactly 1.
    public static string Format(int quantity, string unit) =>
        $"{quantity:N0} {(quantity == 1 && unit.EndsWith('s') ? unit[..^1] : unit)}";
}
