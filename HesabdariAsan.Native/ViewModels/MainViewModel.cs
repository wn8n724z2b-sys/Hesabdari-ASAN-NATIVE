using System.Collections.ObjectModel;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly GlobalSearchService _globalSearch = new();
    private readonly SettingsService _settingsService = new();
    private DashboardViewModel? _dashboard;
    private SalesViewModel? _sales;
    private ProductsViewModel? _products;
    private CategoriesViewModel? _categories;
    private PartiesViewModel? _parties;
    private PurchasesViewModel? _purchases;
    private ExpensesViewModel? _expenses;
    private InventoryViewModel? _inventory;
    private ReportsViewModel? _reports;
    private AuditViewModel? _audit;
    private ArchiveViewModel? _archive;
    private SettingsViewModel? _settings;
    private DataSupportViewModel? _dataSupport;
    private object _currentPage;
    private string _currentPageKey = "Dashboard";
    private string _pageTitle = "داشبورد";
    private string _pageSubtitle = "نمای کلی وضعیت فروشگاه";
    private string _globalQuery = "";
    private bool _isSearchOpen;

    public DashboardViewModel Dashboard => _dashboard ??= new DashboardViewModel();
    public SalesViewModel Sales => _sales ??= new SalesViewModel();
    public ProductsViewModel Products => _products ??= new ProductsViewModel();
    public CategoriesViewModel Categories => _categories ??= new CategoriesViewModel();
    public PartiesViewModel Parties => _parties ??= new PartiesViewModel();
    public PurchasesViewModel Purchases => _purchases ??= new PurchasesViewModel();
    public ExpensesViewModel Expenses => _expenses ??= new ExpensesViewModel();
    public InventoryViewModel Inventory => _inventory ??= new InventoryViewModel();
    public ReportsViewModel Reports => _reports ??= new ReportsViewModel();
    public AuditViewModel Audit => _audit ??= new AuditViewModel();
    public ArchiveViewModel Archive => _archive ??= new ArchiveViewModel();
    public SettingsViewModel Settings => _settings ??= new SettingsViewModel();
    public DataSupportViewModel DataSupport => _dataSupport ??= new DataSupportViewModel();

    public ObservableCollection<SearchHit> SearchResults { get; } = new();
    public object CurrentPage { get => _currentPage; private set => Set(ref _currentPage, value); }
    public string CurrentPageKey { get => _currentPageKey; private set => Set(ref _currentPageKey, value); }
    public string PageTitle { get => _pageTitle; private set => Set(ref _pageTitle, value); }
    public string PageSubtitle { get => _pageSubtitle; private set => Set(ref _pageSubtitle, value); }
    public string GlobalQuery { get => _globalQuery; set { if (Set(ref _globalQuery, value)) RunSearch(); } }
    public bool IsSearchOpen { get => _isSearchOpen; set => Set(ref _isSearchOpen, value); }

    public RelayCommand NavigateCommand { get; }
    public RelayCommand SearchHitCommand { get; }
    public RelayCommand NewSaleCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public MainViewModel()
    {
        _currentPage = Dashboard;
        NavigateCommand = new RelayCommand(x => Navigate(x?.ToString() ?? "Dashboard"));
        SearchHitCommand = new RelayCommand(x => OpenHit(x as SearchHit));
        NewSaleCommand = new RelayCommand(_ => Navigate("Sales"));
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    public void Navigate(string page)
    {
        CurrentPageKey = page;
        (PageTitle, PageSubtitle) = page switch
        {
            "Sales" => ("فروش", "ثبت سریع فاکتور و تسویه"),
            "Products" => ("کالاها", "کالا، قیمت، بارکد و موجودی"),
            "Categories" => ("دسته‌ها", "ساختار دسته‌بندی کالاها"),
            "Purchases" => ("خرید و ورود کالا", "ثبت خرید و افزایش موجودی"),
            "Parties" => ("مشتریان و شرکت‌ها", "مدیریت حساب‌ها، قرض و پرداخت"),
            "Expenses" => ("هزینه‌ها", "ثبت و پیگیری مصارف فروشگاه"),
            "Inventory" => ("انبار", "وضعیت موجودی و ارزش کالاها"),
            "Reports" => ("گزارش‌ها", "تحلیل فروش، سود و گردش مالی"),
            "Archive" => ("آرشیو فاکتورها", "تمام فاکتورها، جستجو و عملیات"),
            "Audit" => ("ثبت رویدادها", "تاریخچه تغییرات و عملیات سیستم"),
            "Data" => ("داده‌ها و پشتیبانی", "Backup و سلامت دیتابیس"),
            "Settings" => ("تنظیمات", "ظاهر، فروشگاه، چاپ و پشتیبان‌گیری"),
            _ => ("داشبورد", "نمای کلی وضعیت فروشگاه")
        };

        CurrentPage = page switch
        {
            "Sales" => Sales,
            "Products" => Products,
            "Categories" => Categories,
            "Purchases" => Purchases,
            "Parties" => Parties,
            "Expenses" => Expenses,
            "Inventory" => Inventory,
            "Reports" => Reports,
            "Archive" => Archive,
            "Audit" => Audit,
            "Data" => DataSupport,
            "Settings" => Settings,
            _ => Dashboard
        };

        switch (CurrentPage)
        {
            case DashboardViewModel d: d.Reload(); break;
            case ProductsViewModel p: p.ReloadCategories(); p.Reload(); break;
            case CategoriesViewModel c: c.Reload(); break;
            case PurchasesViewModel p: p.Reload(); break;
            case PartiesViewModel p: p.Reload(); break;
            case ExpensesViewModel e: e.Reload(); break;
            case InventoryViewModel i: i.Reload(); break;
            case ReportsViewModel r: r.Reload(); break;
            case ArchiveViewModel a: a.Reload(); break;
            case AuditViewModel a: a.Reload(); break;
            case DataSupportViewModel d: d.Reload(); break;
            case SettingsViewModel s: s.Reload(); break;
        }
        IsSearchOpen = false;
    }

    private void ToggleTheme()
    {
        var oldTheme = _settingsService.Get("theme", "Light");
        var newTheme = oldTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        _settingsService.Set("theme", newTheme);
        ThemeService.Apply(newTheme);
        if (_settings is not null) _settings.Reload();
    }

    private void RunSearch()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(GlobalQuery)) { IsSearchOpen = false; return; }
        foreach (var hit in _globalSearch.Search(GlobalQuery, 10)) SearchResults.Add(hit);
        IsSearchOpen = SearchResults.Count > 0;
    }

    private void OpenHit(SearchHit? hit)
    {
        if (hit is null) return;
        Navigate(hit.TargetPage);
        GlobalQuery = "";
        IsSearchOpen = false;
    }

    public void HandleBarcode(string barcode)
    {
        Navigate("Sales");
        Sales.HandleBarcode(barcode);
    }
}
