using System.Globalization;
using System.Windows;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Dialogs;

public partial class ProductEditWindow : Window
{
    public Product Product { get; }
    public ProductEditWindow(Product? product=null)
    {
        InitializeComponent();
        Product=product is null?new Product():new Product{Id=product.Id,Name=product.Name,Barcode=product.Barcode,Category=product.Category,Unit=product.Unit,PurchasePrice=product.PurchasePrice,SalePrice=product.SalePrice,Stock=product.Stock,MinStock=product.MinStock,IsActive=product.IsActive};
        TitleText.Text=Product.Id==0?"کالای جدید":"ویرایش کالا";
        NameBox.Text=Product.Name;BarcodeBox.Text=Product.Barcode;CategoryBox.Text=Product.Category;UnitBox.Text=Product.Unit;
        PurchaseBox.Text=Product.PurchasePrice.ToString(CultureInfo.InvariantCulture);SaleBox.Text=Product.SalePrice.ToString(CultureInfo.InvariantCulture);StockBox.Text=Product.Stock.ToString(CultureInfo.InvariantCulture);MinStockBox.Text=Product.MinStock.ToString(CultureInfo.InvariantCulture);
    }
    private void Save_Click(object sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(NameBox.Text)){MessageBox.Show("نام کالا را وارد کنید.","حسابداری آسان",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        if(!long.TryParse(PurchaseBox.Text,out var pp)||!long.TryParse(SaleBox.Text,out var sp)||!double.TryParse(StockBox.Text,NumberStyles.Any,CultureInfo.InvariantCulture,out var stock)||!double.TryParse(MinStockBox.Text,NumberStyles.Any,CultureInfo.InvariantCulture,out var min)){MessageBox.Show("مقادیر قیمت و موجودی را درست وارد کنید.");return;}
        Product.Name=NameBox.Text.Trim();Product.Barcode=BarcodeBox.Text.Trim();Product.Category=string.IsNullOrWhiteSpace(CategoryBox.Text)?"عمومی":CategoryBox.Text.Trim();Product.Unit=string.IsNullOrWhiteSpace(UnitBox.Text)?"عدد":UnitBox.Text.Trim();Product.PurchasePrice=Math.Max(0,pp);Product.SalePrice=Math.Max(0,sp);Product.Stock=stock;Product.MinStock=Math.Max(0,min);DialogResult=true;
    }
    private void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}
