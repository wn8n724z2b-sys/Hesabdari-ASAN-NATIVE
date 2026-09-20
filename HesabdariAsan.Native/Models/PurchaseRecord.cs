namespace HesabdariAsan.Native.Models;

public sealed class PurchaseRecord
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public double Quantity { get; set; }
    public double Units { get; set; }
    public long UnitCost { get; set; }
    public long Total { get; set; }
    public string PaymentType { get; set; } = "CASH";
    public long? SupplierId { get; set; }
    public string SupplierName { get; set; } = "—";
    public string Note { get; set; } = "";
    public string Mode { get; set; } = "BASE";
    public string PurchaseUnit { get; set; } = "دانه";
    public double UnitsPerPurchase { get; set; } = 1;
    public long PackCost { get; set; }
    public long AverageCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public string QuantityText => Format(Quantity);
    public string UnitsText => Format(Units);
    public string UnitCostText => $"{UnitCost:N0} ؋";
    public string AverageCostText => $"{AverageCost:N0} ؋";
    public string TotalText => $"{Total:N0} ؋";
    public string PaymentText => PaymentType == "CREDIT" ? "نسیه" : "نقدی";
    public string ModeText => Mode == "PURCHASE_UNIT" ? $"{QuantityText} {PurchaseUnit} → {UnitsText} واحد" : $"{UnitsText} واحد";
    public string DateText => CreatedAt.ToString("yyyy/MM/dd HH:mm");
    private static string Format(double value) => Math.Abs(value % 1) < 0.000001 ? value.ToString("N0") : value.ToString("N2");
}
