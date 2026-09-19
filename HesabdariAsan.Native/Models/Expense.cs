namespace HesabdariAsan.Native.Models;

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
