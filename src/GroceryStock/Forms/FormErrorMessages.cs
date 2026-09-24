using Microsoft.Data.Sqlite;

namespace GroceryStock.Forms;

internal static class FormErrorMessages
{
    public static bool IsStorageFailure(Exception exception)
    {
        return exception is SqliteException or IOException or UnauthorizedAccessException;
    }

    public static string StorageFailure(string action, Exception exception)
    {
        return $"{action}. Check that the database or file is available and that you have permission to use it.\r\n\r\nDetails: {exception.Message}";
    }

    public static void ShowStorageFailure(IWin32Window owner, string action, Exception exception)
    {
        MessageBox.Show(owner, StorageFailure(action, exception), "Storage problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
