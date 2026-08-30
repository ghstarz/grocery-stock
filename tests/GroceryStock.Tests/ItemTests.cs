using GroceryStock.Models;

namespace GroceryStock.Tests;

public sealed class ItemTests
{
    [Fact]
    public void Blank_code_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new StandardItem(0, " ", "Rice", 1, "bag", 1.00m, 2.00m, 5));
    }

    [Fact]
    public void Blank_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new StandardItem(0, "RICE001", " ", 1, "bag", 1.00m, 2.00m, 5));
    }

    [Fact]
    public void Negative_prices_and_reorder_level_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StandardItem(0, "RICE001", "Rice", 1, "bag", -0.01m, 2.00m, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StandardItem(0, "RICE001", "Rice", 1, "bag", 1.00m, 2.00m, -1));
    }

    [Fact]
    public void Money_with_more_than_two_decimal_places_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StandardItem(0, "RICE001", "Rice", 1, "bag", 1.001m, 2.00m, 5));
    }

    [Fact]
    public void Codes_are_trimmed_and_normalized_to_uppercase()
    {
        var item = new StandardItem(0, " rice001 ", "Rice", 1, "bag", 1.00m, 2.00m, 5);

        Assert.Equal("RICE001", item.ItemCode);
    }

    [Fact]
    public void Standard_and_perishable_items_expose_polymorphic_batch_rules()
    {
        StockItem standard = new StandardItem(0, "RICE001", "Rice", 1, "bag", 1.00m, 2.00m, 5);
        StockItem perishable = new PerishableItem(0, "MILK001", "Milk", 1, "bottle", 1.00m, 2.00m, 3);

        Assert.False(standard.RequiresBatch);
        Assert.True(perishable.RequiresBatch);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Movement_quantity_must_be_a_positive_whole_unit(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockMovement(
            0, 1, null, MovementType.In, quantity, 1.00m, DateTime.Today, "Receipt", 1, "Tester"));
    }
}
