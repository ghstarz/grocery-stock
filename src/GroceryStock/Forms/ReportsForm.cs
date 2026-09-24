using System.Globalization;
using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Forms;

public sealed class ReportsForm : Form
{
    private readonly ReportService service;
    private readonly NumericUpDown expiryDays = new() { Minimum = 0, Maximum = 3650, DecimalPlaces = 0, Value = 7, Width = 80 };
    private readonly DataGridView expiryGrid = CreateGrid();
    private readonly DataGridView reorderGrid = CreateGrid();
    private readonly DataGridView valuationGrid = CreateGrid();
    private readonly Label valuationBasis = new() { AutoSize = true };
    private readonly Label valuationTotal = new() { AutoSize = true };
    private readonly Label errorLabel = new() { AutoSize = true, ForeColor = Color.DarkRed };

    public ReportsForm(ReportService service)
    {
        this.service = service;
        Text = "Stock reports";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 560);
        ClientSize = new Size(1060, 660);
        BuildLayout();
        Shown += (_, _) => RefreshReports();
        expiryDays.ValueChanged += (_, _) => RefreshExpiryReport();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildExpiryPage());
        tabs.TabPages.Add(BuildReorderPage());
        tabs.TabPages.Add(BuildValuationPage());
        root.Controls.Add(tabs, 0, 0);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(errorLabel, 0, 0);
        var closeButton = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
        footer.Controls.Add(closeButton, 1, 0);
        root.Controls.Add(footer, 0, 1);
        Controls.Add(root);
        CancelButton = closeButton;
    }

    private TabPage BuildExpiryPage()
    {
        var page = new TabPage("Expiry");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            TabIndex = 0
        };
        controls.Controls.Add(new Label { Text = "Show expired stock and items expiring within", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
        expiryDays.TabIndex = 1;
        controls.Controls.Add(expiryDays);
        controls.Controls.Add(new Label { Text = "days after today", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
        layout.Controls.Add(controls, 0, 0);

        AddTextColumn(expiryGrid, "Code", nameof(ExpiryGridRow.ItemCode), 80);
        AddTextColumn(expiryGrid, "Item", nameof(ExpiryGridRow.ItemName), 150);
        AddTextColumn(expiryGrid, "Batch", nameof(ExpiryGridRow.BatchCode), 100);
        AddTextColumn(expiryGrid, "Expiry date", nameof(ExpiryGridRow.ExpiryDate), 95);
        AddTextColumn(expiryGrid, "Remaining", nameof(ExpiryGridRow.OnHand), 75);
        AddTextColumn(expiryGrid, "Status", nameof(ExpiryGridRow.Status), 100);
        expiryGrid.TabIndex = 2;
        layout.Controls.Add(expiryGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildReorderPage()
    {
        var page = new TabPage("Reorder");
        AddTextColumn(reorderGrid, "Code", nameof(ReorderReportRow.ItemCode), 80);
        AddTextColumn(reorderGrid, "Name", nameof(ReorderReportRow.ItemName), 160);
        AddTextColumn(reorderGrid, "Category", nameof(ReorderReportRow.CategoryName), 110);
        AddTextColumn(reorderGrid, "On hand", nameof(ReorderReportRow.OnHand), 75);
        AddTextColumn(reorderGrid, "Reorder level", nameof(ReorderReportRow.ReorderLevel), 90);
        AddTextColumn(reorderGrid, "Shortage", nameof(ReorderReportRow.Shortage), 75);
        reorderGrid.TabIndex = 0;
        page.Controls.Add(reorderGrid);
        return page;
    }

    private TabPage BuildValuationPage()
    {
        var page = new TabPage("Stock value");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        valuationBasis.Text = "Basis: quantity on hand × current catalogue cost. This report does not use FIFO or weighted-average costing.";
        valuationBasis.TabIndex = 0;
        layout.Controls.Add(valuationBasis, 0, 0);
        AddTextColumn(valuationGrid, "Category", nameof(ValuationGridRow.CategoryName), 180);
        var valueColumn = AddTextColumn(valuationGrid, "Stock value", nameof(ValuationGridRow.Value), 120);
        valueColumn.DefaultCellStyle.Format = "0.00";
        valuationGrid.TabIndex = 1;
        layout.Controls.Add(valuationGrid, 0, 1);
        valuationTotal.TabIndex = 2;
        layout.Controls.Add(valuationTotal, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private void RefreshReports()
    {
        try
        {
            RefreshExpiryReport();
            reorderGrid.DataSource = service.GetReorderItems();
            var valuation = service.GetCategoryValuation();
            valuationGrid.DataSource = valuation.Categories
                .Select(value => new ValuationGridRow(value.CategoryName, value.Value))
                .ToList();
            valuationTotal.Text = $"Total stock value: {valuation.TotalValue.ToString("0.00", CultureInfo.InvariantCulture)}";
            errorLabel.Text = string.Empty;
        }
        catch (Exception exception) when (FormErrorMessages.IsStorageFailure(exception))
        {
            errorLabel.Text = FormErrorMessages.StorageFailure("A report could not be read", exception);
        }
    }

    private void RefreshExpiryReport()
    {
        try
        {
            var referenceDate = DateOnly.FromDateTime(DateTime.Today);
            expiryGrid.DataSource = service.GetExpiryReport((int)expiryDays.Value, referenceDate)
                .Select(row => new ExpiryGridRow(
                    row.ItemCode,
                    row.ItemName,
                    row.BatchCode,
                    row.ExpiryDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                    row.OnHand,
                    row.Status))
                .ToList();
            errorLabel.Text = string.Empty;
        }
        catch (Exception exception) when (FormErrorMessages.IsStorageFailure(exception) || exception is ArgumentOutOfRangeException)
        {
            errorLabel.Text = exception is ArgumentOutOfRangeException
                ? exception.Message
                : FormErrorMessages.StorageFailure("The expiry report could not be read", exception);
        }
    }

    private static DataGridView CreateGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false
        };
    }

    private static DataGridViewTextBoxColumn AddTextColumn(DataGridView grid, string title, string propertyName, float fillWeight)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = title,
            DataPropertyName = propertyName,
            Name = propertyName,
            FillWeight = fillWeight,
            ReadOnly = true
        };
        grid.Columns.Add(column);
        return column;
    }

    private sealed record ExpiryGridRow(
        string ItemCode,
        string ItemName,
        string BatchCode,
        string ExpiryDate,
        int OnHand,
        string Status);

    private sealed record ValuationGridRow(string CategoryName, decimal Value);
}
