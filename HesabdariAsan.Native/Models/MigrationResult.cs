namespace HesabdariAsan.Native.Models;

public sealed class MigrationResult
{
    public int Products { get; set; }
    public int Parties { get; set; }
    public int Invoices { get; set; }
    public int InvoiceItems { get; set; }
    public int Expenses { get; set; }
    public int Purchases { get; set; }
    public int Receipts { get; set; }
    public int SupplierPayments { get; set; }
    public int InventoryRows { get; set; }
    public string SafetyBackupPath { get; set; } = "";

    public override string ToString() =>
        $"{Products} کالا، {Parties} مشتری/شرکت، {Invoices} فاکتور، {Expenses} هزینه، {Purchases} خرید";
}
