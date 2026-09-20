namespace HesabdariAsan.Native.Models;

public sealed class CategoryItem
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public int ProductCount { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault => string.Equals(Name,"عمومی",StringComparison.OrdinalIgnoreCase);
    public string CountText => $"{ProductCount:N0} کالا";
}
