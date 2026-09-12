using GroceryStock.Data;
using GroceryStock.Models;
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

    [Fact]
    public void Standard_stock_out_rejects_overselling_and_allows_exact_balance()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 5, false);
        var supplier = service.GetOrCreateSupplier("Example Foods");
        service.ReceiveStock(item.ItemId, 20, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", null, null);

        service.StockOut(item.ItemId, 6, StockOutReason.Sale, null, DateTime.Today, "Tester");
        Assert.Throws<InventoryValidationException>(() => service.StockOut(item.ItemId, 15, StockOutReason.Sale, null, DateTime.Today, "Tester"));
        Assert.Equal(14, new MovementRepository(fixture.Database).GetItemBalance(item.ItemId));
        service.StockOut(item.ItemId, 14, StockOutReason.Sale, null, DateTime.Today, "Tester");

        Assert.Equal(0, new MovementRepository(fixture.Database).GetItemBalance(item.ItemId));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.StockOut(item.ItemId, 0, StockOutReason.Sale, null, DateTime.Today, "Tester"));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.StockOut(item.ItemId, -1, StockOutReason.Sale, null, DateTime.Today, "Tester"));
    }

    [Fact]
    public void Batch_level_balance_and_ownership_are_checked()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var first = service.AddItem("MILK001", "Milk", 2, "bottle", 1.00m, 2.00m, 3, true);
        var second = service.AddItem("YOGURT001", "Yoghurt", 2, "tub", 1.00m, 2.00m, 3, true);
        var supplier = service.GetOrCreateSupplier("Example Foods");
        var expiry = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        service.ReceiveStock(first.ItemId, 4, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", "A", expiry);
        service.ReceiveStock(first.ItemId, 8, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", "B", expiry);
        var batchA = Assert.Single(service.GetBatchBalances(first.ItemId), batch => batch.Batch.BatchCode == "A");

        Assert.Throws<InventoryValidationException>(() => service.StockOut(first.ItemId, 5, StockOutReason.Sale, batchA.Batch.BatchId, DateTime.Today, "Tester"));
        Assert.Throws<InventoryValidationException>(() => service.StockOut(second.ItemId, 1, StockOutReason.Sale, batchA.Batch.BatchId, DateTime.Today, "Tester"));
        Assert.Equal(12, new MovementRepository(fixture.Database).GetItemBalance(first.ItemId));
        Assert.Equal(4, Assert.Single(service.GetBatchBalances(first.ItemId), batch => batch.Batch.BatchCode == "A").OnHand);
    }

    [Fact]
    public void Expired_batch_sale_is_rejected_but_expiry_write_off_is_allowed()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("MILK001", "Milk", 2, "bottle", 1.00m, 2.00m, 3, true);
        var batchId = InsertBatchWithMovement(fixture.Database, item.ItemId, "EXPIRED", DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), 3);

        Assert.Throws<InventoryValidationException>(() => service.StockOut(item.ItemId, 1, StockOutReason.Sale, batchId, DateTime.Today, "Tester"));
        service.StockOut(item.ItemId, 3, StockOutReason.Expiry, batchId, DateTime.Today, "Tester");

        Assert.Equal(0, new MovementRepository(fixture.Database).GetItemBalance(item.ItemId));
    }

    [Fact]
    public void Database_failure_keeps_balance_unchanged()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 5, false);
        var supplier = service.GetOrCreateSupplier("Example Foods");
        service.ReceiveStock(item.ItemId, 5, 1.00m, supplier.SupplierId, DateTime.Today, "Tester", null, null);
        using (var connection = fixture.Database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER FailStockOut
                BEFORE INSERT ON StockMovement
                WHEN NEW.MovementType = 'Out'
                BEGIN
                    SELECT RAISE(ABORT, 'forced stock-out failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        Assert.Throws<InventoryValidationException>(() => service.StockOut(item.ItemId, 2, StockOutReason.Breakage, null, DateTime.Today, "Tester"));
        Assert.Equal(5, new MovementRepository(fixture.Database).GetItemBalance(item.ItemId));
    }

    private static int InsertBatchWithMovement(Database database, int itemId, string code, DateOnly expiryDate, int quantity)
    {
        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var batch = connection.CreateCommand();
        batch.Transaction = transaction;
        batch.CommandText = "INSERT INTO Batch (ItemId, BatchCode, ExpiryDate) VALUES ($itemId, $code, $expiry); SELECT last_insert_rowid();";
        batch.Parameters.AddWithValue("$itemId", itemId);
        batch.Parameters.AddWithValue("$code", code);
        batch.Parameters.AddWithValue("$expiry", expiryDate.ToString("yyyy-MM-dd"));
        var batchId = Convert.ToInt32(batch.ExecuteScalar());
        using var movement = connection.CreateCommand();
        movement.Transaction = transaction;
        movement.CommandText = "INSERT INTO StockMovement (ItemId, BatchId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, RecordedBy) VALUES ($itemId, $batchId, 'In', 'Fixture', $quantity, 100, $date, 'Test');";
        movement.Parameters.AddWithValue("$itemId", itemId);
        movement.Parameters.AddWithValue("$batchId", batchId);
        movement.Parameters.AddWithValue("$quantity", quantity);
        movement.Parameters.AddWithValue("$date", DateTime.Today.ToString("O"));
        movement.ExecuteNonQuery();
        transaction.Commit();
        return batchId;
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
