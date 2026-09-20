using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class PartiesViewModel : ViewModelBase
{
    private readonly PartyService _service = new();
    private string _customerSearch = "";
    private string _supplierSearch = "";
    private int _customerPage = 1, _supplierPage = 1;
    private int _customerPageSize = 10, _supplierPageSize = 10;
    private int _customerTotalPages = 1, _supplierTotalPages = 1;
    private int _customerTotalCount, _supplierTotalCount;

    public ObservableCollection<Party> CustomerItems { get; } = new();
    public ObservableCollection<Party> SupplierItems { get; } = new();
    public IReadOnlyList<int> PageSizes { get; } = new[] { 10, 20, 50 };

    public string CustomerSearch
    {
        get => _customerSearch;
        set { if (Set(ref _customerSearch, value)) { CustomerPage = 1; ReloadCustomers(); } }
    }
    public string SupplierSearch
    {
        get => _supplierSearch;
        set { if (Set(ref _supplierSearch, value)) { SupplierPage = 1; ReloadSuppliers(); } }
    }
    public int CustomerPageSize
    {
        get => _customerPageSize;
        set { if (Set(ref _customerPageSize, value)) { CustomerPage = 1; ReloadCustomers(); } }
    }
    public int SupplierPageSize
    {
        get => _supplierPageSize;
        set { if (Set(ref _supplierPageSize, value)) { SupplierPage = 1; ReloadSuppliers(); } }
    }
    public int CustomerPage
    {
        get => _customerPage;
        private set { if (Set(ref _customerPage, Math.Max(1, value))) { OnPropertyChanged(nameof(CustomerPageText)); OnPropertyChanged(nameof(CustomerCanPrevious)); OnPropertyChanged(nameof(CustomerCanNext)); } }
    }
    public int SupplierPage
    {
        get => _supplierPage;
        private set { if (Set(ref _supplierPage, Math.Max(1, value))) { OnPropertyChanged(nameof(SupplierPageText)); OnPropertyChanged(nameof(SupplierCanPrevious)); OnPropertyChanged(nameof(SupplierCanNext)); } }
    }
    public int CustomerTotalPages
    {
        get => _customerTotalPages;
        private set { if (Set(ref _customerTotalPages, Math.Max(1, value))) { OnPropertyChanged(nameof(CustomerPageText)); OnPropertyChanged(nameof(CustomerCanNext)); } }
    }
    public int SupplierTotalPages
    {
        get => _supplierTotalPages;
        private set { if (Set(ref _supplierTotalPages, Math.Max(1, value))) { OnPropertyChanged(nameof(SupplierPageText)); OnPropertyChanged(nameof(SupplierCanNext)); } }
    }
    public int CustomerTotalCount
    {
        get => _customerTotalCount;
        private set { if (Set(ref _customerTotalCount, value)) OnPropertyChanged(nameof(CustomerTotalText)); }
    }
    public int SupplierTotalCount
    {
        get => _supplierTotalCount;
        private set { if (Set(ref _supplierTotalCount, value)) OnPropertyChanged(nameof(SupplierTotalText)); }
    }

    public string CustomerPageText => $"صفحه {CustomerPage} از {CustomerTotalPages}";
    public string SupplierPageText => $"صفحه {SupplierPage} از {SupplierTotalPages}";
    public string CustomerTotalText => $"{CustomerTotalCount:N0} مشتری";
    public string SupplierTotalText => $"{SupplierTotalCount:N0} شرکت";
    public bool CustomerCanPrevious => CustomerPage > 1;
    public bool CustomerCanNext => CustomerPage < CustomerTotalPages;
    public bool SupplierCanPrevious => SupplierPage > 1;
    public bool SupplierCanNext => SupplierPage < SupplierTotalPages;

    public RelayCommand AddCustomerCommand { get; }
    public RelayCommand AddSupplierCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand PinCommand { get; }
    public RelayCommand PaymentCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand CustomerPreviousCommand { get; }
    public RelayCommand CustomerNextCommand { get; }
    public RelayCommand SupplierPreviousCommand { get; }
    public RelayCommand SupplierNextCommand { get; }

    public PartiesViewModel()
    {
        AddCustomerCommand = new RelayCommand(_ => Add("CUSTOMER"));
        AddSupplierCommand = new RelayCommand(_ => Add("COMPANY"));
        EditCommand = new RelayCommand(x => Edit(x as Party), x => x is Party);
        PinCommand = new RelayCommand(x => Pin(x as Party), x => x is Party);
        PaymentCommand = new RelayCommand(x => Pay(x as Party), x => x is Party p && p.Debt > 0);
        DeleteCommand = new RelayCommand(x => Delete(x as Party), x => x is Party);
        CustomerPreviousCommand = new RelayCommand(_ => { if (CustomerCanPrevious) { CustomerPage--; ReloadCustomers(); } });
        CustomerNextCommand = new RelayCommand(_ => { if (CustomerCanNext) { CustomerPage++; ReloadCustomers(); } });
        SupplierPreviousCommand = new RelayCommand(_ => { if (SupplierCanPrevious) { SupplierPage--; ReloadSuppliers(); } });
        SupplierNextCommand = new RelayCommand(_ => { if (SupplierCanNext) { SupplierPage++; ReloadSuppliers(); } });
        Reload();
    }

    public void Reload()
    {
        ReloadCustomers();
        ReloadSuppliers();
    }

    private void ReloadCustomers()
    {
        var r = _service.Search(CustomerSearch, "CUSTOMER", CustomerPage, CustomerPageSize);
        CustomerItems.Clear(); foreach (var x in r.Items) CustomerItems.Add(x);
        CustomerTotalCount = r.TotalCount; CustomerTotalPages = r.TotalPages;
        if (CustomerPage > CustomerTotalPages) { CustomerPage = CustomerTotalPages; ReloadCustomers(); }
    }

    private void ReloadSuppliers()
    {
        var r = _service.Search(SupplierSearch, "COMPANY", SupplierPage, SupplierPageSize);
        SupplierItems.Clear(); foreach (var x in r.Items) SupplierItems.Add(x);
        SupplierTotalCount = r.TotalCount; SupplierTotalPages = r.TotalPages;
        if (SupplierPage > SupplierTotalPages) { SupplierPage = SupplierTotalPages; ReloadSuppliers(); }
    }

    private void Add(string type)
    {
        var w = new PartyEditWindow(new Party { Type = type }) { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() == true) { _service.Save(w.Party); if (type == "COMPANY") SupplierPage = 1; else CustomerPage = 1; Reload(); }
    }
    private void Edit(Party? p)
    {
        if (p is null) return;
        var w = new PartyEditWindow(p) { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() == true) { _service.Save(w.Party); Reload(); }
    }
    private void Pin(Party? p) { if (p is null) return; _service.TogglePin(p.Id); Reload(); }
    private void Pay(Party? p)
    {
        if (p is null) return;
        var w = new DebtPaymentWindow(p) { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try
        {
            if (p.Type == "COMPANY") _service.RecordCompanyPayment(p.Id, w.Amount);
            else _service.RecordCustomerPayment(p.Id, w.Amount);
            Reload();
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "حسابداری آسان"); }
    }
    private void Delete(Party? p)
    {
        if (p is null) return;
        try
        {
            if (AppDialog.Show($"«{p.Name}» حذف شود؟", "حسابداری آسان", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _service.Delete(p.Id); Reload();
            }
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "حسابداری آسان"); }
    }
}
