using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HesabdariAsan.Native.ViewModels;
namespace HesabdariAsan.Native.Views;
public partial class DataSupportView : UserControl
{
    public DataSupportView(){InitializeComponent();}
    private void Unlock_Click(object sender,RoutedEventArgs e){if(DataContext is DataSupportViewModel vm && vm.Unlock(AdminPasswordBox.Password))AdminPasswordBox.Password="";}
    private void AdminPassword_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){e.Handled=true;Unlock_Click(sender,new RoutedEventArgs());}}
    private void OpenAudit_Click(object sender,RoutedEventArgs e){if(Application.Current.MainWindow?.DataContext is MainViewModel vm)vm.Navigate("Audit");}
}
