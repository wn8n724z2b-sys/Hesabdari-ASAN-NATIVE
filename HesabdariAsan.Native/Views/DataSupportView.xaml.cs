using System.Windows;
using System.Windows.Controls;
using HesabdariAsan.Native.ViewModels;
namespace HesabdariAsan.Native.Views;public partial class DataSupportView:UserControl{public DataSupportView(){InitializeComponent();}private void OpenAudit_Click(object sender,RoutedEventArgs e){if(Application.Current.MainWindow?.DataContext is MainViewModel vm)vm.Navigate("Audit");}}
