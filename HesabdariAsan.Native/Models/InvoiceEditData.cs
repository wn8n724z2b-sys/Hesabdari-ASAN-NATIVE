using System.Collections.ObjectModel;

namespace HesabdariAsan.Native.Models;

public sealed class InvoiceEditData
{
    public long Id { get; set; }
    public long InvoiceNo { get; set; }
    public long? CustomerId { get; set; }
    public string CustomerName { get; set; } = "مشتری عمومی";
    public string PaymentType { get; set; } = "CASH";
    public long Discount { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public ObservableCollection<InvoiceEditLine> Items { get; } = new();
}

public sealed class InvoiceEditLine : HesabdariAsan.Native.ViewModels.ViewModelBase
{
    private double _quantity;
    private long _unitPrice;
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Unit { get; set; } = "دانه";
    public long PurchasePrice { get; set; }
    public double Quantity { get => _quantity; set { if (Set(ref _quantity, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public long UnitPrice { get => _unitPrice; set { if (Set(ref _unitPrice, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public long LineTotal => (long)Math.Round(Math.Max(0, Quantity) * Math.Max(0, UnitPrice), MidpointRounding.AwayFromZero);
}
