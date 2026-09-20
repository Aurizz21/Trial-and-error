using PoultryOS.Models;

namespace PoultryOS.Services;

/// <summary>
/// Thread-safe, database-free store seeded with demo data. Everything resets when the app restarts.
/// The demo catalogue and thresholds follow the Admin Panel screenshot in the project portfolio.
/// </summary>
public sealed class InMemoryInventoryStore : IInventoryStore
{
    private const int MaxMovement = 100_000;

    private readonly object gate = new();
    private readonly Dictionary<int, Product> products = new();
    private readonly List<SaleRecord> sales = new();
    private readonly List<AuditEntry> audit = new();

    public InMemoryInventoryStore()
    {
        Seed(AppClock.Now);
    }

    public InventoryData Read()
    {
        lock (gate)
        {
            return new InventoryData(
                products.Values.OrderBy(p => p.Id).ToList(),
                sales.ToList(),
                audit.ToList());
        }
    }

    public OperationResult RecordSale(int productId, int quantity, string user, DateTimeOffset at)
    {
        lock (gate)
        {
            if (!products.TryGetValue(productId, out var product))
                return new OperationResult(false, "That product no longer exists. Refresh the page.");
            if (quantity < 1)
                return new OperationResult(false, "Enter a quantity of at least 1.");
            if (quantity > product.Stock)
                return new OperationResult(false, $"Only {UnitText.Format(product.Stock, product.Unit)} left in stock.");

            var updated = product with { Stock = product.Stock - quantity };
            products[productId] = updated;
            sales.Add(new SaleRecord(productId, DateOnly.FromDateTime(at.DateTime), quantity, user, at));
            audit.Add(new AuditEntry(at, user, "Sale",
                $"Sold {UnitText.Format(quantity, product.Unit)} of {product.Name}. {UnitText.Format(updated.Stock, product.Unit)} left."));

            return new OperationResult(true, $"Sale recorded: {UnitText.Format(quantity, product.Unit)} of {product.Name}.");
        }
    }

    public OperationResult RecordRestock(int productId, int quantity, string user, DateTimeOffset at)
    {
        lock (gate)
        {
            if (!products.TryGetValue(productId, out var product))
                return new OperationResult(false, "That product no longer exists. Refresh the page.");
            if (quantity < 1 || quantity > MaxMovement)
                return new OperationResult(false, $"Enter a quantity between 1 and {MaxMovement:N0}.");

            var updated = product with { Stock = product.Stock + quantity };
            products[productId] = updated;
            audit.Add(new AuditEntry(at, user, "Restock",
                $"Added {UnitText.Format(quantity, product.Unit)} of {product.Name}. {UnitText.Format(updated.Stock, product.Unit)} in stock."));

            return new OperationResult(true, $"Restock logged: {UnitText.Format(quantity, product.Unit)} of {product.Name}.");
        }
    }

    public void AddAudit(DateTimeOffset at, string user, string action, string description)
    {
        lock (gate)
        {
            audit.Add(new AuditEntry(at, user, action, description));
        }
    }

    // ---------------------------------------------------------------------------------------
    // Demo data
    // ---------------------------------------------------------------------------------------

    private void Seed(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.DateTime);
        var trackedSince = today.AddDays(-28);

        // id, name, category, unit, stock, reorder level, typical units sold per day
        var catalogue = new (int Id, string Name, string Category, string Unit, int Stock, int Threshold, double PerDay)[]
        {
            (1, "Layer Feed (50kg)",     "Feed",        "bags",    48,  10, 3.0),
            (2, "Broiler Starter (25kg)","Feed",        "bags",     6,   8, 2.0),
            (3, "Broiler Finisher (50kg)","Feed",       "bags",     4,  10, 2.9),
            (4, "Newcastle Vaccine",     "Vaccine",     "vials",    3,   5, 0.3),
            (5, "Vitamin Supplement",    "Supplements", "kg",      14,   4, 0.45),
            (6, "Disinfectant 5L",       "Medication",  "bottles",  8,   3, 0.3),
            (7, "Drinking Nipples (pack)","Equipment",  "packs",    2,   2, 0.15),
            (8, "Egg Trays (30-cell)",   "Equipment",   "trays",  840, 100, 7.0),
        };

        // Mild weekly rhythm (busier towards the weekend) plus a repeatable wobble, so the demo
        // history looks like real sales without using random numbers.
        double[] weekday = { 0.7, 0.9, 0.95, 1.0, 1.0, 1.15, 1.3 }; // Sun..Sat

        foreach (var c in catalogue)
        {
            products[c.Id] = new Product(c.Id, c.Name, c.Category, c.Unit, c.Stock, c.Threshold, trackedSince);

            double carry = 0; // carries fractional demand forward so slow movers still sell whole units
            for (var i = 28; i >= 1; i--)
            {
                var day = today.AddDays(-i);
                var wobble = (((i * 37 + c.Id * 17) % 11) - 5) / 50.0; // -0.10 .. +0.10
                carry += c.PerDay * weekday[(int)day.DayOfWeek] * (1 + wobble);
                var qty = (int)Math.Floor(carry);
                carry -= qty;
                if (qty > 0)
                {
                    var at = new DateTimeOffset(day.ToDateTime(new TimeOnly(15, 0)), now.Offset);
                    sales.Add(new SaleRecord(c.Id, day, qty, i % 3 == 0 ? "employee" : "owner", at));
                }
            }
        }

        // A few sales already logged today.
        var earlier = now.AddMinutes(-90);
        foreach (var (id, qty, user) in new[] { (8, 6, "owner"), (1, 2, "employee"), (3, 2, "owner"), (2, 1, "employee") })
        {
            sales.Add(new SaleRecord(id, today, qty, user, earlier));
        }

        audit.Add(new AuditEntry(earlier, "owner", "Sale", "Sold 6 trays of Egg Trays (30-cell)."));
        audit.Add(new AuditEntry(earlier, "employee", "Sale", "Sold 2 bags of Layer Feed (50kg)."));
    }
}
