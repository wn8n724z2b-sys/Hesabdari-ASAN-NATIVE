using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Navigation;
using HesabdariAsan.Native.Services;
using HesabdariAsan.Native.ViewModels;

namespace HesabdariAsan.Native;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _pulseTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _backupTimer = new() { Interval = TimeSpan.FromHours(1) };
    private readonly AutoBackupService _autoBackup = new();
    private readonly BackupService _backup = new();
    private readonly WorkSessionService _workSession = new();
    private readonly RawInputBarcodeService _scanner = new();
    private readonly SettingsService _settings = new();
    private readonly MainViewModel _vm;
    private bool _allowClose;

    private static bool IsSmokeTest => string.Equals(
        Environment.GetEnvironmentVariable("HESABDARI_SMOKE_TEST"), "1", StringComparison.Ordinal);

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        SourceInitialized += (_, _) => AttachScanner();
        Loaded += (_, _) =>
        {
            _workSession.Start();
            Tick();
            _clockTimer.Tick += (_, _) => Tick();
            _clockTimer.Start();
            _pulseTimer.Tick += (_, _) => _workSession.Pulse();
            _pulseTimer.Start();
            _autoBackup.Check();
            _backupTimer.Tick += (_, _) => _autoBackup.Check();
            _backupTimer.Start();
        };
        Closing += OnClosing;
        Closed += (_, _) => StopRuntimeServices();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || IsSmokeTest) return;

        var answer = MessageBox.Show(
            "آیا می‌خواهید از حسابداری آسان خارج شوید؟\n\nقبل از خروج یک نسخه پشتیبان ایمن از دیتابیس ساخته می‌شود.",
            "خروج از حسابداری آسان",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        if (answer != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            _workSession.Pulse();
            _backup.CreateBackup("exit");
            _backup.KeepLatest(30);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "ExitBackup");
            var closeAnyway = MessageBox.Show(
                "تهیه نسخه پشتیبان هنگام خروج با خطا روبه‌رو شد.\n\n" + ex.Message +
                "\n\nآیا با این حال برنامه بسته شود؟",
                "خطای پشتیبان‌گیری",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            if (closeAnyway != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        _allowClose = true;
        StopRuntimeServices();
    }

    private void StopRuntimeServices()
    {
        _scanner.Dispose();
        _backupTimer.Stop();
        _pulseTimer.Stop();
        _clockTimer.Stop();
        _workSession.Stop();
    }

    private void AttachScanner()
    {
        try
        {
            _scanner.Attach(this);
            _scanner.BarcodeScanned += (_, barcode) =>
            {
                if (!IsActive || !IsEnabled) return;
                if (_settings.Get("barcode_scanner_enabled", "1") == "0") return;
                _vm.HandleBarcode(barcode);
            };
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "RawInputBarcode.Attach");
            // Keyboard-wedge scanners can still work through the focused sales search box.
        }
    }

    private void Tick()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        WorkText.Text = _workSession.GetTodayDuration().ToString(@"hh\:mm\:ss");
    }
    private void Creator_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "CreatorLink");
        }
    }

}
