namespace GroceryStock.Models;

public sealed class StockSummary
{
    public StockSummary(StockItem item, int onHand, DateOnly? nearestExpiry = null)
    {
        Item = item;
        OnHand = onHand;
        NearestExpiry = nearestExpiry;
    }

    public StockItem Item { get; }

    public int OnHand { get; }

    public int Shortage => Math.Max(0, Item.ReorderLevel - OnHand);

    public bool IsLowStock => OnHand <= Item.ReorderLevel;

    public DateOnly? NearestExpiry { get; }

    public string StatusText => IsLowStock ? "Low stock" : "OK";
}
