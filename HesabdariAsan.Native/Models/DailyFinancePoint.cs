namespace HesabdariAsan.Native.Models;

public sealed class DailyFinancePoint
{
    public DateTime Date { get; set; }
    public long Sales { get; set; }
    public long Expenses { get; set; }
    public long Debt { get; set; }
    public long Payments { get; set; }
    public long NetProfit { get; set; }
}
