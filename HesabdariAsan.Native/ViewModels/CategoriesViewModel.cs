using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class CategoriesViewModel : PagedViewModelBase
{
    private readonly CategoryService _service = new();
    private string _status = "";

    public ObservableCollection<CategoryItem> Items { get; } = new();
    public string Status { get => _status; private set => Set(ref _status, value); }
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand UpCommand { get; }
    public RelayCommand DownCommand { get; }

    public CategoriesViewModel()
    {
        AddCommand = new RelayCommand(_ => Add());
        EditCommand = new RelayCommand(x => Edit(x as CategoryItem), x => x is CategoryItem);
        DeleteCommand = new RelayCommand(x => Delete(x as CategoryItem), x => x is CategoryItem c && !c.IsDefault);
        UpCommand = new RelayCommand(x => Move(x as CategoryItem, -1), x => x is CategoryItem c && !c.IsDefault);
        DownCommand = new RelayCommand(x => Move(x as CategoryItem, 1), x => x is CategoryItem c && !c.IsDefault);
        Reload();
    }

    public override void Reload()
    {
        var r = _service.Search(Search, Page, PageSize);
        Items.Clear();
        foreach (var x in r.Items) Items.Add(x);
        TotalCount = r.TotalCount;
        TotalPages = r.TotalPages;
        if (Page > TotalPages) { Page = TotalPages; Reload(); }
    }

    private void Add()
    {
        var w = new CategoryEditWindow { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try
        {
            _service.Add(w.CategoryName);
            Page = 1;
            Search = "";
            Reload();
            Status = $"دسته «{w.CategoryName}» اضافه شد.";
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "دسته‌ها", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Edit(CategoryItem? c)
    {
        if (c is null || c.IsDefault) return;
        var w = new CategoryEditWindow(c.Name) { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() != true) return;
        try { _service.Rename(c.Name, w.CategoryName); Reload(); Status = "نام دسته ویرایش شد."; }
        catch (Exception ex) { AppDialog.Show(ex.Message, "دسته‌ها", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Delete(CategoryItem? c)
    {
        if (c is null || c.IsDefault) return;
        if (AppDialog.Show($"دسته «{c.Name}» حذف شود؟\n\nاگر کالایی در این دسته باشد، به دسته عمومی منتقل می‌شود.", "حسابداری آسان", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { _service.Delete(c.Name); Reload(); Status = "دسته حذف شد و کالاهای آن به عمومی منتقل شدند."; }
        catch (Exception ex) { AppDialog.Show(ex.Message, "دسته‌ها", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Move(CategoryItem? c, int d)
    {
        if (c is null) return;
        try { _service.Move(c.Name, d); Reload(); Status = "ترتیب دسته‌ها ذخیره شد."; }
        catch (Exception ex) { AppDialog.Show(ex.Message, "دسته‌ها", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
