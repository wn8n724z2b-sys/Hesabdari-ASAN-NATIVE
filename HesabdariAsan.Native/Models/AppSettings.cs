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
    public string ReceiptFooter { get; set; } = "سپاس از خرید شما";
    public int AutoBackupPerDay { get; set; } = 4;
    public string PrinterName { get; set; } = "";
    public bool PrintAfterSale { get; set; }
    public bool BarcodeScannerEnabled { get; set; } = true;
    public string ScannerSuffix { get; set; } = "Enter";
    public bool OnboardingComplete { get; set; }

    public bool OnlineEnabled { get; set; }
    public string OnlineServerUrl { get; set; } = "";
    public string OnlineStoreCode { get; set; } = "";
    public int OnlineSyncMinutes { get; set; } = 15;
    public bool OnlineSyncSales { get; set; } = true;
    public bool OnlineSyncInventory { get; set; } = true;
    public string OnlineLastCheckAt { get; set; } = "";
    public string OnlineLastSyncAt { get; set; } = "";
    public string OnlineLastStatus { get; set; } = "local";
}
