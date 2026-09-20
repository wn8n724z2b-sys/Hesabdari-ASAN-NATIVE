using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class PurchaseService
{
    public long Record(long productId, long? supplierId, double quantity, long enteredCost, string paymentType, string? note, bool usePurchaseUnit = false)
    {
        if (quantity <= 0) throw new InvalidOperationException("تعداد خرید باید بیشتر از صفر باشد.");
        if (enteredCost < 0) throw new InvalidOperationException("قیمت خرید معتبر نیست.");
        if (paymentType is not ("CASH" or "CREDIT")) throw new InvalidOperationException("روش پرداخت را انتخاب کنید.");
        if (paymentType == "CREDIT" && !supplierId.HasValue) throw new InvalidOperationException("برای خرید نسیه، شرکت / تأمین‌کننده را انتخاب کنید.");

        using var db = Database.Open();
        using var tx = db.BeginTransaction();

        string productName, baseUnit, purchaseUnit;
        double oldStock, unitsPerPurchase;
        long oldUnitCost;
        bool conversionEnabled;
        using (var p = db.CreateCommand())
        {
            p.Transaction = tx;
            p.CommandText = @"SELECT name,stock,purchase_price,COALESCE(NULLIF(base_unit,''),NULLIF(unit,''),'دانه'),
COALESCE(NULLIF(purchase_unit,''),COALESCE(NULLIF(base_unit,''),NULLIF(unit,''),'دانه')),unit_conversion_enabled,units_per_purchase
FROM products WHERE id=$id AND is_active=1";
            p.Parameters.AddWithValue("$id", productId);
            using var r = p.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("کالای انتخاب‌شده پیدا نشد.");
            productName = r.GetString(0); oldStock = r.GetDouble(1); oldUnitCost = r.GetInt64(2);
            baseUnit = r.GetString(3); purchaseUnit = r.GetString(4); conversionEnabled = r.GetInt64(5) == 1;
            unitsPerPurchase = Math.Max(1, r.GetDouble(6));
        }

        if (usePurchaseUnit && !conversionEnabled) usePurchaseUnit = false;
        if (supplierId.HasValue)
        {
            using var s = db.CreateCommand(); s.Transaction = tx;
            s.CommandText = "SELECT COUNT(*) FROM parties WHERE id=$id AND type='COMPANY'";
            s.Parameters.AddWithValue("$id", supplierId.Value);
            if (Convert.ToInt32(s.ExecuteScalar() ?? 0) != 1) throw new InvalidOperationException("شرکت / تأمین‌کننده معتبر نیست.");
        }

        var incomingUnits = usePurchaseUnit ? quantity * unitsPerPurchase : quantity;
        if (incomingUnits <= 0) throw new InvalidOperationException("تعداد واحد ورودی معتبر نیست.");
        var total = usePurchaseUnit
            ? (long)Math.Round(quantity * enteredCost, MidpointRounding.AwayFromZero)
            : (long)Math.Round(incomingUnits * enteredCost, MidpointRounding.AwayFromZero);
        var unitCost = incomingUnits <= 0 ? 0 : (long)Math.Round(total / incomingUnits, MidpointRounding.AwayFromZero);
        var combinedUnits = Math.Max(0, oldStock) + incomingUnits;
        var weightedAverage = combinedUnits <= 0
            ? unitCost
            : (long)Math.Round(((Math.Max(0, oldStock) * oldUnitCost) + (incomingUnits * unitCost)) / combinedUnits, MidpointRounding.AwayFromZero);
        var balance = oldStock + incomingUnits;
        var now = DateTime.Now.ToString("O");
        var mode = usePurchaseUnit ? "PURCHASE_UNIT" : "BASE";
        var packCost = usePurchaseUnit ? enteredCost : 0;

        long id;
        using (var ins = db.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = @"INSERT INTO purchases(product_id,product_name,quantity,units,unit_cost,total,payment_type,supplier_id,note,mode,purchase_unit,units_per_purchase,pack_cost,average_cost,created_at)
VALUES($pid,$pn,$q,$units,$cost,$total,$pt,$sid,$note,$mode,$purchaseUnit,$upp,$pack,$avg,$at); SELECT last_insert_rowid();";
            ins.Parameters.AddWithValue("$pid", productId); ins.Parameters.AddWithValue("$pn", productName);
            ins.Parameters.AddWithValue("$q", quantity); ins.Parameters.AddWithValue("$units", incomingUnits); ins.Parameters.AddWithValue("$cost", unitCost); ins.Parameters.AddWithValue("$total", total);
            ins.Parameters.AddWithValue("$pt", paymentType); ins.Parameters.AddWithValue("$sid", supplierId.HasValue ? supplierId.Value : DBNull.Value);
            ins.Parameters.AddWithValue("$note", note?.Trim() ?? ""); ins.Parameters.AddWithValue("$mode", mode); ins.Parameters.AddWithValue("$purchaseUnit", usePurchaseUnit ? purchaseUnit : baseUnit);
            ins.Parameters.AddWithValue("$upp", usePurchaseUnit ? unitsPerPurchase : 1); ins.Parameters.AddWithValue("$pack", packCost); ins.Parameters.AddWithValue("$avg", weightedAverage); ins.Parameters.AddWithValue("$at", now);
            id = Convert.ToInt64(ins.ExecuteScalar() ?? 0L);
        }

        using (var u = db.CreateCommand())
        {
            u.Transaction = tx;
            u.CommandText = usePurchaseUnit
                ? "UPDATE products SET stock=$stock,purchase_price=$cost,package_buy_price=$pack WHERE id=$id"
                : "UPDATE products SET stock=$stock,purchase_price=$cost WHERE id=$id";
            u.Parameters.AddWithValue("$stock", balance); u.Parameters.AddWithValue("$cost", weightedAverage); u.Parameters.AddWithValue("$id", productId);
            if (usePurchaseUnit) u.Parameters.AddWithValue("$pack", enteredCost);
            u.ExecuteNonQuery();
        }
        using (var ledger = db.CreateCommand())
        {
            ledger.Transaction = tx;
            ledger.CommandText = @"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at)
VALUES($pid,'PURCHASE',$q,$bal,'PURCHASE',$rid,$note,$at)";
            ledger.Parameters.AddWithValue("$pid", productId); ledger.Parameters.AddWithValue("$q", incomingUnits); ledger.Parameters.AddWithValue("$bal", balance);
            ledger.Parameters.AddWithValue("$rid", id); ledger.Parameters.AddWithValue("$note", string.IsNullOrWhiteSpace(note) ? "خرید / ورود کالا" : note.Trim()); ledger.Parameters.AddWithValue("$at", now);
            ledger.ExecuteNonQuery();
        }
        if (paymentType == "CREDIT" && supplierId.HasValue)
        {
            using var debt = db.CreateCommand(); debt.Transaction = tx;
            debt.CommandText = "UPDATE parties SET debt=debt+$amount WHERE id=$id AND type='COMPANY'";
            debt.Parameters.AddWithValue("$amount", total); debt.Parameters.AddWithValue("$id", supplierId.Value);
            if (debt.ExecuteNonQuery() != 1) throw new InvalidOperationException("حساب تأمین‌کننده پیدا نشد.");
        }
        AuditService.Write(db, tx, "PURCHASE_CREATE", "PURCHASE", id.ToString(), JsonSerializer.Serialize(new
        {
            productId, productName, supplierId, quantity, incomingUnits, baseUnit, purchaseUnit, unitsPerPurchase,
            enteredCost, unitCost, weightedAverage, total, paymentType, mode, note
        }));
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
        cmd.CommandText = $@"SELECT p.id,p.product_id,p.product_name,p.quantity,p.units,p.unit_cost,p.total,p.payment_type,p.supplier_id,COALESCE(s.name,'—'),COALESCE(p.note,''),
COALESCE(p.mode,'BASE'),COALESCE(p.purchase_unit,'دانه'),COALESCE(p.units_per_purchase,1),COALESCE(p.pack_cost,0),COALESCE(NULLIF(p.average_cost,0),p.unit_cost),p.created_at
FROM purchases p LEFT JOIN parties s ON s.id=p.supplier_id {where} ORDER BY p.created_at DESC,p.id DESC LIMIT $l OFFSET $o";
        if (query.Length > 0) cmd.Parameters.AddWithValue("$like", $"%{query}%"); cmd.Parameters.AddWithValue("$l", pageSize); cmd.Parameters.AddWithValue("$o", (page - 1) * pageSize);
        using var r = cmd.ExecuteReader(); var i = 0;
        while (r.Read()) list.Add(new PurchaseRecord
        {
            Id = r.GetInt64(0), RowNumber = (page - 1) * pageSize + ++i, ProductId = r.IsDBNull(1) ? null : r.GetInt64(1), ProductName = r.GetString(2),
            Quantity = r.GetDouble(3), Units = r.GetDouble(4), UnitCost = r.GetInt64(5), Total = r.GetInt64(6), PaymentType = r.GetString(7),
            SupplierId = r.IsDBNull(8) ? null : r.GetInt64(8), SupplierName = r.GetString(9), Note = r.GetString(10), Mode = r.GetString(11), PurchaseUnit = r.GetString(12),
            UnitsPerPurchase = r.GetDouble(13), PackCost = r.GetInt64(14), AverageCost = r.GetInt64(15), CreatedAt = DateTime.Parse(r.GetString(16))
        });
        return new PagedResult<PurchaseRecord> { Items = list, TotalCount = total, Page = page, PageSize = pageSize };
    }
}
