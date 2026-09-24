using System.Globalization;
using System.Text;
using GroceryStock.Data;
using GroceryStock.Services;

namespace GroceryStock.Tests;

public sealed class CsvExportTests
{
    [Fact]
    public void Export_quotes_special_text_and_writes_numeric_values_with_invariant_decimal_points()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        service.AddItem("000123", "Tea, \"green\"\r\nloose", 1, "box, small", 12.34m, 18.99m, 3, false);
        var rows = service.SearchCatalogue();
        var destination = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"grocery-stock-{Guid.NewGuid():N}.csv");
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            new StockCsvExporter().Export(destination, rows);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        try
        {
            var csv = File.ReadAllText(destination, new UTF8Encoding(true));
            Assert.StartsWith("Item code,Name,Category,Unit,On hand,Reorder level,Cost price,Sale price,Active\r\n", csv);
            Assert.Contains("\"000123\",\"Tea, \"\"green\"\"\r\nloose\",\"Grocery\",\"box, small\",\"0\",\"3\",\"12.34\",\"18.99\",\"Yes\"\r\n", csv);
        }
        finally
        {
            if (File.Exists(destination)) File.Delete(destination);
        }
    }

    [Fact]
    public void Export_prefixes_formula_like_user_text_without_changing_the_catalogue_value()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        var item = service.AddItem("FORMULA", "=SUM(1,2)", 1, "each", 1.00m, 2.00m, 0, false);
        var destination = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"grocery-stock-{Guid.NewGuid():N}.csv");

        try
        {
            new StockCsvExporter().Export(destination, service.SearchCatalogue());
            var csv = File.ReadAllText(destination, new UTF8Encoding(true));

            Assert.Contains("\"FORMULA\",\"'=SUM(1,2)\"", csv);
            Assert.Equal("=SUM(1,2)", new ItemRepository(fixture.Database).GetById(item.ItemId)!.Name);
        }
        finally
        {
            if (File.Exists(destination)) File.Delete(destination);
        }
    }

    [Fact]
    public void Locked_destination_keeps_existing_contents_and_does_not_leave_a_temporary_file()
    {
        using var fixture = new DatabaseFixture();
        fixture.Database.Initialize();
        var service = new InventoryService(fixture.Database);
        service.AddItem("RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 1, false);
        var rows = service.SearchCatalogue();
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"grocery-stock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var destination = System.IO.Path.Combine(directory, "stock.csv");
        File.WriteAllText(destination, "original", new UTF8Encoding(false));

        try
        {
            using (var lockedFile = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var error = Record.Exception(() => new StockCsvExporter().Export(destination, rows));
                Assert.NotNull(error);
                Assert.True(error is IOException or UnauthorizedAccessException, error.GetType().FullName);
            }

            Assert.Equal("original", File.ReadAllText(destination, new UTF8Encoding(false)));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            if (File.Exists(destination)) File.Delete(destination);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
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
