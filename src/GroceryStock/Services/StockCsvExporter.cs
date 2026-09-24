using System.Globalization;
using System.Text;
using GroceryStock.Models;

namespace GroceryStock.Services;

public sealed class StockCsvExporter
{
    private static readonly UTF8Encoding Utf8WithBom = new(true);

    public void Export(string destinationPath, IEnumerable<StockSummary> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(rows);

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("The export destination must include a directory.", nameof(destinationPath));
        }

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8WithBom, leaveOpen: true) { NewLine = "\r\n" })
            {
                writer.WriteLine("Item code,Name,Category,Unit,On hand,Reorder level,Cost price,Sale price,Active");
                foreach (var row in rows)
                {
                    var item = row.Item;
                    var fields = new[]
                    {
                        item.ItemCode,
                        item.Name,
                        item.CategoryName,
                        item.Unit,
                        row.OnHand.ToString(CultureInfo.InvariantCulture),
                        item.ReorderLevel.ToString(CultureInfo.InvariantCulture),
                        item.CostPrice.ToString("0.00", CultureInfo.InvariantCulture),
                        item.SalePrice.ToString("0.00", CultureInfo.InvariantCulture),
                        item.IsActive ? "Yes" : "No"
                    };
                    writer.WriteLine(string.Join(",", fields.Select(EncodeField)));
                }

                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Keep the original destination intact if temporary-file cleanup is blocked.
                }
                catch (UnauthorizedAccessException)
                {
                    // Keep the original destination intact if temporary-file cleanup is blocked.
                }
            }
        }
    }

    private static string EncodeField(string value)
    {
        var firstTextCharacter = value.FirstOrDefault(character => !char.IsWhiteSpace(character));
        if (value.Length > 0
            && (char.IsControl(value[0]) || "=+-@".Contains(firstTextCharacter, StringComparison.Ordinal)))
        {
            value = "'" + value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
