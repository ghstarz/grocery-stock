namespace GroceryStock.Models;

public sealed record ReorderReportRow(
    string ItemCode,
    string ItemName,
    string CategoryName,
    string Unit,
    int OnHand,
    int ReorderLevel)
{
    public int Shortage => Math.Max(0, ReorderLevel - OnHand);
}
