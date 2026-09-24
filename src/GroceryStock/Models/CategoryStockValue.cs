namespace GroceryStock.Models;

public sealed record CategoryStockValue(string CategoryName, long ValueCents)
{
    public decimal Value => ValueCents / 100m;
}
