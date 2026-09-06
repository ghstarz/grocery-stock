using GroceryStock.Data;
using GroceryStock.Services;

namespace GroceryStock.Tests;

public sealed class EditingTests
{
    [Fact]
    public void Editing_catalogue_details_persists_without_changing_identity()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 5, false);

        var edited = service.UpdateItem(item.ItemId, "RICE001", "Long grain rice", 1, "bag", 1.25m, 2.50m, 6, false);

        Assert.Equal(item.ItemId, edited.ItemId);
        Assert.Equal("Long grain rice", edited.Name);
        Assert.Equal(6, edited.ReorderLevel);
    }

    [Fact]
    public void Item_type_and_unit_cannot_change_after_movements_exist()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("MILK001", "Milk", 2, "bottle", 1.00m, 2.00m, 3, true);
        InsertMovement(fixture.Database, item.ItemId, 4);

        Assert.Throws<InventoryValidationException>(() => service.UpdateItem(item.ItemId, "MILK001", "Milk", 2, "each", 1.00m, 2.00m, 3, true));
        Assert.Throws<InventoryValidationException>(() => service.UpdateItem(item.ItemId, "MILK001", "Milk", 2, "bottle", 1.00m, 2.00m, 3, false));
    }

    [Fact]
    public void Retirement_requires_zero_balance_and_preserves_history()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var items = new ItemRepository(fixture.Database);
        var item = service.AddItem("RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 5, false);
        InsertMovement(fixture.Database, item.ItemId, 2);

        Assert.Throws<InventoryValidationException>(() => service.RetireItem(item.ItemId));
        InsertOutMovement(fixture.Database, item.ItemId, 2);

        service.RetireItem(item.ItemId);

        var retired = Assert.Single(service.SearchCatalogue(active: false));
        Assert.Equal(item.ItemId, retired.Item.ItemId);
        Assert.Equal(2, items.GetMovementCount(item.ItemId));
    }

    private static void InsertMovement(Database database, int itemId, int quantity)
    {
        Insert(database, itemId, "In", quantity);
    }

    private static void InsertOutMovement(Database database, int itemId, int quantity)
    {
        Insert(database, itemId, "Out", quantity);
    }

    private static void Insert(Database database, int itemId, string movementType, int quantity)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO StockMovement
                (ItemId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, RecordedBy)
            VALUES ($itemId, $type, 'Test', $quantity, 100, $date, 'Test');
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$type", movementType);
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
