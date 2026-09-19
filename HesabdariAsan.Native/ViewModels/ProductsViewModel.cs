using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class ProductsViewModel : PagedViewModelBase
{
    private readonly ProductService _service=new();
    public ObservableCollection<Product> Items { get; }=new();
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public ProductsViewModel()
    {
        AddCommand=new RelayCommand(_=>Add());
        EditCommand=new RelayCommand(x=>Edit(x as Product),x=>x is Product);
        DeleteCommand=new RelayCommand(x=>Delete(x as Product),x=>x is Product);
        Reload();
    }
    public override void Reload()
    {
        var r=_service.Search(Search,Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();}
    }
    private void Add(){var w=new ProductEditWindow{Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){_service.Save(w.Product);Page=1;Reload();}}
    private void Edit(Product? p){if(p is null)return;var w=new ProductEditWindow(p){Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){_service.Save(w.Product);Reload();}}
    private void Delete(Product? p){if(p is null)return;if(MessageBox.Show($"کالای «{p.Name}» حذف شود؟","حسابداری آسان",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){_service.Delete(p.Id);Reload();}}
}
