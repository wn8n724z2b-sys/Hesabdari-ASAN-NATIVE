using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class PurchaseService
{
    public long Record(long productId, long? supplierId, double quantity, long unitCost, string paymentType, string? note)
    {
        if (quantity <= 0) throw new InvalidOperationException("تعداد خرید باید بیشتر از صفر باشد.");
        if (unitCost < 0) throw new InvalidOperationException("قیمت خرید معتبر نیست.");
        if (paymentType is not ("CASH" or "CREDIT")) throw new InvalidOperationException("روش پرداخت را انتخاب کنید.");
        if (paymentType == "CREDIT" && !supplierId.HasValue) throw new InvalidOperationException("برای خرید نسیه، شرکت / تأمین‌کننده را انتخاب کنید.");
        var total = (long)Math.Round(quantity * unitCost, MidpointRounding.AwayFromZero);
        using var db = Database.Open();
        using var tx = db.BeginTransaction();

        string productName;
        double oldStock;
        using (var p = db.CreateCommand())
        {
            p.Transaction = tx;
            p.CommandText = "SELECT name,stock FROM products WHERE id=$id AND is_active=1";
            p.Parameters.AddWithValue("$id", productId);
            using var r = p.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("کالای انتخاب‌شده پیدا نشد.");
            productName = r.GetString(0); oldStock = r.GetDouble(1);
        }

        if (supplierId.HasValue)
        {
            using var s = db.CreateCommand();
            s.Transaction = tx;
            s.CommandText = "SELECT COUNT(*) FROM parties WHERE id=$id AND type='COMPANY'";
            s.Parameters.AddWithValue("$id", supplierId.Value);
            if (Convert.ToInt32(s.ExecuteScalar() ?? 0) != 1) throw new InvalidOperationException("شرکت / تأمین‌کننده معتبر نیست.");
        }

        long id;
        using (var ins = db.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = @"INSERT INTO purchases(product_id,product_name,quantity,units,unit_cost,total,payment_type,supplier_id,note,created_at)
VALUES($pid,$pn,$q,$q,$cost,$total,$pt,$sid,$note,$at); SELECT last_insert_rowid();";
            ins.Parameters.AddWithValue("$pid", productId); ins.Parameters.AddWithValue("$pn", productName);
            ins.Parameters.AddWithValue("$q", quantity); ins.Parameters.AddWithValue("$cost", unitCost); ins.Parameters.AddWithValue("$total", total);
            ins.Parameters.AddWithValue("$pt", paymentType); ins.Parameters.AddWithValue("$sid", supplierId.HasValue ? supplierId.Value : DBNull.Value);
            ins.Parameters.AddWithValue("$note", note?.Trim() ?? ""); ins.Parameters.AddWithValue("$at", DateTime.Now.ToString("O"));
            id = Convert.ToInt64(ins.ExecuteScalar() ?? 0L);
        }

        var balance = oldStock + quantity;
        using (var u = db.CreateCommand())
        {
            u.Transaction = tx;
            u.CommandText = "UPDATE products SET stock=$stock,purchase_price=$cost WHERE id=$id";
            u.Parameters.AddWithValue("$stock", balance); u.Parameters.AddWithValue("$cost", unitCost); u.Parameters.AddWithValue("$id", productId); u.ExecuteNonQuery();
        }
        using (var ledger = db.CreateCommand())
        {
            ledger.Transaction = tx;
            ledger.CommandText = @"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at)
VALUES($pid,'PURCHASE',$q,$bal,'PURCHASE',$rid,$note,$at)";
            ledger.Parameters.AddWithValue("$pid", productId); ledger.Parameters.AddWithValue("$q", quantity); ledger.Parameters.AddWithValue("$bal", balance);
            ledger.Parameters.AddWithValue("$rid", id); ledger.Parameters.AddWithValue("$note", string.IsNullOrWhiteSpace(note) ? "خرید / ورود کالا" : note.Trim()); ledger.Parameters.AddWithValue("$at", DateTime.Now.ToString("O"));
            ledger.ExecuteNonQuery();
        }
        if (paymentType == "CREDIT" && supplierId.HasValue)
        {
            using var debt = db.CreateCommand(); debt.Transaction = tx;
            debt.CommandText = "UPDATE parties SET debt=debt+$amount WHERE id=$id AND type='COMPANY'";
            debt.Parameters.AddWithValue("$amount", total); debt.Parameters.AddWithValue("$id", supplierId.Value); debt.ExecuteNonQuery();
        }
        AuditService.Write(db, tx, "PURCHASE_CREATE", "PURCHASE", id.ToString(), JsonSerializer.Serialize(new { productId, productName, supplierId, quantity, unitCost, total, paymentType, note }));
        tx.Commit();
        return id;
    }

    public PagedResult<PurchaseRecord> Search(string? query, int page, int pageSize)
    {
        page = Math.Max(1, page); pageSize = pageSize is 10 or 20 or 50 ? pageSize : 10; query = (query ?? "").Trim();
        var where = query.Length == 0 ? "" : "WHERE p.product_name LIKE $like OR COALESCE(s.name,'') LIKE $like OR COALESCE(p.note,'') LIKE $like";
        using var db = Database.Open();
        int total;
        using (var count = db.CreateCommand()) { count.CommandText = $"SELECT COUNT(*) FROM purchases p LEFT JOIN parties s ON s.id=p.supplier_id {where}"; if (query.Length > 0) count.Parameters.AddWithValue("$like", $"%{query}%"); total = Convert.ToInt32(count.ExecuteScalar() ?? 0); }
        var list = new List<PurchaseRecord>();
        using var cmd = db.CreateCommand();
        cmd.CommandText = $@"SELECT p.id,p.product_id,p.product_name,p.quantity,p.unit_cost,p.total,p.payment_type,p.supplier_id,COALESCE(s.name,'—'),COALESCE(p.note,''),p.created_at
FROM purchases p LEFT JOIN parties s ON s.id=p.supplier_id {where} ORDER BY p.created_at DESC,p.id DESC LIMIT $l OFFSET $o";
        if (query.Length > 0) cmd.Parameters.AddWithValue("$like", $"%{query}%"); cmd.Parameters.AddWithValue("$l", pageSize); cmd.Parameters.AddWithValue("$o", (page - 1) * pageSize);
        using var r = cmd.ExecuteReader(); var i = 0;
        while (r.Read()) list.Add(new PurchaseRecord { Id = r.GetInt64(0), RowNumber = (page - 1) * pageSize + ++i, ProductId = r.IsDBNull(1) ? null : r.GetInt64(1), ProductName = r.GetString(2), Quantity = r.GetDouble(3), UnitCost = r.GetInt64(4), Total = r.GetInt64(5), PaymentType = r.GetString(6), SupplierId = r.IsDBNull(7) ? null : r.GetInt64(7), SupplierName = r.GetString(8), Note = r.GetString(9), CreatedAt = DateTime.Parse(r.GetString(10)) });
        return new PagedResult<PurchaseRecord> { Items = list, TotalCount = total, Page = page, PageSize = pageSize };
    }
}
