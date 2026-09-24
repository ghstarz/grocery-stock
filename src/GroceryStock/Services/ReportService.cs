using GroceryStock.Data;
using GroceryStock.Models;

namespace GroceryStock.Services;

public sealed class ReportService
{
    private readonly ReportRepository reports;
    private readonly ItemRepository items;

    public ReportService(Database database)
        : this(new ReportRepository(database), new ItemRepository(database))
    {
    }

    public ReportService(ReportRepository reports, ItemRepository items)
    {
        this.reports = reports;
        this.items = items;
    }

    public IReadOnlyList<ExpiryReportRow> GetExpiryReport(int daysAhead = 7)
    {
        return GetExpiryReport(daysAhead, DateOnly.FromDateTime(DateTime.Today));
    }

    public IReadOnlyList<ExpiryReportRow> GetExpiryReport(int daysAhead, DateOnly referenceDate)
    {
        if (daysAhead < 0 || daysAhead > DateOnly.MaxValue.DayNumber - referenceDate.DayNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(daysAhead), "The expiry interval must be nonnegative and fit within the supported date range.");
        }

        return reports.GetExpiryReport(referenceDate, referenceDate.AddDays(daysAhead));
    }

    public IReadOnlyList<ReorderReportRow> GetReorderItems()
    {
        return items.GetSummaries(includeInactive: false)
            .Where(summary => summary.IsLowStock)
            .Select(summary => new ReorderReportRow(
                summary.Item.ItemCode,
                summary.Item.Name,
                summary.Item.CategoryName,
                summary.Item.Unit,
                summary.OnHand,
                summary.Item.ReorderLevel))
            .OrderBy(row => row.ItemName)
            .ThenBy(row => row.ItemCode)
            .ToList();
    }

    public StockValuationReport GetCategoryValuation() => reports.GetCategoryValuation();
}
