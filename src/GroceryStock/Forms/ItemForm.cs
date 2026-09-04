using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Forms;

public sealed class ItemForm : Form
{
    private readonly InventoryService service;
    private readonly StockItem? existingItem;
    private readonly TextBox codeText = new();
    private readonly TextBox nameText = new();
    private readonly ComboBox categoryCombo = new();
    private readonly TextBox unitText = new();
    private readonly NumericUpDown costInput = MoneyInput();
    private readonly NumericUpDown saleInput = MoneyInput();
    private readonly NumericUpDown reorderInput = new() { Minimum = 0, Maximum = 1_000_000, DecimalPlaces = 0 };
    private readonly ComboBox typeCombo = new();
    private readonly Label errorLabel = new();

    public ItemForm(InventoryService service, IReadOnlyList<Category> categories, StockItem? existingItem)
    {
        this.service = service;
        this.existingItem = existingItem;
        Text = existingItem is null ? "Add item" : "Edit item";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 420);
        BuildLayout(categories);
        if (existingItem is not null) LoadExisting(existingItem);
    }

    private void BuildLayout(IReadOnlyList<Category> categories)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10, Padding = new Padding(12), AutoSize = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(layout, 0, "Item code", codeText);
        AddField(layout, 1, "Name", nameText);
        categoryCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        categoryCombo.DisplayMember = nameof(Category.Name);
        categoryCombo.ValueMember = nameof(Category.CategoryId);
        categoryCombo.DataSource = categories.ToList();
        AddField(layout, 2, "Category", categoryCombo);
        AddField(layout, 3, "Unit", unitText);
        AddField(layout, 4, "Cost price", costInput);
        AddField(layout, 5, "Sale price", saleInput);
        AddField(layout, 6, "Reorder level", reorderInput);
        typeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        typeCombo.Items.AddRange(new object[] { "Standard", "Perishable" });
        typeCombo.SelectedIndex = 0;
        AddField(layout, 7, "Item type", typeCombo);
        errorLabel.ForeColor = Color.DarkRed;
        errorLabel.AutoSize = true;
        layout.Controls.Add(errorLabel, 0, 8);
        layout.SetColumnSpan(errorLabel, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) => SaveItem();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons, 0, 9);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void LoadExisting(StockItem item)
    {
        codeText.Text = item.ItemCode;
        nameText.Text = item.Name;
        categoryCombo.SelectedValue = item.CategoryId;
        unitText.Text = item.Unit;
        costInput.Value = item.CostPrice;
        saleInput.Value = item.SalePrice;
        reorderInput.Value = item.ReorderLevel;
        typeCombo.SelectedIndex = item.RequiresBatch ? 1 : 0;
    }

    private void SaveItem()
    {
        try
        {
            var categoryId = categoryCombo.SelectedValue is int value ? value : 0;
            var perishable = typeCombo.SelectedIndex == 1;
            if (existingItem is null)
            {
                service.AddItem(codeText.Text, nameText.Text, categoryId, unitText.Text, costInput.Value, saleInput.Value, (int)reorderInput.Value, perishable);
            }
            else
            {
                service.UpdateItem(existingItem.ItemId, codeText.Text, nameText.Text, categoryId, unitText.Text, costInput.Value, saleInput.Value, (int)reorderInput.Value, perishable);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is ArgumentException or InventoryValidationException or InvalidOperationException)
        {
            errorLabel.Text = ex.Message;
        }
    }

    private static void AddField(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }

    private static NumericUpDown MoneyInput()
    {
        return new NumericUpDown { Minimum = 0, Maximum = 1_000_000, DecimalPlaces = 2, Increment = 0.01m, ThousandsSeparator = true };
    }
}
