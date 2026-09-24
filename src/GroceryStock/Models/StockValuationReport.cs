namespace GroceryStock.Models;

public sealed record StockValuationReport(
    IReadOnlyList<CategoryStockValue> Categories,
    long TotalValueCents)
{
    public decimal TotalValue => TotalValueCents / 100m;
}
