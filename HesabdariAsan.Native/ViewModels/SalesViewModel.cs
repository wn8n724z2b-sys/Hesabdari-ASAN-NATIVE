using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class SalesViewModel : ViewModelBase
{
    private readonly ProductService _products = new();
    private readonly PartyService _parties = new();
    private readonly SalesService _sales = new();
    private readonly DashboardService _dashboard = new();
    private readonly ReceiptPrinterService _printer = new();
    private readonly SettingsService _settings = new();
    private string _productSearch = "", _customerSearch = "", _paymentText = "", _discountText = "0";
    private Party? _selectedCustomer;
    private bool _isProductResultsOpen, _isCustomerResultsOpen, _isCustomerPickerOpen;
    private long? _lastInvoiceNo;
    private string _lastBarcode = "";
    private DateTime _lastBarcodeAt = DateTime.MinValue;
    private int _recentLimit = 10;

    public ObservableCollection<Product> ProductResults { get; } = new();
    public ObservableCollection<Party> CustomerResults { get; } = new();
    public ObservableCollection<CartItem> Cart { get; } = new();
    public ObservableCollection<InvoiceSummary> RecentInvoices { get; } = new();
    public IReadOnlyList<int> RecentLimits { get; } = new[] { 10, 20, 50 };

    public string ProductSearch { get => _productSearch; set { if (Set(ref _productSearch, value)) ReloadProducts(); } }
    public string CustomerSearch { get => _customerSearch; set { if (Set(ref _customerSearch, value)) ReloadCustomers(); } }
    public string PaymentText { get => _paymentText; set { if (Set(ref _paymentText, value)) { OnPropertyChanged(nameof(IsCash)); OnPropertyChanged(nameof(IsCredit)); OnPropertyChanged(nameof(PaymentRequired)); CheckoutCommand?.RaiseCanExecuteChanged(); } } }
    public bool IsCash => PaymentText == "نقدی";
    public bool IsCredit => PaymentText == "نسیه";
    public bool PaymentRequired => string.IsNullOrWhiteSpace(PaymentText);
    public string DiscountText { get => _discountText; set { if (Set(ref _discountText, value)) OnPropertyChanged(nameof(TotalText)); } }
    public bool IsProductResultsOpen { get => _isProductResultsOpen; set => Set(ref _isProductResultsOpen, value); }
    public bool IsCustomerResultsOpen { get => _isCustomerResultsOpen; set => Set(ref _isCustomerResultsOpen, value); }
    public bool IsCustomerPickerOpen { get => _isCustomerPickerOpen; set { if (Set(ref _isCustomerPickerOpen, value) && !value) { CustomerSearch = ""; CustomerResults.Clear(); IsCustomerResultsOpen = false; } } }
    public Party? SelectedCustomer { get => _selectedCustomer; private set { if (Set(ref _selectedCustomer, value)) OnPropertyChanged(nameof(SelectedCustomerText)); } }
    public string SelectedCustomerText => SelectedCustomer?.Name ?? "مشتری عمومی";
    public string SelectedCustomerMeta => SelectedCustomer is null || SelectedCustomer.Name == "مشتری عمومی" ? "مشتری پیش‌فرض" : (string.IsNullOrWhiteSpace(SelectedCustomer.Phone) ? "مشتری انتخاب‌شده" : SelectedCustomer.Phone);
    public string SubtotalText => $"{Cart.Sum(x => x.LineTotal):N0} ؋";
    public string SubtotalLabel => $"جمع: {SubtotalText}";
    public long Discount => long.TryParse(DiscountText, out var d) ? Math.Max(0, d) : 0;
    public string TotalText => $"{Math.Max(0, Cart.Sum(x => x.LineTotal) - Discount):N0} ؋";
    public int RecentLimit { get => _recentLimit; set { if (Set(ref _recentLimit, value)) ReloadRecent(); } }

    public RelayCommand AddProductCommand { get; }
    public RelayCommand AddExactCommand { get; }
    public RelayCommand IncreaseCommand { get; }
    public RelayCommand DecreaseCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand SelectCustomerCommand { get; }
    public RelayCommand CheckoutCommand { get; }
    public RelayCommand PrintLastCommand { get; }
    public RelayCommand PrintInvoiceCommand { get; }
    public RelayCommand SetPaymentCommand { get; }
    public RelayCommand ClearCartCommand { get; }
    public RelayCommand ToggleCustomerPickerCommand { get; }

    public string LastInvoiceText => _lastInvoiceNo.HasValue ? $"آخرین فاکتور: #{_lastInvoiceNo.Value}" : "هنوز فاکتوری ثبت نشده";
    public string NextInvoiceText => $"#{_sales.GetNextInvoiceNo():D6}";

    public SalesViewModel()
    {
        AddProductCommand = new RelayCommand(x => AddProduct(x as Product), x => x is Product);
        AddExactCommand = new RelayCommand(_ => AddExact());
        IncreaseCommand = new RelayCommand(x => ChangeQty(x as CartItem, 1), x => x is CartItem);
        DecreaseCommand = new RelayCommand(x => ChangeQty(x as CartItem, -1), x => x is CartItem);
        RemoveCommand = new RelayCommand(x => Remove(x as CartItem), x => x is CartItem);
        SelectCustomerCommand = new RelayCommand(x => SelectCustomer(x as Party), x => x is Party);
        CheckoutCommand = new RelayCommand(_ => Checkout(), _ => Cart.Count > 0 && !PaymentRequired);
        PrintLastCommand = new RelayCommand(_ => PrintLast(), _ => _lastInvoiceNo.HasValue);
        PrintInvoiceCommand = new RelayCommand(x => PrintInvoice(x as InvoiceSummary), x => x is InvoiceSummary);
        SetPaymentCommand = new RelayCommand(x => PaymentText = x?.ToString() ?? "");
        ClearCartCommand = new RelayCommand(_ => ClearCart(), _ => Cart.Count > 0);
        ToggleCustomerPickerCommand = new RelayCommand(_ => IsCustomerPickerOpen = !IsCustomerPickerOpen);
        SelectedCustomer = _parties.QuickSearch("مشتری عمومی", 10).FirstOrDefault(x => x.Name == "مشتری عمومی");
        ReloadRecent();
    }

    private void ReloadProducts()
    {
        ProductResults.Clear();
        if (string.IsNullOrWhiteSpace(ProductSearch)) { IsProductResultsOpen = false; return; }
        foreach (var p in _products.Search(ProductSearch, 1, 10).Items) ProductResults.Add(p);
        IsProductResultsOpen = ProductResults.Count > 0;
    }

    private void ReloadCustomers()
    {
        CustomerResults.Clear();
        if (string.IsNullOrWhiteSpace(CustomerSearch)) { IsCustomerResultsOpen = false; return; }
        foreach (var p in _parties.QuickSearch(CustomerSearch, 10)) CustomerResults.Add(p);
        IsCustomerResultsOpen = CustomerResults.Count > 0;
    }

    private void ReloadRecent()
    {
        RecentInvoices.Clear();
        foreach (var invoice in _dashboard.RecentInvoices(RecentLimit)) RecentInvoices.Add(invoice);
    }

    private void AddExact()
    {
        if (string.IsNullOrWhiteSpace(ProductSearch)) return;
        var value = ProductSearch.Trim();
        var p = _products.FindByBarcode(value);
        if (p is not null) { HandleBarcode(value); return; }
        if (ProductResults.Count == 1) AddProduct(ProductResults[0]);
    }

    public void HandleBarcode(string barcode)
    {
        barcode = (barcode ?? "").Trim();
        if (barcode.Length == 0) return;
        var now = DateTime.UtcNow;
        if (string.Equals(_lastBarcode, barcode, StringComparison.OrdinalIgnoreCase) && (now - _lastBarcodeAt).TotalMilliseconds < 700) return;
        _lastBarcode = barcode; _lastBarcodeAt = now;
        var p = _products.FindByBarcode(barcode);
        if (p is null)
        {
            ProductSearch = barcode;
            AppDialog.Show($"بارکد «{barcode}» در کالاها پیدا نشد.", "بارکد", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        AddProduct(p);
    }

    private void AddProduct(Product? p)
    {
        if (p is null) return;
        if (p.Stock <= 0) { AppDialog.Show("موجودی این کالا صفر است."); return; }
        var item = Cart.FirstOrDefault(x => x.ProductId == p.Id);
        if (item is null)
        {
            item = new CartItem { ProductId = p.Id, Name = p.Name, Barcode = p.Barcode, Unit = p.BaseUnit, UnitPrice = p.SalePrice, PurchasePrice = p.PurchasePrice, AvailableStock = p.Stock, Quantity = 1 };
            item.PropertyChanged += CartItemChanged;
            Cart.Add(item);
        }
        else if (item.Quantity + 1 <= item.AvailableStock) item.Quantity++;
        else { AppDialog.Show("موجودی کافی نیست."); return; }
        ProductSearch = "";
        IsProductResultsOpen = false;
        Renumber();
        TotalsChanged();
        ClearCartCommand.RaiseCanExecuteChanged();
    }

    private void ChangeQty(CartItem? item, double delta)
    {
        if (item is null) return;
        var q = item.Quantity + delta;
        if (q <= 0) { Remove(item); return; }
        if (q > item.AvailableStock) { AppDialog.Show("موجودی کافی نیست."); return; }
        item.Quantity = q;
        TotalsChanged();
    }

    private void Remove(CartItem? item)
    {
        if (item is null) return;
        item.PropertyChanged -= CartItemChanged;
        Cart.Remove(item);
        Renumber();
        TotalsChanged();
        ClearCartCommand.RaiseCanExecuteChanged();
    }

    private void ClearCart()
    {
        foreach (var item in Cart) item.PropertyChanged -= CartItemChanged;
        Cart.Clear();
        DiscountText = "0";
        PaymentText = "";
        TotalsChanged();
        ClearCartCommand.RaiseCanExecuteChanged();
    }

    private void SelectCustomer(Party? party)
    {
        if (party is null) return;
        SelectedCustomer = party;
        IsCustomerPickerOpen = false;
        OnPropertyChanged(nameof(SelectedCustomerMeta));
        CustomerSearch = "";
        CustomerResults.Clear();
        IsCustomerResultsOpen = false;
    }

    private void CartItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CartItem.Quantity) or nameof(CartItem.LineTotal)) TotalsChanged();
    }

    private void Renumber() { for (var i = 0; i < Cart.Count; i++) Cart[i].RowNumber = i + 1; }
    private void TotalsChanged() { OnPropertyChanged(nameof(SubtotalText)); OnPropertyChanged(nameof(SubtotalLabel)); OnPropertyChanged(nameof(TotalText)); CheckoutCommand.RaiseCanExecuteChanged(); }

    private void Checkout()
    {
        try
        {
            var pt = PaymentText == "نقدی" ? "CASH" : PaymentText == "نسیه" ? "CREDIT" : "";
            if (pt == "CREDIT" && (SelectedCustomer is null || SelectedCustomer.Name == "مشتری عمومی"))
            {
                AppDialog.Show("برای فروش نسیه یک مشتری مشخص انتخاب کنید.");
                return;
            }
            var no = _sales.Checkout(Cart.ToList(), SelectedCustomer, pt, Discount);
            _lastInvoiceNo = no;
            OnPropertyChanged(nameof(LastInvoiceText));
            OnPropertyChanged(nameof(NextInvoiceText));
            PrintLastCommand.RaiseCanExecuteChanged();
            foreach (var item in Cart) item.PropertyChanged -= CartItemChanged;
            Cart.Clear();
            DiscountText = "0";
            PaymentText = "";
            SelectedCustomer = _parties.QuickSearch("مشتری عمومی", 10).FirstOrDefault(x => x.Name == "مشتری عمومی");
            OnPropertyChanged(nameof(SelectedCustomerMeta));
            TotalsChanged();
            ClearCartCommand.RaiseCanExecuteChanged();
            ReloadRecent();
            var settings = _settings.Load();
            if (settings.PrintAfterSale && !string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                try { _printer.PrintInvoice(no); }
                catch (Exception printEx) { AppDialog.Show("فاکتور ثبت شد، اما چاپ انجام نشد:\n" + printEx.Message, "چاپ", MessageBoxButton.OK, MessageBoxImage.Warning); }
            }
            AppDialog.Show($"فاکتور {no} با موفقیت ثبت شد.", "حسابداری آسان", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void PrintLast()
    {
        if (!_lastInvoiceNo.HasValue) return;
        try { _printer.PrintInvoice(_lastInvoiceNo.Value); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "چاپ فاکتور", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void PrintInvoice(InvoiceSummary? invoice)
    {
        if (invoice is null) return;
        try { _printer.PrintInvoice(invoice.InvoiceNo); }
        catch (Exception ex) { AppDialog.Show(ex.Message, "چاپ فاکتور", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
