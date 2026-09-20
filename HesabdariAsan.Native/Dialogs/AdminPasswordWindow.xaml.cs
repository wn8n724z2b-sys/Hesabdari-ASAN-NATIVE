using System.Windows;
using HesabdariAsan.Native.Services;
namespace HesabdariAsan.Native.Dialogs;
public partial class AdminPasswordWindow : Window
{
    private readonly AdminSecurityService _security = new();
    public AdminPasswordWindow(){InitializeComponent();CurrentPanel.Visibility=_security.HasPassword?Visibility.Visible:Visibility.Collapsed;}
    private void Cancel_Click(object sender,RoutedEventArgs e){DialogResult=false;}
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        if(_security.HasPassword && !_security.Verify(CurrentPassword.Password)){MessageBox.Show("رمز فعلی نادرست است.","رمز مدیر",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(NewPassword.Password.Length<4){MessageBox.Show("رمز جدید حداقل 4 کاراکتر باشد.");return;}
        if(NewPassword.Password!=ConfirmPassword.Password){MessageBox.Show("تکرار رمز جدید یکسان نیست.");return;}
        try{_security.SetPassword(NewPassword.Password);DialogResult=true;}catch(Exception ex){MessageBox.Show(ex.Message,"رمز مدیر",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
}
