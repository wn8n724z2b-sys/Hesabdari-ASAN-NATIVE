using HesabdariAsan.Native.Data;

namespace HesabdariAsan.Native.Services;

public static class AppLog
{
    private static readonly object Gate = new();
    public static string LogDir => Path.Combine(Database.AppDataDir, "Logs");

    public static void Error(Exception ex, string context)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var path = Path.Combine(LogDir, $"error-{DateTime.Now:yyyyMM}.log");
            var text = $"[{DateTime.Now:O}] {context}\n{ex}\n\n";
            lock (Gate) File.AppendAllText(path, text);
        }
        catch { }
    }
}
