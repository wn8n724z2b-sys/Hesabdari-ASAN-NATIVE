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
    private DashboardMetrics _metrics = new();

    public DashboardMetrics Metrics { get => _metrics; private set => Set(ref _metrics, value); }
    public ObservableCollection<InvoiceSummary> RecentInvoices { get; } = new();
    public ObservableCollection<DailyFinancePoint> Trend { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand AddProductCommand { get; }
    public RelayCommand AddCustomerCommand { get; }
    public RelayCommand AddExpenseCommand { get; }

    public DashboardViewModel()
    {
        RefreshCommand = new RelayCommand(_ => Reload());
        AddProductCommand = new RelayCommand(_ => OpenProduct());
        AddCustomerCommand = new RelayCommand(_ => OpenCustomer());
        AddExpenseCommand = new RelayCommand(_ => OpenExpense());
        Reload();
    }

    public void Reload()
    {
        Metrics = _service.Load();
        RecentInvoices.Clear();
        foreach (var x in _service.RecentInvoices(10)) RecentInvoices.Add(x);
        Trend.Clear();
        foreach (var x in _reports.GetDailyTrend(14)) Trend.Add(x);
    }

    private void OpenProduct()
    {
        var w = new ProductEditWindow { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try { _products.Save(w.Product); Reload(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "ثبت کالا", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void OpenCustomer()
    {
        var w = new PartyEditWindow(new Party { Type = "CUSTOMER" }) { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try { _parties.Save(w.Party); Reload(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "ثبت مشتری", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void OpenExpense()
    {
        var w = new ExpenseEditWindow { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try { _expenses.Save(w.Expense); Reload(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "ثبت هزینه", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
