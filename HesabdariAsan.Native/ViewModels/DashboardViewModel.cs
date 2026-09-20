using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _service = new();
    private readonly ReportService _reports = new();
    private readonly ProductService _products = new();
    private readonly PartyService _parties = new();
    private readonly ExpenseService _expenses = new();
    private readonly SettingsService _settings = new();
    private DashboardMetrics _metrics = new();
    private bool _moneyVisible;
    private string _chartRange = "7";

    public DashboardMetrics Metrics { get => _metrics; private set { if (Set(ref _metrics, value)) RaiseMoneyProperties(); } }
    public ObservableCollection<InvoiceSummary> RecentInvoices { get; } = new();
    public ObservableCollection<DailyFinancePoint> Trend { get; } = new();
    public bool MoneyVisible { get => _moneyVisible; private set { if (Set(ref _moneyVisible, value)) { RaiseMoneyProperties(); OnPropertyChanged(nameof(MoneyToggleText)); } } }
    public string MoneyToggleText => MoneyVisible ? "پنهان کردن مبلغ‌ها" : "نمایش مبلغ‌ها";
    public string SalesText => Money(Metrics.TodaySales);
    public string ProfitText => Money(Metrics.TodayNetProfit);
    public string ExpenseText => Money(Metrics.TodayExpenses);
    public string CustomerDebtText => Money(Metrics.CustomerDebt);
    public string InventoryValueText => Money(Metrics.InventoryValue);
    public string ChartRange { get => _chartRange; private set => Set(ref _chartRange, value); }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ToggleMoneyCommand { get; }
    public RelayCommand SetChartRangeCommand { get; }
    public RelayCommand AddProductCommand { get; }
    public RelayCommand AddCustomerCommand { get; }
    public RelayCommand AddExpenseCommand { get; }

    public DashboardViewModel()
    {
        _moneyVisible = _settings.Get("hide_dashboard_money", "0") != "1";
        RefreshCommand = new RelayCommand(_ => Reload());
        ToggleMoneyCommand = new RelayCommand(_ => ToggleMoney());
        SetChartRangeCommand = new RelayCommand(x => SetChartRange(x?.ToString() ?? "7"));
        AddProductCommand = new RelayCommand(_ => OpenProduct());
        AddCustomerCommand = new RelayCommand(_ => OpenCustomer());
        AddExpenseCommand = new RelayCommand(_ => OpenExpense());
        Reload();
    }

    public void Reload()
    {
        Metrics = _service.Load();
        RecentInvoices.Clear();
        foreach (var x in _service.RecentInvoices(6)) RecentInvoices.Add(x);
        ReloadChart();
    }

    private void ToggleMoney()
    {
        MoneyVisible = !MoneyVisible;
        _settings.Set("hide_dashboard_money", MoneyVisible ? "0" : "1");
    }

    private string Money(long value) => MoneyVisible ? $"{value:N0} ؋" : "••••••";
    private void RaiseMoneyProperties()
    {
        OnPropertyChanged(nameof(SalesText));
        OnPropertyChanged(nameof(ProfitText));
        OnPropertyChanged(nameof(ExpenseText));
        OnPropertyChanged(nameof(CustomerDebtText));
        OnPropertyChanged(nameof(InventoryValueText));
    }

    private void SetChartRange(string range)
    {
        if (range is not ("7" or "30" or "month")) range = "7";
        ChartRange = range;
        ReloadChart();
    }

    private void ReloadChart()
    {
        Trend.Clear();
        IEnumerable<DailyFinancePoint> values;
        if (ChartRange == "month")
        {
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            values = _reports.GetDailyTrend(start, DateTime.Today.AddDays(1));
        }
        else values = _reports.GetDailyTrend(ChartRange == "30" ? 30 : 7);
        foreach (var x in values) Trend.Add(x);
    }

    private void OpenProduct()
    {
        var w = new ProductEditWindow { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try { _products.Save(w.Product); Reload(); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "ثبت کالا", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void OpenCustomer()
    {
        var w = new PartyEditWindow(new Party { Type = "CUSTOMER" }) { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try { _parties.Save(w.Party); Reload(); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "ثبت مشتری", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void OpenExpense()
    {
        var w = new ExpenseEditWindow { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try { _expenses.Save(w.Expense); Reload(); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "ثبت هزینه", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
