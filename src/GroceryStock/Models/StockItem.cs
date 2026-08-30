namespace GroceryStock.Models;

public abstract class StockItem
{
    protected StockItem(
        int itemId,
        string itemCode,
        string name,
        int categoryId,
        string unit,
        decimal costPrice,
        decimal salePrice,
        int reorderLevel,
        bool isActive = true)
    {
        ItemId = itemId;
        ItemCode = NormalizeCode(itemCode);
        Name = RequireText(name, nameof(name));
        CategoryId = RequirePositive(categoryId, nameof(categoryId));
        Unit = RequireText(unit, nameof(unit));
        CostPrice = ValidateMoney(costPrice, nameof(costPrice));
        SalePrice = ValidateMoney(salePrice, nameof(salePrice));
        ReorderLevel = RequireNonNegative(reorderLevel, nameof(reorderLevel));
        IsActive = isActive;
    }

    public int ItemId { get; }

    public string ItemCode { get; private set; }

    public string Name { get; private set; }

    public int CategoryId { get; private set; }

    public string CategoryName { get; internal set; } = string.Empty;

    public string Unit { get; private set; }

    public decimal CostPrice { get; private set; }

    public decimal SalePrice { get; private set; }

    public int ReorderLevel { get; private set; }

    public bool IsActive { get; internal set; }

    public abstract bool RequiresBatch { get; }

    public void UpdateDetails(
        string itemCode,
        string name,
        int categoryId,
        string unit,
        decimal costPrice,
        decimal salePrice,
        int reorderLevel)
    {
        ItemCode = NormalizeCode(itemCode);
        Name = RequireText(name, nameof(name));
        CategoryId = RequirePositive(categoryId, nameof(categoryId));
        Unit = RequireText(unit, nameof(unit));
        CostPrice = ValidateMoney(costPrice, nameof(costPrice));
        SalePrice = ValidateMoney(salePrice, nameof(salePrice));
        ReorderLevel = RequireNonNegative(reorderLevel, nameof(reorderLevel));
    }

    public static string NormalizeCode(string value)
    {
        return RequireText(value, nameof(value)).ToUpperInvariant();
    }

    public static decimal ValidateMoney(decimal value, string parameterName)
    {
        if (value < 0 || decimal.Round(value, 2) != value)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Money values must be non-negative and use no more than two decimal places.");
        }

        return value;
    }

    public static int RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value must be greater than zero.");
        }

        return value;
    }

    public static int RequireNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value cannot be negative.");
        }

        return value;
    }

    public static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }
}
