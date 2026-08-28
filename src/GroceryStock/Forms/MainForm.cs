namespace GroceryStock.Forms;

public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "Grocery Stock";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 560);
        ClientSize = new Size(1100, 650);

        var message = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Grocery Stock\r\nMilestone 2 inventory checkpoint",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 16, FontStyle.Regular)
        };
        Controls.Add(message);
    }
}
