using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class ArchiveViewModel : PagedViewModelBase
{
    private readonly InvoiceService _service = new();
    private readonly ReceiptPrinterService _printer = new();
    private readonly InvoiceAdjustmentService _adjustments = new();
    private string _selectedPayment = "همه روش‌ها";
    private DateTime? _from;
    private DateTime? _to;

    public ObservableCollection<InvoiceSummary> Items { get; } = new();
    public IReadOnlyList<string> PaymentOptions { get; } = new[] { "همه روش‌ها", "نقدی", "نسیه" };
    public string SelectedPayment { get => _selectedPayment; set { if (Set(ref _selectedPayment, value)) { Page = 1; Reload(); } } }
    public DateTime? From { get => _from; set { if (Set(ref _from, value)) { Page = 1; Reload(); } } }
    public DateTime? To { get => _to; set { if (Set(ref _to, value)) { Page = 1; Reload(); } } }

    public RelayCommand PrintCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ClearDatesCommand { get; }

    public ArchiveViewModel()
    {
        PrintCommand = new RelayCommand(x => Print(x as InvoiceSummary), x => x is InvoiceSummary);
        EditCommand = new RelayCommand(x => Edit(x as InvoiceSummary), x => x is InvoiceSummary i && i.IsActive);
        CancelCommand = new RelayCommand(x => Cancel(x as InvoiceSummary), x => x is InvoiceSummary i && i.IsActive);
        ClearDatesCommand = new RelayCommand(_ => { _from = null; _to = null; OnPropertyChanged(nameof(From)); OnPropertyChanged(nameof(To)); Page = 1; Reload(); });
        Reload();
    }

    public override void Reload()
    {
        var payment = SelectedPayment switch { "نقدی" => "CASH", "نسیه" => "CREDIT", _ => "ALL" };
        var from = From?.Date;
        var to = To?.Date.AddDays(1);
        var r = _service.Search(Search, payment, from, to, Page, PageSize);
        Items.Clear(); foreach (var x in r.Items) Items.Add(x);
        TotalCount = r.TotalCount; TotalPages = r.TotalPages;
        if (Page > TotalPages) { Page = TotalPages; Reload(); }
    }

    private void Print(InvoiceSummary? invoice)
    {
        if (invoice is null) return;
        try { _printer.PrintInvoice(invoice.InvoiceNo); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "چاپ فاکتور", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Edit(InvoiceSummary? invoice)
    {
        if (invoice is null || !invoice.IsActive) return;
        try { var w = new InvoiceEditWindow(invoice.InvoiceNo) { Owner = Application.Current.MainWindow }; if (w.ShowDialog() == true) Reload(); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "ویرایش فاکتور", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Cancel(InvoiceSummary? invoice)
    {
        if (invoice is null || !invoice.IsActive) return;
        var w = new InvoiceCancelWindow { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try { _adjustments.Cancel(invoice.InvoiceNo, w.Reason); Reload(); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "ابطال فاکتور", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
