using System.Globalization;
using GroceryStock.Models;
using GroceryStock.Services;
using Microsoft.Data.Sqlite;

namespace GroceryStock.Data;

public sealed class MovementRepository
{
    private readonly Database database;

    public MovementRepository(Database database)
    {
        this.database = database;
    }

    public StockMovement ReceiveStock(
        int itemId,
        int quantity,
        decimal unitCost,
        int? supplierId,
        DateTime movementDate,
        string recordedBy,
        string? batchCode,
        DateOnly? expiryDate)
    {
        StockItem.RequirePositive(quantity, nameof(quantity));
        StockItem.ValidateMoney(unitCost, nameof(unitCost));
        StockItem.RequireText(recordedBy, nameof(recordedBy));
        if (supplierId is null or <= 0)
        {
            throw new InventoryValidationException("A supplier is required for an incoming delivery.");
        }

        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var itemInfo = ReadItemInfo(connection, transaction, itemId);
            if (!itemInfo.IsActive)
            {
                throw new InventoryValidationException("Retired items cannot receive stock.");
            }

            int? batchId = null;
            if (itemInfo.RequiresBatch)
            {
                if (string.IsNullOrWhiteSpace(batchCode) || !expiryDate.HasValue)
                {
                    throw new InventoryValidationException("A perishable delivery needs a batch code and expiry date.");
                }

                var normalizedBatchCode = batchCode.Trim().ToUpperInvariant();

                if (expiryDate.Value < DateOnly.FromDateTime(DateTime.Today))
                {
                    throw new InventoryValidationException("A delivery with an expiry date before today cannot be received.");
                }

                batchId = FindOrCreateBatch(connection, transaction, itemId, normalizedBatchCode, expiryDate.Value);
            }
            else if (!string.IsNullOrWhiteSpace(batchCode) || expiryDate.HasValue)
            {
                throw new InventoryValidationException("Standard items cannot receive batch details.");
            }

            using var movementCommand = connection.CreateCommand();
            movementCommand.Transaction = transaction;
            movementCommand.CommandText = """
                INSERT INTO StockMovement
                    (ItemId, BatchId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, SupplierId, RecordedBy)
                VALUES ($itemId, $batchId, 'In', 'Receipt', $quantity, $unitCost, $movementDate, $supplierId, $recordedBy);
                SELECT last_insert_rowid();
                """;
            movementCommand.Parameters.AddWithValue("$itemId", itemId);
            movementCommand.Parameters.AddWithValue("$batchId", (object?)batchId ?? DBNull.Value);
            movementCommand.Parameters.AddWithValue("$quantity", quantity);
            movementCommand.Parameters.AddWithValue("$unitCost", ItemRepository.ToCents(unitCost));
            movementCommand.Parameters.AddWithValue("$movementDate", FormatDateTime(movementDate));
            movementCommand.Parameters.AddWithValue("$supplierId", supplierId.Value);
            movementCommand.Parameters.AddWithValue("$recordedBy", recordedBy.Trim());
            var movementId = Convert.ToInt32(movementCommand.ExecuteScalar());
            transaction.Commit();
            return new StockMovement(movementId, itemId, batchId, MovementType.In, quantity, unitCost, movementDate, "Receipt", supplierId, recordedBy);
        }
        catch (InventoryValidationException)
        {
            transaction.Rollback();
            throw;
        }
        catch (SqliteException ex)
        {
            transaction.Rollback();
            throw new InventoryValidationException("The delivery could not be saved, so no stock was changed.", ex);
        }
    }

    public int GetItemBalance(int itemId)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = BalanceSql + " WHERE ItemId = $id;";
        command.Parameters.AddWithValue("$id", itemId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public IReadOnlyList<BatchBalance> GetBatchBalances(int itemId)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.BatchId, b.ItemId, b.BatchCode, b.ExpiryDate,
                   COALESCE(SUM(CASE WHEN m.MovementType = 'In' THEN m.Quantity ELSE -m.Quantity END), 0)
            FROM Batch b
            LEFT JOIN StockMovement m ON m.BatchId = b.BatchId
            WHERE b.ItemId = $itemId
            GROUP BY b.BatchId
            ORDER BY b.ExpiryDate, b.BatchCode;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        using var reader = command.ExecuteReader();
        var batches = new List<BatchBalance>();
        while (reader.Read())
        {
            var batch = new Batch(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture));
            batches.Add(new BatchBalance(batch, reader.GetInt32(4)));
        }

        return batches;
    }

    public IReadOnlyList<StockMovement> GetMovements(int itemId)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MovementId, ItemId, BatchId, MovementType, Quantity,
                   UnitCostCents, MovementDate, Reason, SupplierId, RecordedBy
            FROM StockMovement
            WHERE ItemId = $itemId
            ORDER BY MovementDate, MovementId;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        using var reader = command.ExecuteReader();
        var movements = new List<StockMovement>();
        while (reader.Read())
        {
            movements.Add(new StockMovement(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Enum.Parse<MovementType>(reader.GetString(3), true),
                reader.GetInt32(4),
                ItemRepository.FromCents(reader.GetInt64(5)),
                DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.GetString(9)));
        }

        return movements;
    }

    public StockMovement RecordStockOut(
        int itemId,
        int quantity,
        StockOutReason reason,
        int? batchId,
        DateTime movementDate,
        string recordedBy)
    {
        StockItem.RequirePositive(quantity, nameof(quantity));
        StockItem.RequireText(recordedBy, nameof(recordedBy));

        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var itemInfo = ReadItemInfo(connection, transaction, itemId);
            if (!itemInfo.IsActive)
            {
                throw new InventoryValidationException("Retired items cannot issue stock.");
            }

            if (itemInfo.RequiresBatch && batchId is null)
            {
                throw new InventoryValidationException("Select a batch for a perishable stock out.");
            }

            if (!itemInfo.RequiresBatch && batchId.HasValue)
            {
                throw new InventoryValidationException("Standard items do not use batches.");
            }

            DateOnly? expiryDate = null;
            if (batchId.HasValue)
            {
                expiryDate = ReadBatchExpiry(connection, transaction, itemId, batchId.Value);
                if (expiryDate is null)
                {
                    throw new InventoryValidationException("The selected batch does not belong to this item.");
                }

                if (reason == StockOutReason.Sale && expiryDate.Value < DateOnly.FromDateTime(DateTime.Today))
                {
                    throw new InventoryValidationException("An expired batch cannot be sold. Record an expiry write-off instead.");
                }

                var batchBalance = ReadBatchBalance(connection, transaction, itemId, batchId.Value);
                if (batchBalance < quantity)
                {
                    throw new InventoryValidationException("The selected batch does not have enough stock.");
                }
            }

            var itemBalance = ReadItemBalance(connection, transaction, itemId);
            if (itemBalance < quantity)
            {
                throw new InventoryValidationException("The stock out would make the item balance negative.");
            }

            using var movementCommand = connection.CreateCommand();
            movementCommand.Transaction = transaction;
            movementCommand.CommandText = """
                INSERT INTO StockMovement
                    (ItemId, BatchId, MovementType, Reason, Quantity, UnitCostCents, MovementDate, SupplierId, RecordedBy)
                SELECT $itemId, $batchId, 'Out', $reason, $quantity, CostPriceCents, $movementDate, NULL, $recordedBy
                FROM StockItem
                WHERE ItemId = $itemId;
                SELECT last_insert_rowid();
                """;
            movementCommand.Parameters.AddWithValue("$itemId", itemId);
            movementCommand.Parameters.AddWithValue("$batchId", (object?)batchId ?? DBNull.Value);
            movementCommand.Parameters.AddWithValue("$reason", reason.ToString());
            movementCommand.Parameters.AddWithValue("$quantity", quantity);
            movementCommand.Parameters.AddWithValue("$movementDate", FormatDateTime(movementDate));
            movementCommand.Parameters.AddWithValue("$recordedBy", recordedBy.Trim());
            var movementId = Convert.ToInt32(movementCommand.ExecuteScalar());
            transaction.Commit();
            var cost = ReadItemCost(connection, itemId);
            return new StockMovement(movementId, itemId, batchId, MovementType.Out, quantity, cost, movementDate, reason.ToString(), null, recordedBy);
        }
        catch (InventoryValidationException)
        {
            transaction.Rollback();
            throw;
        }
        catch (SqliteException ex)
        {
            transaction.Rollback();
            throw new InventoryValidationException("The stock out could not be saved, so no stock was changed.", ex);
        }
    }

    private static int? FindOrCreateBatch(SqliteConnection connection, SqliteTransaction transaction, int itemId, string batchCode, DateOnly expiryDate)
    {
        using (var find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT BatchId, ExpiryDate FROM Batch WHERE ItemId = $itemId AND BatchCode = $code;";
            find.Parameters.AddWithValue("$itemId", itemId);
            find.Parameters.AddWithValue("$code", batchCode);
            using var reader = find.ExecuteReader();
            if (reader.Read())
            {
                var existingExpiry = DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (existingExpiry != expiryDate)
                {
                    throw new InventoryValidationException("The existing batch code already has a different expiry date.");
                }

                return reader.GetInt32(0);
            }
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO Batch (ItemId, BatchCode, ExpiryDate)
            VALUES ($itemId, $code, $expiryDate);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$itemId", itemId);
        insert.Parameters.AddWithValue("$code", batchCode);
        insert.Parameters.AddWithValue("$expiryDate", expiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return Convert.ToInt32(insert.ExecuteScalar());
    }

    private static (bool RequiresBatch, bool IsActive) ReadItemInfo(SqliteConnection connection, SqliteTransaction transaction, int itemId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT IsPerishable, IsActive FROM StockItem WHERE ItemId = $id;";
        command.Parameters.AddWithValue("$id", itemId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InventoryValidationException("The item could not be found.");
        }

        return (reader.GetInt32(0) == 1, reader.GetInt32(1) == 1);
    }

    private static DateOnly? ReadBatchExpiry(SqliteConnection connection, SqliteTransaction transaction, int itemId, int batchId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ExpiryDate FROM Batch WHERE BatchId = $batchId AND ItemId = $itemId;";
        command.Parameters.AddWithValue("$batchId", batchId);
        command.Parameters.AddWithValue("$itemId", itemId);
        var value = command.ExecuteScalar();
        return value is null || value is DBNull
            ? null
            : DateOnly.ParseExact((string)value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static int ReadBatchBalance(SqliteConnection connection, SqliteTransaction transaction, int itemId, int batchId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = BalanceSql + " WHERE ItemId = $itemId AND BatchId = $batchId;";
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$batchId", batchId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int ReadItemBalance(SqliteConnection connection, SqliteTransaction transaction, int itemId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = BalanceSql + " WHERE ItemId = $itemId;";
        command.Parameters.AddWithValue("$itemId", itemId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private decimal ReadItemCost(SqliteConnection connection, int itemId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CostPriceCents FROM StockItem WHERE ItemId = $id;";
        command.Parameters.AddWithValue("$id", itemId);
        return ItemRepository.FromCents(Convert.ToInt64(command.ExecuteScalar()));
    }

    private static string FormatDateTime(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string BalanceSql = """
        SELECT COALESCE(SUM(CASE WHEN MovementType = 'In' THEN Quantity ELSE -Quantity END), 0)
        FROM StockMovement
        """;
}
