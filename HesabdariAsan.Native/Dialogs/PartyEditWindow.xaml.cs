using System.Windows;
using System.Windows.Controls;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Dialogs;

public partial class PartyEditWindow : Window
{
    public Party Party { get; }
    public PartyEditWindow(Party? party=null)
    {
        InitializeComponent();Party=party is null?new Party():new Party{Id=party.Id,Type=party.Type,Name=party.Name,Phone=party.Phone,Address=party.Address,Debt=party.Debt,IsPinned=party.IsPinned};TitleText.Text=Party.Id==0?"مشتری / شرکت جدید":"ویرایش مشتری / شرکت";TypeBox.SelectedIndex=Party.Type=="COMPANY"?1:0;NameBox.Text=Party.Name;PhoneBox.Text=Party.Phone;AddressBox.Text=Party.Address;DebtBox.Text=Party.Debt.ToString();PinBox.IsChecked=Party.IsPinned;
    }
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(NameBox.Text)){MessageBox.Show("نام را وارد کنید.");return;}if(!long.TryParse(DebtBox.Text,out var debt)){MessageBox.Show("مقدار قرض درست نیست.");return;}
        Party.Type=((ComboBoxItem)TypeBox.SelectedItem).Tag?.ToString()=="COMPANY"?"COMPANY":"CUSTOMER";Party.Name=NameBox.Text.Trim();Party.Phone=PhoneBox.Text.Trim();Party.Address=AddressBox.Text.Trim();Party.Debt=debt;Party.IsPinned=PinBox.IsChecked==true;DialogResult=true;
    }
    private void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}
