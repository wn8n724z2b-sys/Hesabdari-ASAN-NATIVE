namespace HesabdariAsan.Native.Models;

public sealed class Product
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Category { get; set; } = "عمومی";
    public string Unit { get; set; } = "عدد";
    public long PurchasePrice { get; set; }
    public long SalePrice { get; set; }
    public double Stock { get; set; }
    public double MinStock { get; set; }
    public bool IsActive { get; set; } = true;
    public string SalePriceText => $"{SalePrice:N0} ؋";
    public string PurchasePriceText => $"{PurchasePrice:N0} ؋";
    public string StockText => Stock % 1 == 0 ? Stock.ToString("N0") : Stock.ToString("N2");
}
