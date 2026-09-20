namespace HesabdariAsan.Native.Models;

public sealed class FinancialPeriodInfo
{
    public long Id { get; set; }
    public int PeriodNo { get; set; } = 1;
    public DateTime StartedAt { get; set; } = DateTime.Today;
    public DateTime? EndedAt { get; set; }
    public int MonthNumber { get; set; } = 1;
    public double ProgressPercent { get; set; }
    public bool IsPastRecommendedLength { get; set; }
}
