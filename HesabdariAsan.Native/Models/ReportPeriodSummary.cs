namespace HesabdariAsan.Native.Models;
public sealed class ReportPeriodSummary
{
    public long Sales { get; set; }
    public int InvoiceCount { get; set; }
    public long Expenses { get; set; }
    public long DebtSales { get; set; }
    public long Payments { get; set; }
    public long NetProfit { get; set; }
}
