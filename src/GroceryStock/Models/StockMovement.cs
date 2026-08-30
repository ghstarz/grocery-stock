namespace GroceryStock.Models;

public sealed class StockMovement
{
    public StockMovement(
        int movementId,
        int itemId,
        int? batchId,
        MovementType movementType,
        int quantity,
        decimal unitCost,
        DateTime movementDate,
        string reason,
        int? supplierId,
        string recordedBy)
    {
        MovementId = movementId;
        ItemId = StockItem.RequirePositive(itemId, nameof(itemId));
        if (batchId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchId), "A batch identifier must be positive when supplied.");
        }

        BatchId = batchId;
        MovementType = movementType;
        Quantity = StockItem.RequirePositive(quantity, nameof(quantity));
        UnitCost = StockItem.ValidateMoney(unitCost, nameof(unitCost));
        MovementDate = movementDate;
        Reason = StockItem.RequireText(reason, nameof(reason));
        if (supplierId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(supplierId), "A supplier identifier must be positive when supplied.");
        }

        SupplierId = supplierId;
        RecordedBy = StockItem.RequireText(recordedBy, nameof(recordedBy));
    }

    public int MovementId { get; }

    public int ItemId { get; }

    public int? BatchId { get; }

    public MovementType MovementType { get; }

    public int Quantity { get; }

    public decimal UnitCost { get; }

    public DateTime MovementDate { get; }

    public string Reason { get; }

    public int? SupplierId { get; }

    public string RecordedBy { get; }
}
