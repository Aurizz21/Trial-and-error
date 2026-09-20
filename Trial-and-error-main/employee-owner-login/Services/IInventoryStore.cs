using PoultryOS.Models;

namespace PoultryOS.Services;

/// <summary>
/// The only thing the dashboard needs from storage. Today it is backed by memory;
/// when the SQL Server tables from the ERD exist, add an EF Core implementation of this
/// interface and swap the registration in Program.cs. Nothing else has to change.
/// </summary>
public interface IInventoryStore
{
    /// <summary>A consistent copy of products, sales and audit entries.</summary>
    InventoryData Read();

    OperationResult RecordSale(int productId, int quantity, string user, DateTimeOffset at);

    OperationResult RecordRestock(int productId, int quantity, string user, DateTimeOffset at);

    void AddAudit(DateTimeOffset at, string user, string action, string description);
}
