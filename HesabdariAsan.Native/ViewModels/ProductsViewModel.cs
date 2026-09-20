using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class ProductsViewModel : PagedViewModelBase
{
    private readonly ProductService _service=new();
    private readonly CategoryService _categories=new();
    private string _selectedCategory="همه دسته‌ها",_selectedFilter="همه کالاها";
    public ObservableCollection<Product> Items { get; }=new();
    public ObservableCollection<string> CategoryOptions { get; }=new();
    public IReadOnlyList<string> FilterOptions { get; }=new[]{"همه کالاها","موجودی کم"};
    public string SelectedCategory{get=>_selectedCategory;set{if(Set(ref _selectedCategory,value)){Page=1;Reload();}}}
    public string SelectedFilter{get=>_selectedFilter;set{if(Set(ref _selectedFilter,value)){Page=1;Reload();}}}
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public ProductsViewModel()
    {
        AddCommand=new RelayCommand(_=>Add());EditCommand=new RelayCommand(x=>Edit(x as Product),x=>x is Product);DeleteCommand=new RelayCommand(x=>Delete(x as Product),x=>x is Product);ReloadCategories();Reload();
    }
    public override void Reload()
    {
        var cat=SelectedCategory=="همه دسته‌ها"?"ALL":SelectedCategory;var filter=SelectedFilter=="موجودی کم"?"LOW":"ALL";
        var r=_service.Search(Search,cat,filter,Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();}
    }
    public void ReloadCategories(){var old=SelectedCategory;CategoryOptions.Clear();CategoryOptions.Add("همه دسته‌ها");foreach(var x in _categories.Names())CategoryOptions.Add(x);if(CategoryOptions.Contains(old))_selectedCategory=old;else _selectedCategory="همه دسته‌ها";OnPropertyChanged(nameof(SelectedCategory));}
    private void Add(){var w=new ProductEditWindow{Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){_service.Save(w.Product);ReloadCategories();Page=1;Reload();}}
    private void Edit(Product? p){if(p is null)return;var w=new ProductEditWindow(p){Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){_service.Save(w.Product);ReloadCategories();Reload();}}
    private void Delete(Product? p){if(p is null)return;if(MessageBox.Show($"کالای «{p.Name}» حذف شود؟","حسابداری آسان",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){_service.Delete(p.Id);Reload();}}
}
