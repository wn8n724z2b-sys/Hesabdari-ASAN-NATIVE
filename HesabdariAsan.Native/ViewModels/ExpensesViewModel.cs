using System.Collections.ObjectModel;
using System.Windows;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class ExpensesViewModel : PagedViewModelBase
{
    private readonly ExpenseService _service=new();
    private string _today="0 ؋",_month="0 ؋",_max="0 ؋",_maxTitle="—",_period="ماه جاری";
    public ObservableCollection<Expense> Items { get; }=new();
    public IReadOnlyList<string> PeriodOptions { get; }=new[]{"امروز","7 روز گذشته","ماه جاری","ماه قبل","همه"};
    public string TodayText{get=>_today;private set=>Set(ref _today,value);}public string MonthText{get=>_month;private set=>Set(ref _month,value);}public string MaxText{get=>_max;private set=>Set(ref _max,value);}public string MaxTitle{get=>_maxTitle;private set=>Set(ref _maxTitle,value);}
    public string SelectedPeriod{get=>_period;set{if(Set(ref _period,value)){Page=1;Reload();}}}
    public RelayCommand AddCommand{get;}public RelayCommand EditCommand{get;}public RelayCommand DeleteCommand{get;}
    public ExpensesViewModel(){AddCommand=new RelayCommand(_=>Add());EditCommand=new RelayCommand(x=>Edit(x as Expense),x=>x is Expense);DeleteCommand=new RelayCommand(x=>Delete(x as Expense),x=>x is Expense);Reload();}
    public override void Reload(){var r=_service.Search(Search,PeriodKey(),Page,PageSize);Items.Clear();foreach(var x in r.Items)Items.Add(x);TotalCount=r.TotalCount;TotalPages=r.TotalPages;if(Page>TotalPages){Page=TotalPages;Reload();return;}var s=_service.GetSummary();TodayText=$"{s.Today:N0} ؋";MonthText=$"{s.Month:N0} ؋";MaxText=$"{s.Max:N0} ؋";MaxTitle=s.MaxTitle;}
    private string PeriodKey()=>SelectedPeriod switch{"امروز"=>"TODAY","7 روز گذشته"=>"LAST7","ماه جاری"=>"MONTH","ماه قبل"=>"PREVMONTH",_=>"ALL"};
    private void Add(){var w=new ExpenseEditWindow{Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){_service.Save(w.Expense);Page=1;Reload();}}
    private void Edit(Expense? e){if(e is null)return;var w=new ExpenseEditWindow(e){Owner=Application.Current.MainWindow};if(w.ShowDialog()==true){_service.Save(w.Expense);Reload();}}
    private void Delete(Expense? e){if(e is null)return;if(MessageBox.Show("این هزینه حذف شود؟","حسابداری آسان",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){_service.Delete(e.Id);Reload();}}
}
