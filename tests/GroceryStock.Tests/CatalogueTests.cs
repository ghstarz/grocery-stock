using GroceryStock.Data;
using GroceryStock.Services;

namespace GroceryStock.Tests;

public sealed class CatalogueTests
{
    [Fact]
    public void Search_matches_code_and_name_and_category_filter()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        service.AddItem("RICE001", "Rice bag", 1, "bag", 1.00m, 2.00m, 5, false);
        service.AddItem("MILK001", "Milk bottle", 2, "bottle", 1.00m, 2.00m, 3, true);

        Assert.Single(service.SearchCatalogue("rice"));
        Assert.Single(service.SearchCatalogue("MILK001", categoryId: 2));
        Assert.Empty(service.SearchCatalogue("rice", categoryId: 2));
    }

    [Fact]
    public void Quantity_sort_is_numeric_instead_of_lexicographic()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var two = service.AddItem("TWO", "Two", 1, "each", 1.00m, 2.00m, 1, false);
        var ten = service.AddItem("TEN", "Ten", 1, "each", 1.00m, 2.00m, 1, false);
        InsertMovement(fixture.Database, two.ItemId, 2);
        InsertMovement(fixture.Database, ten.ItemId, 10);

        var sorted = service.SearchCatalogue(sortByQuantity: true);

        Assert.Equal(new[] { "TWO", "TEN" }, sorted.Select(summary => summary.Item.ItemCode));
    }

    [Fact]
    public void Newly_created_item_is_visible_with_zero_balance()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        service.AddItem("SOAP001", "Soap", 5, "each", 1.00m, 2.00m, 2, false);

        var result = Assert.Single(service.SearchCatalogue(lowStockOnly: true));

        Assert.Equal(0, result.OnHand);
        Assert.Equal(2, result.Shortage);
    }

    private static void InsertMovement(Database database, int itemId, int quantity)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO StockMovement
                (ItemId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, RecordedBy)
            VALUES ($itemId, 'In', 'Receipt', $quantity, 100, $date, 'Test');
            """;
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
