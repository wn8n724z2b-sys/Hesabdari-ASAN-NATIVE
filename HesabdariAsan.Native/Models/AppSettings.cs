namespace HesabdariAsan.Native.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Light";
    public double UiScale { get; set; } = 1.0;
    public string ManagerName { get; set; } = "مدیر سیستم";
    public string ManagerImagePath { get; set; } = "";
    public string ShopName { get; set; } = "فروشگاه من";
    public string ShopPhone { get; set; } = "";
    public string ShopAddress { get; set; } = "";
    public string ShopLogoPath { get; set; } = "";
    public int AutoBackupPerDay { get; set; } = 4;
    public string PrinterName { get; set; } = "";
    public bool PrintAfterSale { get; set; }
    public bool BarcodeScannerEnabled { get; set; } = true;
}
