namespace HesabdariAsan.Native.Models;

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
