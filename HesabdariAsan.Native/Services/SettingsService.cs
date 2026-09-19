using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class SettingsService
{
    public string Get(string key, string fallback = "")
    {
        using var db = Database.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key=$k LIMIT 1";
        cmd.Parameters.AddWithValue("$k", key);
        return Convert.ToString(cmd.ExecuteScalar()) ?? fallback;
    }

    public void Set(string key, string value)
    {
        using var db = Database.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"INSERT INTO settings(key,value) VALUES($k,$v)
ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value ?? "");
        cmd.ExecuteNonQuery();
    }

    public AppSettings Load()
    {
        var settings = new AppSettings
        {
            Theme = Get("theme", "Light"),
            ManagerName = Get("manager_name", "مدیر سیستم"),
            ManagerImagePath = Get("manager_image", ""),
            ShopName = Get("shop_name", "فروشگاه من"),
            ShopPhone = Get("shop_phone", ""),
            ShopAddress = Get("shop_address", ""),
            ShopLogoPath = Get("shop_logo", ""),
            PrinterName = Get("printer_name", ""),
            PrintAfterSale = Get("print_after_sale", "0") == "1",
            BarcodeScannerEnabled = Get("barcode_scanner_enabled", "1") != "0"
        };
        if (double.TryParse(Get("ui_scale", "1"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var scale))
            settings.UiScale = Math.Clamp(scale, 1.0, 2.0);
        if (int.TryParse(Get("auto_backup_per_day", "4"), out var backup))
            settings.AutoBackupPerDay = Math.Clamp(backup, 1, 6);
        return settings;
    }

    public void Save(AppSettings s)
    {
        using var db = Database.Open();
        using var tx = db.BeginTransaction();
        void Upsert(string key, string value)
        {
            using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
            cmd.Parameters.AddWithValue("$k", key); cmd.Parameters.AddWithValue("$v", value ?? ""); cmd.ExecuteNonQuery();
        }
        Upsert("theme", s.Theme);
        Upsert("ui_scale", s.UiScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Upsert("manager_name", s.ManagerName);
        Upsert("manager_image", s.ManagerImagePath);
        Upsert("shop_name", s.ShopName);
        Upsert("shop_phone", s.ShopPhone);
        Upsert("shop_address", s.ShopAddress);
        Upsert("shop_logo", s.ShopLogoPath);
        Upsert("auto_backup_per_day", Math.Clamp(s.AutoBackupPerDay, 1, 6).ToString());
        Upsert("printer_name", s.PrinterName);
        Upsert("print_after_sale", s.PrintAfterSale ? "1" : "0");
        Upsert("barcode_scanner_enabled", s.BarcodeScannerEnabled ? "1" : "0");
        AuditService.Write(db, tx, "SETTINGS_SAVE", "SETTINGS", "main", JsonSerializer.Serialize(new { s.Theme, s.UiScale, s.ManagerName, s.ShopName, s.AutoBackupPerDay, s.PrinterName, s.PrintAfterSale, s.BarcodeScannerEnabled }));
        tx.Commit();
    }
}
