namespace HesabdariAsan.Native.Models;

public sealed class ReportBalanceSnapshot
{
    public long CustomerDebt { get; set; }
    public long SupplierDebt { get; set; }
    public long InventoryValue { get; set; }
    public long CashMonth { get; set; }
    public long CashToday { get; set; }
}
