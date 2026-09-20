using System.Windows;
using System.Windows.Controls;
using HesabdariAsan.Native.ViewModels;

namespace HesabdariAsan.Native.Views;
public partial class InventoryView:UserControl
{
    public InventoryView(){InitializeComponent();}
    private void OpenPurchases_Click(object sender,RoutedEventArgs e)
    {
        if(Application.Current.MainWindow?.DataContext is MainViewModel vm) vm.Navigate("Purchases");
    }
}
