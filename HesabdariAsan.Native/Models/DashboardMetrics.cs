namespace HesabdariAsan.Native.Models;

public sealed class DashboardMetrics
{
    public long TodaySales { get; set; }
    public long TodayExpenses { get; set; }
    public long CustomerDebt { get; set; }
    public long InventoryValue { get; set; }
    public long TodayNetProfit { get; set; }
    public string TodaySalesText => $"{TodaySales:N0} ؋";
    public string TodayExpensesText => $"{TodayExpenses:N0} ؋";
    public string CustomerDebtText => $"{CustomerDebt:N0} ؋";
    public string InventoryValueText => $"{InventoryValue:N0} ؋";
    public string TodayNetProfitText => $"{TodayNetProfit:N0} ؋";
}
