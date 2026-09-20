namespace HesabdariAsan.Native.Models;

public sealed class DashboardMetrics
{
    public long TodaySales { get; set; }
    public long TodayExpenses { get; set; }
    public long CustomerDebt { get; set; }
    public long InventoryValue { get; set; }
    public long TodayNetProfit { get; set; }
    public long TodaySalesCount { get; set; }
    public long LowStockCount { get; set; }
    public long CustomerCount { get; set; }

    public string TodaySalesText => $"{TodaySales:N0} ؋";
    public string TodayExpensesText => $"{TodayExpenses:N0} ؋";
    public string CustomerDebtText => $"{CustomerDebt:N0} ؋";
    public string InventoryValueText => $"{InventoryValue:N0} ؋";
    public string TodayNetProfitText => $"{TodayNetProfit:N0} ؋";
    public string TodaySalesCountText => $"{TodaySalesCount:N0} فاکتور";
    public string LowStockCountText => $"{LowStockCount:N0}";
    public string CustomerCountText => $"{CustomerCount:N0}";
}
