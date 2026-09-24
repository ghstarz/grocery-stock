using GroceryStock.Data;
using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Tests;

public sealed class ReportingTests
{
    [Fact]
    public void Expiry_report_includes_expired_today_and_seven_day_boundary_in_order()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var inventory = new InventoryService(fixture.Database);
        var milk = inventory.AddItem("MILK001", "Milk", 2, "bottle", 1.25m, 2.00m, 3, true);
        var rice = inventory.AddItem("RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 1, false);
        var referenceDate = new DateOnly(2026, 9, 24);

        InsertBatch(fixture.Database, milk.ItemId, "YESTERDAY", referenceDate.AddDays(-1), 2);
        InsertBatch(fixture.Database, milk.ItemId, "TODAY", referenceDate, 3);
        InsertBatch(fixture.Database, milk.ItemId, "DAY-7", referenceDate.AddDays(7), 4);
        InsertBatch(fixture.Database, milk.ItemId, "DAY-8", referenceDate.AddDays(8), 5);
        InsertBatch(fixture.Database, milk.ItemId, "DEPLETED", referenceDate.AddDays(1), 2, depleted: true);
        InsertBatch(fixture.Database, rice.ItemId, "STANDARD", referenceDate.AddDays(1), 6);

        var rows = new ReportService(fixture.Database).GetExpiryReport(7, referenceDate);

        Assert.Equal(new[] { "YESTERDAY", "TODAY", "DAY-7" }, rows.Select(row => row.BatchCode));
        Assert.Equal(new[] { "Expired", "Expiring soon", "Expiring soon" }, rows.Select(row => row.Status));
        Assert.Equal(new[] { 2, 3, 4 }, rows.Select(row => row.OnHand));
    }

    [Fact]
    public void Zero_day_expiry_interval_includes_expired_and_today_but_rejects_negative_interval()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var inventory = new InventoryService(fixture.Database);
        var item = inventory.AddItem("YOGURT001", "Yoghurt", 2, "tub", 1.00m, 2.00m, 1, true);
        var referenceDate = new DateOnly(2026, 9, 24);
        InsertBatch(fixture.Database, item.ItemId, "OLD", referenceDate.AddDays(-1), 1);
        InsertBatch(fixture.Database, item.ItemId, "TODAY", referenceDate, 1);
        InsertBatch(fixture.Database, item.ItemId, "TOMORROW", referenceDate.AddDays(1), 1);
        var reports = new ReportService(fixture.Database);

        var rows = reports.GetExpiryReport(0, referenceDate);

