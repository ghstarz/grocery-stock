using GroceryStock.Data;
using GroceryStock.Services;

namespace GroceryStock.Tests;

public sealed class LowStockTests
{
    [Fact]
    public void Below_equal_and_above_reorder_levels_have_clear_status_and_shortage()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var below = service.AddItem("BELOW", "Below", 1, "each", 1.00m, 2.00m, 5, false);
        var equal = service.AddItem("EQUAL", "Equal", 1, "each", 1.00m, 2.00m, 5, false);
        var above = service.AddItem("ABOVE", "Above", 1, "each", 1.00m, 2.00m, 5, false);
        InsertMovement(fixture.Database, below.ItemId, 4);
        InsertMovement(fixture.Database, equal.ItemId, 5);
        InsertMovement(fixture.Database, above.ItemId, 6);

        var summaries = service.SearchCatalogue(sortByQuantity: true).ToDictionary(summary => summary.Item.ItemCode);

        Assert.True(summaries["BELOW"].IsLowStock);
        Assert.Equal(1, summaries["BELOW"].Shortage);
        Assert.True(summaries["EQUAL"].IsLowStock);
        Assert.Equal(0, summaries["EQUAL"].Shortage);
        Assert.False(summaries["ABOVE"].IsLowStock);
        Assert.Equal("OK", summaries["ABOVE"].StatusText);
        Assert.Equal(new[] { "BELOW", "EQUAL" }, service.SearchCatalogue(lowStockOnly: true, sortByQuantity: true).Select(summary => summary.Item.ItemCode));
    }

    private static void InsertMovement(Database database, int itemId, int quantity)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO StockMovement (ItemId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, RecordedBy) VALUES ($itemId, 'In', 'Fixture', $quantity, 100, $date, 'Test');";
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$quantity", quantity);
        command.Parameters.AddWithValue("$date", DateTime.Today.ToString("O"));
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
