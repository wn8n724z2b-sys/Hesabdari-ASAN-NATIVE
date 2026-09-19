using System.Collections.ObjectModel;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly GlobalSearchService _globalSearch = new();
    private DashboardViewModel? _dashboard;
    private SalesViewModel? _sales;
    private ProductsViewModel? _products;
    private PartiesViewModel? _parties;
    private PurchasesViewModel? _purchases;
    private ExpensesViewModel? _expenses;
    private InventoryViewModel? _inventory;
    private ReportsViewModel? _reports;
    private AuditViewModel? _audit;
    private SettingsViewModel? _settings;
    private object _currentPage;
    private string _globalQuery = "";
    private bool _isSearchOpen;

    public DashboardViewModel Dashboard => _dashboard ??= new DashboardViewModel();
    public SalesViewModel Sales => _sales ??= new SalesViewModel();
    public ProductsViewModel Products => _products ??= new ProductsViewModel();
    public PartiesViewModel Parties => _parties ??= new PartiesViewModel();
    public PurchasesViewModel Purchases => _purchases ??= new PurchasesViewModel();
    public ExpensesViewModel Expenses => _expenses ??= new ExpensesViewModel();
    public InventoryViewModel Inventory => _inventory ??= new InventoryViewModel();
    public ReportsViewModel Reports => _reports ??= new ReportsViewModel();
    public AuditViewModel Audit => _audit ??= new AuditViewModel();
    public SettingsViewModel Settings => _settings ??= new SettingsViewModel();

    public ObservableCollection<SearchHit> SearchResults { get; } = new();
    public object CurrentPage { get => _currentPage; private set => Set(ref _currentPage, value); }
    public string GlobalQuery { get => _globalQuery; set { if (Set(ref _globalQuery, value)) RunSearch(); } }
    public bool IsSearchOpen { get => _isSearchOpen; set => Set(ref _isSearchOpen, value); }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand SearchHitCommand { get; }

    public MainViewModel()
    {
        _currentPage = Dashboard;
        NavigateCommand = new RelayCommand(x => Navigate(x?.ToString() ?? "Dashboard"));
        SearchHitCommand = new RelayCommand(x => OpenHit(x as SearchHit));
    }

    public void Navigate(string page)
    {
        CurrentPage = page switch
        {
            "Sales" => Sales,
            "Products" => Products,
            "Purchases" => Purchases,
            "Parties" => Parties,
            "Expenses" => Expenses,
            "Inventory" => Inventory,
            "Reports" => Reports,
            "Audit" => Audit,
            "Settings" => Settings,
            _ => Dashboard
        };

        switch (CurrentPage)
        {
            case DashboardViewModel d: d.Reload(); break;
            case ProductsViewModel p: p.Reload(); break;
            case PurchasesViewModel p: p.Reload(); break;
            case PartiesViewModel p: p.Reload(); break;
            case ExpensesViewModel e: e.Reload(); break;
            case InventoryViewModel i: i.Reload(); break;
            case ReportsViewModel r: r.Reload(); break;
            case AuditViewModel a: a.Reload(); break;
            case SettingsViewModel s: s.Reload(); break;
        }
        IsSearchOpen = false;
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
