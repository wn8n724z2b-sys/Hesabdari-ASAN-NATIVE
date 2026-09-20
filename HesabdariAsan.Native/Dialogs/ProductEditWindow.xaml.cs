using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.Dialogs;

public partial class ProductEditWindow : Window
{
    private static readonly string[] Units = { "دانه", "عدد", "بسته", "جعبه", "کارتن", "دوجین", "رول", "متر", "کیلو", "لیتر" };
    private readonly CategoryService _categories = new();
    private readonly MediaStorageService _media = new();
    private readonly string _originalImage;
    private string _selectedImage = "";
    private bool _loaded;

    public Product Product { get; }

    public ProductEditWindow(Product? source = null)
    {
        InitializeComponent();
        Product = Clone(source ?? new Product { Category = "عمومی", Unit = "دانه", BaseUnit = "دانه", PurchaseUnit = "بسته", UnitsPerPurchase = 1 });
        _originalImage = Product.ImagePath;
        _selectedImage = Product.ImagePath;
        TitleText.Text = Product.Id == 0 ? "کالای جدید" : "ویرایش کالا";
        foreach (var u in Units) { SimpleUnitBoxCombo.Items.Add(u); BaseUnitBox.Items.Add(u); PurchaseUnitBox.Items.Add(u); }
        ReloadCategories();
        LoadValues();
        _loaded = true;
        UpdateUnitMode();
        UpdateConversionPreview();
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private static Product Clone(Product p) => new()
    {
        Id = p.Id, Name = p.Name, Producer = p.Producer, Barcode = p.Barcode, Barcodes = p.Barcodes.ToList(), Category = p.Category,
        Unit = p.Unit, BaseUnit = p.BaseUnit, PurchaseUnit = p.PurchaseUnit, UnitConversionEnabled = p.UnitConversionEnabled,
        UnitsPerPurchase = p.UnitsPerPurchase, PackageBuyPrice = p.PackageBuyPrice, ImagePath = p.ImagePath,
        PurchasePrice = p.PurchasePrice, SalePrice = p.SalePrice, Stock = p.Stock, MinStock = p.MinStock, IsActive = p.IsActive
    };

    private void ReloadCategories()
    {
        var selected = CategoryBox.Text;
        CategoryBox.Items.Clear();
        foreach (var c in _categories.Names()) CategoryBox.Items.Add(c);
        CategoryBox.Text = string.IsNullOrWhiteSpace(selected) ? Product.Category : selected;
    }

    private void LoadValues()
    {
        NameBox.Text = Product.Name; ProducerBox.Text = Product.Producer; CategoryBox.Text = Product.Category;
        SaleBox.Text = Product.SalePrice.ToString(CultureInfo.InvariantCulture); StockBox.Text = Product.Stock.ToString(CultureInfo.InvariantCulture); MinStockBox.Text = Product.MinStock.ToString(CultureInfo.InvariantCulture);
        SimpleUnitBoxCombo.SelectedItem = string.IsNullOrWhiteSpace(Product.BaseUnit) ? "دانه" : Product.BaseUnit;
        BaseUnitBox.SelectedItem = string.IsNullOrWhiteSpace(Product.BaseUnit) ? "دانه" : Product.BaseUnit;
        PurchaseUnitBox.SelectedItem = string.IsNullOrWhiteSpace(Product.PurchaseUnit) ? "بسته" : Product.PurchaseUnit;
        PurchaseBox.Text = Product.PurchasePrice.ToString(CultureInfo.InvariantCulture);
        UnitsPerPurchaseBox.Text = Math.Max(1, Product.UnitsPerPurchase).ToString(CultureInfo.InvariantCulture);
        PackageBuyPriceBox.Text = Math.Max(0, Product.PackageBuyPrice).ToString(CultureInfo.InvariantCulture);
        ConversionCheck.IsChecked = Product.UnitConversionEnabled;
        SetImage(_selectedImage);
        var codes = Product.Barcodes.Count > 0 ? Product.Barcodes : (!string.IsNullOrWhiteSpace(Product.Barcode) ? new List<string> { Product.Barcode } : new List<string>());
        if (codes.Count == 0) codes.Add("");
        foreach (var code in codes.Take(15)) AddBarcodeRow(code);
        UpdateBarcodeCount();
    }

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "تصویر|*.png;*.jpg;*.jpeg;*.bmp|همه فایل‌ها|*.*" };
        if (d.ShowDialog() == true) { _selectedImage = d.FileName; SetImage(_selectedImage); }
    }

    private void RemoveImage_Click(object sender, RoutedEventArgs e) { _selectedImage = ""; ProductImage.Source = null; }

    private void SetImage(string? path)
    {
        ProductImage.Source = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try { var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(path); b.EndInit(); ProductImage.Source = b; } catch { }
    }

    private void QuickAddCategory_Click(object sender, RoutedEventArgs e)
    {
        var w = new CategoryEditWindow { Owner = this };
        if (w.ShowDialog() != true) return;
        try { _categories.Add(w.CategoryName); ReloadCategories(); CategoryBox.Text = w.CategoryName; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "دسته", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ConversionCheck_Changed(object sender, RoutedEventArgs e) { if (_loaded) { UpdateUnitMode(); UpdateConversionPreview(); } }
    private void ConversionValue_Changed(object sender, RoutedEventArgs e) { if (_loaded) UpdateConversionPreview(); }
    private void ConversionValue_Changed(object sender, TextChangedEventArgs e) { if (_loaded) UpdateConversionPreview(); }

    private void UpdateUnitMode()
    {
        var on = ConversionCheck.IsChecked == true;
        SimpleUnitBox.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        ConversionUnitBox.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateConversionPreview()
    {
        var unit = BaseUnitBox.SelectedItem?.ToString() ?? BaseUnitBox.Text ?? "دانه";
        var units = ParseDouble(UnitsPerPurchaseBox.Text, 1);
        var pack = ParseLong(PackageBuyPriceBox.Text, 0);
        var per = units <= 0 ? 0 : pack / units;
        UnitCostPreview.Text = $"{per:N0} ؋";
        UnitFormulaText.Text = $"{pack:N0} ÷ {Math.Max(1, units):N0} = {per:N0} ؋ / {unit}";
    }

    private void AddBarcode_Click(object sender, RoutedEventArgs e)
    {
        if (BarcodePanel.Children.Count >= 15) { ValidationText.Text = "حداکثر 15 بارکد مجاز است."; return; }
        AddBarcodeRow(""); UpdateBarcodeCount();
    }

    private void AddBarcodeRow(string value)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        var box = new TextBox { Text = value, Style = (Style)FindResource("Input"), FlowDirection = FlowDirection.LeftToRight, TextAlignment = TextAlignment.Left, Tag = "barcode" };
        var remove = new Button { Content = "حذف", Style = (Style)FindResource("DangerGhostButton"), Margin = new Thickness(7, 0, 0, 0), MinHeight = 38, Tag = row };
        remove.Click += (_, _) => { if (BarcodePanel.Children.Count > 1) { BarcodePanel.Children.Remove(row); UpdateBarcodeCount(); } else box.Text = ""; };
        Grid.SetColumn(remove, 1); row.Children.Add(box); row.Children.Add(remove); BarcodePanel.Children.Add(row);
    }

    private List<string> CollectBarcodes()
    {
        var list = new List<string>();
        foreach (var child in BarcodePanel.Children.OfType<Grid>())
        {
            var box = child.Children.OfType<TextBox>().FirstOrDefault();
            var value = box?.Text.Trim() ?? "";
            if (value.Length > 0 && !list.Contains(value, StringComparer.OrdinalIgnoreCase)) list.Add(value);
        }
        return list;
    }

    private void UpdateBarcodeCount() => BarcodeCountText.Text = $"{CollectBarcodes().Count} از 15 بارکد";

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        try
        {
            var name = NameBox.Text.Trim(); if (name.Length == 0) throw new InvalidOperationException("نام کالا را وارد کنید.");
            var barcodes = CollectBarcodes(); if (barcodes.Count == 0) throw new InvalidOperationException("حداقل یک بارکد وارد کنید.");
            if (!long.TryParse(SaleBox.Text.Replace(",", ""), out var sale) || sale < 0) throw new InvalidOperationException("قیمت فروش معتبر نیست.");
            var stock = ParseDoubleRequired(StockBox.Text, "موجودی معتبر نیست."); var min = ParseDoubleRequired(MinStockBox.Text, "حداقل موجودی معتبر نیست.");
            if (stock < 0 || min < 0) throw new InvalidOperationException("موجودی نمی‌تواند منفی باشد.");

            var conversion = ConversionCheck.IsChecked == true;
            var baseUnit = conversion ? (BaseUnitBox.SelectedItem?.ToString() ?? BaseUnitBox.Text) : (SimpleUnitBoxCombo.SelectedItem?.ToString() ?? SimpleUnitBoxCombo.Text);
            baseUnit = string.IsNullOrWhiteSpace(baseUnit) ? "دانه" : baseUnit.Trim();
            var purchaseUnit = conversion ? (PurchaseUnitBox.SelectedItem?.ToString() ?? PurchaseUnitBox.Text) : baseUnit;
            purchaseUnit = string.IsNullOrWhiteSpace(purchaseUnit) ? "بسته" : purchaseUnit.Trim();
            var units = conversion ? ParseDoubleRequired(UnitsPerPurchaseBox.Text, "تعداد داخل واحد خرید معتبر نیست.") : 1;
            if (units < 1) throw new InvalidOperationException("تعداد داخل واحد خرید حداقل 1 است.");
            var packagePrice = conversion ? ParseLongRequired(PackageBuyPriceBox.Text, "قیمت خرید واحد خرید معتبر نیست.") : ParseLongRequired(PurchaseBox.Text, "قیمت خرید معتبر نیست.");
            if (packagePrice < 0) throw new InvalidOperationException("قیمت خرید نمی‌تواند منفی باشد.");
            var unitCost = conversion ? (long)Math.Round(packagePrice / units, MidpointRounding.AwayFromZero) : packagePrice;

            var finalImage = _originalImage;
            if (string.IsNullOrWhiteSpace(_selectedImage)) finalImage = "";
            else if (!string.Equals(_selectedImage, _originalImage, StringComparison.OrdinalIgnoreCase)) finalImage = _media.ImportImage(_selectedImage, "products");

            Product.Name = name; Product.Producer = ProducerBox.Text.Trim(); Product.Category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? "عمومی" : CategoryBox.Text.Trim();
            Product.Barcodes = barcodes; Product.Barcode = barcodes[0]; Product.BaseUnit = baseUnit; Product.Unit = baseUnit; Product.PurchaseUnit = purchaseUnit;
            Product.UnitConversionEnabled = conversion; Product.UnitsPerPurchase = units; Product.PackageBuyPrice = packagePrice; Product.PurchasePrice = Math.Max(0, unitCost);
            Product.SalePrice = sale; Product.Stock = stock; Product.MinStock = min; Product.ImagePath = finalImage;
            if (!string.Equals(_originalImage, finalImage, StringComparison.OrdinalIgnoreCase)) _media.TryDeleteOwned(_originalImage);
            DialogResult = true;
        }
        catch (Exception ex) { ValidationText.Text = ex.Message; }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private static double ParseDouble(string value, double fallback) => double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) || double.TryParse(value, out n) ? n : fallback;
    private static double ParseDoubleRequired(string value, string message) { if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) || double.TryParse(value, out n)) return n; throw new InvalidOperationException(message); }
    private static long ParseLong(string value, long fallback) => long.TryParse((value ?? "").Replace(",", ""), out var n) ? n : fallback;
    private static long ParseLongRequired(string value, string message) { if (long.TryParse((value ?? "").Replace(",", ""), out var n)) return n; throw new InvalidOperationException(message); }
}
