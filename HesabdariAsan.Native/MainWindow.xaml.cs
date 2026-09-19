using System.Windows;
using System.Windows.Threading;
using HesabdariAsan.Native.Services;
using HesabdariAsan.Native.ViewModels;

namespace HesabdariAsan.Native;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _pulseTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _backupTimer = new() { Interval = TimeSpan.FromHours(1) };
    private readonly AutoBackupService _autoBackup = new();
    private readonly WorkSessionService _workSession = new();
    private readonly RawInputBarcodeService _scanner = new();
    private readonly SettingsService _settings = new();
    private readonly MainViewModel _vm;

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
        Closing += (_, _) =>
        {
            _scanner.Dispose();
            _backupTimer.Stop();
            _pulseTimer.Stop();
            _clockTimer.Stop();
            _workSession.Stop();
        };
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
}
