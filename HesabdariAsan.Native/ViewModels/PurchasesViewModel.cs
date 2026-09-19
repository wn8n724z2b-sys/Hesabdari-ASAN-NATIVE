using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class PurchasesViewModel : PagedViewModelBase
{
    private readonly PurchaseService _service = new();
    public ObservableCollection<PurchaseRecord> Items { get; } = new();
    public RelayCommand AddCommand { get; }
    public PurchasesViewModel()
    {
        AddCommand = new RelayCommand(_ => Add());
        Reload();
    }
    public override void Reload()
    {
        var r = _service.Search(Search, Page, PageSize);
        Items.Clear(); foreach (var x in r.Items) Items.Add(x);
        TotalCount = r.TotalCount; TotalPages = r.TotalPages;
        if (Page > TotalPages) { Page = TotalPages; Reload(); }
    }
    private void Add()
    {
        var w = new PurchaseEntryWindow { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() == true) { Page = 1; Reload(); }
    }
}
