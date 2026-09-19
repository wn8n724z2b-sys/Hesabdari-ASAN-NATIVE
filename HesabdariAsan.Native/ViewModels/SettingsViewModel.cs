using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _service=new();
    private readonly BackupService _backup=new();
    private readonly ReceiptPrinterService _printer=new();
    private readonly V34MigrationService _migration=new();
    private AppSettings _settings;
    private string _status="";

    public IReadOnlyList<int> BackupOptions{get;}=new[]{1,2,3,4,5,6};
    public IReadOnlyList<string> ThemeOptions{get;}=new[]{"روشن","دارک"};
    public string ManagerName{get=>_settings.ManagerName;set{_settings.ManagerName=value;OnPropertyChanged();}}
    public string ManagerImagePath{get=>_settings.ManagerImagePath;set{_settings.ManagerImagePath=value;OnPropertyChanged();}}
    public string ShopName{get=>_settings.ShopName;set{_settings.ShopName=value;OnPropertyChanged();}}
    public string ShopPhone{get=>_settings.ShopPhone;set{_settings.ShopPhone=value;OnPropertyChanged();}}
    public string ShopAddress{get=>_settings.ShopAddress;set{_settings.ShopAddress=value;OnPropertyChanged();}}
    public string ShopLogoPath{get=>_settings.ShopLogoPath;set{_settings.ShopLogoPath=value;OnPropertyChanged();}}
    public double UiScale{get=>_settings.UiScale;set{_settings.UiScale=Math.Clamp(value,1,2);OnPropertyChanged();OnPropertyChanged(nameof(UiScaleText));FontScaleService.Apply(_settings.UiScale);}}
    public string UiScaleText=>$"{UiScale*100:0}%";
    public int AutoBackupPerDay{get=>_settings.AutoBackupPerDay;set{_settings.AutoBackupPerDay=Math.Clamp(value,1,6);OnPropertyChanged();}}
    public string SelectedTheme{get=>_settings.Theme=="Dark"?"دارک":"روشن";set{_settings.Theme=value=="دارک"?"Dark":"Light";OnPropertyChanged();ThemeService.Apply(_settings.Theme);}}
    public string PrinterName{get=>_settings.PrinterName;set{_settings.PrinterName=value;OnPropertyChanged();OnPropertyChanged(nameof(PrinterText));}}
    public string PrinterText=>string.IsNullOrWhiteSpace(PrinterName)?"پرینتر انتخاب نشده":PrinterName;
    public bool PrintAfterSale{get=>_settings.PrintAfterSale;set{_settings.PrintAfterSale=value;OnPropertyChanged();}}
    public bool BarcodeScannerEnabled{get=>_settings.BarcodeScannerEnabled;set{_settings.BarcodeScannerEnabled=value;OnPropertyChanged();}}
    public string Status{get=>_status;private set=>Set(ref _status,value);}

    public RelayCommand SaveCommand{get;}
    public RelayCommand BrowseManagerImageCommand{get;}
    public RelayCommand BrowseLogoCommand{get;}
    public RelayCommand BackupCommand{get;}
    public RelayCommand RestoreCommand{get;}
    public RelayCommand OpenBackupFolderCommand{get;}
    public RelayCommand SelectPrinterCommand{get;}
    public RelayCommand MigrateV34Command{get;}
    public RelayCommand CheckDatabaseCommand{get;}

    public SettingsViewModel()
    {
        _settings=_service.Load();
        SaveCommand=new RelayCommand(_=>Save());
        BrowseManagerImageCommand=new RelayCommand(_=>Browse(true));
        BrowseLogoCommand=new RelayCommand(_=>Browse(false));
        BackupCommand=new RelayCommand(_=>Backup());
        RestoreCommand=new RelayCommand(_=>Restore());
        OpenBackupFolderCommand=new RelayCommand(_=>OpenFolder());
        SelectPrinterCommand=new RelayCommand(_=>SelectPrinter());
        MigrateV34Command=new RelayCommand(_=>MigrateV34());
        CheckDatabaseCommand=new RelayCommand(_=>CheckDatabase());
    }

    public void Reload(){_settings=_service.Load();OnPropertyChanged(string.Empty);OnPropertyChanged(nameof(PrinterText));}

    private void Save()
    {
        if(string.IsNullOrWhiteSpace(ManagerName))ManagerName="مدیر سیستم";
        if(string.IsNullOrWhiteSpace(ShopName))ShopName="فروشگاه من";
        _service.Save(_settings);Status="تنظیمات ذخیره شد.";
    }

    private void Browse(bool manager)
    {
        var dlg=new OpenFileDialog{Filter="تصویر|*.png;*.jpg;*.jpeg;*.bmp;*.webp|همه فایل‌ها|*.*"};
        if(dlg.ShowDialog()==true){if(manager)ManagerImagePath=dlg.FileName;else ShopLogoPath=dlg.FileName;}
    }

    private void Backup()
    {
        try{var path=_backup.CreateBackup("manual");_backup.KeepLatest();Status=$"Backup سالم ساخته شد: {Path.GetFileName(path)}";}
        catch(Exception ex){MessageBox.Show(ex.Message,"Backup",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }

    private void Restore()
    {
        var dlg=new OpenFileDialog{Filter="SQLite Backup|*.sqlite3;*.db|همه فایل‌ها|*.*",InitialDirectory=Directory.Exists(Database.BackupsDir)?Database.BackupsDir:null};
        if(dlg.ShowDialog()!=true)return;
        if(MessageBox.Show("دیتابیس فعلی قبل از بازیابی Backup می‌شود. سپس فایل انتخاب‌شده جایگزین شود؟","بازیابی اطلاعات",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        try{_backup.RestoreBackup(dlg.FileName);RestartApplication();}
        catch(Exception ex){MessageBox.Show(ex.Message,"بازیابی",MessageBoxButton.OK,MessageBoxImage.Error);}
    }

    private void SelectPrinter()
    {
        try{var selected=_printer.ChoosePrinter();if(string.IsNullOrWhiteSpace(selected))return;PrinterName=selected;_service.Set("printer_name",selected);Status="پرینتر انتخاب شد.";}
        catch(Exception ex){MessageBox.Show(ex.Message,"پرینتر",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }

    private void MigrateV34()
    {
        var detected=_migration.DetectDatabase();
        var dlg=new OpenFileDialog{Filter="دیتابیس حسابداری آسان v3|hesabdari_asan.sqlite3;*.sqlite3|همه فایل‌ها|*.*"};
        if(!string.IsNullOrWhiteSpace(detected)){dlg.InitialDirectory=Path.GetDirectoryName(detected);dlg.FileName=Path.GetFileName(detected);}
        if(dlg.ShowDialog()!=true)return;
        if(MessageBox.Show("انتقال v3.4 یک Backup ایمنی می‌سازد و اطلاعات حسابداری فعلی v4 را با دیتای نسخه قدیمی جایگزین می‌کند. ادامه می‌دهید؟","انتقال اطلاعات v3.4",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        try
        {
            var result=_migration.Import(dlg.FileName,true);
            MessageBox.Show($"انتقال کامل شد.\n{result}\n\nبرنامه اکنون دوباره اجرا می‌شود.","انتقال اطلاعات",MessageBoxButton.OK,MessageBoxImage.Information);
            RestartApplication();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"انتقال اطلاعات",MessageBoxButton.OK,MessageBoxImage.Error);}
    }

    private void CheckDatabase()
    {
        try{var check=Database.QuickCheck();Status=string.Equals(check,"ok",StringComparison.OrdinalIgnoreCase)?"سلامت دیتابیس: OK":"نتیجه بررسی: "+check;}
        catch(Exception ex){Status="خطا در بررسی دیتابیس";MessageBox.Show(ex.Message,"دیتابیس",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }

    private void OpenFolder(){Directory.CreateDirectory(Database.BackupsDir);Process.Start(new ProcessStartInfo{FileName=Database.BackupsDir,UseShellExecute=true});}

    private static void RestartApplication()
    {
        var exe=Environment.ProcessPath;
        if(!string.IsNullOrWhiteSpace(exe))Process.Start(new ProcessStartInfo{FileName=exe,UseShellExecute=true});
        Application.Current.Shutdown();
    }
}
