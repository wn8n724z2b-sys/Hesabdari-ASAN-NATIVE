namespace HesabdariAsan.Native.Models;

public sealed class InventoryRow
{
    public int RowNumber { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Category { get; set; } = "";
    public double Stock { get; set; }
    public double MinStock { get; set; }
    public long PurchasePrice { get; set; }
    public long SalePrice { get; set; }
    public long Value => (long)Math.Round(Stock * PurchasePrice);
    public string StockText => Stock % 1 == 0 ? Stock.ToString("N0") : Stock.ToString("N2");
    public long SaleValue => (long)Math.Round(Stock * SalePrice);
    public string ValueText => $"{Value:N0} ؋";
    public string SaleValueText => $"{SaleValue:N0} ؋";
    public string MinStockText => MinStock % 1 == 0 ? MinStock.ToString("N0") : MinStock.ToString("N2");
    public string Status => Stock <= 0 ? "تمام" : Stock <= MinStock ? "کم" : "مناسب";
}
