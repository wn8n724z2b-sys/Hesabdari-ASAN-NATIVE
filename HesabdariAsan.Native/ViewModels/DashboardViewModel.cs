using System.Collections.ObjectModel;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _service = new();
    private readonly ReportService _reports = new();
    private DashboardMetrics _metrics = new();
    public DashboardMetrics Metrics { get => _metrics; private set => Set(ref _metrics,value); }
    public ObservableCollection<InvoiceSummary> RecentInvoices { get; } = new();
    public ObservableCollection<DailyFinancePoint> Trend { get; } = new();
    public RelayCommand RefreshCommand { get; }
    public DashboardViewModel(){RefreshCommand=new RelayCommand(_=>Reload());Reload();}
    public void Reload(){Metrics=_service.Load();RecentInvoices.Clear();foreach(var x in _service.RecentInvoices(10))RecentInvoices.Add(x);Trend.Clear();foreach(var x in _reports.GetDailyTrend(14))Trend.Add(x);}
}
