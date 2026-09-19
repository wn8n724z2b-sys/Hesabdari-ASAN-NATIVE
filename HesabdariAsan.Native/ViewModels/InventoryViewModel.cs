using System.Collections.ObjectModel;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class InventoryViewModel : PagedViewModelBase
{
    private readonly InventoryService _service=new();
    public ObservableCollection<InventoryRow> Items{get;}=new();
    private string _total="0",_low="0",_out="0",_value="0 ؋";
    public string TotalProducts{get=>_total;private set=>Set(ref _total,value);}public string LowStock{get=>_low;private set=>Set(ref _low,value);}public string OutOfStock{get=>_out;private set=>Set(ref _out,value);}public string InventoryValue{get=>_value;private set=>Set(ref _value,value);}
    public InventoryViewModel(){Reload();}
    public override void Reload(){var r=_service.Search(Search,Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();return;}var s=_service.Summary();TotalProducts=s.TotalProducts.ToString("N0");LowStock=s.LowStock.ToString("N0");OutOfStock=s.OutOfStock.ToString("N0");InventoryValue=$"{s.InventoryValue:N0} ؋";}
}
