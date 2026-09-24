using System.Globalization;
using GroceryStock.Models;

namespace GroceryStock.Data;

public sealed class ReportRepository
{
    private readonly Database database;

    public ReportRepository(Database database)
    {
        this.database = database;
    }

    public IReadOnlyList<ExpiryReportRow> GetExpiryReport(DateOnly referenceDate, DateOnly throughDate)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.ItemCode, i.Name, b.BatchCode, b.ExpiryDate,
                   COALESCE(SUM(CASE WHEN m.MovementType = 'In' THEN m.Quantity ELSE -m.Quantity END), 0)
            FROM StockItem i
            INNER JOIN Batch b ON b.ItemId = i.ItemId
            LEFT JOIN StockMovement m ON m.BatchId = b.BatchId
            WHERE i.IsPerishable = 1
              AND b.ExpiryDate <= $throughDate
            GROUP BY i.ItemId, b.BatchId
            HAVING COALESCE(SUM(CASE WHEN m.MovementType = 'In' THEN m.Quantity ELSE -m.Quantity END), 0) > 0
            ORDER BY b.ExpiryDate, i.Name, b.BatchCode;
            """;
        command.Parameters.AddWithValue("$throughDate", throughDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        using var reader = command.ExecuteReader();
        var rows = new List<ExpiryReportRow>();
        while (reader.Read())
        {
            var expiryDate = DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            rows.Add(new ExpiryReportRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                expiryDate,
                reader.GetInt32(4),
                expiryDate < referenceDate ? "Expired" : "Expiring soon"));
        }

        return rows;
    }

    public StockValuationReport GetCategoryValuation()
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH ItemBalances AS (
                SELECT ItemId,
                       SUM(CASE WHEN MovementType = 'In' THEN Quantity ELSE -Quantity END) AS OnHand
                FROM StockMovement
                GROUP BY ItemId
            )
            SELECT c.Name,
                   COALESCE(SUM(COALESCE(b.OnHand, 0) * i.CostPriceCents), 0)
            FROM Category c
            LEFT JOIN StockItem i ON i.CategoryId = c.CategoryId
            LEFT JOIN ItemBalances b ON b.ItemId = i.ItemId
            GROUP BY c.CategoryId, c.Name
            ORDER BY c.Name;
            """;

        using var reader = command.ExecuteReader();
        var categories = new List<CategoryStockValue>();
        long totalValueCents = 0;
        while (reader.Read())
        {
            var valueCents = reader.GetInt64(1);
            categories.Add(new CategoryStockValue(reader.GetString(0), valueCents));
            totalValueCents = checked(totalValueCents + valueCents);
        }

        return new StockValuationReport(categories, totalValueCents);
    }
}
