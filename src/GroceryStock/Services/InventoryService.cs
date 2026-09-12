using GroceryStock.Data;
using GroceryStock.Models;

namespace GroceryStock.Services;

public sealed class InventoryService
{
    private readonly ItemRepository items;
    private readonly LookupRepository lookups;
    private readonly MovementRepository movements;

    public InventoryService(Database database)
        : this(new ItemRepository(database), new LookupRepository(database), new MovementRepository(database))
    {
    }

    public InventoryService(ItemRepository items, LookupRepository lookups, MovementRepository movements)
    {
        this.items = items;
        this.lookups = lookups;
        this.movements = movements;
    }

    public IReadOnlyList<Category> GetCategories() => lookups.GetCategories();

    public IReadOnlyList<Supplier> GetSuppliers() => lookups.GetSuppliers();

    public Supplier GetOrCreateSupplier(string name) => lookups.GetOrCreateSupplier(name);

    public IReadOnlyList<BatchBalance> GetBatchBalances(int itemId) => movements.GetBatchBalances(itemId);

    public IReadOnlyList<StockMovement> GetMovements(int itemId) => movements.GetMovements(itemId);

    public StockMovement ReceiveStock(
        int itemId,
        int quantity,
        decimal unitCost,
        int supplierId,
        DateTime movementDate,
        string recordedBy,
        string? batchCode,
        DateOnly? expiryDate)
    {
        var item = items.GetById(itemId) ?? throw new InventoryValidationException("The item could not be found.");
        if (!item.IsActive)
        {
            throw new InventoryValidationException("Retired items cannot receive stock.");
        }

        return movements.ReceiveStock(itemId, quantity, unitCost, supplierId, movementDate, recordedBy, batchCode, expiryDate);
    }

    public StockMovement StockOut(
        int itemId,
        int quantity,
        StockOutReason reason,
        int? batchId,
        DateTime movementDate,
        string recordedBy)
    {
        var item = items.GetById(itemId) ?? throw new InventoryValidationException("The item could not be found.");
        if (!item.IsActive)
        {
            throw new InventoryValidationException("Retired items cannot issue stock.");
        }

        return movements.RecordStockOut(itemId, quantity, reason, batchId, movementDate, recordedBy);
    }

    public StockItem AddItem(
        string itemCode,
        string name,
        int categoryId,
        string unit,
        decimal costPrice,
        decimal salePrice,
        int reorderLevel,
        bool perishable)
    {
        StockItem item = perishable
            ? new PerishableItem(0, itemCode, name, categoryId, unit, costPrice, salePrice, reorderLevel)
            : new StandardItem(0, itemCode, name, categoryId, unit, costPrice, salePrice, reorderLevel);
        return items.Add(item);
    }

    public StockItem UpdateItem(
        int itemId,
        string itemCode,
        string name,
        int categoryId,
        string unit,
        decimal costPrice,
        decimal salePrice,
        int reorderLevel,
        bool perishable)
    {
        var existing = items.GetById(itemId) ?? throw new InventoryValidationException("The item could not be found.");
        var hasMovements = items.HasMovements(itemId);
        if (hasMovements && existing.RequiresBatch != perishable)
        {
            throw new InventoryValidationException("The item type cannot change after stock movements exist.");
        }

        if (hasMovements && !string.Equals(existing.Unit, unit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InventoryValidationException("The unit cannot change after stock movements exist.");
        }

        StockItem replacement = perishable
            ? new PerishableItem(itemId, itemCode, name, categoryId, unit, costPrice, salePrice, reorderLevel, existing.IsActive)
            : new StandardItem(itemId, itemCode, name, categoryId, unit, costPrice, salePrice, reorderLevel, existing.IsActive);
        items.Update(replacement);
        return items.GetById(itemId) ?? replacement;
    }

    public void RetireItem(int itemId)
    {
        var item = items.GetById(itemId) ?? throw new InventoryValidationException("The item could not be found.");
        if (!item.IsActive)
        {
            throw new InventoryValidationException("The item is already retired.");
        }

        if (items.GetOnHand(itemId) != 0)
        {
            throw new InventoryValidationException("An item can only be retired when its stock balance is zero.");
        }

        items.SetActive(itemId, false);
    }

    public IReadOnlyList<StockSummary> SearchCatalogue(
        string? searchText = null,
        int? categoryId = null,
        bool? active = true,
        bool lowStockOnly = false,
        bool sortByQuantity = false,
        bool descending = false)
    {
        var summaries = items.GetSummaries(includeInactive: true).AsEnumerable();
        if (active is true)
        {
            summaries = summaries.Where(summary => summary.Item.IsActive);
        }
        else if (active is false)
        {
            summaries = summaries.Where(summary => !summary.Item.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            summaries = summaries.Where(summary =>
                summary.Item.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                || summary.Item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (categoryId.HasValue)
        {
            summaries = summaries.Where(summary => summary.Item.CategoryId == categoryId.Value);
        }

        if (lowStockOnly)
        {
            summaries = summaries.Where(summary => summary.IsLowStock);
        }

        summaries = sortByQuantity
            ? (descending ? summaries.OrderByDescending(summary => summary.OnHand) : summaries.OrderBy(summary => summary.OnHand))
            : summaries.OrderBy(summary => summary.Item.Name).ThenBy(summary => summary.Item.ItemCode);
        return summaries.ToList();
    }
}
