namespace HesabdariAsan.Native.Models;

public sealed class InvoiceSummary
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public long InvoiceNo { get; set; }
    public string CustomerName { get; set; } = "مشتری عمومی";
    public string PaymentType { get; set; } = "CASH";
    public long Total { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public string TotalText => $"{Total:N0} ؋";
    public string PaymentText => PaymentType == "CREDIT" ? "نسیه" : "نقدی";
    public string StatusText => Status == "CANCELLED" ? "باطل" : "فعال";
    public bool IsActive => Status != "CANCELLED";
    public string DateText => CreatedAt.ToString("yyyy/MM/dd HH:mm");
}
