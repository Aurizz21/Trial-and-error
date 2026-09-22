# Manok ni Rene - Inventory & Forecast Platform

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
