using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class PartiesViewModel : PagedViewModelBase
{
    private readonly PartyService _service=new();
    private string _type="ALL";
    public ObservableCollection<Party> Items { get; }=new();
    public IReadOnlyList<string> TypeOptions { get; }=new[]{"همه","مشتریان","شرکت‌ها"};
    public string SelectedType
    {
        get=>_type switch{"CUSTOMER"=>"مشتریان","COMPANY"=>"شرکت‌ها",_=>"همه"};
        set{var v=value=="مشتریان"?"CUSTOMER":value=="شرکت‌ها"?"COMPANY":"ALL";if(_type!=v){_type=v;OnPropertyChanged();Page=1;Reload();}}
    }
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand PinCommand { get; }
    public RelayCommand PaymentCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public PartiesViewModel(){AddCommand=new RelayCommand(_=>Add());EditCommand=new RelayCommand(x=>Edit(x as Party),x=>x is Party);PinCommand=new RelayCommand(x=>Pin(x as Party),x=>x is Party);PaymentCommand=new RelayCommand(x=>Pay(x as Party),x=>x is Party p && p.Debt>0);DeleteCommand=new RelayCommand(x=>Delete(x as Party),x=>x is Party);Reload();}
    public override void Reload(){var r=_service.Search(Search,_type,Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();}}
    private void Add(){var w=new PartyEditWindow{Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){_service.Save(w.Party);Page=1;Reload();}}
    private void Edit(Party? p){if(p is null)return;var w=new PartyEditWindow(p){Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){_service.Save(w.Party);Reload();}}
    private void Pin(Party? p){if(p is null)return;_service.TogglePin(p.Id);Reload();}
    private void Pay(Party? p){if(p is null)return;var w=new DebtPaymentWindow(p){Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){try{if(p.Type=="COMPANY")_service.RecordCompanyPayment(p.Id,w.Amount);else _service.RecordCustomerPayment(p.Id,w.Amount);Reload();}catch(Exception ex){MessageBox.Show(ex.Message,"حسابداری آسان");}}}
    private void Delete(Party? p){if(p is null)return;try{if(MessageBox.Show($"«{p.Name}» حذف شود؟","حسابداری آسان",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){_service.Delete(p.Id);Reload();}}catch(Exception ex){MessageBox.Show(ex.Message,"حسابداری آسان");}}
}
