using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class ReportsViewModel : PagedViewModelBase
{
    private readonly InvoiceService _service = new();
    private readonly ReportService _reports = new();
    private readonly ReceiptPrinterService _printer = new();
    private readonly InvoiceAdjustmentService _adjustments = new();
    private string _salesText="0 ؋",_expensesText="0 ؋",_debtText="0 ؋",_paymentsText="0 ؋",_profitText="0 ؋",_salesMeta="0 فاکتور";
    private string _selectedPeriod="ماه جاری",_selectedPayment="همه روش‌ها";
    private DateTime? _customFrom=DateTime.Today.AddDays(-30),_customTo=DateTime.Today;

    public ObservableCollection<InvoiceSummary> Items { get; } = new();
    public ObservableCollection<DailyFinancePoint> Trend { get; } = new();
    public IReadOnlyList<string> PeriodOptions { get; }=new[]{"امروز","دیروز","7 روز گذشته","ماه جاری","ماه قبل","دوره مالی جاری","همه اطلاعات","بازه سفارشی"};
    public IReadOnlyList<string> PaymentOptions { get; }=new[]{"همه روش‌ها","نقدی","نسیه"};
    public string SalesText{get=>_salesText;private set=>Set(ref _salesText,value);}public string ExpensesText{get=>_expensesText;private set=>Set(ref _expensesText,value);}public string DebtText{get=>_debtText;private set=>Set(ref _debtText,value);}public string PaymentsText{get=>_paymentsText;private set=>Set(ref _paymentsText,value);}public string ProfitText{get=>_profitText;private set=>Set(ref _profitText,value);}public string SalesMeta{get=>_salesMeta;private set=>Set(ref _salesMeta,value);}
    public string SelectedPeriod{get=>_selectedPeriod;set{if(Set(ref _selectedPeriod,value)){OnPropertyChanged(nameof(IsCustomPeriod));if(!IsCustomPeriod){Page=1;Reload();}}}}
    public string SelectedPayment{get=>_selectedPayment;set{if(Set(ref _selectedPayment,value)){Page=1;Reload();}}}
    public DateTime? CustomFrom{get=>_customFrom;set=>Set(ref _customFrom,value);}public DateTime? CustomTo{get=>_customTo;set=>Set(ref _customTo,value);}
    public bool IsCustomPeriod=>SelectedPeriod=="بازه سفارشی";

    public RelayCommand PrintCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ApplyCustomCommand { get; }
    public RelayCommand PrintLastCommand { get; }

    public ReportsViewModel()
    {
        PrintCommand=new RelayCommand(x=>Print(x as InvoiceSummary),x=>x is InvoiceSummary);
        EditCommand=new RelayCommand(x=>Edit(x as InvoiceSummary),x=>x is InvoiceSummary i&&i.IsActive);
        CancelCommand=new RelayCommand(x=>Cancel(x as InvoiceSummary),x=>x is InvoiceSummary i&&i.IsActive);
        ApplyCustomCommand=new RelayCommand(_=>{Page=1;Reload();},_=>IsCustomPeriod&&CustomFrom is not null&&CustomTo is not null);
        PrintLastCommand=new RelayCommand(_=>Print(Items.FirstOrDefault()),_=>Items.Count>0);
        Reload();
    }

    public override void Reload()
    {
        var (from,to)=ResolveRange();var payment=SelectedPayment switch{"نقدی"=>"CASH","نسیه"=>"CREDIT",_=>"ALL"};
        var r=_service.Search(Search,payment,from,to,Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();return;}
        var summary=_reports.GetSummary(from,to);SalesText=$"{summary.Sales:N0} ؋";ExpensesText=$"{summary.Expenses:N0} ؋";DebtText=$"{summary.DebtSales:N0} ؋";PaymentsText=$"{summary.Payments:N0} ؋";ProfitText=$"{summary.NetProfit:N0} ؋";SalesMeta=$"{summary.InvoiceCount:N0} فاکتور";
        Trend.Clear();var chartTo=to??DateTime.Today.AddDays(1);var chartFrom=from??chartTo.AddDays(-30);if((chartTo-chartFrom).TotalDays>90)chartFrom=chartTo.AddDays(-90);foreach(var x in _reports.GetDailyTrend(chartFrom,chartTo))Trend.Add(x);
        PrintLastCommand.RaiseCanExecuteChanged();ApplyCustomCommand.RaiseCanExecuteChanged();
    }

    private (DateTime? From,DateTime? To) ResolveRange()
    {
        var today=DateTime.Today;var month=new DateTime(today.Year,today.Month,1);
        return SelectedPeriod switch
        {
            "امروز"=>(today,today.AddDays(1)),
            "دیروز"=>(today.AddDays(-1),today),
            "7 روز گذشته"=>(today.AddDays(-6),today.AddDays(1)),
            "ماه جاری"=>(month,month.AddMonths(1)),
            "ماه قبل"=>(month.AddMonths(-1),month),
            "دوره مالی جاری"=>(_reports.CurrentFinancialPeriodStart()??month,today.AddDays(1)),
            "همه اطلاعات"=>(null,null),
            "بازه سفارشی"=>(CustomFrom?.Date,CustomTo?.Date.AddDays(1)),
            _=>(month,month.AddMonths(1))
        };
    }

    private void Print(InvoiceSummary? invoice){if(invoice is null)return;try{_printer.PrintInvoice(invoice.InvoiceNo);}catch(Exception ex){MessageBox.Show(ex.Message,"چاپ فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    private void Edit(InvoiceSummary? invoice){if(invoice is null||!invoice.IsActive)return;try{var w=new InvoiceEditWindow(invoice.InvoiceNo){Owner=Application.Current.MainWindow};if(w.ShowDialog()==true)Reload();}catch(Exception ex){MessageBox.Show(ex.Message,"ویرایش فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    private void Cancel(InvoiceSummary? invoice){if(invoice is null||!invoice.IsActive)return;var w=new InvoiceCancelWindow{Owner=Application.Current.MainWindow};if(w.ShowDialog()!=true)return;try{_adjustments.Cancel(invoice.InvoiceNo,w.Reason);Reload();MessageBox.Show($"فاکتور #{invoice.InvoiceNo} باطل شد. موجودی و حساب مرتبط برگشت داده شد.","ابطال فاکتور",MessageBoxButton.OK,MessageBoxImage.Information);}catch(Exception ex){MessageBox.Show(ex.Message,"ابطال فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}}
}
