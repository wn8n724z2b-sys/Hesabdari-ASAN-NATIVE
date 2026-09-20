using System.Windows;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Dialogs;

public partial class ExpenseEditWindow : Window
{
    public Expense Expense { get; }
    public ExpenseEditWindow(Expense? expense=null)
    {
        InitializeComponent();Expense=expense is null?new Expense{CreatedAt=DateTime.Now}:new Expense{Id=expense.Id,Title=expense.Title,Category=expense.Category,Amount=expense.Amount,Note=expense.Note,CreatedAt=expense.CreatedAt};TitleText.Text=Expense.Id==0?"ثبت هزینه":"ویرایش هزینه";NameBox.Text=Expense.Title;CategoryBox.Text=Expense.Category;AmountBox.Text=Expense.Amount.ToString();NoteBox.Text=Expense.Note;
    }
    private void Save_Click(object sender,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(NameBox.Text)){MessageBox.Show("عنوان هزینه را وارد کنید.");return;}if(!long.TryParse(AmountBox.Text,out var amount)||amount<0){MessageBox.Show("مبلغ درست نیست.");return;}Expense.Title=NameBox.Text.Trim();Expense.Category=string.IsNullOrWhiteSpace(CategoryBox.Text)?"هزینه":CategoryBox.Text.Trim();Expense.Amount=amount;Expense.Note=NoteBox.Text.Trim();DialogResult=true;}
    private void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}
