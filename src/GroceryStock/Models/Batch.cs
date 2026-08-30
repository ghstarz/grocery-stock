namespace GroceryStock.Models;

public sealed class Batch
{
    public Batch(int batchId, int itemId, string batchCode, DateOnly expiryDate)
    {
        BatchId = batchId;
        ItemId = StockItem.RequirePositive(itemId, nameof(itemId));
        BatchCode = StockItem.RequireText(batchCode, nameof(batchCode)).ToUpperInvariant();
        ExpiryDate = expiryDate;
    }

    public int BatchId { get; }

    public int ItemId { get; }

    public string BatchCode { get; }

    public DateOnly ExpiryDate { get; }
}
