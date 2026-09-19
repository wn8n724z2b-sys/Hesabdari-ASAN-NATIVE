namespace HesabdariAsan.Native.ViewModels;

public abstract class PagedViewModelBase : ViewModelBase
{
    private int _page=1,_pageSize=10,_totalPages=1,_totalCount;
    private string _search="";
    public IReadOnlyList<int> PageSizes { get; } = new[]{10,20,50};
    public int Page { get=>_page; protected set{if(Set(ref _page,value)){OnPropertyChanged(nameof(PageText));OnPropertyChanged(nameof(CanPrevious));OnPropertyChanged(nameof(CanNext));}} }
    public int PageSize { get=>_pageSize; set{if(Set(ref _pageSize,value)){Page=1;Reload();}} }
    public int TotalPages { get=>_totalPages; protected set{if(Set(ref _totalPages,value)){OnPropertyChanged(nameof(PageText));OnPropertyChanged(nameof(CanNext));}} }
    public int TotalCount { get=>_totalCount; protected set{if(Set(ref _totalCount,value))OnPropertyChanged(nameof(TotalCountText));} }
    public string TotalCountText=>$"تعداد کل: {TotalCount:N0}";
    public string Search { get=>_search; set{if(Set(ref _search,value)){Page=1;Reload();}} }
    public string PageText=>$"صفحه {Page} از {TotalPages}";
    public bool CanPrevious=>Page>1;
    public bool CanNext=>Page<TotalPages;
    public RelayCommand PreviousCommand { get; }
    public RelayCommand NextCommand { get; }
    protected PagedViewModelBase(){PreviousCommand=new RelayCommand(_=>{if(CanPrevious){Page--;Reload();}});NextCommand=new RelayCommand(_=>{if(CanNext){Page++;Reload();}});}
    public abstract void Reload();
}
