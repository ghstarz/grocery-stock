using GroceryStock.Models;
using GroceryStock.Services;
using Microsoft.Data.Sqlite;

namespace GroceryStock.Data;

public sealed class ItemRepository
{
    private readonly Database database;

    public ItemRepository(Database database)
    {
        this.database = database;
    }

    public StockItem Add(StockItem item)
    {
        try
        {
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO StockItem
                    (ItemCode, Name, CategoryId, Unit, CostPriceCents, SalePriceCents, ReorderLevel, IsPerishable, IsActive)
                VALUES ($code, $name, $categoryId, $unit, $cost, $sale, $reorder, $perishable, $active);
                SELECT last_insert_rowid();
                """;
            AddItemParameters(command, item);
            var id = Convert.ToInt32(command.ExecuteScalar());
            return WithId(item, id);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 19 or 1555 or 2067)
        {
            throw new InventoryValidationException("The item code must be unique and the selected category must exist.", ex);
        }
    }

    public StockItem? GetById(int itemId)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE i.ItemId = $id;";
        command.Parameters.AddWithValue("$id", itemId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadItem(reader) : null;
    }

    public IReadOnlyList<StockItem> GetAll(bool includeInactive = true)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = SelectSql + (includeInactive ? string.Empty : " WHERE i.IsActive = 1") + " ORDER BY i.Name, i.ItemCode;";
        using var reader = command.ExecuteReader();
        var items = new List<StockItem>();
        while (reader.Read())
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    public void Update(StockItem item)
    {
        try
        {
            using var connection = database.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE StockItem
                SET ItemCode = $code,
                    Name = $name,
                    CategoryId = $categoryId,
                    Unit = $unit,
                    CostPriceCents = $cost,
                    SalePriceCents = $sale,
                    ReorderLevel = $reorder
                WHERE ItemId = $id;
                """;
            command.Parameters.AddWithValue("$id", item.ItemId);
            AddItemParameters(command, item);
            if (command.ExecuteNonQuery() != 1)
            {
                throw new InventoryValidationException("The item could not be found.");
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 19 or 1555 or 2067)
        {
            throw new InventoryValidationException("The item code must be unique and the selected category must exist.", ex);
        }
    }

    public void SetActive(int itemId, bool isActive)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE StockItem SET IsActive = $active WHERE ItemId = $id;";
        command.Parameters.AddWithValue("$active", isActive ? 1 : 0);
        command.Parameters.AddWithValue("$id", itemId);
        command.ExecuteNonQuery();
    }

    public bool HasMovements(int itemId)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM StockMovement WHERE ItemId = $id);";
        command.Parameters.AddWithValue("$id", itemId);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public int GetMovementCount(int itemId)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM StockMovement WHERE ItemId = $id;";
        command.Parameters.AddWithValue("$id", itemId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int GetOnHand(int itemId)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(CASE WHEN MovementType = 'In' THEN Quantity ELSE -Quantity END), 0)
            FROM StockMovement
            WHERE ItemId = $id;
            """;
        command.Parameters.AddWithValue("$id", itemId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public IReadOnlyList<StockSummary> GetSummaries(bool includeInactive = true)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.ItemId, i.ItemCode, i.Name, i.CategoryId, c.Name,
                   i.Unit, i.CostPriceCents, i.SalePriceCents, i.ReorderLevel,
                   i.IsPerishable, i.IsActive,
                   COALESCE(SUM(CASE WHEN m.MovementType = 'In' THEN m.Quantity ELSE -m.Quantity END), 0)
            FROM StockItem i
            INNER JOIN Category c ON c.CategoryId = i.CategoryId
            LEFT JOIN StockMovement m ON m.ItemId = i.ItemId
            """ + (includeInactive ? string.Empty : " WHERE i.IsActive = 1") + " GROUP BY i.ItemId ORDER BY i.Name, i.ItemCode;";
        using var reader = command.ExecuteReader();
        var summaries = new List<StockSummary>();
        while (reader.Read())
        {
            StockItem item = reader.GetInt32(9) == 1
                ? new PerishableItem(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(5), FromCents(reader.GetInt64(6)), FromCents(reader.GetInt64(7)), reader.GetInt32(8), reader.GetInt32(10) == 1)
                : new StandardItem(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(5), FromCents(reader.GetInt64(6)), FromCents(reader.GetInt64(7)), reader.GetInt32(8), reader.GetInt32(10) == 1);
            item.CategoryName = reader.GetString(4);
            summaries.Add(new StockSummary(item, reader.GetInt32(11)));
        }

        return summaries;
    }

    private static string SelectSql => """
        SELECT i.ItemId, i.ItemCode, i.Name, i.CategoryId, c.Name,
               i.Unit, i.CostPriceCents, i.SalePriceCents, i.ReorderLevel,
               i.IsPerishable, i.IsActive
        FROM StockItem i
        INNER JOIN Category c ON c.CategoryId = i.CategoryId
        """;

    private static void AddItemParameters(SqliteCommand command, StockItem item)
    {
        command.Parameters.AddWithValue("$code", item.ItemCode);
        command.Parameters.AddWithValue("$name", item.Name);
        command.Parameters.AddWithValue("$categoryId", item.CategoryId);
        command.Parameters.AddWithValue("$unit", item.Unit);
        command.Parameters.AddWithValue("$cost", ToCents(item.CostPrice));
        command.Parameters.AddWithValue("$sale", ToCents(item.SalePrice));
        command.Parameters.AddWithValue("$reorder", item.ReorderLevel);
        command.Parameters.AddWithValue("$perishable", item.RequiresBatch ? 1 : 0);
        command.Parameters.AddWithValue("$active", item.IsActive ? 1 : 0);
    }

    private static StockItem ReadItem(SqliteDataReader reader)
    {
        var id = reader.GetInt32(0);
        StockItem item = reader.GetInt32(9) == 1
            ? new PerishableItem(id, reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(5), FromCents(reader.GetInt64(6)), FromCents(reader.GetInt64(7)), reader.GetInt32(8), reader.GetInt32(10) == 1)
            : new StandardItem(id, reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(5), FromCents(reader.GetInt64(6)), FromCents(reader.GetInt64(7)), reader.GetInt32(8), reader.GetInt32(10) == 1);
        item.CategoryName = reader.GetString(4);
        return item;
    }

    private static StockItem WithId(StockItem item, int id)
    {
        return item.RequiresBatch
            ? new PerishableItem(id, item.ItemCode, item.Name, item.CategoryId, item.Unit, item.CostPrice, item.SalePrice, item.ReorderLevel, item.IsActive)
            : new StandardItem(id, item.ItemCode, item.Name, item.CategoryId, item.Unit, item.CostPrice, item.SalePrice, item.ReorderLevel, item.IsActive);
    }

    internal static long ToCents(decimal value) => decimal.ToInt64(decimal.Round(value * 100m, 0));

    internal static decimal FromCents(long value) => value / 100m;
}