        Assert.Equal(new[] { "OLD", "TODAY" }, rows.Select(row => row.BatchCode));
        Assert.Throws<ArgumentOutOfRangeException>(() => { reports.GetExpiryReport(-1, referenceDate); });
    }

    [Fact]
    public void Reorder_report_includes_zero_movement_and_threshold_items_but_excludes_retired_items()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var inventory = new InventoryService(fixture.Database);
        var below = inventory.AddItem("BELOW", "Below", 1, "each", 1.00m, 2.00m, 5, false);
        var equal = inventory.AddItem("EQUAL", "Equal", 1, "each", 1.00m, 2.00m, 5, false);
        var above = inventory.AddItem("ABOVE", "Above", 1, "each", 1.00m, 2.00m, 5, false);
        var empty = inventory.AddItem("EMPTY", "Empty", 1, "each", 1.00m, 2.00m, 2, false);
        var retired = inventory.AddItem("RETIRED", "Retired", 1, "each", 1.00m, 2.00m, 5, false);
        InsertMovement(fixture.Database, below.ItemId, 4);
        InsertMovement(fixture.Database, equal.ItemId, 5);
        InsertMovement(fixture.Database, above.ItemId, 6);
        new ItemRepository(fixture.Database).SetActive(retired.ItemId, false);

        var rows = new ReportService(fixture.Database).GetReorderItems().ToDictionary(row => row.ItemCode);

        Assert.Equal(new[] { "BELOW", "EQUAL", "EMPTY" }.Order(), rows.Keys.Order());
        Assert.Equal(1, rows["BELOW"].Shortage);
        Assert.Equal(0, rows["EQUAL"].Shortage);
        Assert.Equal(2, rows["EMPTY"].Shortage);
        Assert.Equal(0, rows["EMPTY"].OnHand);
        Assert.DoesNotContain("ABOVE", rows.Keys);
        Assert.DoesNotContain("RETIRED", rows.Keys);
    }

    [Fact]
    public void Category_valuation_uses_current_cost_and_exact_cents_without_batch_duplication()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var inventory = new InventoryService(fixture.Database);
        var rice = inventory.AddItem("RICE001", "Rice", 1, "bag", 12.34m, 18.99m, 1, false);
        var milk = inventory.AddItem("MILK001", "Milk", 2, "bottle", 1.15m, 2.00m, 1, true);
        var supplier = inventory.GetOrCreateSupplier("Fictional Foods");
        var today = DateTime.Today;
        inventory.ReceiveStock(rice.ItemId, 2, 20.00m, supplier.SupplierId, today, "Tester", null, null);
        inventory.ReceiveStock(rice.ItemId, 3, 5.00m, supplier.SupplierId, today, "Tester", null, null);
        var expiry = DateOnly.FromDateTime(today.AddDays(5));
        inventory.ReceiveStock(milk.ItemId, 4, 3.00m, supplier.SupplierId, today, "Tester", "MILK-A", expiry);
        inventory.ReceiveStock(milk.ItemId, 8, 3.00m, supplier.SupplierId, today, "Tester", "MILK-B", expiry);
        var batchA = Assert.Single(inventory.GetBatchBalances(milk.ItemId), batch => batch.Batch.BatchCode == "MILK-A");
        inventory.StockOut(milk.ItemId, 2, StockOutReason.Breakage, batchA.Batch.BatchId, today, "Tester");

        var report = new ReportService(fixture.Database).GetCategoryValuation();
        var values = report.Categories.ToDictionary(value => value.CategoryName, value => value.ValueCents);

        Assert.Equal(6170L, values["Grocery"]);
        Assert.Equal(1150L, values["Dairy"]);
        Assert.All(report.Categories.Where(value => value.CategoryName is not ("Grocery" or "Dairy")), value => Assert.Equal(0L, value.ValueCents));
        Assert.Equal(7320L, report.TotalValueCents);
        Assert.Equal(73.20m, report.TotalValue);
    }

    [Fact]
    public void Empty_inventory_has_zero_value_for_each_category_and_total()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();

        var report = new ReportService(fixture.Database).GetCategoryValuation();

        Assert.Equal(5, report.Categories.Count);
        Assert.All(report.Categories, value => Assert.Equal(0L, value.ValueCents));
        Assert.Equal(0L, report.TotalValueCents);
    }

    private static void InsertBatch(Database database, int itemId, string code, DateOnly expiryDate, int quantity, bool depleted = false)
    {
        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var batch = connection.CreateCommand();
        batch.Transaction = transaction;
        batch.CommandText = """
            INSERT INTO Batch (ItemId, BatchCode, ExpiryDate)
            VALUES ($itemId, $code, $expiry);
            SELECT last_insert_rowid();
            """;
        batch.Parameters.AddWithValue("$itemId", itemId);
        batch.Parameters.AddWithValue("$code", code);
        batch.Parameters.AddWithValue("$expiry", expiryDate.ToString("yyyy-MM-dd"));
        var batchId = Convert.ToInt32(batch.ExecuteScalar());

        using (var movement = connection.CreateCommand())
        {
            movement.Transaction = transaction;
            movement.CommandText = """
                INSERT INTO StockMovement
                    (ItemId, BatchId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, RecordedBy)
                VALUES ($itemId, $batchId, 'In', 'Fixture', $quantity, 100, '2026-09-24T12:00:00.0000000+00:00', 'Test');
                """;
            movement.Parameters.AddWithValue("$itemId", itemId);
            movement.Parameters.AddWithValue("$batchId", batchId);
            movement.Parameters.AddWithValue("$quantity", quantity);
            movement.ExecuteNonQuery();
        }

        if (depleted)
        {
            using var stockOut = connection.CreateCommand();
            stockOut.Transaction = transaction;
            stockOut.CommandText = """
                INSERT INTO StockMovement
                    (ItemId, BatchId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, RecordedBy)
                VALUES ($itemId, $batchId, 'Out', 'Expiry', $quantity, 100, '2026-09-24T12:00:00.0000000+00:00', 'Test');
                """;
            stockOut.Parameters.AddWithValue("$itemId", itemId);
            stockOut.Parameters.AddWithValue("$batchId", batchId);
            stockOut.Parameters.AddWithValue("$quantity", quantity);
            stockOut.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void InsertMovement(Database database, int itemId, int quantity)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO StockMovement
                (ItemId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, RecordedBy)
            VALUES ($itemId, 'In', 'Fixture', $quantity, 100, '2026-09-24T12:00:00.0000000+00:00', 'Test');
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$quantity", quantity);
        command.ExecuteNonQuery();
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
