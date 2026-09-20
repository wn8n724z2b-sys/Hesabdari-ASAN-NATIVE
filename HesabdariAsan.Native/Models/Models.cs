using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace HesabdariAsan.Native.Models;

// ---- AppSettings ----
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

// ---- AuditEntry ----
public sealed class AuditEntry
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public string Actor { get; set; } = "Admin";
    public long? InvoiceNo { get; set; }
    public string DateText => CreatedAt.ToString("yyyy/MM/dd HH:mm:ss");
    public string InvoiceText => InvoiceNo.HasValue ? $"#{InvoiceNo.Value}" : "—";
}

// ---- CartItem ----
public sealed class CartItem : INotifyPropertyChanged
{
    private double _quantity = 1;
    public long ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Unit { get; set; } = "دانه";
    public long UnitPrice { get; set; }
    public long PurchasePrice { get; set; }
    public double AvailableStock { get; set; }
    public int RowNumber { get; set; }
    public double Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(LineTotal)); OnPropertyChanged(nameof(QuantityText)); OnPropertyChanged(nameof(LineTotalText)); } }
    public long LineTotal => (long)Math.Round(UnitPrice * Quantity);
    public string UnitPriceText => $"{UnitPrice:N0} ؋";
    public string LineTotalText => $"{LineTotal:N0} ؋";
    public string QuantityText => Quantity % 1 == 0 ? Quantity.ToString("N0") : Quantity.ToString("N2");
    public string QuantityWithUnitText => $"{QuantityText} {Unit}";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(n));
}

// ---- CategoryItem ----
public sealed class CategoryItem
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public int ProductCount { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault => string.Equals(Name,"عمومی",StringComparison.OrdinalIgnoreCase);
    public string CountText => $"{ProductCount:N0} کالا";
}

// ---- DailyFinancePoint ----
public sealed class DailyFinancePoint
{
    public DateTime Date { get; set; }
    public long Sales { get; set; }
    public long Expenses { get; set; }
    public long Debt { get; set; }
    public long Payments { get; set; }
    public long NetProfit { get; set; }
}

// ---- DashboardMetrics ----
public sealed class DashboardMetrics
{
    public long TodaySales { get; set; }
    public long TodayExpenses { get; set; }
    public long CustomerDebt { get; set; }
    public long InventoryValue { get; set; }
    public long TodayNetProfit { get; set; }
    public long TodaySalesCount { get; set; }
    public long LowStockCount { get; set; }
    public long CustomerCount { get; set; }

    public string TodaySalesText => $"{TodaySales:N0} ؋";
    public string TodayExpensesText => $"{TodayExpenses:N0} ؋";
    public string CustomerDebtText => $"{CustomerDebt:N0} ؋";
    public string InventoryValueText => $"{InventoryValue:N0} ؋";
    public string TodayNetProfitText => $"{TodayNetProfit:N0} ؋";
    public string TodaySalesCountText => $"{TodaySalesCount:N0} فاکتور";
    public string LowStockCountText => $"{LowStockCount:N0}";
    public string CustomerCountText => $"{CustomerCount:N0}";
}

// ---- Expense ----
public sealed class Expense
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "هزینه";
    public long Amount { get; set; }
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string AmountText => $"{Amount:N0} ؋";
    public string DateText => CreatedAt.ToString("yyyy/MM/dd HH:mm");
}

// ---- FinancialPeriodInfo ----
public sealed class FinancialPeriodInfo
{
    public long Id { get; set; }
    public int PeriodNo { get; set; } = 1;
    public DateTime StartedAt { get; set; } = DateTime.Today;
    public DateTime? EndedAt { get; set; }
    public int MonthNumber { get; set; } = 1;
    public double ProgressPercent { get; set; }
    public bool IsPastRecommendedLength { get; set; }
}

