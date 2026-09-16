# Grocery Stock

Grocery Stock is a small Windows Forms inventory application for the Milestone 2 checkpoint. It uses C# on .NET 10 and Microsoft.Data.Sqlite 10.0.12. The production project keeps domain classes, SQLite access, inventory rules, and forms in separate folders; the test project uses xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, and isolated temporary databases.

## Prerequisites

- Windows 10 or 11
- .NET SDK 10.0.401 or a compatible .NET 10 SDK

## Build and run

```powershell
dotnet restore GroceryStock.sln
dotnet build GroceryStock.sln
dotnet run --project src/GroceryStock/GroceryStock.csproj
```

The database is created at `%LOCALAPPDATA%\GroceryStock\grocery-stock.db` when the application is first started. It is not stored beside the executable or in the source tree. SQLite foreign keys are enabled for every connection, and catalogue prices are stored as integer cents.

## Tests

```powershell
dotnet test tests/GroceryStock.Tests/GroceryStock.Tests.csproj
```

The checkpoint includes catalogue add/edit/retire, name/code search, category and status filters, numeric quantity sorting, supplier-linked receiving, perishable batches, stock-out reasons with balance checks, and low-stock visibility. Quantities are whole units; the application does not support fractional bulk weights. Dedicated expiry monitoring, category valuation reports, and CSV export are later work.

Supplier names entered in the receive form are saved to the supplier directory before delivery validation; a failed delivery therefore leaves the supplier entry but never leaves a partial batch or movement.
