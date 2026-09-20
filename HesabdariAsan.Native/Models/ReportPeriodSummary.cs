namespace HesabdariAsan.Native.Models;

public sealed class ReportPeriodSummary
{
    public long Sales { get; set; }
    public int InvoiceCount { get; set; }
    public long Expenses { get; set; }
    public long DebtSales { get; set; }
    public long Payments { get; set; }
    public long NetProfit { get; set; }
    public long GrossProfit { get; set; }
    public long CostOfGoods { get; set; }
    public long CashSales { get; set; }
    public long CreditSales { get; set; }
    public long CustomerReceipts { get; set; }
    public long Purchases { get; set; }
    public long CashPurchases { get; set; }
    public long SupplierPayments { get; set; }
    public long Discounts { get; set; }
    public long CashIn { get; set; }
    public long CashOut { get; set; }
    public long NetCash { get; set; }
}
