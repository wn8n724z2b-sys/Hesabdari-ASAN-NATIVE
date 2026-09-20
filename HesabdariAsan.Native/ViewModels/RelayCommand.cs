using System.Windows.Input;

namespace HesabdariAsan.Native.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _can;
    public RelayCommand(Action<object?> execute,Predicate<object?>? can=null){_execute=execute;_can=can;}
    public bool CanExecute(object? parameter)=>_can?.Invoke(parameter)??true;
    public void Execute(object? parameter)=>_execute(parameter);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
}
