using System.Windows;
namespace HesabdariAsan.Native.Dialogs;
public partial class InvoiceCancelWindow : Window
{
    public string Reason => ReasonBox.Text.Trim();
    public InvoiceCancelWindow() => InitializeComponent();
    private void Confirm_Click(object sender, RoutedEventArgs e) { if (Reason.Length < 2) { MessageBox.Show("دلیل ابطال را بنویسید.", "ابطال فاکتور"); return; } DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
