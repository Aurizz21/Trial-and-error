# PoultryOS - Inventory & Forecast Platform

A database-free ASP.NET Core MVC login demo with two fixed accounts and cookie authentication.

## Accounts

- Owner: `owner` / `owner123`
- Employee: `employee` / `employee123`

## Run

1. Install the .NET 8 SDK.
2. From this folder, run:

```powershell
dotnet run
```

3. Open the HTTPS URL shown by the command, typically `https://localhost:7xxx`.

Passwords are stored as ASP.NET Core Identity-compatible hashes in `Services/AuthService.cs`. This project has no registration, password reset, database, or external UI framework.

## Dashboard

After signing in, both accounts land on the dashboard (`/Dashboard`).

- **Owner**: everything, including restock logging and the recent-activity feed.
- **Employee**: dashboard and sales only (matches the scope in the portfolio).

What it shows: units in stock, sold today, active alerts, forecast accuracy, a "needs reordering" list with suggested reorder amounts, daily sales vs the 7-day weighted moving average, stock health, weekly use by category, a depletion forecast per product, and a searchable stock table. Sales and restocks can be recorded from the right-hand panel (a bottom sheet on phones). The page refreshes every 30 seconds.

### How the forecast works (`Services/ForecastService.cs`)

- Daily rate = 7-day weighted moving average of the last 7 complete days (oldest day weight 1, newest weight 7, divided by 28).
- Days left = stock / daily rate. Depletion date = today + days left.
- Reorder amount = 14 days of forecast demand (`SafetyBufferDays`).
- Critical: stock is at or below the owner's reorder level, or under 20% of last week's use. Warning: within 1.5x the reorder level, or forecast to run out within 7 days.
- Accuracy = 1 minus the total absolute error of the last 7 one-day forecasts, averaged over products that sold at least 7 units that week. Products with under 7 days of history show "Needs 7 days of sales".
- Every rule is a constant at the top of the file.

### Data

`Services/InMemoryInventoryStore.cs` holds demo data (the products and reorder levels from the Admin Panel screenshot, plus 28 days of generated sales). It resets when the app restarts. To move to SQL Server, implement `IInventoryStore` with EF Core and change one line in `Program.cs`.

### Files

- `Controllers/DashboardController.cs`: page, `/Dashboard/Snapshot`, `/Dashboard/Sale`, `/Dashboard/Restock`
- `Services/ForecastService.cs`, `DashboardService.cs`, `IInventoryStore.cs`, `InMemoryInventoryStore.cs`, `AppClock.cs`
- `Models/InventoryModels.cs`, `Models/DashboardModels.cs`
- `Views/Dashboard/Index.cshtml`, `wwwroot/css/dashboard.css`, `wwwroot/js/dashboard.js`
