using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class ReceiptPrinterService
{
    private readonly InvoiceService _invoices = new();
    private readonly SettingsService _settings = new();

    public IReadOnlyList<string> GetInstalledPrinters()
    {
        try
        {
            using var server = new LocalPrintServer();
            return server.GetPrintQueues()
                .Select(q => q.FullName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string? ChoosePrinter()
    {
        var dialog = new PrintDialog();
        var saved = _settings.Get("printer_name", "");
        var queue = FindQueue(saved);
        if (queue is not null) dialog.PrintQueue = queue;
        if (dialog.ShowDialog() != true || dialog.PrintQueue is null) return null;
        return dialog.PrintQueue.FullName;
    }

    public bool PrintInvoice(long invoiceNo, bool forceDialog = false)
    {
        var invoice = _invoices.GetByInvoiceNo(invoiceNo)
            ?? throw new InvalidOperationException("فاکتور برای چاپ پیدا نشد.");

        var dialog = new PrintDialog();
        var saved = _settings.Get("printer_name", "");
        var queue = FindQueue(saved);
        if (!forceDialog && queue is not null)
        {
            dialog.PrintQueue = queue;
        }
        else if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var doc = BuildReceipt(invoice, _settings.Load());
        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"حسابداری آسان - فاکتور {invoice.InvoiceNo}");
        return true;
    }

    private static PrintQueue? FindQueue(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            using var server = new LocalPrintServer();
            return server.GetPrintQueues().FirstOrDefault(q =>
                string.Equals(q.FullName, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static FlowDocument BuildReceipt(InvoiceDetail invoice, AppSettings settings)
    {
        var doc = new FlowDocument
        {
            PageWidth = 302,
            PageHeight = 1120,
            PagePadding = new Thickness(10),
            ColumnWidth = 282,
            FlowDirection = FlowDirection.RightToLeft,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = Brushes.Black
        };

        Paragraph P(string text, double size = 11, FontWeight? weight = null, TextAlignment align = TextAlignment.Right)
        {
            return new Paragraph(new Run(text))
            {
                Margin = new Thickness(0, 2, 0, 2),
                FontSize = size,
                FontWeight = weight ?? FontWeights.Normal,
                TextAlignment = align,
                FlowDirection = FlowDirection.RightToLeft
            };
        }

        doc.Blocks.Add(P(string.IsNullOrWhiteSpace(settings.ShopName) ? "حسابداری آسان" : settings.ShopName, 17, FontWeights.Bold, TextAlignment.Center));
        if (!string.IsNullOrWhiteSpace(settings.ShopAddress)) doc.Blocks.Add(P(settings.ShopAddress, 10, null, TextAlignment.Center));
        if (!string.IsNullOrWhiteSpace(settings.ShopPhone)) doc.Blocks.Add(P(settings.ShopPhone, 10, null, TextAlignment.Center));
        doc.Blocks.Add(P("--------------------------------", 10, null, TextAlignment.Center));
        doc.Blocks.Add(P($"فاکتور: {invoice.InvoiceNo}", 12, FontWeights.Bold));
        doc.Blocks.Add(P($"تاریخ: {invoice.CreatedAt:yyyy/MM/dd HH:mm}"));
        doc.Blocks.Add(P($"مشتری: {invoice.CustomerName}"));
        doc.Blocks.Add(P($"پرداخت: {invoice.PaymentText}"));
        if (invoice.Status == "CANCELLED") doc.Blocks.Add(P("*** فاکتور باطل شده ***", 14, FontWeights.Bold, TextAlignment.Center));
        doc.Blocks.Add(P("--------------------------------", 10, null, TextAlignment.Center));

        foreach (var item in invoice.Items)
        {
            doc.Blocks.Add(P($"{item.RowNumber}. {item.ProductName}", 11, FontWeights.SemiBold));
            doc.Blocks.Add(P($"{item.QuantityText} × {item.UnitPrice:N0} = {item.LineTotal:N0} ؋", 10));
        }

        doc.Blocks.Add(P("--------------------------------", 10, null, TextAlignment.Center));
        doc.Blocks.Add(P($"جمع: {invoice.Subtotal:N0} ؋", 11, FontWeights.SemiBold));
        if (invoice.Discount > 0) doc.Blocks.Add(P($"تخفیف: {invoice.Discount:N0} ؋", 11));
        doc.Blocks.Add(P($"قابل پرداخت: {invoice.Total:N0} ؋", 15, FontWeights.Bold));
        doc.Blocks.Add(P(string.IsNullOrWhiteSpace(settings.ReceiptFooter) ? "سپاس از خرید شما" : settings.ReceiptFooter, 10, null, TextAlignment.Center));
        doc.Blocks.Add(P("حسابداری آسان · MNRAHIMI . Ltd", 8, null, TextAlignment.Center));
        return doc;
    }
}
