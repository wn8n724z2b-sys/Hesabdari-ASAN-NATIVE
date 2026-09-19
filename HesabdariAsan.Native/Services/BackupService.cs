using HesabdariAsan.Native.Data;
using Microsoft.Data.Sqlite;

namespace HesabdariAsan.Native.Services;

public sealed class BackupService
{
    public string CreateBackup(string? label = null)
    {
        Directory.CreateDirectory(Database.BackupsDir);
        var safeLabel = string.IsNullOrWhiteSpace(label) ? "backup" : string.Concat(label.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safeLabel)) safeLabel = "backup";
        var path = Path.Combine(Database.BackupsDir, $"hesabdari-asan-{safeLabel}-{DateTime.Now:yyyyMMdd-HHmmss}.sqlite3");
        using var source = Database.Open();
        using var destination = new SqliteConnection($"Data Source={path}");
        destination.Open();
        source.BackupDatabase(destination);
        ValidateBackup(path);
        return path;
    }

    public string? LatestBackup()
    {
        if (!Directory.Exists(Database.BackupsDir)) return null;
        return Directory.GetFiles(Database.BackupsDir, "*.sqlite3")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public void KeepLatest(int count = 30)
    {
        Directory.CreateDirectory(Database.BackupsDir);
        var files = Directory.GetFiles(Database.BackupsDir, "*.sqlite3")
            .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
        foreach (var file in files.Skip(Math.Max(1, count)))
        {
            try { File.Delete(file); } catch { }
        }
    }

    public void ValidateBackup(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("فایل پشتیبان پیدا نشد.", path);
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly };
        using var db = new SqliteConnection(builder.ToString());
        db.Open();
        using var check = db.CreateCommand();
        check.CommandText = "PRAGMA quick_check;";
        var result = Convert.ToString(check.ExecuteScalar()) ?? "";
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"فایل پشتیبان سالم نیست: {result}");
        using var schema = db.CreateCommand();
        schema.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='invoices'";
        if (Convert.ToInt32(schema.ExecuteScalar() ?? 0) != 1)
            throw new InvalidDataException("این فایل، دیتابیس معتبر حسابداری آسان v4 نیست.");
    }

    public string RestoreBackup(string path)
    {
        ValidateBackup(path);
        var safety = CreateBackup("before-restore");

        using (var db = Database.Open())
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }

        TryDelete(Database.DbPath + "-wal");
        TryDelete(Database.DbPath + "-shm");
        File.Copy(path, Database.DbPath, true);
        Database.Initialize();
        var result = Database.QuickCheck();
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(Database.DbPath + "-wal");
            TryDelete(Database.DbPath + "-shm");
            File.Copy(safety, Database.DbPath, true);
            Database.Initialize();
            throw new InvalidDataException("بازیابی کامل نشد؛ دیتابیس قبلی به‌صورت خودکار برگردانده شد.");
        }
        using (var auditDb = Database.Open()) AuditService.Write(auditDb, null, "DATABASE_RESTORE", "DATABASE", "main", $"{{\"source\":\"{Path.GetFileName(path)}\"}}");
        return safety;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
