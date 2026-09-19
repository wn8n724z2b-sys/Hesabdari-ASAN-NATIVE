using System.Text.Json;
using Microsoft.Data.Sqlite;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class InvoiceAdjustmentService
{
    public InvoiceEditData LoadForEdit(long invoiceNo)
    {
        var detail = new InvoiceService().GetByInvoiceNo(invoiceNo) ?? throw new InvalidOperationException("فاکتور پیدا نشد.");
        if (detail.Status == "CANCELLED") throw new InvalidOperationException("فاکتور باطل‌شده قابل ویرایش نیست.");
        var data = new InvoiceEditData
        {
            Id = detail.Id, InvoiceNo = detail.InvoiceNo, CustomerId = detail.CustomerId,
            CustomerName = detail.CustomerName, PaymentType = detail.PaymentType, Discount = detail.Discount, Status = detail.Status
        };
        foreach (var x in detail.Items)
        {
            data.Items.Add(new InvoiceEditLine
            {
                ProductId = x.ProductId, ProductName = x.ProductName, Barcode = x.Barcode,
                Quantity = x.Quantity, UnitPrice = x.UnitPrice, PurchasePrice = x.PurchasePrice
            });
        }
        return data;
    }

    public void SaveEdit(InvoiceEditData edited)
    {
        if (edited.Items.Count == 0) throw new InvalidOperationException("فاکتور باید حداقل یک قلم داشته باشد.");
        foreach (var item in edited.Items)
        {
            if (!item.ProductId.HasValue) throw new InvalidOperationException($"کالای «{item.ProductName}» دیگر به انبار متصل نیست؛ این فاکتور را نمی‌توان ایمن ویرایش کرد.");
            if (item.Quantity <= 0) throw new InvalidOperationException("تعداد اقلام باید بیشتر از صفر باشد.");
            if (item.UnitPrice < 0) throw new InvalidOperationException("قیمت فروش معتبر نیست.");
        }

        using var db = Database.Open();
        using var tx = db.BeginTransaction();
        var original = LoadSnapshot(db, tx, edited.InvoiceNo);
        EnsureActive(original);
        EnsureCreditCanReverse(db, tx, original);
        SaveRevision(db, tx, original, "EDIT_BEFORE");
        ReverseOriginal(db, tx, original, "ویرایش فاکتور");

        var subtotal = edited.Items.Sum(x => x.LineTotal);
        var discount = Math.Clamp(edited.Discount, 0, subtotal);
        var total = subtotal - discount;

        foreach (var item in edited.Items)
        {
            using var stock = db.CreateCommand();
            stock.Transaction = tx;
            stock.CommandText = "SELECT stock,name,purchase_price FROM products WHERE id=$id";
            stock.Parameters.AddWithValue("$id", item.ProductId!.Value);
            using var r = stock.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException($"کالای «{item.ProductName}» پیدا نشد.");
            var available = r.GetDouble(0);
            if (available + 0.000001 < item.Quantity) throw new InvalidOperationException($"موجودی «{item.ProductName}» برای مقدار جدید کافی نیست.");
            if (item.PurchasePrice < 0) item.PurchasePrice = r.GetInt64(2);
        }

        using (var del = db.CreateCommand())
        {
            del.Transaction = tx; del.CommandText = "DELETE FROM invoice_items WHERE invoice_id=$id"; del.Parameters.AddWithValue("$id", original.Id); del.ExecuteNonQuery();
        }
        using (var inv = db.CreateCommand())
        {
            inv.Transaction = tx;
            inv.CommandText = @"UPDATE invoices SET subtotal=$sub,discount=$dis,total=$total,paid=$paid,updated_at=$at WHERE id=$id";
            inv.Parameters.AddWithValue("$sub", subtotal); inv.Parameters.AddWithValue("$dis", discount); inv.Parameters.AddWithValue("$total", total);
            inv.Parameters.AddWithValue("$paid", original.PaymentType == "CASH" ? total : 0); inv.Parameters.AddWithValue("$at", DateTime.Now.ToString("O")); inv.Parameters.AddWithValue("$id", original.Id); inv.ExecuteNonQuery();
        }
        foreach (var item in edited.Items)
        {
            using (var line = db.CreateCommand())
            {
                line.Transaction = tx;
                line.CommandText = @"INSERT INTO invoice_items(invoice_id,product_id,product_name,barcode,qty,unit_price,purchase_price,line_total)
VALUES($iid,$pid,$name,$barcode,$qty,$price,$purchase,$total)";
                line.Parameters.AddWithValue("$iid", original.Id); line.Parameters.AddWithValue("$pid", item.ProductId!.Value); line.Parameters.AddWithValue("$name", item.ProductName);
                line.Parameters.AddWithValue("$barcode", item.Barcode ?? ""); line.Parameters.AddWithValue("$qty", item.Quantity); line.Parameters.AddWithValue("$price", item.UnitPrice);
                line.Parameters.AddWithValue("$purchase", item.PurchasePrice); line.Parameters.AddWithValue("$total", item.LineTotal); line.ExecuteNonQuery();
            }
            ApplyStock(db, tx, item.ProductId!.Value, -item.Quantity, "INVOICE_EDIT", original.Id, "فروش پس از ویرایش فاکتور");
        }
        if (original.PaymentType == "CREDIT") AddCustomerDebt(db, tx, original.CustomerId, total);
        var after = LoadSnapshot(db, tx, edited.InvoiceNo);
        AuditService.Write(db, tx, "INVOICE_EDIT", "INVOICE", original.Id.ToString(), JsonSerializer.Serialize(new { before = original, after }), original.InvoiceNo);
        tx.Commit();
    }

    public void Cancel(long invoiceNo, string reason)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length < 2) throw new InvalidOperationException("دلیل ابطال را بنویسید.");
        using var db = Database.Open();
        using var tx = db.BeginTransaction();
        var original = LoadSnapshot(db, tx, invoiceNo);
        EnsureActive(original);
        EnsureCreditCanReverse(db, tx, original);
        SaveRevision(db, tx, original, "CANCEL_BEFORE");
        ReverseOriginal(db, tx, original, "ابطال فاکتور");
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE invoices SET status='CANCELLED',cancelled_at=$at,cancel_reason=$reason,updated_at=$at WHERE id=$id";
            cmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("O")); cmd.Parameters.AddWithValue("$reason", reason); cmd.Parameters.AddWithValue("$id", original.Id); cmd.ExecuteNonQuery();
        }
        AuditService.Write(db, tx, "INVOICE_CANCEL", "INVOICE", original.Id.ToString(), JsonSerializer.Serialize(new { original, reason }), original.InvoiceNo);
        tx.Commit();
    }

    private static void EnsureActive(InvoiceSnapshot x)
    {
        if (x.Status == "CANCELLED") throw new InvalidOperationException("این فاکتور قبلاً باطل شده است.");
    }

    private static void EnsureCreditCanReverse(SqliteConnection db, SqliteTransaction tx, InvoiceSnapshot x)
    {
        if (x.PaymentType != "CREDIT") return;
        if (!x.CustomerId.HasValue) throw new InvalidOperationException("مشتری فاکتور نسیه مشخص نیست؛ عملیات برای حفاظت از حساب‌ها متوقف شد.");
        using var receipts = db.CreateCommand();
        receipts.Transaction = tx;
        receipts.CommandText = "SELECT COUNT(*) FROM customer_receipts WHERE party_id=$id AND datetime(created_at)>datetime($created)";
        receipts.Parameters.AddWithValue("$id", x.CustomerId.Value); receipts.Parameters.AddWithValue("$created", x.CreatedAt);
        if (Convert.ToInt32(receipts.ExecuteScalar() ?? 0) > 0)
            throw new InvalidOperationException("بعد از این فاکتور برای مشتری پرداخت قرض ثبت شده است. برای جلوگیری از به‌هم‌خوردن حساب، این فاکتور مستقیم قابل ویرایش/ابطال نیست.");
        using var debt = db.CreateCommand(); debt.Transaction = tx; debt.CommandText = "SELECT debt FROM parties WHERE id=$id"; debt.Parameters.AddWithValue("$id", x.CustomerId.Value);
        var currentDebt = Convert.ToInt64(debt.ExecuteScalar() ?? -1L);
        if (currentDebt < x.Total) throw new InvalidOperationException("مانده قرض مشتری از مبلغ این فاکتور کمتر است؛ عملیات برای حفاظت از حساب متوقف شد.");
    }

    private static void ReverseOriginal(SqliteConnection db, SqliteTransaction tx, InvoiceSnapshot x, string note)
    {
        foreach (var item in x.Items)
        {
            if (!item.ProductId.HasValue) throw new InvalidOperationException($"قلم «{item.ProductName}» به کالای انبار متصل نیست؛ برگشت موجودی ایمن ممکن نیست.");
            ApplyStock(db, tx, item.ProductId.Value, item.Quantity, "INVOICE_REVERSE", x.Id, note);
        }
        if (x.PaymentType == "CREDIT") AddCustomerDebt(db, tx, x.CustomerId, -x.Total);
    }

    private static void AddCustomerDebt(SqliteConnection db, SqliteTransaction tx, long? customerId, long amount)
    {
        if (!customerId.HasValue) throw new InvalidOperationException("مشتری فاکتور نسیه پیدا نشد.");
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "UPDATE parties SET debt=debt+$amount WHERE id=$id AND type='CUSTOMER'";
        cmd.Parameters.AddWithValue("$amount", amount); cmd.Parameters.AddWithValue("$id", customerId.Value);
        if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("حساب مشتری پیدا نشد.");
        using var check = db.CreateCommand(); check.Transaction = tx; check.CommandText = "SELECT debt FROM parties WHERE id=$id"; check.Parameters.AddWithValue("$id", customerId.Value);
        if (Convert.ToInt64(check.ExecuteScalar() ?? -1L) < 0) throw new InvalidOperationException("عملیات باعث منفی‌شدن قرض مشتری می‌شود و لغو شد.");
    }

    private static void ApplyStock(SqliteConnection db, SqliteTransaction tx, long productId, double delta, string movementType, long invoiceId, string note)
    {
        double balance;
        using (var update = db.CreateCommand())
        {
            update.Transaction = tx; update.CommandText = "UPDATE products SET stock=stock+$delta WHERE id=$id"; update.Parameters.AddWithValue("$delta", delta); update.Parameters.AddWithValue("$id", productId);
            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("کالای مرتبط با فاکتور پیدا نشد.");
        }
        using (var read = db.CreateCommand()) { read.Transaction = tx; read.CommandText = "SELECT stock FROM products WHERE id=$id"; read.Parameters.AddWithValue("$id", productId); balance = Convert.ToDouble(read.ExecuteScalar() ?? 0d); }
        if (balance < -0.000001) throw new InvalidOperationException("عملیات باعث منفی‌شدن موجودی می‌شود و لغو شد.");
        using var ledger = db.CreateCommand(); ledger.Transaction = tx;
        ledger.CommandText = @"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at)
VALUES($pid,$type,$qty,$bal,'INVOICE',$rid,$note,$at)";
        ledger.Parameters.AddWithValue("$pid", productId); ledger.Parameters.AddWithValue("$type", movementType); ledger.Parameters.AddWithValue("$qty", delta); ledger.Parameters.AddWithValue("$bal", balance);
        ledger.Parameters.AddWithValue("$rid", invoiceId); ledger.Parameters.AddWithValue("$note", note); ledger.Parameters.AddWithValue("$at", DateTime.Now.ToString("O")); ledger.ExecuteNonQuery();
    }

    private static void SaveRevision(SqliteConnection db, SqliteTransaction tx, InvoiceSnapshot snapshot, string action)
    {
        long revision;
        using (var no = db.CreateCommand()) { no.Transaction = tx; no.CommandText = "SELECT COALESCE(MAX(revision_no),0)+1 FROM invoice_revisions WHERE invoice_no=$no"; no.Parameters.AddWithValue("$no", snapshot.InvoiceNo); revision = Convert.ToInt64(no.ExecuteScalar() ?? 1L); }
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO invoice_revisions(invoice_id,invoice_no,revision_no,changed_at,action,actor,snapshot_json)
VALUES($id,$no,$rev,$at,$action,'Admin',$json)";
        cmd.Parameters.AddWithValue("$id", snapshot.Id); cmd.Parameters.AddWithValue("$no", snapshot.InvoiceNo); cmd.Parameters.AddWithValue("$rev", revision); cmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("O"));
        cmd.Parameters.AddWithValue("$action", action); cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot)); cmd.ExecuteNonQuery();
    }

    private static InvoiceSnapshot LoadSnapshot(SqliteConnection db, SqliteTransaction tx, long invoiceNo)
    {
        var x = new InvoiceSnapshot();
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"SELECT id,invoice_no,customer_id,customer_name,payment_type,subtotal,discount,total,paid,COALESCE(status,'ACTIVE'),created_at
FROM invoices WHERE invoice_no=$no LIMIT 1";
            cmd.Parameters.AddWithValue("$no", invoiceNo);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("فاکتور پیدا نشد.");
            x.Id = r.GetInt64(0); x.InvoiceNo = r.GetInt64(1); x.CustomerId = r.IsDBNull(2) ? null : r.GetInt64(2); x.CustomerName = r.GetString(3); x.PaymentType = r.GetString(4);
            x.Subtotal = r.GetInt64(5); x.Discount = r.GetInt64(6); x.Total = r.GetInt64(7); x.Paid = r.GetInt64(8); x.Status = r.GetString(9); x.CreatedAt = r.GetString(10);
        }
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = tx; cmd.CommandText = @"SELECT product_id,product_name,COALESCE(barcode,''),qty,unit_price,purchase_price,line_total FROM invoice_items WHERE invoice_id=$id ORDER BY id"; cmd.Parameters.AddWithValue("$id", x.Id);
            using var r = cmd.ExecuteReader();
            while (r.Read()) x.Items.Add(new SnapshotItem { ProductId = r.IsDBNull(0) ? null : r.GetInt64(0), ProductName = r.GetString(1), Barcode = r.GetString(2), Quantity = r.GetDouble(3), UnitPrice = r.GetInt64(4), PurchasePrice = r.GetInt64(5), LineTotal = r.GetInt64(6) });
        }
        return x;
    }

    private sealed class InvoiceSnapshot
    {
        public long Id { get; set; }
        public long InvoiceNo { get; set; }
        public long? CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string PaymentType { get; set; } = "CASH";
        public long Subtotal { get; set; }
        public long Discount { get; set; }
        public long Total { get; set; }
        public long Paid { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public string CreatedAt { get; set; } = "";
        public List<SnapshotItem> Items { get; set; } = new();
    }

    private sealed class SnapshotItem
    {
        public long? ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string Barcode { get; set; } = "";
        public double Quantity { get; set; }
        public long UnitPrice { get; set; }
        public long PurchasePrice { get; set; }
        public long LineTotal { get; set; }
    }
}