// ---- InventoryLedgerEntry ----
public sealed class InventoryLedgerEntry
{
    public int RowNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ProductName { get; set; } = "";
    public string MovementType { get; set; } = "";
    public double Quantity { get; set; }
    public string ReferenceType { get; set; } = "";
    public string Note { get; set; } = "";
    public string DateText => CreatedAt.ToString("yyyy/MM/dd HH:mm");
    public string QuantityText => Quantity % 1 == 0 ? Quantity.ToString("+0;-0;0") : Quantity.ToString("+0.##;-0.##;0");
    public string MovementText => MovementType switch
    {
        "SALE" => "فروش",
        "PURCHASE" => "خرید",
        "OPENING" => "موجودی اولیه",
        "ADJUSTMENT" => "اصلاح",
        "INVOICE_CANCEL" => "برگشت فاکتور",
        _ => MovementType
    };
}

// ---- InventoryRow ----
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

// ---- InvoiceDetail ----
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

// ---- InvoiceEditData ----
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

// ---- InvoiceSummary ----
public sealed class InvoiceSummary
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public long InvoiceNo { get; set; }
    public string CustomerName { get; set; } = "مشتری عمومی";
    public string PaymentType { get; set; } = "CASH";
    public double ItemQuantity { get; set; }
    public long Total { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public string TotalText => $"{Total:N0} ؋";
    public string PaymentText => PaymentType == "CREDIT" ? "نسیه" : "نقدی";
    public string StatusText => Status == "CANCELLED" ? "باطل" : "فعال";
    public bool IsActive => Status != "CANCELLED";
    public string DateText => CreatedAt.ToString("yyyy/MM/dd HH:mm");
    public string ItemsText => Math.Abs(ItemQuantity - Math.Round(ItemQuantity)) < 0.000001
        ? $"{ItemQuantity:N0}"
        : $"{ItemQuantity:N2}".TrimEnd('0').TrimEnd('.');
}

// ---- MigrationResult ----
public sealed class MigrationResult
{
    public int Products { get; set; }
    public int Parties { get; set; }
    public int Invoices { get; set; }
    public int InvoiceItems { get; set; }
    public int Expenses { get; set; }
    public int Purchases { get; set; }
    public int Receipts { get; set; }
    public int SupplierPayments { get; set; }
    public int InventoryRows { get; set; }
    public string SafetyBackupPath { get; set; } = "";

    public override string ToString() =>
        $"{Products} کالا، {Parties} مشتری/شرکت، {Invoices} فاکتور، {Expenses} هزینه، {Purchases} خرید";
}

// ---- PagedResult ----
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
}

// ---- Party ----
public sealed class Party
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
    public string Type { get; set; } = "CUSTOMER";
    public string TypeText => Type == "COMPANY" ? "شرکت / تأمین‌کننده" : "مشتری";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public long Debt { get; set; }
    public bool IsPinned { get; set; }
    public string DebtText => $"{Debt:N0} ؋";
    public string PinText => IsPinned ? "★" : "☆";
}

// ---- Product ----
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

// ---- PurchaseRecord ----
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

// ---- ReportBalanceSnapshot ----
public sealed class ReportBalanceSnapshot
{
    public long CustomerDebt { get; set; }
    public long SupplierDebt { get; set; }
    public long InventoryValue { get; set; }
    public long CashMonth { get; set; }
    public long CashToday { get; set; }
}

// ---- ReportPeriodSummary ----
public sealed class ReportPeriodSummary
{
    public long Sales { get; set; }
    public int InvoiceCount { get; set; }
    public long Expenses { get; set; }
    public long DebtSales { get; set; }
    public long Payments { get; set; }
    public long NetProfit { get; set; }
    public long GrossProfit { get; set; }
    public long CostOfGoods { get; set; }
    public long CashSales { get; set; }
    public long CreditSales { get; set; }
    public long CustomerReceipts { get; set; }
    public long Purchases { get; set; }
    public long CashPurchases { get; set; }
    public long SupplierPayments { get; set; }
    public long Discounts { get; set; }
    public long CashIn { get; set; }
    public long CashOut { get; set; }
    public long NetCash { get; set; }
}

// ---- SearchHit ----
public sealed class SearchHit
{
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string TargetPage { get; set; } = "";
}
