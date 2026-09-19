using System.Windows;
using System.Windows.Threading;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_,args) =>
        {
            if(args.ExceptionObject is Exception ex)AppLog.Error(ex,"AppDomain.UnhandledException");
        };
        try
        {
            Database.Initialize();
            var check=Database.QuickCheck();
            if(!string.Equals(check,"ok",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("بررسی سلامت دیتابیس موفق نبود: "+check);
            ThemeService.ApplySavedTheme();
            FontScaleService.ApplySavedScale();
        }
        catch(Exception ex)
        {
            AppLog.Error(ex,"Startup");
            MessageBox.Show("برنامه نتوانست دیتابیس را آماده کند.\n\n"+ex.Message,"حسابداری آسان",MessageBoxButton.OK,MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error(e.Exception,"Dispatcher");
        MessageBox.Show("یک خطای غیرمنتظره رخ داد و در Log ثبت شد.\n\n"+e.Exception.Message,"حسابداری آسان",MessageBoxButton.OK,MessageBoxImage.Error);
        e.Handled=true;
    }
}
