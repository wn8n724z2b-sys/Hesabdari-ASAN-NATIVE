using System.Windows;
using HesabdariAsan.Native.Dialogs;

namespace HesabdariAsan.Native.Services;

public static class AppDialog
{
    public static MessageBoxResult Show(
        string messageBoxText,
        string caption = "حسابداری آسان",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        MessageBoxOptions options = MessageBoxOptions.None)
    {
        if (Application.Current?.Dispatcher is not null && !Application.Current.Dispatcher.CheckAccess())
            return Application.Current.Dispatcher.Invoke(() => Show(messageBoxText, caption, button, icon, defaultResult, options));

        var window = new AppDialogWindow(messageBoxText, caption, button, icon, defaultResult);
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsLoaded && owner.IsVisible && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        window.ShowDialog();
        return window.Result;
    }
}
