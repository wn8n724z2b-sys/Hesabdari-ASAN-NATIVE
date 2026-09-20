using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HesabdariAsan.Native.Models;

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
