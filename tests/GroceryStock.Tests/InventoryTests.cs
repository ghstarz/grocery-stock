using GroceryStock.Data;
using GroceryStock.Services;

namespace GroceryStock.Tests;

public sealed class InventoryTests
{
    [Fact]
    public void Standard_receipt_adds_twenty_units_and_persists()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 5, false);
        var supplier = service.GetOrCreateSupplier("Example Foods");

        service.ReceiveStock(item.ItemId, 20, 1.23m, supplier.SupplierId, DateTime.Today, "Tester", null, null);

        Assert.Equal(20, new MovementRepository(new Database(fixture.Path)).GetItemBalance(item.ItemId));
        Assert.Single(service.GetMovements(item.ItemId));
    }

    [Fact]
    public void Perishable_receipt_requires_batch_and_expiry()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("MILK001", "Milk", 2, "bottle", 1.00m, 2.00m, 3, true);
        var supplier = service.GetOrCreateSupplier("Example Foods");

        Assert.Throws<InventoryValidationException>(() => service.ReceiveStock(item.ItemId, 4, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", null, null));
        service.ReceiveStock(item.ItemId, 4, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", "BATCH-A", DateOnly.FromDateTime(DateTime.Today));

        var batch = Assert.Single(service.GetBatchBalances(item.ItemId));
        Assert.Equal("BATCH-A", batch.Batch.BatchCode);
        Assert.Equal(4, batch.OnHand);
    }

    [Fact]
    public void Reusing_batch_code_requires_the_same_expiry_date()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("BREAD001", "Bread", 3, "loaf", 1.00m, 2.00m, 2, true);
        var supplier = service.GetOrCreateSupplier("Example Foods");
        var expiry = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        service.ReceiveStock(item.ItemId, 3, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", "BATCH-A", expiry);
        service.ReceiveStock(item.ItemId, 2, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", "batch-a", expiry);

        Assert.Equal(5, Assert.Single(service.GetBatchBalances(item.ItemId)).OnHand);
        Assert.Throws<InventoryValidationException>(() => service.ReceiveStock(item.ItemId, 1, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", "BATCH-A", expiry.AddDays(1)));
    }

    [Fact]
    public void Failed_movement_rolls_back_a_new_batch()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var repository = new ItemRepository(fixture.Database);
        var item = repository.Add(new Models.PerishableItem(0, "MILK001", "Milk", 2, "bottle", 1.00m, 2.00m, 3));
        var movements = new MovementRepository(fixture.Database);

        Assert.Throws<InventoryValidationException>(() => movements.ReceiveStock(
            item.ItemId, 4, 1.00m, 999999, DateTime.Today, "Tester", "ROLLBACK", DateOnly.FromDateTime(DateTime.Today.AddDays(5))));

        Assert.Empty(movements.GetBatchBalances(item.ItemId));
        Assert.Equal(0, movements.GetItemBalance(item.ItemId));
    }

    [Fact]
    public void Expiry_before_today_is_rejected_but_today_is_allowed()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("MILK001", "Milk", 2, "bottle", 1.00m, 2.00m, 3, true);
        var supplier = service.GetOrCreateSupplier("Example Foods");

        Assert.Throws<InventoryValidationException>(() => service.ReceiveStock(item.ItemId, 1, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", "OLD", DateOnly.FromDateTime(DateTime.Today.AddDays(-1))));
        service.ReceiveStock(item.ItemId, 1, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", "TODAY", DateOnly.FromDateTime(DateTime.Today));
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
