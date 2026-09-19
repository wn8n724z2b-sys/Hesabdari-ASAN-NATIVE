namespace HesabdariAsan.Native.Models;

public sealed class PurchaseRecord
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public double Quantity { get; set; }
    public long UnitCost { get; set; }
    public long Total { get; set; }
    public string PaymentType { get; set; } = "CASH";
    public long? SupplierId { get; set; }
    public string SupplierName { get; set; } = "—";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string QuantityText => Quantity % 1 == 0 ? Quantity.ToString("N0") : Quantity.ToString("N2");
    public string UnitCostText => $"{UnitCost:N0} ؋";
    public string TotalText => $"{Total:N0} ؋";
    public string PaymentText => PaymentType == "CREDIT" ? "نسیه" : "نقدی";
    public string DateText => CreatedAt.ToString("yyyy/MM/dd HH:mm");
}
