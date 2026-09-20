namespace HesabdariAsan.Native.Models;

public sealed class DatabaseDiagnostics
{
    public long DatabaseBytes { get; init; }
    public long WalBytes { get; init; }
    public int SchemaVersion { get; init; }
    public int IndexCount { get; init; }
    public long ProductCount { get; init; }
    public long InvoiceCount { get; init; }
    public long InvoiceItemCount { get; init; }
    public long PartyCount { get; init; }
    public long PurchaseCount { get; init; }
    public long ExpenseCount { get; init; }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
