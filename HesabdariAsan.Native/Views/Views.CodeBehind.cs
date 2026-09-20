using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using HesabdariAsan.Native.ViewModels;

namespace HesabdariAsan.Native.Views;

// ---- ArchiveView.xaml ----
public partial class ArchiveView : UserControl { public ArchiveView() { InitializeComponent(); } }

// ---- AuditView.xaml ----
public partial class AuditView : UserControl { public AuditView() { InitializeComponent(); } }

// ---- CategoriesView.xaml ----
public partial class CategoriesView:UserControl{public CategoriesView(){InitializeComponent();}}

// ---- DashboardView.xaml ----
public partial class DashboardView:UserControl{public DashboardView(){InitializeComponent();}}

// ---- DataSupportView.xaml ----
public partial class DataSupportView : UserControl
{
    public DataSupportView(){InitializeComponent();}
    private void Unlock_Click(object sender,RoutedEventArgs e){if(DataContext is DataSupportViewModel vm && vm.Unlock(AdminPasswordBox.Password))AdminPasswordBox.Password="";}
    private void AdminPassword_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){e.Handled=true;Unlock_Click(sender,new RoutedEventArgs());}}
    private void OpenAudit_Click(object sender,RoutedEventArgs e){if(Application.Current.MainWindow?.DataContext is MainViewModel vm)vm.Navigate("Audit");}
}

// ---- ExpensesView.xaml ----
public partial class ExpensesView:UserControl{public ExpensesView(){InitializeComponent();}}

// ---- InventoryView.xaml ----
public partial class InventoryView : UserControl { public InventoryView() { InitializeComponent(); } }

// ---- PartiesView.xaml ----
public partial class PartiesView:UserControl{public PartiesView(){InitializeComponent();}}

// ---- ProductsView.xaml ----
public partial class ProductsView:UserControl
{
    public ProductsView(){InitializeComponent();}
    private void ManageCategories_Click(object sender,RoutedEventArgs e){if(Application.Current.MainWindow?.DataContext is MainViewModel vm)vm.Navigate("Categories");}
}

// ---- PurchasesView.xaml ----
public partial class PurchasesView : UserControl { public PurchasesView() { InitializeComponent(); } }

// ---- ReportsView.xaml ----
public partial class ReportsView:UserControl{public ReportsView(){InitializeComponent();}}

// ---- SalesView.xaml ----
public partial class SalesView:UserControl{public SalesView(){InitializeComponent();}}

// ---- SettingsView.xaml ----
public partial class SettingsView:UserControl{public SettingsView(){InitializeComponent();}}
