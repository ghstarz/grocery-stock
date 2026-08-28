# Grocery Stock

Grocery Stock is a small Windows Forms inventory application for the Milestone 2 checkpoint. It uses C# on .NET 10 and Microsoft.Data.Sqlite 10.0.12. The production project keeps domain classes, SQLite access, inventory rules, and forms in separate folders; the test project uses xUnit and temporary databases.

## Prerequisites

- Windows 10 or 11
- .NET SDK 10.0.401 or a compatible .NET 10 SDK

## Build and run

```powershell
dotnet restore GroceryStock.sln
dotnet build GroceryStock.sln
dotnet run --project src/GroceryStock/GroceryStock.csproj
```

The database is created under the current user's local application-data folder when the application is first started. It is not stored beside the executable or in the source tree.

## Tests

```powershell
dotnet test tests/GroceryStock.Tests/GroceryStock.Tests.csproj
```

The checkpoint includes catalogue management, receiving, stock out validation, perishable batches, and low-stock visibility. Dedicated expiry monitoring, category valuation reports, and CSV export are later work.
