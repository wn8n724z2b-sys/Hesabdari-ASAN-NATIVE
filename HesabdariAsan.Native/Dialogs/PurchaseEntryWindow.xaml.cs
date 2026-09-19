using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.Dialogs;

public partial class PurchaseEntryWindow : Window
{
    private readonly ProductService _products = new();
    private readonly PartyService _parties = new();
    private readonly PurchaseService _purchases = new();
    private Product? _selectedProduct;
    private Party? _selectedSupplier;
    private bool _internalChange;

    public PurchaseEntryWindow() => InitializeComponent();

    private void ProductSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_internalChange) return;
        _selectedProduct = null; SelectedProductText.Text = "کالایی انتخاب نشده";
        var q = ProductSearchBox.Text.Trim();
        if (q.Length == 0) { ProductPopup.IsOpen = false; return; }
        ProductList.ItemsSource = _products.Search(q, 1, 10).Items;
        ProductPopup.IsOpen = ProductList.Items.Count > 0;
    }
    private void ProductList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductList.SelectedItem is not Product p) return;
        _selectedProduct = p; _internalChange = true; ProductSearchBox.Text = p.Name; _internalChange = false;
        SelectedProductText.Text = $"انتخاب شد: {p.Name} · موجودی {p.StockText} · خرید فعلی {p.PurchasePriceText}"; ProductPopup.IsOpen = false;
    }
    private void SupplierSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_internalChange) return;
        _selectedSupplier = null; SelectedSupplierText.Text = "بدون شرکت (برای خرید نقدی اختیاری)";
        var q = SupplierSearchBox.Text.Trim();
        if (q.Length == 0) { SupplierPopup.IsOpen = false; _selectedSupplier = null; SelectedSupplierText.Text = "بدون شرکت (برای خرید نقدی اختیاری)"; return; }
        SupplierList.ItemsSource = _parties.QuickSearchCompanies(q, 10); SupplierPopup.IsOpen = SupplierList.Items.Count > 0;
    }
    private void SupplierList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SupplierList.SelectedItem is not Party p) return;
        _selectedSupplier = p; _internalChange = true; SupplierSearchBox.Text = p.Name; _internalChange = false;
        SelectedSupplierText.Text = $"انتخاب شد: {p.Name} · قرض فعلی {p.DebtText}"; SupplierPopup.IsOpen = false;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedProduct is null) throw new InvalidOperationException("ابتدا کالا را انتخاب کنید.");
            if (!double.TryParse(QuantityBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var qty) && !double.TryParse(QuantityBox.Text.Trim(), out qty)) throw new InvalidOperationException("تعداد معتبر نیست.");
            if (!long.TryParse(UnitCostBox.Text.Trim().Replace(",", ""), out var cost)) throw new InvalidOperationException("قیمت خرید معتبر نیست.");
            var payment = (PaymentBox.SelectedItem as ComboBoxItem)?.Content?.ToString() == "نسیه" ? "CREDIT" : "CASH";
            _purchases.Record(_selectedProduct.Id, _selectedSupplier?.Id, qty, cost, payment, NoteBox.Text);
            DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "ثبت خرید", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
