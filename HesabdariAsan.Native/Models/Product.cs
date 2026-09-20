namespace HesabdariAsan.Native.Models;

public sealed class Product
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public string Producer { get; set; } = "";
    public string Barcode { get; set; } = "";
    public List<string> Barcodes { get; set; } = new();
    public string Category { get; set; } = "عمومی";
    public string Unit { get; set; } = "دانه";
    public string BaseUnit { get; set; } = "دانه";
    public string PurchaseUnit { get; set; } = "دانه";
    public bool UnitConversionEnabled { get; set; }
    public double UnitsPerPurchase { get; set; } = 1;
    public long PackageBuyPrice { get; set; }
    public string ImagePath { get; set; } = "";
    public long PurchasePrice { get; set; }
    public long SalePrice { get; set; }
    public double Stock { get; set; }
    public double MinStock { get; set; }
    public bool IsActive { get; set; } = true;

    public string SalePriceText => $"{SalePrice:N0} ؋";
    public string PurchasePriceText => $"{PurchasePrice:N0} ؋";
    public string StockText => $"{FormatNumber(Stock)} {BaseUnit}";
    public string MinStockText => FormatNumber(MinStock);
    public string BarcodeCountText => $"{Math.Max(Barcodes.Count, string.IsNullOrWhiteSpace(Barcode) ? 0 : 1)}/15";
    public string UnitModeText => UnitConversionEnabled
        ? $"{FormatNumber(UnitsPerPurchase)} {BaseUnit} / {PurchaseUnit}"
        : $"تک‌واحد · {BaseUnit}";
    public string ProductMeta => string.IsNullOrWhiteSpace(Producer) ? Category : $"{Category} · {Producer}";

    private static string FormatNumber(double value) => Math.Abs(value % 1) < 0.000001 ? value.ToString("N0") : value.ToString("N2");
}
