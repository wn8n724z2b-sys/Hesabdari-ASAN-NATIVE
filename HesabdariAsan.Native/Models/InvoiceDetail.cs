namespace HesabdariAsan.Native.Models;

public sealed class InvoiceDetail
{
    public long Id { get; set; }
    public long InvoiceNo { get; set; }
    public long? CustomerId { get; set; }
    public string CustomerName { get; set; } = "مشتری عمومی";
    public string PaymentType { get; set; } = "CASH";
    public long Subtotal { get; set; }
    public long Discount { get; set; }
    public long Total { get; set; }
    public long Paid { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string CancelReason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<InvoiceLine> Items { get; set; } = new();
    public string PaymentText => PaymentType == "CREDIT" ? "نسیه" : "نقدی";
    public string StatusText => Status == "CANCELLED" ? "باطل شده" : "فعال";
}

public sealed class InvoiceLine
{
    public int RowNumber { get; set; }
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Unit { get; set; } = "دانه";
    public double Quantity { get; set; }
    public long UnitPrice { get; set; }
    public long PurchasePrice { get; set; }
    public long LineTotal { get; set; }
    public string QuantityText => Quantity % 1 == 0 ? Quantity.ToString("N0") : Quantity.ToString("N2");
    public string QuantityWithUnitText => $"{QuantityText} {Unit}";
}
