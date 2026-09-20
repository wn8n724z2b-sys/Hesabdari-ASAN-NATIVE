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
            ReceiptFooter = Get("receipt_footer", "سپاس از خرید شما"),
            PrinterName = Get("printer_name", ""),
            PrintAfterSale = Get("print_after_sale", "0") == "1",
            BarcodeScannerEnabled = Get("barcode_scanner_enabled", "1") != "0",
            ScannerSuffix = Get("scanner_suffix", "Enter"),
            OnboardingComplete = Get("onboarding_complete", "") == "1",
            OnlineEnabled = Get("online_enabled", "0") == "1",
            OnlineServerUrl = Get("online_server_url", ""),
            OnlineStoreCode = Get("online_store_code", ""),
            OnlineSyncSales = Get("online_sync_sales", "1") != "0",
            OnlineSyncInventory = Get("online_sync_inventory", "1") != "0",
            OnlineLastCheckAt = Get("online_last_check_at", ""),
            OnlineLastSyncAt = Get("online_last_sync_at", ""),
            OnlineLastStatus = Get("online_last_status", "local")
        };
        if (double.TryParse(Get("ui_scale", "1"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var scale))
            settings.UiScale = Math.Clamp(scale, 1.0, 2.0);
        if (int.TryParse(Get("auto_backup_per_day", "4"), out var backup))
            settings.AutoBackupPerDay = Math.Clamp(backup, 1, 6);
        if (int.TryParse(Get("online_sync_minutes", "15"), out var syncMinutes))
            settings.OnlineSyncMinutes = syncMinutes is 5 or 15 or 30 or 60 ? syncMinutes : 15;
        // Existing installations created before onboarding was introduced should not be interrupted
        // when a real store identity was already configured.
        if (!settings.OnboardingComplete && string.IsNullOrWhiteSpace(Get("onboarding_complete", "")) &&
            !string.IsNullOrWhiteSpace(settings.ShopName) && settings.ShopName != "فروشگاه من")
            settings.OnboardingComplete = true;
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
        Upsert("receipt_footer", s.ReceiptFooter);
        Upsert("auto_backup_per_day", Math.Clamp(s.AutoBackupPerDay, 1, 6).ToString());
        Upsert("printer_name", s.PrinterName);
        Upsert("print_after_sale", s.PrintAfterSale ? "1" : "0");
        Upsert("barcode_scanner_enabled", s.BarcodeScannerEnabled ? "1" : "0");
        Upsert("scanner_suffix", s.ScannerSuffix is "Tab" ? "Tab" : "Enter");
        Upsert("onboarding_complete", s.OnboardingComplete ? "1" : "0");
        Upsert("online_enabled", s.OnlineEnabled ? "1" : "0");
        Upsert("online_server_url", s.OnlineServerUrl.Trim());
        Upsert("online_store_code", s.OnlineStoreCode.Trim());
        Upsert("online_sync_minutes", (s.OnlineSyncMinutes is 5 or 15 or 30 or 60 ? s.OnlineSyncMinutes : 15).ToString());
        Upsert("online_sync_sales", s.OnlineSyncSales ? "1" : "0");
        Upsert("online_sync_inventory", s.OnlineSyncInventory ? "1" : "0");
        Upsert("online_last_check_at", s.OnlineLastCheckAt);
        Upsert("online_last_sync_at", s.OnlineLastSyncAt);
        Upsert("online_last_status", s.OnlineLastStatus);
        AuditService.Write(db, tx, "SETTINGS_SAVE", "SETTINGS", "main", JsonSerializer.Serialize(new { s.Theme, s.UiScale, s.ManagerName, s.ShopName, s.AutoBackupPerDay, s.PrinterName, s.PrintAfterSale, s.BarcodeScannerEnabled, s.ScannerSuffix }));
        tx.Commit();
    }
}
