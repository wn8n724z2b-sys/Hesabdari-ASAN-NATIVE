using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.Dialogs;

public partial class InvoiceEditWindow : Window
{
    private readonly InvoiceAdjustmentService _service = new();
    public InvoiceEditData Invoice { get; }
    public InvoiceEditWindow(long invoiceNo)
    {
        InitializeComponent(); Invoice = _service.LoadForEdit(invoiceNo); DataContext = Invoice;
        InfoText.Text = $"فاکتور #{Invoice.InvoiceNo} · {Invoice.CustomerName} · {(Invoice.PaymentType == "CREDIT" ? "نسیه" : "نقدی")}";
        Invoice.Items.CollectionChanged += Items_CollectionChanged; foreach (var x in Invoice.Items) x.PropertyChanged += LineChanged; UpdateTotal();
    }
    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (InvoiceEditLine x in e.OldItems) x.PropertyChanged -= LineChanged;
        if (e.NewItems is not null) foreach (InvoiceEditLine x in e.NewItems) x.PropertyChanged += LineChanged;
        UpdateTotal();
    }
    private void LineChanged(object? sender, PropertyChangedEventArgs e) => UpdateTotal();
    private void UpdateTotal() { var subtotal = Invoice.Items.Sum(x => x.LineTotal); TotalText.Text = $"جمع اقلام: {subtotal:N0} ؋"; }
    private void RemoveLine_Click(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.Tag is InvoiceEditLine line) Invoice.Items.Remove(line); }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { _service.SaveEdit(Invoice); DialogResult = true; }
        catch (Exception ex) { AppDialog.Show(ex.Message, "ویرایش فاکتور", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
