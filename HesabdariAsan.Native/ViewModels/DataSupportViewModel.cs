using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Dialogs;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class DataSupportViewModel : ViewModelBase
{
    private readonly SettingsService _settings = new();
    private readonly BackupService _backup = new();
    private readonly V34MigrationService _migration = new();
    private readonly AdminSecurityService _security = new();
    private int _autoBackupPerDay = 4;
    private string _status = "", _lastBackup = "—";
    private bool _isUnlocked;

    public IReadOnlyList<int> BackupOptions { get; } = new[] { 1, 2, 3, 4, 5, 6 };
    public int AutoBackupPerDay { get => _autoBackupPerDay; set { if (Set(ref _autoBackupPerDay, Math.Clamp(value, 1, 6))) _settings.Set("auto_backup_per_day", _autoBackupPerDay.ToString()); } }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string LastBackup { get => _lastBackup; private set => Set(ref _lastBackup, value); }
    public bool IsUnlocked { get => _isUnlocked; private set { if (Set(ref _isUnlocked, value)) OnPropertyChanged(nameof(IsLocked)); } }
    public bool IsLocked => !IsUnlocked;
    public bool HasAdminPassword => _security.HasPassword;
    public string LockHint => HasAdminPassword ? "برای دسترسی رمز مدیر را وارد کنید." : "رمز مدیر هنوز تعیین نشده است؛ بخش فعلاً باز است.";

    public RelayCommand BackupCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand RestoreLatestCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand CheckDatabaseCommand { get; }
    public RelayCommand MigrateV34Command { get; }
    public RelayCommand LockCommand { get; }
    public RelayCommand ChangePasswordCommand { get; }
    public RelayCommand ResetCommand { get; }

    public DataSupportViewModel()
    {
        _isUnlocked = !_security.HasPassword;
        BackupCommand = new RelayCommand(_ => Backup());
        RestoreCommand = new RelayCommand(_ => Restore());
        RestoreLatestCommand = new RelayCommand(_ => RestoreLatest());
        OpenFolderCommand = new RelayCommand(_ => OpenFolder());
        CheckDatabaseCommand = new RelayCommand(_ => Check());
        MigrateV34Command = new RelayCommand(_ => Migrate());
        LockCommand = new RelayCommand(_ => IsUnlocked = false, _ => _security.HasPassword);
        ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
        ResetCommand = new RelayCommand(_ => ResetApplication(), _ => IsUnlocked);
        Reload();
    }

    public void Reload()
    {
        if (int.TryParse(_settings.Get("auto_backup_per_day", "4"), out var n)) _autoBackupPerDay = Math.Clamp(n, 1, 6);
        OnPropertyChanged(nameof(AutoBackupPerDay));
        OnPropertyChanged(nameof(HasAdminPassword)); OnPropertyChanged(nameof(LockHint));
        var latest = _backup.LatestBackup();
        LastBackup = latest is null ? "هنوز پشتیبان ساخته نشده" : $"آخرین پشتیبان: {Path.GetFileName(latest)}";
    }

    public bool Unlock(string password)
    {
        if (!_security.HasPassword) { IsUnlocked = true; return true; }
        var ok = _security.Verify(password); IsUnlocked = ok;
        if (!ok) Status = "رمز مدیر نادرست است.";
        return ok;
    }

    private void ChangePassword()
    {
        var w = new AdminPasswordWindow { Owner = Application.Current.MainWindow };
        if (w.ShowDialog() == true) { IsUnlocked = true; Status = "رمز مدیر تغییر کرد."; Reload(); }
    }

    private void Backup()
    {
        try
        {
            var internalPath = _backup.CreateBackup("manual");
            _backup.KeepLatest();
            var save = new SaveFileDialog
            {
                Title = "ذخیره فایل پشتیبان حسابداری آسان",
                Filter = "پشتیبان حسابداری آسان|*.sqlite3",
                DefaultExt = "sqlite3",
                AddExtension = true,
                FileName = $"hesabdari-asan-backup-{DateTime.Now:yyyyMMdd-HHmm}.sqlite3"
            };
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads)) save.InitialDirectory = downloads;
            if (save.ShowDialog() == true)
            {
                File.Copy(internalPath, save.FileName, true);
                Status = $"فایل پشتیبان ذخیره شد: {save.FileName}";
                AppDialog.Show("فایل پشتیبان با موفقیت ذخیره شد.\n\n" + save.FileName, "پشتیبان‌گیری", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else Status = $"پشتیبان داخلی ساخته شد: {Path.GetFileName(internalPath)}";
            Reload();
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "پشتیبان‌گیری", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void Restore() { var d = new OpenFileDialog { Filter = "SQLite Backup|*.sqlite3;*.db|همه فایل‌ها|*.*", InitialDirectory = Directory.Exists(Database.BackupsDir) ? Database.BackupsDir : null }; if (d.ShowDialog() != true) return; RestorePath(d.FileName); }
    private void RestoreLatest() { var p = _backup.LatestBackup(); if (p is null) { AppDialog.Show("پشتیبان خودکاری پیدا نشد.", "بازیابی"); return; } RestorePath(p); }
    private void RestorePath(string p) { if (AppDialog.Show("قبل از بازیابی، از دیتابیس فعلی پشتیبان ایمنی ساخته می‌شود. ادامه می‌دهید؟", "بازیابی اطلاعات", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; try { _backup.RestoreBackup(p); Restart(); } catch (Exception ex) { AppDialog.Show(ex.Message, "بازیابی", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void OpenFolder() { Directory.CreateDirectory(Database.BackupsDir); Process.Start(new ProcessStartInfo { FileName = Database.BackupsDir, UseShellExecute = true }); }
    private void Check() { try { var q = Database.QuickCheck(); Status = string.Equals(q, "ok", StringComparison.OrdinalIgnoreCase) ? "سلامت دیتابیس: OK" : "نتیجه بررسی: " + q; } catch (Exception ex) { AppDialog.Show(ex.Message, "دیتابیس", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    private void Migrate() { var detected = _migration.DetectDatabase(); var d = new OpenFileDialog { Filter = "دیتابیس حسابداری آسان v3|hesabdari_asan.sqlite3;*.sqlite3|همه فایل‌ها|*.*" }; if (!string.IsNullOrWhiteSpace(detected)) { d.InitialDirectory = Path.GetDirectoryName(detected); d.FileName = Path.GetFileName(detected); } if (d.ShowDialog() != true) return; if (AppDialog.Show("اطلاعات فعلی ابتدا Backup می‌شود و سپس داده‌های v3.4 وارد می‌شوند. ادامه می‌دهید؟", "انتقال v3.4", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; try { var result = _migration.Import(d.FileName, true); AppDialog.Show($"انتقال کامل شد.\n{result}", "انتقال اطلاعات"); Restart(); } catch (Exception ex) { AppDialog.Show(ex.Message, "انتقال اطلاعات", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private void ResetApplication()
    {
        var first = AppDialog.Show(
            "تمام کالاها، فاکتورها، مشتریان، شرکت‌ها، هزینه‌ها، خریدها و تنظیمات پاک شوند؟\n\nقبل از پاک‌سازی یک Backup ایمنی کامل ساخته می‌شود.",
            "پاک‌کردن همه اطلاعات", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (first != MessageBoxResult.Yes) return;
        var second = AppDialog.Show(
            "این عملیات قابل برگشت مستقیم نیست. فقط از Backup ایمنی می‌توانید اطلاعات را برگردانید.\n\nبرای تأیید نهایی «بله» را انتخاب کنید.",
            "تأیید نهایی بازنشانی", MessageBoxButton.YesNo, MessageBoxImage.Stop, MessageBoxResult.No,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        if (second != MessageBoxResult.Yes) return;
        try
        {
            var safety = _backup.ResetToFreshDatabase();
            AppDialog.Show($"اطلاعات برنامه بازنشانی شد.\n\nBackup ایمنی:\n{Path.GetFileName(safety)}\n\nبرنامه اکنون دوباره راه‌اندازی می‌شود.", "بازنشانی کامل", MessageBoxButton.OK, MessageBoxImage.Information);
            Restart();
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "بازنشانی اطلاعات", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private static void Restart() { var exe = Environment.ProcessPath; if (!string.IsNullOrWhiteSpace(exe)) Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true }); Application.Current.Shutdown(); }
}
