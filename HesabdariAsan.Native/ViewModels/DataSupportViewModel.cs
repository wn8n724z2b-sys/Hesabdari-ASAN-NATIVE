using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.ViewModels;

public sealed class DataSupportViewModel:ViewModelBase
{
    private readonly SettingsService _settings=new();
    private readonly BackupService _backup=new();
    private readonly V34MigrationService _migration=new();
    private int _autoBackupPerDay=4;private string _status="";private string _lastBackup="—";
    public IReadOnlyList<int> BackupOptions{get;}=new[]{1,2,3,4,5,6};
    public int AutoBackupPerDay{get=>_autoBackupPerDay;set{if(Set(ref _autoBackupPerDay,Math.Clamp(value,1,6)))_settings.Set("auto_backup_per_day",_autoBackupPerDay.ToString());}}
    public string Status{get=>_status;private set=>Set(ref _status,value);}public string LastBackup{get=>_lastBackup;private set=>Set(ref _lastBackup,value);}
    public RelayCommand BackupCommand{get;}public RelayCommand RestoreCommand{get;}public RelayCommand RestoreLatestCommand{get;}public RelayCommand OpenFolderCommand{get;}public RelayCommand CheckDatabaseCommand{get;}public RelayCommand MigrateV34Command{get;}
    public DataSupportViewModel(){BackupCommand=new RelayCommand(_=>Backup());RestoreCommand=new RelayCommand(_=>Restore());RestoreLatestCommand=new RelayCommand(_=>RestoreLatest());OpenFolderCommand=new RelayCommand(_=>OpenFolder());CheckDatabaseCommand=new RelayCommand(_=>Check());MigrateV34Command=new RelayCommand(_=>Migrate());Reload();}
    public void Reload(){if(int.TryParse(_settings.Get("auto_backup_per_day","4"),out var n))_autoBackupPerDay=Math.Clamp(n,1,6);OnPropertyChanged(nameof(AutoBackupPerDay));var latest=_backup.LatestBackup();LastBackup=latest is null?"هنوز پشتیبان ساخته نشده":$"آخرین پشتیبان: {Path.GetFileName(latest)}";}
    private void Backup(){try{var p=_backup.CreateBackup("manual");_backup.KeepLatest();Status=$"پشتیبان سالم ساخته شد: {Path.GetFileName(p)}";Reload();}catch(Exception ex){MessageBox.Show(ex.Message,"Backup",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    private void Restore(){var d=new OpenFileDialog{Filter="SQLite Backup|*.sqlite3;*.db|همه فایل‌ها|*.*",InitialDirectory=Directory.Exists(Database.BackupsDir)?Database.BackupsDir:null};if(d.ShowDialog()!=true)return;RestorePath(d.FileName);}
    private void RestoreLatest(){var p=_backup.LatestBackup();if(p is null){MessageBox.Show("پشتیبان خودکاری پیدا نشد.","بازیابی");return;}RestorePath(p);}
    private void RestorePath(string p){if(MessageBox.Show("قبل از بازیابی، از دیتابیس فعلی پشتیبان ایمنی ساخته می‌شود. ادامه می‌دهید؟","بازیابی اطلاعات",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;try{_backup.RestoreBackup(p);Restart();}catch(Exception ex){MessageBox.Show(ex.Message,"بازیابی",MessageBoxButton.OK,MessageBoxImage.Error);}}
    private void OpenFolder(){Directory.CreateDirectory(Database.BackupsDir);Process.Start(new ProcessStartInfo{FileName=Database.BackupsDir,UseShellExecute=true});}
    private void Check(){try{var q=Database.QuickCheck();Status=string.Equals(q,"ok",StringComparison.OrdinalIgnoreCase)?"سلامت دیتابیس: OK":"نتیجه بررسی: "+q;}catch(Exception ex){MessageBox.Show(ex.Message,"دیتابیس",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    private void Migrate(){var detected=_migration.DetectDatabase();var d=new OpenFileDialog{Filter="دیتابیس حسابداری آسان v3|hesabdari_asan.sqlite3;*.sqlite3|همه فایل‌ها|*.*"};if(!string.IsNullOrWhiteSpace(detected)){d.InitialDirectory=Path.GetDirectoryName(detected);d.FileName=Path.GetFileName(detected);}if(d.ShowDialog()!=true)return;if(MessageBox.Show("اطلاعات فعلی ابتدا Backup می‌شود و سپس داده‌های v3.4 وارد می‌شوند. ادامه می‌دهید؟","انتقال v3.4",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;try{var result=_migration.Import(d.FileName,true);MessageBox.Show($"انتقال کامل شد.\n{result}","انتقال اطلاعات");Restart();}catch(Exception ex){MessageBox.Show(ex.Message,"انتقال اطلاعات",MessageBoxButton.OK,MessageBoxImage.Error);}}
    private static void Restart(){var exe=Environment.ProcessPath;if(!string.IsNullOrWhiteSpace(exe))Process.Start(new ProcessStartInfo{FileName=exe,UseShellExecute=true});Application.Current.Shutdown();}
}
