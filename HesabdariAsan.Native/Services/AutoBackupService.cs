namespace HesabdariAsan.Native.Services;

public sealed class AutoBackupService
{
    private readonly SettingsService _settings=new();
    private readonly BackupService _backup=new();

    public void Check()
    {
        try
        {
            var count=int.TryParse(_settings.Get("auto_backup_per_day","4"),out var n)?Math.Clamp(n,1,6):4;
            var now=DateTime.Now;
            var slotHours=24d/count;
            var slot=Math.Min(count-1,(int)Math.Floor(now.TimeOfDay.TotalHours/slotHours));
            var token=$"{now:yyyyMMdd}-{slot}";
            if(_settings.Get("last_auto_backup_slot","")==token)return;
            _backup.CreateBackup();
            _backup.KeepLatest(30);
            _settings.Set("last_auto_backup_slot",token);
        }
        catch
        {
            // Auto backup must never stop the accounting UI. Manual backup surfaces errors to the user.
        }
    }
}
