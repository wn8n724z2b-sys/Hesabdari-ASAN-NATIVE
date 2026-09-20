using System.Windows;
namespace HesabdariAsan.Native.Dialogs;
public partial class CategoryEditWindow:Window
{
    public string CategoryName{get;private set;}="";
    public CategoryEditWindow(string? name=null){InitializeComponent();CategoryName=name??"";NameBox.Text=CategoryName;TitleText.Text=string.IsNullOrWhiteSpace(name)?"دسته جدید":"ویرایش دسته";Loaded+=(_,_)=>{NameBox.Focus();NameBox.SelectAll();};}
    private void Save_Click(object sender,RoutedEventArgs e){var n=NameBox.Text.Trim();if(n.Length==0){AppDialog.Show("نام دسته را وارد کنید.","حسابداری آسان");return;}CategoryName=n;DialogResult=true;}
    private void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}
