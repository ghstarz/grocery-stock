using GroceryStock.Data;
using GroceryStock.Models;

namespace GroceryStock.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public void Fresh_database_is_initialized_and_repeated_initialization_is_safe()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        fixture.Database.Initialize();

        using var connection = fixture.Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Category;";

        Assert.Equal(5L, (long)command.ExecuteScalar()!);
    }

    [Fact]
    public void Catalogue_round_trips_after_reopening_a_new_connection()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var itemRepository = new ItemRepository(fixture.Database);
        var original = itemRepository.Add(new StandardItem(0, " rice001 ", "Rice", 1, "bag", 12.34m, 18.99m, 5));

        var reopened = new ItemRepository(new Database(fixture.Path));
        var loaded = reopened.GetById(original.ItemId);

        Assert.NotNull(loaded);
        Assert.Equal("RICE001", loaded!.ItemCode);
        Assert.Equal(12.34m, loaded.CostPrice);
        Assert.Equal(18.99m, loaded.SalePrice);
    }

    [Fact]
    public void Case_normalized_duplicate_item_codes_are_rejected()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var itemRepository = new ItemRepository(fixture.Database);
        itemRepository.Add(new StandardItem(0, "RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 5));

        Assert.Throws<Services.InventoryValidationException>(() => itemRepository.Add(
            new StandardItem(0, " rice001 ", "Other rice", 1, "bag", 1.00m, 2.00m, 5)));
    }

    [Fact]
    public void Invalid_category_foreign_key_is_rejected()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var itemRepository = new ItemRepository(fixture.Database);

        Assert.Throws<Services.InventoryValidationException>(() => itemRepository.Add(
            new StandardItem(0, "RICE001", "Rice", 999, "bag", 1.00m, 2.00m, 5)));
    }

    [Fact]
    public void Foreign_keys_are_enabled_for_each_connection()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        using var connection = fixture.Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";

        Assert.Equal(1L, (long)command.ExecuteScalar()!);
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
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
