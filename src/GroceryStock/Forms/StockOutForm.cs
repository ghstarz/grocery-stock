using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Forms;

public sealed class StockOutForm : Form
{
    private readonly InventoryService service;
    private readonly StockItem item;
    private readonly NumericUpDown quantityInput = new() { Minimum = 1, Maximum = 1_000_000, DecimalPlaces = 0, Value = 1 };
    private readonly ComboBox reasonCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox batchCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker datePicker = new() { Format = DateTimePickerFormat.Short };
    private readonly TextBox recordedByText = new();
    private readonly Label batchLabel = new();
    private readonly Label errorLabel = new();
    private Button? saveButton;

    public StockOutForm(InventoryService service, StockItem item)
    {
        this.service = service;
        this.item = item;
        Text = $"Stock out — {item.ItemCode}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(500, item.RequiresBatch ? 350 : 300);
        BuildLayout();
        LoadBatches();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(layout, 0, "Item", new Label { Text = $"{item.ItemCode} — {item.Name}", AutoSize = true });
        AddField(layout, 1, "Quantity", quantityInput);
        reasonCombo.Items.AddRange(Enum.GetValues<StockOutReason>().Cast<object>().ToArray());
        reasonCombo.SelectedIndex = 0;
        AddField(layout, 2, "Reason", reasonCombo);
        batchLabel.Text = "Batch";
        AddField(layout, 3, batchLabel, batchCombo);
        AddField(layout, 4, "Date", datePicker);
        AddField(layout, 5, "Recorded by", recordedByText);
        errorLabel.ForeColor = Color.DarkRed;
        errorLabel.AutoSize = true;
        layout.Controls.Add(errorLabel, 0, 6);
        layout.SetColumnSpan(errorLabel, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        saveButton = new Button { Text = "Save", AutoSize = true };
        saveButton.Click += (_, _) => SaveStockOut();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(saveButton);
        layout.Controls.Add(buttons, 0, 7);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancel;
        if (!item.RequiresBatch)
        {
            batchLabel.Visible = false;
            batchCombo.Visible = false;
        }
    }

    private void LoadBatches()
    {
        if (!item.RequiresBatch) return;
        batchCombo.DisplayMember = nameof(BatchBalance.DisplayText);
        foreach (var batch in service.GetBatchBalances(item.ItemId).Where(batch => batch.OnHand > 0))
        {
            batchCombo.Items.Add(batch);
        }

        if (batchCombo.Items.Count > 0)
        {
            batchCombo.SelectedIndex = 0;
        }
    }

    private void SaveStockOut()
    {
        if (saveButton is null) return;
        saveButton.Enabled = false;
        try
        {
            var reason = (StockOutReason)reasonCombo.SelectedItem!;
            var batchId = batchCombo.SelectedItem is BatchBalance selectedBatch ? selectedBatch.Batch.BatchId : (int?)null;
            service.StockOut(item.ItemId, (int)quantityInput.Value, reason, batchId, datePicker.Value, recordedByText.Text);
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
