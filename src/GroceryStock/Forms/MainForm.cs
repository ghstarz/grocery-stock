using GroceryStock.Models;
using GroceryStock.Services;

namespace GroceryStock.Forms;

public sealed class MainForm : Form
{
    private readonly InventoryService service;
    private readonly DataGridView catalogueGrid = new();
    private readonly TextBox searchText = new();
    private readonly ComboBox categoryFilter = new();
    private readonly ComboBox statusFilter = new();
    private readonly ComboBox sortFilter = new();
    private readonly CheckBox lowStockFilter = new();
    private readonly Label emptyLabel = new();
    private readonly Button editButton = new();
    private readonly Button retireButton = new();
    private readonly Button receiveButton = new();
    private readonly Button stockOutButton = new();
    private readonly BindingSource bindingSource = new();

    public MainForm(InventoryService service)
    {
        this.service = service;
        Text = "Grocery Stock";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 560);
        ClientSize = new Size(1160, 700);
        BuildLayout();
        LoadLookups();
        RefreshCatalogue();
    }

    private void BuildLayout()
    {
        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = false
        };
        filters.Controls.Add(new Label { Text = "Search", AutoSize = true, Margin = new Padding(4, 8, 4, 0) });
        searchText.Width = 180;
        searchText.PlaceholderText = "Name or code";
        searchText.TextChanged += (_, _) => RefreshCatalogue();
        filters.Controls.Add(searchText);

        filters.Controls.Add(new Label { Text = "Category", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
        categoryFilter.Width = 140;
        categoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        categoryFilter.SelectedIndexChanged += (_, _) => RefreshCatalogue();
        filters.Controls.Add(categoryFilter);

        filters.Controls.Add(new Label { Text = "Status", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
        statusFilter.Width = 120;
        statusFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        statusFilter.Items.AddRange(new object[] { "Active", "All", "Inactive" });
        statusFilter.SelectedIndex = 0;
        statusFilter.SelectedIndexChanged += (_, _) => RefreshCatalogue();
        filters.Controls.Add(statusFilter);

        lowStockFilter.Text = "Low stock only";
        lowStockFilter.AutoSize = true;
        lowStockFilter.Margin = new Padding(12, 5, 4, 0);
        lowStockFilter.CheckedChanged += (_, _) => RefreshCatalogue();
        filters.Controls.Add(lowStockFilter);

        filters.Controls.Add(new Label { Text = "Sort", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
        sortFilter.Width = 150;
        sortFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        sortFilter.Items.AddRange(new object[] { "Name", "Quantity (low to high)", "Quantity (high to low)" });
        sortFilter.SelectedIndex = 0;
        sortFilter.SelectedIndexChanged += (_, _) => RefreshCatalogue();
        filters.Controls.Add(sortFilter);
        Controls.Add(filters);

        catalogueGrid.Dock = DockStyle.Fill;
        catalogueGrid.AutoGenerateColumns = false;
        catalogueGrid.AllowUserToAddRows = false;
        catalogueGrid.AllowUserToDeleteRows = false;
        catalogueGrid.ReadOnly = true;
        catalogueGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        catalogueGrid.MultiSelect = false;
        catalogueGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        catalogueGrid.RowHeadersVisible = false;
        catalogueGrid.SelectionChanged += (_, _) => UpdateActionButtons();
        AddColumn("Code", "ItemCode");
        AddColumn("Name", "Name");
        AddColumn("Category", "CategoryName");
        AddColumn("Unit", "Unit");
        AddColumn("On hand", "OnHand");
        AddColumn("Reorder level", "ReorderLevel");
        AddColumn("Shortage", "Shortage");
        AddColumn("Status", "StatusText");
        AddColumn("ItemId", "ItemId");
        var idColumn = catalogueGrid.Columns["ItemId"];
        if (idColumn is not null)
        {
            idColumn.Visible = false;
        }
        Controls.Add(catalogueGrid);

        emptyLabel.Dock = DockStyle.Bottom;
        emptyLabel.Height = 34;
        emptyLabel.Text = "No catalogue items match the current filters.";
        emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
        emptyLabel.Visible = false;
        Controls.Add(emptyLabel);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = false
        };
        var addButton = MakeButton("Add item", (_, _) => AddItem());
        editButton.Text = "Edit";
        editButton.AutoSize = true;
        editButton.Click += (_, _) => EditItem();
        retireButton.Text = "Retire";
        retireButton.AutoSize = true;
        retireButton.Click += (_, _) => RetireItem();
        receiveButton.Text = "Receive stock";
        receiveButton.AutoSize = true;
        receiveButton.Click += (_, _) => ReceiveStock();
        stockOutButton.Text = "Stock out";
        stockOutButton.AutoSize = true;
        stockOutButton.Click += (_, _) => StockOut();
        actions.Controls.AddRange(new Control[] { addButton, editButton, retireButton, receiveButton, stockOutButton });
        Controls.Add(actions);
    }

    private void LoadLookups()
    {
        categoryFilter.Items.Clear();
        categoryFilter.Items.Add(new CategoryChoice(null, "All categories"));
        foreach (var category in service.GetCategories())
        {
            categoryFilter.Items.Add(new CategoryChoice(category.CategoryId, category.Name));
        }

        categoryFilter.DisplayMember = nameof(CategoryChoice.Name);
        categoryFilter.SelectedIndex = 0;
    }

    private void RefreshCatalogue()
    {
        var status = statusFilter.SelectedIndex switch
        {
            2 => false,
            1 => (bool?)null,
            _ => true
        };
        var category = categoryFilter.SelectedItem as CategoryChoice;
        var sortIndex = sortFilter.SelectedIndex;
        var rows = service.SearchCatalogue(
            searchText.Text,
            category?.CategoryId,
            status,
            lowStockFilter.Checked,
            sortIndex > 0,
            sortIndex == 2);

        bindingSource.DataSource = rows.Select(summary => new CatalogueRow(summary)).ToList();
        catalogueGrid.DataSource = bindingSource;
        emptyLabel.Visible = rows.Count == 0;
        UpdateActionButtons();
    }

    private void AddItem()
    {
        using var form = new ItemForm(service, service.GetCategories(), null);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            RefreshCatalogue();
        }
    }

    private void EditItem()
    {
        var item = SelectedItem();
        if (item is null) return;
        using var form = new ItemForm(service, service.GetCategories(), item);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            RefreshCatalogue();
        }
    }

    private void RetireItem()
    {
        var item = SelectedItem();
        if (item is null) return;
        try
        {
            service.RetireItem(item.ItemId);
            RefreshCatalogue();
        }
        catch (Exception ex) when (ex is InventoryValidationException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Cannot retire item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ReceiveStock()
    {
        var item = SelectedItem();
        if (item is null) return;
        using var form = new ReceiveStockForm(service, item);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            RefreshCatalogue();
        }
    }

    private void StockOut()
    {
        var item = SelectedItem();
        if (item is null) return;
        using var form = new StockOutForm(service, item);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            RefreshCatalogue();
        }
    }

    private StockItem? SelectedItem()
    {
        return catalogueGrid.CurrentRow?.DataBoundItem is CatalogueRow row ? row.Summary.Item : null;
    }

    private void UpdateActionButtons()
    {
        var item = SelectedItem();
        var hasItem = item is not null;
        editButton.Enabled = hasItem;
        retireButton.Enabled = item?.IsActive == true;
        receiveButton.Enabled = item?.IsActive == true;
        stockOutButton.Enabled = item?.IsActive == true;
    }

    private void AddColumn(string header, string propertyName)
    {
        catalogueGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = propertyName, Name = propertyName });
    }

    private static Button MakeButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += handler;
        return button;
    }

    private sealed record CategoryChoice(int? CategoryId, string Name);

    private sealed class CatalogueRow
    {
        public CatalogueRow(StockSummary summary)
        {
            Summary = summary;
        }

        public StockSummary Summary { get; }
        public int ItemId => Summary.Item.ItemId;
        public string ItemCode => Summary.Item.ItemCode;
        public string Name => Summary.Item.Name;
        public string CategoryName => Summary.Item.CategoryName;
        public string Unit => Summary.Item.Unit;
        public int OnHand => Summary.OnHand;
        public int ReorderLevel => Summary.Item.ReorderLevel;
        public int Shortage => Summary.Shortage;
        public string StatusText => Summary.StatusText;
    }
}
