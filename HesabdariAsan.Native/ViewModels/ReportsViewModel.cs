using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using Microsoft.Win32;
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
    private string _periodLabel="ماه جاری",_summarySales="0 ؋",_summaryCashIn="0 ؋",_summaryCashOut="0 ؋",_summaryNetCash="0 ؋",_summaryCredit="0 ؋",_summaryReceipts="0 ؋",_summaryCost="0 ؋",_summaryPurchases="0 ؋",_summaryCashPurchases="0 ؋",_summaryExpenses="0 ؋",_summarySupplierPaid="0 ؋",_summaryDiscounts="0 ؋",_summaryGrossProfit="0 ؋",_summaryNetProfit="0 ؋";
    private string _currentCustomerDebt="0 ؋",_currentSupplierDebt="0 ؋",_currentInventory="0 ؋",_currentCashMonth="0 ؋";
    private string _financialPeriodTitle="دوره حسابی اول",_financialPeriodDates="—",_financialPeriodStatus="هدف استاندارد: 6 ماه",_invoiceStats="0 فاکتور";
    private double _financialPeriodProgress;
    private bool _showSales=true,_showExpenses=true,_showDebt=true,_showPayments=true,_showProfit=true;

    public ObservableCollection<InvoiceSummary> Items { get; } = new();
    public ObservableCollection<DailyFinancePoint> Trend { get; } = new();
    public IReadOnlyList<string> PeriodOptions { get; }=new[]{"امروز","دیروز","7 روز گذشته","ماه جاری","ماه قبل","دوره مالی جاری","همه اطلاعات","بازه سفارشی"};
    public IReadOnlyList<string> PaymentOptions { get; }=new[]{"همه روش‌ها","نقدی","نسیه"};

    public string SalesText{get=>_salesText;private set=>Set(ref _salesText,value);} public string ExpensesText{get=>_expensesText;private set=>Set(ref _expensesText,value);} public string DebtText{get=>_debtText;private set=>Set(ref _debtText,value);} public string PaymentsText{get=>_paymentsText;private set=>Set(ref _paymentsText,value);} public string ProfitText{get=>_profitText;private set=>Set(ref _profitText,value);} public string SalesMeta{get=>_salesMeta;private set=>Set(ref _salesMeta,value);}
    public string SelectedPeriod{get=>_selectedPeriod;set{if(Set(ref _selectedPeriod,value)){OnPropertyChanged(nameof(IsCustomPeriod));if(!IsCustomPeriod){Page=1;Reload();}}}}
    public string SelectedPayment{get=>_selectedPayment;set{if(Set(ref _selectedPayment,value)){Page=1;Reload();}}}
    public DateTime? CustomFrom{get=>_customFrom;set=>Set(ref _customFrom,value);} public DateTime? CustomTo{get=>_customTo;set=>Set(ref _customTo,value);}
    public bool IsCustomPeriod=>SelectedPeriod=="بازه سفارشی";

    public string PeriodLabel{get=>_periodLabel;private set=>Set(ref _periodLabel,value);} public string SummarySales{get=>_summarySales;private set=>Set(ref _summarySales,value);} public string SummaryCashIn{get=>_summaryCashIn;private set=>Set(ref _summaryCashIn,value);} public string SummaryCashOut{get=>_summaryCashOut;private set=>Set(ref _summaryCashOut,value);} public string SummaryNetCash{get=>_summaryNetCash;private set=>Set(ref _summaryNetCash,value);} public string SummaryCredit{get=>_summaryCredit;private set=>Set(ref _summaryCredit,value);} public string SummaryReceipts{get=>_summaryReceipts;private set=>Set(ref _summaryReceipts,value);} public string SummaryCost{get=>_summaryCost;private set=>Set(ref _summaryCost,value);} public string SummaryPurchases{get=>_summaryPurchases;private set=>Set(ref _summaryPurchases,value);} public string SummaryCashPurchases{get=>_summaryCashPurchases;private set=>Set(ref _summaryCashPurchases,value);} public string SummaryExpenses{get=>_summaryExpenses;private set=>Set(ref _summaryExpenses,value);} public string SummarySupplierPaid{get=>_summarySupplierPaid;private set=>Set(ref _summarySupplierPaid,value);} public string SummaryDiscounts{get=>_summaryDiscounts;private set=>Set(ref _summaryDiscounts,value);} public string SummaryGrossProfit{get=>_summaryGrossProfit;private set=>Set(ref _summaryGrossProfit,value);} public string SummaryNetProfit{get=>_summaryNetProfit;private set=>Set(ref _summaryNetProfit,value);}
    public string CurrentCustomerDebt{get=>_currentCustomerDebt;private set=>Set(ref _currentCustomerDebt,value);} public string CurrentSupplierDebt{get=>_currentSupplierDebt;private set=>Set(ref _currentSupplierDebt,value);} public string CurrentInventory{get=>_currentInventory;private set=>Set(ref _currentInventory,value);} public string CurrentCashMonth{get=>_currentCashMonth;private set=>Set(ref _currentCashMonth,value);}
    public string FinancialPeriodTitle{get=>_financialPeriodTitle;private set=>Set(ref _financialPeriodTitle,value);} public string FinancialPeriodDates{get=>_financialPeriodDates;private set=>Set(ref _financialPeriodDates,value);} public string FinancialPeriodStatus{get=>_financialPeriodStatus;private set=>Set(ref _financialPeriodStatus,value);} public double FinancialPeriodProgress{get=>_financialPeriodProgress;private set=>Set(ref _financialPeriodProgress,value);} public string InvoiceStats{get=>_invoiceStats;private set=>Set(ref _invoiceStats,value);}

    public bool ShowSales{get=>_showSales;set=>SetSeries(ref _showSales,value,nameof(ShowSales));}
    public bool ShowExpenses{get=>_showExpenses;set=>SetSeries(ref _showExpenses,value,nameof(ShowExpenses));}
    public bool ShowDebt{get=>_showDebt;set=>SetSeries(ref _showDebt,value,nameof(ShowDebt));}
    public bool ShowPayments{get=>_showPayments;set=>SetSeries(ref _showPayments,value,nameof(ShowPayments));}
    public bool ShowProfit{get=>_showProfit;set=>SetSeries(ref _showProfit,value,nameof(ShowProfit));}

    public RelayCommand PrintCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ApplyCustomCommand { get; }
    public RelayCommand PrintLastCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand StartNewPeriodCommand { get; }

    public ReportsViewModel()
    {
        PrintCommand=new RelayCommand(x=>Print(x as InvoiceSummary),x=>x is InvoiceSummary);
        EditCommand=new RelayCommand(x=>Edit(x as InvoiceSummary),x=>x is InvoiceSummary i&&i.IsActive);
        CancelCommand=new RelayCommand(x=>Cancel(x as InvoiceSummary),x=>x is InvoiceSummary i&&i.IsActive);
        ApplyCustomCommand=new RelayCommand(_=>{Page=1;Reload();},_=>IsCustomPeriod&&CustomFrom is not null&&CustomTo is not null&&CustomFrom<=CustomTo);
        PrintLastCommand=new RelayCommand(_=>Print(Items.FirstOrDefault()),_=>Items.Count>0);
        ExportCsvCommand=new RelayCommand(_=>ExportCsv());
        StartNewPeriodCommand=new RelayCommand(_=>StartNewPeriod());
        Reload();
    }

    public override void Reload()
    {
        var (from,to)=ResolveRange();var payment=SelectedPayment switch{"نقدی"=>"CASH","نسیه"=>"CREDIT",_=>"ALL"};
        var r=_service.Search(Search,payment,from,to,Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();return;}
        var summary=_reports.GetSummary(from,to);SalesText=Money(summary.Sales);ExpensesText=Money(summary.Expenses);DebtText=Money(summary.DebtSales);PaymentsText=Money(summary.Payments);ProfitText=Money(summary.NetProfit);SalesMeta=$"{summary.InvoiceCount:N0} فاکتور";InvoiceStats=$"{TotalCount:N0} فاکتور در فیلتر فعلی";
        PeriodLabel=ResolvePeriodLabel();SummarySales=Money(summary.Sales);SummaryCashIn=Money(summary.CashIn);SummaryCashOut=Money(summary.CashOut);SummaryNetCash=Money(summary.NetCash);SummaryCredit=Money(summary.CreditSales);SummaryReceipts=Money(summary.CustomerReceipts);SummaryCost=Money(summary.CostOfGoods);SummaryPurchases=Money(summary.Purchases);SummaryCashPurchases=Money(summary.CashPurchases);SummaryExpenses=Money(summary.Expenses);SummarySupplierPaid=Money(summary.SupplierPayments);SummaryDiscounts=Money(summary.Discounts);SummaryGrossProfit=Money(summary.GrossProfit);SummaryNetProfit=Money(summary.NetProfit);
        var balances=_reports.GetCurrentBalances();CurrentCustomerDebt=Money(balances.CustomerDebt);CurrentSupplierDebt=Money(balances.SupplierDebt);CurrentInventory=Money(balances.InventoryValue);CurrentCashMonth=Money(balances.CashMonth);
        var period=_reports.GetCurrentFinancialPeriod();FinancialPeriodTitle=AccountingPeriodName(period.PeriodNo);FinancialPeriodDates=$"ماه {period.MonthNumber:N0} · شروع {period.StartedAt:yyyy/MM/dd} · تا امروز {DateTime.Today:yyyy/MM/dd}";FinancialPeriodProgress=period.ProgressPercent;FinancialPeriodStatus=period.IsPastRecommendedLength?"از مرز پیشنهادی 6 ماه گذشته است · شروع دوره جدید در هر زمان امکان‌پذیر است.":"6 ماه، بازه پیشنهادی است · شروع دوره جدید در هر زمان امکان‌پذیر است.";
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

    private string ResolvePeriodLabel()
    {
        if(SelectedPeriod!="دوره مالی جاری")return SelectedPeriod;
        var p=_reports.GetCurrentFinancialPeriod();return $"{AccountingPeriodName(p.PeriodNo)} · ماه {p.MonthNumber:N0}";
    }

    private void SetSeries(ref bool field,bool value,string property)
    {
        if(!value&&EnabledSeriesCount()<=1)return;
        if(field==value)return;field=value;OnPropertyChanged(property);
    }
    private int EnabledSeriesCount() => new[]{ShowSales,ShowExpenses,ShowDebt,ShowPayments,ShowProfit}.Count(x=>x);

    private void ExportCsv()
    {
        try
        {
            var (from,to)=ResolveRange();var payment=SelectedPayment switch{"نقدی"=>"CASH","نسیه"=>"CREDIT",_=>"ALL"};var rows=_service.ListForExport(Search,payment,from,to);
            var dlg=new SaveFileDialog{Title="خروجی Excel / CSV",Filter="CSV قابل باز شدن در Excel|*.csv",FileName=$"hesabdari-asan-sales-{DateTime.Now:yyyyMMdd}.csv",AddExtension=true,DefaultExt="csv"};
            var downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");if(Directory.Exists(downloads))dlg.InitialDirectory=downloads;if(dlg.ShowDialog()!=true)return;
            static string Q(object? v)=>"\""+Convert.ToString(v)?.Replace("\"","\"\"")+"\"";
            var sb=new StringBuilder();sb.Append('\uFEFF');sb.AppendLine(string.Join(",",new[]{"شماره فاکتور","تاریخ و زمان","مشتری","روش پرداخت","تعداد اقلام","مبلغ","وضعیت"}.Select(Q)));
            foreach(var x in rows)sb.AppendLine(string.Join(",",new[]{Q(x.InvoiceNo),Q(x.DateText),Q(x.CustomerName),Q(x.PaymentText),Q(x.ItemsText),Q(x.Total),Q(x.StatusText)}));
            File.WriteAllText(dlg.FileName,sb.ToString(),new UTF8Encoding(false));MessageBox.Show($"خروجی با موفقیت ذخیره شد.\n\n{dlg.FileName}","خروجی گزارش",MessageBoxButton.OK,MessageBoxImage.Information);
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"خروجی گزارش",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }

    private void StartNewPeriod()
    {
        var p=_reports.GetCurrentFinancialPeriod();var answer=MessageBox.Show($"{AccountingPeriodName(p.PeriodNo)} بسته شود و دوره حسابی جدید از همین لحظه آغاز گردد؟\n\nخلاصه دوره فعلی در تاریخچه ذخیره می‌شود و هیچ فاکتور یا داده‌ای حذف نخواهد شد.","شروع دوره حسابی جدید",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No,MessageBoxOptions.RtlReading|MessageBoxOptions.RightAlign);if(answer!=MessageBoxResult.Yes)return;
        try{var next=_reports.StartNewFinancialPeriod();Reload();MessageBox.Show($"{AccountingPeriodName(next.PeriodNo)} آغاز شد.","دوره حسابی",MessageBoxButton.OK,MessageBoxImage.Information);}catch(Exception ex){MessageBox.Show(ex.Message,"دوره حسابی",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }

    private static string AccountingPeriodName(int no)
    {
        var names=new Dictionary<int,string>{{1,"اول"},{2,"دوم"},{3,"سوم"},{4,"چهارم"},{5,"پنجم"},{6,"ششم"},{7,"هفتم"},{8,"هشتم"},{9,"نهم"},{10,"دهم"}};return names.TryGetValue(no,out var n)?$"دوره حسابی {n}":$"دوره حسابی {no:N0}";
    }
    private static string Money(long value)=>$"{value:N0} ؋";
    private void Print(InvoiceSummary? invoice){if(invoice is null)return;try{_printer.PrintInvoice(invoice.InvoiceNo);}catch(Exception ex){MessageBox.Show(ex.Message,"چاپ فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    private void Edit(InvoiceSummary? invoice){if(invoice is null||!invoice.IsActive)return;try{var w=new InvoiceEditWindow(invoice.InvoiceNo){Owner=Application.Current.MainWindow};if(w.ShowDialog()==true)Reload();}catch(Exception ex){MessageBox.Show(ex.Message,"ویرایش فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    private void Cancel(InvoiceSummary? invoice){if(invoice is null||!invoice.IsActive)return;var w=new InvoiceCancelWindow{Owner=Application.Current.MainWindow};if(w.ShowDialog()!=true)return;try{_adjustments.Cancel(invoice.InvoiceNo,w.Reason);Reload();MessageBox.Show($"فاکتور #{invoice.InvoiceNo} باطل شد. موجودی و حساب مرتبط برگشت داده شد.","ابطال فاکتور",MessageBoxButton.OK,MessageBoxImage.Information);}catch(Exception ex){MessageBox.Show(ex.Message,"ابطال فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}}
}
