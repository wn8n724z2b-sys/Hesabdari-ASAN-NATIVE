using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class ReportsViewModel : PagedViewModelBase
{
    private readonly InvoiceService _service=new();
    private readonly ReportService _reports=new();
    private readonly ReceiptPrinterService _printer=new();
    private readonly InvoiceAdjustmentService _adjustments=new();
    public ObservableCollection<InvoiceSummary> Items{get;}=new();
    public ObservableCollection<DailyFinancePoint> Trend{get;}=new();
    public RelayCommand PrintCommand{get;}
    public RelayCommand EditCommand{get;}
    public RelayCommand CancelCommand{get;}
    public ReportsViewModel()
    {
        PrintCommand=new RelayCommand(x=>Print(x as InvoiceSummary),x=>x is InvoiceSummary);
        EditCommand=new RelayCommand(x=>Edit(x as InvoiceSummary),x=>x is InvoiceSummary i && i.IsActive);
        CancelCommand=new RelayCommand(x=>Cancel(x as InvoiceSummary),x=>x is InvoiceSummary i && i.IsActive);
        Reload();
    }
    public override void Reload()
    {
        var r=_service.Search(Search,Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();return;}
        Trend.Clear();foreach(var x in _reports.GetDailyTrend(30))Trend.Add(x);
    }
    private void Print(InvoiceSummary? invoice){if(invoice is null)return;try{_printer.PrintInvoice(invoice.InvoiceNo);}catch(Exception ex){MessageBox.Show(ex.Message,"چاپ فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    private void Edit(InvoiceSummary? invoice)
    {
        if(invoice is null||!invoice.IsActive)return;
        try{var w=new InvoiceEditWindow(invoice.InvoiceNo){Owner=Application.Current.MainWindow};if(w.ShowDialog()==true)Reload();}
        catch(Exception ex){MessageBox.Show(ex.Message,"ویرایش فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    private void Cancel(InvoiceSummary? invoice)
    {
        if(invoice is null||!invoice.IsActive)return;
        var w=new InvoiceCancelWindow{Owner=Application.Current.MainWindow};if(w.ShowDialog()!=true)return;
        try{_adjustments.Cancel(invoice.InvoiceNo,w.Reason);Reload();MessageBox.Show($"فاکتور #{invoice.InvoiceNo} باطل شد. موجودی و حساب مرتبط برگشت داده شد.","ابطال فاکتور",MessageBoxButton.OK,MessageBoxImage.Information);}
        catch(Exception ex){MessageBox.Show(ex.Message,"ابطال فاکتور",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
}
