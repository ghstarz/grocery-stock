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
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GroceryStock",
            "grocery-stock.db");
        var database = new Database(databasePath);
        database.Initialize();
        Application.Run(new MainForm(new InventoryService(database)));
    }    
}
