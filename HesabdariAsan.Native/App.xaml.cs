using System.Windows;
using System.Windows.Threading;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Services;
using HesabdariAsan.Native.Dialogs;

namespace HesabdariAsan.Native;

public partial class App : Application
{
    private static bool IsSmokeTest => string.Equals(
        Environment.GetEnvironmentVariable("HESABDARI_SMOKE_TEST"), "1", StringComparison.Ordinal);

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                BootstrapLog("AppDomain.UnhandledException", ex);
                AppLog.Error(ex, "AppDomain.UnhandledException");
            }
        };

        base.OnStartup(e);
        BootstrapLog("OnStartup entered");

        try
        {
            Database.Initialize();
            BootstrapLog("Database.Initialize OK");

            var check = Database.QuickCheck();
            if (!string.Equals(check, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("بررسی سلامت دیتابیس موفق نبود: " + check);
            BootstrapLog("Database.QuickCheck OK");

            ThemeService.ApplySavedTheme();
            FontScaleService.ApplySavedScale();
            BootstrapLog("Theme and font scale OK");

            if (!IsSmokeTest)
            {
                var settings = new SettingsService().Load();
                if (!settings.OnboardingComplete)
                {
                    BootstrapLog("First-run onboarding opened");
                    var firstRun = new FirstRunWindow();
                    if (firstRun.ShowDialog() != true)
                    {
                        BootstrapLog("First-run onboarding cancelled");
                        Shutdown(0);
                        return;
                    }
                    ThemeService.ApplySavedTheme();
                    FontScaleService.ApplySavedScale();
                    BootstrapLog("First-run onboarding completed");
                }
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            BootstrapLog("MainWindow shown");

            if (IsSmokeTest)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    BootstrapLog("Smoke test completed");
                    Shutdown(0);
                };
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            BootstrapLog("Startup failed", ex);
            AppLog.Error(ex, "Startup");
            if (!IsSmokeTest)
            {
                AppDialog.Show(
                    "برنامه هنگام شروع با خطا روبه‌رو شد.\n\n" + ex.Message +
                    "\n\nگزارش خطا در پوشه Logs ذخیره شده است.",
                    "حسابداری آسان", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Shutdown(-1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        BootstrapLog("DispatcherUnhandledException", e.Exception);
        AppLog.Error(e.Exception, "Dispatcher");
        e.Handled = true;

        if (IsSmokeTest)
        {
            Shutdown(-2);
            return;
        }

        AppDialog.Show(
            "یک خطای غیرمنتظره رخ داد و در Log ثبت شد.\n\n" + e.Exception.Message,
            "حسابداری آسان", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void BootstrapLog(string message, Exception? ex = null)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HesabdariAsan", "Logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "startup.log");
            var line = $"[{DateTime.Now:O}] {message}" +
                       (ex is null ? "" : $"\n{ex}\n") + Environment.NewLine;
            File.AppendAllText(path, line);
        }
        catch
        {
            // Startup diagnostics must never become another startup failure.
        }
    }
}
