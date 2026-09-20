using HesabdariAsan.Native.Data;

namespace HesabdariAsan.Native.Services;

public sealed class MediaStorageService
{
    public string ImportImage(string sourcePath, string bucket)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) throw new FileNotFoundException("فایل تصویر پیدا نشد.", sourcePath);
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (ext is not ".png" and not ".jpg" and not ".jpeg" and not ".bmp") throw new InvalidOperationException("فرمت تصویر باید PNG، JPG، JPEG یا BMP باشد.");
        var dir = Path.Combine(Database.AppDataDir, "Media", Sanitize(bucket));
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{ext}");
        File.Copy(sourcePath, target, true);
        return target;
    }

    public void TryDeleteOwned(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            var mediaRoot = Path.GetFullPath(Path.Combine(Database.AppDataDir, "Media")) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(path);
            if (full.StartsWith(mediaRoot, StringComparison.OrdinalIgnoreCase)) File.Delete(full);
        }
        catch { }
    }

    private static string Sanitize(string value)
    {
        var chars = (value ?? "media").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray();
        return chars.Length == 0 ? "media" : new string(chars);
    }
}
