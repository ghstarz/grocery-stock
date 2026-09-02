using Microsoft.Data.Sqlite;
using System.Reflection;

namespace GroceryStock.Data;

public sealed class Database
{
    public Database(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A database file path is required.", nameof(filePath));
        }

        FilePath = Path.GetFullPath(filePath);
    }

    public string FilePath { get; }

    public SqliteConnection OpenConnection()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={FilePath};Foreign Keys=True;Pooling=False");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText = ReadSchema();
            schemaCommand.ExecuteNonQuery();
        }

        using var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = """
            INSERT OR IGNORE INTO Category (CategoryId, Name) VALUES
                (1, 'Grocery'),
                (2, 'Dairy'),
                (3, 'Bakery'),
                (4, 'Frozen'),
                (5, 'Household');
            """;
        seedCommand.ExecuteNonQuery();
    }

    private static string ReadSchema()
    {
        var assembly = typeof(Database).GetTypeInfo().Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("Data.Schema.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The SQLite schema resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
