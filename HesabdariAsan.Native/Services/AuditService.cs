using Microsoft.Data.Sqlite;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class AuditService
{
    public static void Write(SqliteConnection db, SqliteTransaction? tx, string action, string? entityType = null, string? entityId = null, string? payloadJson = null, long? invoiceNo = null, string actor = "Admin")
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO audit_log(created_at,action,entity_type,entity_id,payload_json,actor,invoice_no)
VALUES($at,$a,$et,$eid,$p,$actor,$ino)";
        cmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("O"));
        cmd.Parameters.AddWithValue("$a", action);
        cmd.Parameters.AddWithValue("$et", string.IsNullOrWhiteSpace(entityType) ? DBNull.Value : entityType);
        cmd.Parameters.AddWithValue("$eid", string.IsNullOrWhiteSpace(entityId) ? DBNull.Value : entityId);
        cmd.Parameters.AddWithValue("$p", string.IsNullOrWhiteSpace(payloadJson) ? DBNull.Value : payloadJson);
        cmd.Parameters.AddWithValue("$actor", string.IsNullOrWhiteSpace(actor) ? "Admin" : actor);
        cmd.Parameters.AddWithValue("$ino", invoiceNo.HasValue ? invoiceNo.Value : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public PagedResult<AuditEntry> Search(string? query, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = pageSize is 10 or 20 or 50 ? pageSize : 10;
        query = (query ?? "").Trim();
        var where = query.Length == 0 ? "" : "WHERE action LIKE $like OR entity_type LIKE $like OR entity_id LIKE $like OR actor LIKE $like OR CAST(invoice_no AS TEXT) LIKE $like";
        using var db = Database.Open();
        int total;
        using (var count = db.CreateCommand())
        {
            count.CommandText = $"SELECT COUNT(*) FROM audit_log {where}";
            if (query.Length > 0) count.Parameters.AddWithValue("$like", $"%{query}%");
            total = Convert.ToInt32(count.ExecuteScalar() ?? 0);
        }
        var items = new List<AuditEntry>();
        using var cmd = db.CreateCommand();
        cmd.CommandText = $@"SELECT id,created_at,action,COALESCE(entity_type,''),COALESCE(entity_id,''),COALESCE(payload_json,''),actor,invoice_no
FROM audit_log {where} ORDER BY created_at DESC,id DESC LIMIT $l OFFSET $o";
        if (query.Length > 0) cmd.Parameters.AddWithValue("$like", $"%{query}%");
        cmd.Parameters.AddWithValue("$l", pageSize);
        cmd.Parameters.AddWithValue("$o", (page - 1) * pageSize);
        using var r = cmd.ExecuteReader();
        var i = 0;
        while (r.Read())
        {
            items.Add(new AuditEntry
            {
                Id = r.GetInt64(0), RowNumber = (page - 1) * pageSize + ++i,
                CreatedAt = DateTime.Parse(r.GetString(1)), Action = r.GetString(2), EntityType = r.GetString(3), EntityId = r.GetString(4),
                PayloadJson = r.GetString(5), Actor = r.GetString(6), InvoiceNo = r.IsDBNull(7) ? null : r.GetInt64(7)
            });
        }
        return new PagedResult<AuditEntry> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }
}
