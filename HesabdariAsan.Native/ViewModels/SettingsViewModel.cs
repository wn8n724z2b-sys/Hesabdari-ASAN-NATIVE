using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _service = new();
    private readonly BackupService _backup = new();
    private readonly ReceiptPrinterService _printer = new();
    private readonly V34MigrationService _migration = new();
    private readonly OnlineSyncService _online = new();
    private readonly MediaStorageService _media = new();
    private AppSettings _settings;
    private string _status = "";
    private bool _onlineBusy;

    public IReadOnlyList<int> BackupOptions { get; } = new[] { 1, 2, 3, 4, 5, 6 };
    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "روشن", "دارک" };
    public IReadOnlyList<string> ScannerSuffixOptions { get; } = new[] { "Enter", "Tab" };
    public IReadOnlyList<int> OnlineSyncMinuteOptions { get; } = new[] { 5, 15, 30, 60 };
    public ObservableCollection<string> Printers { get; } = new();

    public string ManagerName { get => _settings.ManagerName; set { _settings.ManagerName = value; OnPropertyChanged(); } }
    public string ManagerImagePath { get => _settings.ManagerImagePath; set { _settings.ManagerImagePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasManagerImage)); } }
    public bool HasManagerImage => !string.IsNullOrWhiteSpace(ManagerImagePath) && File.Exists(ManagerImagePath);
    public string ShopName { get => _settings.ShopName; set { _settings.ShopName = value; OnPropertyChanged(); } }
    public string ShopPhone { get => _settings.ShopPhone; set { _settings.ShopPhone = value; OnPropertyChanged(); } }
    public string ShopAddress { get => _settings.ShopAddress; set { _settings.ShopAddress = value; OnPropertyChanged(); } }
    public string ShopLogoPath { get => _settings.ShopLogoPath; set { _settings.ShopLogoPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasShopLogo)); } }
    public bool HasShopLogo => !string.IsNullOrWhiteSpace(ShopLogoPath) && File.Exists(ShopLogoPath);
    public string ReceiptFooter { get => _settings.ReceiptFooter; set { _settings.ReceiptFooter = value; OnPropertyChanged(); } }
    public double UiScale { get => _settings.UiScale; set { _settings.UiScale = Math.Clamp(value, 1, 2); OnPropertyChanged(); OnPropertyChanged(nameof(UiScaleText)); FontScaleService.Apply(_settings.UiScale); } }
    public string UiScaleText => $"{UiScale * 100:0}%";
    public int AutoBackupPerDay { get => _settings.AutoBackupPerDay; set { _settings.AutoBackupPerDay = Math.Clamp(value, 1, 6); OnPropertyChanged(); } }
    public string SelectedTheme { get => _settings.Theme == "Dark" ? "دارک" : "روشن"; set { _settings.Theme = value == "دارک" ? "Dark" : "Light"; OnPropertyChanged(); ThemeService.Apply(_settings.Theme); } }
    public string PrinterName { get => _settings.PrinterName; set { _settings.PrinterName = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(PrinterText)); } }
    public string PrinterText => string.IsNullOrWhiteSpace(PrinterName) ? "پرینتر انتخاب نشده" : PrinterName;
    public bool PrintAfterSale { get => _settings.PrintAfterSale; set { _settings.PrintAfterSale = value; OnPropertyChanged(); } }
    public bool BarcodeScannerEnabled { get => _settings.BarcodeScannerEnabled; set { _settings.BarcodeScannerEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(ScannerStatusText)); } }
    public string ScannerSuffix { get => _settings.ScannerSuffix; set { _settings.ScannerSuffix = value == "Tab" ? "Tab" : "Enter"; OnPropertyChanged(); } }
    public string ScannerStatusText => BarcodeScannerEnabled ? "آماده دریافت USB HID / Keyboard Wedge" : "بارکدخوان غیرفعال است";

    public bool OnlineEnabled { get => _settings.OnlineEnabled; set { _settings.OnlineEnabled = value; if (!value) _settings.OnlineLastStatus = "local"; OnPropertyChanged(); RaiseOnlineState(); } }
    public string OnlineServerUrl { get => _settings.OnlineServerUrl; set { _settings.OnlineServerUrl = value ?? ""; _settings.OnlineLastStatus = OnlineEnabled ? "ready" : "local"; OnPropertyChanged(); RaiseOnlineState(); } }
    public string OnlineStoreCode { get => _settings.OnlineStoreCode; set { _settings.OnlineStoreCode = value ?? ""; _settings.OnlineLastStatus = OnlineEnabled ? "ready" : "local"; OnPropertyChanged(); RaiseOnlineState(); } }
    public int OnlineSyncMinutes { get => _settings.OnlineSyncMinutes; set { _settings.OnlineSyncMinutes = value is 5 or 15 or 30 or 60 ? value : 15; OnPropertyChanged(); } }
    public bool OnlineSyncSales { get => _settings.OnlineSyncSales; set { _settings.OnlineSyncSales = value; OnPropertyChanged(); } }
    public bool OnlineSyncInventory { get => _settings.OnlineSyncInventory; set { _settings.OnlineSyncInventory = value; OnPropertyChanged(); } }
    public bool OnlineBusy { get => _onlineBusy; private set { if (Set(ref _onlineBusy, value)) RaiseOnlineState(); } }
    public bool OnlineControlsEnabled => OnlineEnabled && !OnlineBusy;
    public string OnlineStatusLabel => !OnlineEnabled ? "محلی" : _settings.OnlineLastStatus == "connected" ? "متصل" : _settings.OnlineLastStatus == "error" ? "خطا" : "آماده";
    public string OnlineStatusText
    {
        get
        {
            var text = !OnlineEnabled ? "وضعیت: حالت محلی" : _settings.OnlineLastStatus switch { "connected" => "وضعیت: اتصال سرور تأیید شده", "error" => "وضعیت: آخرین بررسی اتصال ناموفق بود", _ => "وضعیت: تنظیمات آنلاین فعال است" };
            if (!string.IsNullOrWhiteSpace(_settings.OnlineLastSyncAt) && DateTime.TryParse(_settings.OnlineLastSyncAt, out var d)) text += $" · آخرین Sync: {d:yyyy/MM/dd HH:mm}";
            return text;
        }
    }

    public string Status { get => _status; private set => Set(ref _status, value); }

    public RelayCommand SaveCommand { get; }
    public RelayCommand BrowseManagerImageCommand { get; }
    public RelayCommand RemoveManagerImageCommand { get; }
    public RelayCommand BrowseLogoCommand { get; }
    public RelayCommand RemoveLogoCommand { get; }
    public RelayCommand BackupCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand OpenBackupFolderCommand { get; }
    public RelayCommand SelectPrinterCommand { get; }
    public RelayCommand DetectPrintersCommand { get; }
    public RelayCommand TestPrintCommand { get; }
    public RelayCommand MigrateV34Command { get; }
    public RelayCommand CheckDatabaseCommand { get; }
    public RelayCommand TestOnlineCommand { get; }
    public RelayCommand SyncOnlineCommand { get; }

    public SettingsViewModel()
    {
        _settings = _service.Load();
        SaveCommand = new RelayCommand(_ => Save());
        BrowseManagerImageCommand = new RelayCommand(_ => Browse(true));
        RemoveManagerImageCommand = new RelayCommand(_ => { var p = ManagerImagePath; ManagerImagePath = ""; _media.TryDeleteOwned(p); });
        BrowseLogoCommand = new RelayCommand(_ => Browse(false));
        RemoveLogoCommand = new RelayCommand(_ => { var p = ShopLogoPath; ShopLogoPath = ""; _media.TryDeleteOwned(p); });
        BackupCommand = new RelayCommand(_ => Backup());
        RestoreCommand = new RelayCommand(_ => Restore());
        OpenBackupFolderCommand = new RelayCommand(_ => OpenFolder());
        SelectPrinterCommand = new RelayCommand(_ => SelectPrinter());
        DetectPrintersCommand = new RelayCommand(_ => DetectPrinters());
        TestPrintCommand = new RelayCommand(_ => TestPrint());
        MigrateV34Command = new RelayCommand(_ => MigrateV34());
        CheckDatabaseCommand = new RelayCommand(_ => CheckDatabase());
        TestOnlineCommand = new RelayCommand(async _ => await TestOnlineAsync());
        SyncOnlineCommand = new RelayCommand(async _ => await SyncOnlineAsync());
        DetectPrinters();
    }

    public void Reload()
    {
        _settings = _service.Load();
        OnPropertyChanged(string.Empty); OnPropertyChanged(nameof(PrinterText)); OnPropertyChanged(nameof(HasManagerImage)); OnPropertyChanged(nameof(HasShopLogo));
        RaiseOnlineState();
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ManagerName)) ManagerName = "مدیر سیستم";
        if (string.IsNullOrWhiteSpace(ShopName)) ShopName = "فروشگاه من";
        _service.Save(_settings); Status = "تنظیمات ذخیره شد.";
    }

    private void Browse(bool manager)
    {
        var dlg = new OpenFileDialog { Filter = "تصویر|*.png;*.jpg;*.jpeg;*.bmp|همه فایل‌ها|*.*" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var previous = manager ? ManagerImagePath : ShopLogoPath;
                var imported = _media.ImportImage(dlg.FileName, manager ? "manager" : "store");
                if (manager) ManagerImagePath = imported; else ShopLogoPath = imported;
                if (!string.Equals(previous, imported, StringComparison.OrdinalIgnoreCase)) _media.TryDeleteOwned(previous);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "تصویر", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    private void DetectPrinters()
    {
        Printers.Clear(); foreach (var name in _printer.GetInstalledPrinters()) Printers.Add(name);
        Status = Printers.Count == 0 ? "پرینتری توسط Windows پیدا نشد." : $"{Printers.Count} پرینتر شناسایی شد.";
    }

    private void TestPrint()
    {
        try { if (_printer.PrintTest(PrinterName)) Status = "چاپ آزمایشی ارسال شد."; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "چاپ آزمایشی", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async Task TestOnlineAsync()
    {
        if (OnlineBusy) return; Save(); OnlineBusy = true;
        try { await _online.TestConnectionAsync(_settings); _settings.OnlineLastStatus = "connected"; _settings.OnlineLastCheckAt = DateTime.Now.ToString("O"); Status = "اتصال آنلاین برقرار است."; }
        catch (Exception ex) { _settings.OnlineLastStatus = "error"; _settings.OnlineLastCheckAt = DateTime.Now.ToString("O"); Status = "اتصال به سرور برقرار نشد."; AppLog.Error(ex, "Online.Test"); }
        finally { OnlineBusy = false; _service.Save(_settings); RaiseOnlineState(); }
    }

    private async Task SyncOnlineAsync()
    {
        if (OnlineBusy) return; Save(); OnlineBusy = true;
        try { await _online.SyncAsync(_settings); _settings.OnlineLastStatus = "connected"; _settings.OnlineLastCheckAt = DateTime.Now.ToString("O"); _settings.OnlineLastSyncAt = DateTime.Now.ToString("O"); Status = "همگام‌سازی انجام شد."; }
        catch (Exception ex) { _settings.OnlineLastStatus = "error"; _settings.OnlineLastCheckAt = DateTime.Now.ToString("O"); Status = "همگام‌سازی انجام نشد؛ تنظیمات سرور را بررسی کنید."; AppLog.Error(ex, "Online.Sync"); }
        finally { OnlineBusy = false; _service.Save(_settings); RaiseOnlineState(); }
    }

    private void RaiseOnlineState()
    {
        OnPropertyChanged(nameof(OnlineControlsEnabled)); OnPropertyChanged(nameof(OnlineStatusLabel)); OnPropertyChanged(nameof(OnlineStatusText));
    }

    private void Backup() { try { var path = _backup.CreateBackup("manual"); _backup.KeepLatest(); Status = $"Backup سالم ساخته شد: {Path.GetFileName(path)}"; } catch (Exception ex) { MessageBox.Show(ex.Message, "Backup", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    private void Restore() { var dlg = new OpenFileDialog { Filter = "SQLite Backup|*.sqlite3;*.db|همه فایل‌ها|*.*", InitialDirectory = Directory.Exists(Database.BackupsDir) ? Database.BackupsDir : null }; if (dlg.ShowDialog() != true) return; if (MessageBox.Show("دیتابیس فعلی قبل از بازیابی Backup می‌شود. سپس فایل انتخاب‌شده جایگزین شود؟", "بازیابی اطلاعات", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; try { _backup.RestoreBackup(dlg.FileName); RestartApplication(); } catch (Exception ex) { MessageBox.Show(ex.Message, "بازیابی", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void SelectPrinter() { try { var selected = _printer.ChoosePrinter(); if (string.IsNullOrWhiteSpace(selected)) return; PrinterName = selected; _service.Set("printer_name", selected); Status = "پرینتر انتخاب شد."; } catch (Exception ex) { MessageBox.Show(ex.Message, "پرینتر", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    private void MigrateV34() { var detected = _migration.DetectDatabase(); var dlg = new OpenFileDialog { Filter = "دیتابیس حسابداری آسان v3|hesabdari_asan.sqlite3;*.sqlite3|همه فایل‌ها|*.*" }; if (!string.IsNullOrWhiteSpace(detected)) { dlg.InitialDirectory = Path.GetDirectoryName(detected); dlg.FileName = Path.GetFileName(detected); } if (dlg.ShowDialog() != true) return; if (MessageBox.Show("انتقال v3.4 یک Backup ایمنی می‌سازد و اطلاعات حسابداری فعلی v4 را با دیتای نسخه قدیمی جایگزین می‌کند. ادامه می‌دهید؟", "انتقال اطلاعات v3.4", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; try { var result = _migration.Import(dlg.FileName, true); MessageBox.Show($"انتقال کامل شد.\n{result}\n\nبرنامه اکنون دوباره اجرا می‌شود.", "انتقال اطلاعات", MessageBoxButton.OK, MessageBoxImage.Information); RestartApplication(); } catch (Exception ex) { MessageBox.Show(ex.Message, "انتقال اطلاعات", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void CheckDatabase() { try { var check = Database.QuickCheck(); Status = string.Equals(check, "ok", StringComparison.OrdinalIgnoreCase) ? "سلامت دیتابیس: OK" : "نتیجه بررسی: " + check; } catch (Exception ex) { Status = "خطا در بررسی دیتابیس"; MessageBox.Show(ex.Message, "دیتابیس", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    private void OpenFolder() { Directory.CreateDirectory(Database.BackupsDir); Process.Start(new ProcessStartInfo { FileName = Database.BackupsDir, UseShellExecute = true }); }
    private static void RestartApplication() { var exe = Environment.ProcessPath; if (!string.IsNullOrWhiteSpace(exe)) Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true }); Application.Current.Shutdown(); }
}
