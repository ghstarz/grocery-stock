using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Forms;

public sealed class ReceiveStockForm : Form
{
    private readonly InventoryService service;
    private readonly StockItem item;
    private readonly NumericUpDown quantityInput = new() { Minimum = 1, Maximum = 1_000_000, DecimalPlaces = 0, Value = 1 };
    private readonly NumericUpDown costInput = new() { Minimum = 0, Maximum = 1_000_000, DecimalPlaces = 2, Increment = 0.01m };
    private readonly ComboBox supplierCombo = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly DateTimePicker datePicker = new() { Format = DateTimePickerFormat.Short };
    private readonly TextBox batchCodeText = new();
    private readonly DateTimePicker expiryPicker = new() { Format = DateTimePickerFormat.Short };
    private readonly TextBox recordedByText = new();
    private readonly Label batchLabel = new();
    private readonly Label expiryLabel = new();
    private readonly Label errorLabel = new();
    private Button? saveButton;

    public ReceiveStockForm(InventoryService service, StockItem item)
    {
        this.service = service;
        this.item = item;
        Text = $"Receive stock — {item.ItemCode}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(500, item.RequiresBatch ? 430 : 350);
        BuildLayout();
        LoadSuppliers();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(layout, 0, "Item", new Label { Text = $"{item.ItemCode} — {item.Name}", AutoSize = true });
        AddField(layout, 1, "Quantity", quantityInput);
        costInput.Value = item.CostPrice;
        AddField(layout, 2, "Unit cost", costInput);
        supplierCombo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        supplierCombo.AutoCompleteSource = AutoCompleteSource.ListItems;
        AddField(layout, 3, "Supplier", supplierCombo);
        AddField(layout, 4, "Delivery date", datePicker);
        batchLabel.Text = "Batch code";
        AddField(layout, 5, batchLabel, batchCodeText);
        expiryLabel.Text = "Expiry date";
        AddField(layout, 6, expiryLabel, expiryPicker);
        AddField(layout, 7, "Recorded by", recordedByText);
        errorLabel.ForeColor = Color.DarkRed;
        errorLabel.AutoSize = true;
        layout.Controls.Add(errorLabel, 0, 8);
        layout.SetColumnSpan(errorLabel, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        saveButton = new Button { Text = "Save", AutoSize = true };
        saveButton.Click += (_, _) => SaveReceipt();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(saveButton);
        layout.Controls.Add(buttons, 0, 9);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancel;
        if (!item.RequiresBatch)
        {
            batchLabel.Visible = false;
            batchCodeText.Visible = false;
            expiryLabel.Visible = false;
            expiryPicker.Visible = false;
        }
    }

    private void LoadSuppliers()
    {
        supplierCombo.Items.AddRange(service.GetSuppliers().Select(supplier => supplier.Name).Cast<object>().ToArray());
    }

    private void SaveReceipt()
    {
        if (saveButton is null) return;
        saveButton.Enabled = false;
        try
        {
            var supplier = service.GetOrCreateSupplier(supplierCombo.Text);
            service.ReceiveStock(
                item.ItemId,
                (int)quantityInput.Value,
                costInput.Value,
                supplier.SupplierId,
                datePicker.Value,
                recordedByText.Text,
                item.RequiresBatch ? batchCodeText.Text : null,
                item.RequiresBatch ? DateOnly.FromDateTime(expiryPicker.Value.Date) : null);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is ArgumentException or InventoryValidationException or InvalidOperationException)
        {
            errorLabel.Text = ex.Message;
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }

    private static void AddField(TableLayoutPanel layout, int row, string label, Control control) => AddField(layout, row, new Label { Text = label, AutoSize = true }, control);

    private static void AddField(TableLayoutPanel layout, int row, Control label, Control control)
    {
        label.Anchor = AnchorStyles.Left;
        label.Margin = new Padding(3, 7, 3, 3);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
