namespace HesabdariAsan.Native.Models;

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
