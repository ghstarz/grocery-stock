namespace GroceryStock.Models;

public sealed class BatchBalance
{
    public BatchBalance(Batch batch, int onHand)
    {
        Batch = batch;
        OnHand = onHand;
    }

    public Batch Batch { get; }

    public int OnHand { get; }

    public string DisplayText => $"{Batch.BatchCode} — expires {Batch.ExpiryDate:dd MMM yyyy} ({OnHand} available)";
}
