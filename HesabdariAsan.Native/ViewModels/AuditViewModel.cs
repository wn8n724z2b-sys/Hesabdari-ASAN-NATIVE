using System.Collections.ObjectModel;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class AuditViewModel : PagedViewModelBase
{
    private readonly AuditService _service = new();
    public ObservableCollection<AuditEntry> Items { get; } = new();
    public AuditViewModel() => Reload();
    public override void Reload()
    {
        var r = _service.Search(Search, Page, PageSize);
        Items.Clear(); foreach (var x in r.Items) Items.Add(x);
        TotalCount = r.TotalCount; TotalPages = r.TotalPages;
        if (Page > TotalPages) { Page = TotalPages; Reload(); }
    }
}
