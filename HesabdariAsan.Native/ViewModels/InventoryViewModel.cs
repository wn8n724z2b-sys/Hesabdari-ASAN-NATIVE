using System.Collections.ObjectModel;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class InventoryViewModel : PagedViewModelBase
{
    private readonly InventoryService _service=new();
    private readonly PurchaseService _purchases=new();
    public ObservableCollection<InventoryRow> Items{get;}=new();
    public ObservableCollection<InventoryLedgerEntry> Ledger{get;}=new();
    public ObservableCollection<PurchaseRecord> RecentPurchases{get;}=new();
    public IReadOnlyList<string> StatusOptions{get;}=new[]{"همه وضعیت‌ها","ناموجود","رو به اتمام","موجود"};
    private string _total="0",_units="0",_low="0",_out="0",_value="0 ؋",_status="همه وضعیت‌ها";
    public string TotalProducts{get=>_total;private set=>Set(ref _total,value);}public string TotalUnits{get=>_units;private set=>Set(ref _units,value);}public string LowStock{get=>_low;private set=>Set(ref _low,value);}public string OutOfStock{get=>_out;private set=>Set(ref _out,value);}public string InventoryValue{get=>_value;private set=>Set(ref _value,value);}
    public string SelectedStatus{get=>_status;set{if(Set(ref _status,value)){Page=1;Reload();}}}
    public string OutOfStockMeta=>$"{OutOfStock} ناموجود";
    public InventoryViewModel(){Reload();}
    public override void Reload()
    {
        var r=_service.Search(Search,StatusKey(),Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();return;}
        var s=_service.Summary();TotalProducts=s.TotalProducts.ToString("N0");TotalUnits=s.TotalUnits%1==0?s.TotalUnits.ToString("N0"):s.TotalUnits.ToString("N2");LowStock=s.LowStock.ToString("N0");OutOfStock=s.OutOfStock.ToString("N0");InventoryValue=$"{s.InventoryValue:N0} ؋";OnPropertyChanged(nameof(OutOfStockMeta));
        Ledger.Clear();foreach(var x in _service.RecentLedger(10))Ledger.Add(x);
        RecentPurchases.Clear();foreach(var x in _purchases.Search("",1,10).Items)RecentPurchases.Add(x);
    }
    private string StatusKey()=>SelectedStatus switch{"ناموجود"=>"OUT","رو به اتمام"=>"LOW","موجود"=>"OK",_=>"ALL"};
}
