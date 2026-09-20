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

    public PurchaseEntryWindow(Product? product = null)
    {
        InitializeComponent();
        if (product is not null) SelectProduct(_products.Get(product.Id) ?? product);
    }

    private void ProductSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_internalChange) return;
        _selectedProduct = null; SelectedProductText.Text = "کالایی انتخاب نشده"; UpdatePurchaseModeUi();
        var q = ProductSearchBox.Text.Trim();
        if (q.Length == 0) { ProductPopup.IsOpen = false; return; }
        ProductList.ItemsSource = _products.Search(q, 1, 10).Items;
        ProductPopup.IsOpen = ProductList.Items.Count > 0;
    }

    private void ProductList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductList.SelectedItem is Product p) SelectProduct(p);
    }

    private void SelectProduct(Product p)
    {
        _selectedProduct = p;
        _internalChange = true; ProductSearchBox.Text = p.Name; _internalChange = false;
        SelectedProductText.Text = $"{p.Name} · موجودی {p.StockText} · میانگین خرید فعلی {p.PurchasePriceText}";
        ProductPopup.IsOpen = false;
        PurchaseModeRadio.IsEnabled = p.UnitConversionEnabled;
        if (!p.UnitConversionEnabled) BaseModeRadio.IsChecked = true;
        else PurchaseModeRadio.IsChecked = true;
        if (p.UnitConversionEnabled && p.PackageBuyPrice > 0) UnitCostBox.Text = p.PackageBuyPrice.ToString(CultureInfo.InvariantCulture);
        else UnitCostBox.Text = p.PurchasePrice.ToString(CultureInfo.InvariantCulture);
        UpdatePurchaseModeUi();
    }

    private void SupplierSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_internalChange) return;
        _selectedSupplier = null; SelectedSupplierText.Text = "بدون شرکت (برای خرید نقدی اختیاری)";
        var q = SupplierSearchBox.Text.Trim();
        if (q.Length == 0) { SupplierPopup.IsOpen = false; return; }
        SupplierList.ItemsSource = _parties.QuickSearchCompanies(q, 10); SupplierPopup.IsOpen = SupplierList.Items.Count > 0;
    }

    private void SupplierList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SupplierList.SelectedItem is not Party p) return;
        _selectedSupplier = p; _internalChange = true; SupplierSearchBox.Text = p.Name; _internalChange = false;
        SelectedSupplierText.Text = $"انتخاب شد: {p.Name} · قرض فعلی {p.DebtText}"; SupplierPopup.IsOpen = false;
    }

    private void PurchaseMode_Changed(object sender, RoutedEventArgs e) => UpdatePurchaseModeUi();
    private void PurchaseValue_Changed(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void PaymentBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (IsCredit() && _selectedSupplier is null) SelectedSupplierText.Text = "برای خرید نسیه انتخاب تأمین‌کننده الزامی است.";
        else if (_selectedSupplier is null) SelectedSupplierText.Text = "بدون شرکت (برای خرید نقدی اختیاری)";
    }

    private void UpdatePurchaseModeUi()
    {
        if (QuantityLabel is null || CostLabel is null || UnitModeHint is null) return;
        if (_selectedProduct is null)
        {
            PurchaseModeRadio.IsEnabled = false; QuantityLabel.Text = "تعداد واحد"; CostLabel.Text = "قیمت خرید هر واحد";
            UnitModeHint.Text = "بعد از انتخاب کالا، واحدها و تبدیل اینجا نمایش داده می‌شود."; UpdatePreview(); return;
        }
        var packageMode = PurchaseModeRadio.IsChecked == true && _selectedProduct.UnitConversionEnabled;
        if (packageMode)
        {
            QuantityLabel.Text = $"تعداد {_selectedProduct.PurchaseUnit}";
            CostLabel.Text = $"قیمت هر {_selectedProduct.PurchaseUnit}";
            UnitModeHint.Text = $"هر {_selectedProduct.PurchaseUnit} = {Format(_selectedProduct.UnitsPerPurchase)} {_selectedProduct.BaseUnit} · موجودی همیشه با {_selectedProduct.BaseUnit} نگهداری می‌شود.";
        }
        else
        {
            QuantityLabel.Text = $"تعداد {_selectedProduct.BaseUnit}";
            CostLabel.Text = $"قیمت هر {_selectedProduct.BaseUnit}";
            UnitModeHint.Text = _selectedProduct.UnitConversionEnabled
                ? $"ورود مستقیم با واحد فروش ({_selectedProduct.BaseUnit})"
                : $"این کالا تک‌واحدی است: {_selectedProduct.BaseUnit}";
        }
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (IncomingUnitsText is null) return;
        if (_selectedProduct is null || !TryReadQuantity(out var qty) || !TryReadCost(out var cost))
        {
            IncomingUnitsText.Text = "—"; CalculatedUnitCostText.Text = "—"; PurchaseTotalText.Text = "—"; return;
        }
        var packageMode = PurchaseModeRadio.IsChecked == true && _selectedProduct.UnitConversionEnabled;
        var incoming = packageMode ? qty * Math.Max(1, _selectedProduct.UnitsPerPurchase) : qty;
        var total = packageMode ? qty * cost : incoming * cost;
        var unitCost = incoming <= 0 ? 0 : total / incoming;
        IncomingUnitsText.Text = $"{Format(incoming)} {_selectedProduct.BaseUnit}";
        CalculatedUnitCostText.Text = $"{unitCost:N0} ؋ / {_selectedProduct.BaseUnit}";
        PurchaseTotalText.Text = $"{total:N0} ؋";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedProduct is null) throw new InvalidOperationException("ابتدا کالا را انتخاب کنید.");
            if (!TryReadQuantity(out var qty) || qty <= 0) throw new InvalidOperationException("تعداد معتبر نیست.");
            if (!TryReadCost(out var cost) || cost < 0) throw new InvalidOperationException("قیمت خرید معتبر نیست.");
            var payment = IsCredit() ? "CREDIT" : "CASH";
            var packageMode = PurchaseModeRadio.IsChecked == true && _selectedProduct.UnitConversionEnabled;
            _purchases.Record(_selectedProduct.Id, _selectedSupplier?.Id, qty, cost, payment, NoteBox.Text, packageMode);
            DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "ثبت خرید", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private bool IsCredit() => (PaymentBox.SelectedItem as ComboBoxItem)?.Content?.ToString() == "نسیه";
    private bool TryReadQuantity(out double value)
    {
        var raw = QuantityBox.Text.Trim().Replace(",", "");
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.TryParse(raw, out value);
    }
    private bool TryReadCost(out long value) => long.TryParse(UnitCostBox.Text.Trim().Replace(",", ""), out value);
    private static string Format(double value) => Math.Abs(value % 1) < 0.000001 ? value.ToString("N0") : value.ToString("N2");
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
