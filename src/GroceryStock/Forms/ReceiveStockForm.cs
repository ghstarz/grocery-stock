using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Forms;

public sealed class ReceiveStockForm : Form
{
    public ReceiveStockForm(InventoryService service, StockItem item)
    {
        Text = "Receive stock";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 180);
        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Receiving stock for {item.ItemCode} will be available in the movement checkpoint.",
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12)
        });
    }
}
