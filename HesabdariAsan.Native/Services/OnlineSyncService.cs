using System.Net.Http;
using System.Text;
using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class OnlineSyncService
{
    private static readonly HttpClient Http = new();

    public async Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var baseUrl = Normalize(settings.OnlineServerUrl);
        if (!settings.OnlineEnabled) throw new InvalidOperationException("ابتدا اتصال آنلاین را فعال کنید.");
        if (string.IsNullOrWhiteSpace(baseUrl)) throw new InvalidOperationException("آدرس سرور را وارد کنید.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(7));
        using var response = await Http.GetAsync(baseUrl + "/health", timeout.Token);
        response.EnsureSuccessStatusCode();
    }

    public async Task SyncAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var baseUrl = Normalize(settings.OnlineServerUrl);
        if (!settings.OnlineEnabled) throw new InvalidOperationException("ابتدا اتصال آنلاین را فعال کنید.");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(settings.OnlineStoreCode))
            throw new InvalidOperationException("آدرس سرور و کد فروشگاه را کامل کنید.");

        var payload = new Dictionary<string, object?>
        {
            ["app"] = "hesabdari-asan",
            ["version"] = "4.0-native",
            ["storeCode"] = settings.OnlineStoreCode.Trim(),
            ["syncedAt"] = DateTime.Now.ToString("O"),
            ["sales"] = settings.OnlineSyncSales ? ReadRows("SELECT * FROM invoices ORDER BY id") : Array.Empty<object>(),
            ["customers"] = settings.OnlineSyncSales ? ReadRows("SELECT * FROM parties WHERE type='CUSTOMER' ORDER BY id") : Array.Empty<object>(),
            ["products"] = settings.OnlineSyncInventory ? ReadRows("SELECT * FROM products ORDER BY id") : Array.Empty<object>(),
            ["purchases"] = settings.OnlineSyncInventory ? ReadRows("SELECT * FROM purchases ORDER BY id") : Array.Empty<object>(),
            ["inventory"] = settings.OnlineSyncInventory ? ReadRows("SELECT * FROM inventory_ledger ORDER BY id") : Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var response = await Http.PostAsync(baseUrl + "/api/v1/sync", content, timeout.Token);
        response.EnsureSuccessStatusCode();
    }

    private static IReadOnlyList<Dictionary<string, object?>> ReadRows(string sql)
    {
        using var db = Database.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader(); var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    private static string Normalize(string? value) => (value ?? "").Trim().TrimEnd('/');
}
