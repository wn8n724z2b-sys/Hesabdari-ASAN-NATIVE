using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.Dialogs;

public partial class InventoryAdjustWindow : Window
{
    private readonly ProductService _products = new();
    private readonly InventoryService _inventory = new();
    private Product? _selected;
    private bool _internal;

    public InventoryAdjustWindow(Product? product = null)
    {
        InitializeComponent();
        if (product is not null) SelectProduct(_products.Get(product.Id) ?? product);
    }

    private void ProductSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_internal) return;
        _selected = null; CurrentStockText.Text = "کالایی انتخاب نشده"; UpdatePreview();
        var q = ProductSearchBox.Text.Trim();
        if (q.Length == 0) { ProductPopup.IsOpen = false; return; }
        ProductList.ItemsSource = _products.Search(q, 1, 10).Items; ProductPopup.IsOpen = ProductList.Items.Count > 0;
    }

    private void ProductList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductList.SelectedItem is Product p) SelectProduct(p);
    }

    private void SelectProduct(Product p)
    {
        _selected = p; _internal = true; ProductSearchBox.Text = p.Name; NewStockBox.Text = Format(p.Stock); _internal = false;
        CurrentStockText.Text = $"موجودی ثبت‌شده: {p.StockText}"; ProductPopup.IsOpen = false; UpdatePreview();
    }

    private void NewStockBox_TextChanged(object sender, TextChangedEventArgs e) { if (!_internal) UpdatePreview(); }
    private void UpdatePreview()
    {
        if (DeltaText is null) return;
        if (_selected is null || !TryReadStock(out var value)) { DeltaText.Text = "—"; AfterText.Text = "—"; return; }
        var delta = value - _selected.Stock;
        DeltaText.Text = $"{(delta > 0 ? "+" : "")}{Format(delta)} {_selected.BaseUnit}";
        DeltaText.Foreground = delta < 0 ? (System.Windows.Media.Brush)FindResource("Danger") : (System.Windows.Media.Brush)FindResource("Success");
        AfterText.Text = $"{Format(value)} {_selected.BaseUnit}";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_selected is null) throw new InvalidOperationException("ابتدا کالا را انتخاب کنید.");
            if (!TryReadStock(out var stock) || stock < 0) throw new InvalidOperationException("موجودی جدید معتبر نیست.");
            _inventory.Adjust(_selected.Id, stock, ReasonBox.Text); DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "اصلاح موجودی", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private bool TryReadStock(out double value)
    {
        var raw = NewStockBox.Text.Trim().Replace(",", "");
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.TryParse(raw, out value);
    }
    private static string Format(double v) => Math.Abs(v % 1) < 0.000001 ? v.ToString("0") : v.ToString("0.##");
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
