namespace GroceryStock.Models;

public sealed record ExpiryReportRow(
    string ItemCode,
    string ItemName,
    string BatchCode,
    DateOnly ExpiryDate,
    int OnHand,
    string Status);
