using System.Windows;
using System.Windows.Controls;
using HesabdariAsan.Native.ViewModels;
namespace HesabdariAsan.Native.Views;
public partial class ProductsView:UserControl
{
    public ProductsView(){InitializeComponent();}
    private void ManageCategories_Click(object sender,RoutedEventArgs e){if(Application.Current.MainWindow?.DataContext is MainViewModel vm)vm.Navigate("Categories");}
}
