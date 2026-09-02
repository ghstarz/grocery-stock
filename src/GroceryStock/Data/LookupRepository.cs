using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Data;

public sealed class LookupRepository
{
    private readonly Database database;

    public LookupRepository(Database database)
    {
        this.database = database;
    }

    public IReadOnlyList<Category> GetCategories()
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CategoryId, Name FROM Category ORDER BY Name;";
        using var reader = command.ExecuteReader();
        var categories = new List<Category>();
        while (reader.Read())
        {
            categories.Add(new Category(reader.GetInt32(0), reader.GetString(1)));
        }

        return categories;
    }

    public IReadOnlyList<Supplier> GetSuppliers()
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SupplierId, Name FROM Supplier ORDER BY Name;";
        using var reader = command.ExecuteReader();
        var suppliers = new List<Supplier>();
        while (reader.Read())
        {
            suppliers.Add(new Supplier(reader.GetInt32(0), reader.GetString(1)));
        }

        return suppliers;
    }

    public Supplier GetOrCreateSupplier(string name)
    {
        var normalizedName = StockItem.RequireText(name, nameof(name));
        using var connection = database.OpenConnection();
        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT OR IGNORE INTO Supplier (Name) VALUES ($name);";
            insert.Parameters.AddWithValue("$name", normalizedName);
            insert.ExecuteNonQuery();
        }

        using var select = connection.CreateCommand();
        select.CommandText = "SELECT SupplierId, Name FROM Supplier WHERE Name = $name;";
        select.Parameters.AddWithValue("$name", normalizedName);
        using var reader = select.ExecuteReader();
        if (!reader.Read())
        {
            throw new InventoryValidationException("The supplier could not be saved.");
        }

        return new Supplier(reader.GetInt32(0), reader.GetString(1));
    }
}
