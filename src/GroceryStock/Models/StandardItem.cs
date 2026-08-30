namespace GroceryStock.Models;

public sealed class StandardItem : StockItem
{
    public StandardItem(
        int itemId,
        string itemCode,
        string name,
        int categoryId,
        string unit,
        decimal costPrice,
        decimal salePrice,
        int reorderLevel,
        bool isActive = true)
        : base(itemId, itemCode, name, categoryId, unit, costPrice, salePrice, reorderLevel, isActive)
    {
    }

    public override bool RequiresBatch => false;
}
