namespace HesabdariAsan.Native.Models;

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
