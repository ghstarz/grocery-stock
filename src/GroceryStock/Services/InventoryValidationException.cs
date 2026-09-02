namespace GroceryStock.Services;

public sealed class InventoryValidationException : Exception
{
    public InventoryValidationException(string message)
        : base(message)
    {
    }

    public InventoryValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
