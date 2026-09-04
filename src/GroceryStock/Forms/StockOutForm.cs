using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Forms;

public sealed class StockOutForm : Form
{
    public StockOutForm(InventoryService service, StockItem item)
    {
        Text = "Stock out";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 180);
        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Recording stock out for {item.ItemCode} will be available in the movement checkpoint.",
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12)
        });
    }
}
