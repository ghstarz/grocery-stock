using GroceryStock.Data;
using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Tests;

public sealed class RegressionTests
{
    [Fact]
    public void Complete_workflow_survives_restart_with_derived_balance()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("RICE001", "Rice bag", 1, "bag", 12.34m, 18.99m, 5, false);
        var supplier = service.GetOrCreateSupplier("Example Foods");
        service.ReceiveStock(item.ItemId, 20, 12.34m, supplier.SupplierId, DateTime.Today, "Tester", null, null);
        service.StockOut(item.ItemId, 6, StockOutReason.Sale, null, DateTime.Today, "Tester");

        var reopened = new InventoryService(new Database(fixture.Path));
        var summary = Assert.Single(reopened.SearchCatalogue("RICE001"));

        Assert.Equal(14, summary.OnHand);
        Assert.False(summary.IsLowStock);
        Assert.Equal(2, reopened.GetMovements(item.ItemId).Count);
    }

    private sealed class DatabaseFixture : IDisposable
    {
        public DatabaseFixture()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"grocery-stock-{Guid.NewGuid():N}.db");
            Database = new Database(Path);
        }

        public string Path { get; }
        public Database Database { get; }

        public void Dispose()
        {
            foreach (var path in new[] { Path, Path + "-wal", Path + "-shm" })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
