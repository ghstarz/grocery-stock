# Grocery Stock

Grocery Stock is a Windows desktop stock register for a small independent grocery. This is the Assessment C Milestone 3 build, extending the catalogue and stock movement work from Milestone 2 with expiry monitoring, reorder and stock value reports, and CSV export.

## Requirements

- Windows 10 or 11
- .NET 10 SDK (the project was built with SDK 10.0.401)

## Run

From this directory:

```powershell
dotnet restore GroceryStock.sln
dotnet build GroceryStock.sln -c Release
dotnet run --project src/GroceryStock/GroceryStock.csproj
```

The normal database is created at `%LOCALAPPDATA%\GroceryStock\grocery-stock.db`. To run a separate demonstration copy without touching that database, provide a different absolute path:

```powershell
dotnet run --project src/GroceryStock/GroceryStock.csproj -- --demo-database "$env:TEMP\grocery-stock-demo.db"
```

## Features

- Add, edit, search, filter, sort, and retire catalogue items. Items use a standard or perishable type; perishable items keep batch and expiry details.
- Record supplier-linked receipts and reason-coded stock-outs. Invalid deliveries and issues are rejected before a partial movement can be stored.
- See active, inactive, low-stock, and quantity-sorted catalogue views.
- Review perishable batches that are already expired or expire within a selectable number of days (default seven). Depleted batches and standard items do not appear in the expiry report.
- Review active items at or below their reorder level, including items with no stock movements.
- Compare on-hand value by category. The calculation is **current catalogue cost × quantity on hand**; it is not FIFO or weighted-average costing.
- Export the current catalogue view, including its applied search, category, status, low-stock, and sort filters, to UTF-8 CSV. Fields are quoted, numbers use invariant formatting, and formula-leading text is prefixed for spreadsheet safety. The export uses a temporary file and replaces the destination only after a successful write.

Quantities are whole units; fractional bulk weights are outside the scope of this milestone. The stock value report is a current-cost estimate and does not reconstruct historical purchase costs.

## Test

```powershell
dotnet test tests/GroceryStock.Tests/GroceryStock.Tests.csproj -c Release
```

The xUnit suite uses isolated temporary SQLite databases and covers catalogue rules, receiving and stock-out validation, expiry boundaries, reorder thresholds, current-cost valuation, and CSV quoting/formatting/replacement behaviour.

## Design and data notes

The UI calls `InventoryService` and `ReportService`; those services apply application rules and delegate data access to repositories. `StandardItem` and `PerishableItem` share the `StockItem` base type, while `MovementRepository` writes the stock movement ledger transactionally. SQLite stores prices in integer cents. The report valuation uses grouped movement balances so that batch and movement joins cannot multiply a stock quantity.

CSV opens in spreadsheet software, but automatic type inference can remove leading zeroes from item codes. For code-preserving review in Excel, import the file through **Data → From Text/CSV** and set **Item code** to Text before loading. The source CSV remains unchanged.

## Prior milestone

The proposal began as **“Stock Register: a stock control application for a small independent grocery,”** submitted for ITS203 Assessment C Milestone 1 on 28 August 2026. Milestone 3 continues that scope with reports and export while retaining the earlier inventory and transaction rules.

## Tools and references

- Microsoft .NET 10 SDK, C# and Windows Forms; Microsoft.Data.Sqlite 10.0.12.
- SQLite schema and transactional storage; xUnit 2.9.3 and Microsoft.NET.Test.Sdk 17.14.1 for automated tests.
- Microsoft documentation: [Windows Forms](https://learn.microsoft.com/dotnet/desktop/winforms/), [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/), and [`dotnet test`](https://learn.microsoft.com/dotnet/core/tools/dotnet-test).
- xUnit documentation: [Getting started with xUnit.net v2](https://xunit.net/docs/getting-started/v2/getting-started).
- Documentation lookup: Context7 was used to check current Microsoft and xUnit API guidance.
