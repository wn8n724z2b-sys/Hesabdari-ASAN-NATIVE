using System.Windows;
using HesabdariAsan.Native.Models;
namespace HesabdariAsan.Native.Dialogs;
public partial class DebtPaymentWindow:Window
{
 public long Amount{get;private set;} public DebtPaymentWindow(Party p){InitializeComponent();TitleText.Text=p.Type=="COMPANY"?"پرداخت قرض شرکت":"دریافت قرض مشتری";CustomerText.Text=$"{p.Name} · قرض فعلی {p.Debt:N0} ؋";}
 private void Save_Click(object sender,RoutedEventArgs e){if(!long.TryParse(AmountBox.Text,out var a)||a<=0){MessageBox.Show("مبلغ درست وارد نشده است.");return;}Amount=a;DialogResult=true;}
 private void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}
