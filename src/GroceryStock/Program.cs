using GroceryStock.Forms;
using GroceryStock.Data;
using GroceryStock.Services;

namespace GroceryStock;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Database database;
        try
        {
            database = new Database(GetDatabasePath(args));
            database.Initialize();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(
                $"Grocery Stock could not open its database. Check that the selected file is available and writable.\r\n\r\nDetails: {exception.Message}",
                "Cannot open database",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm(new InventoryService(database), new ReportService(database)));
    }

    private static string GetDatabasePath(string[] args)
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GroceryStock",
            "grocery-stock.db");
        if (args.Length == 0)
        {
            return defaultPath;
        }

        if (args.Length != 2 || !string.Equals(args[0], "--demo-database", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Use --demo-database <path> to open a separate demonstration database.");
        }

        var demoPath = Path.GetFullPath(args[1]);
        if (string.Equals(demoPath, Path.GetFullPath(defaultPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The demonstration database must use a separate path from the normal stock database.");
        }

        return demoPath;
    }
}
