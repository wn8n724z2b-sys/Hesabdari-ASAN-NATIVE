using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;
using Microsoft.Data.Sqlite;

namespace HesabdariAsan.Native.Services;

public sealed class V34MigrationService
{
    private readonly BackupService _backup = new();

    public string? DetectDatabase()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidates = new[]
        {
            Path.Combine(roaming, "com.mnrahimi.hesabdariasan", "hesabdari_asan.sqlite3"),
            Path.Combine(roaming, "حسابداری آسان", "hesabdari_asan.sqlite3"),
            Path.Combine(roaming, "hesabdari-asan", "hesabdari_asan.sqlite3"),
            Path.Combine(roaming, "HesabdariAsan", "hesabdari_asan.sqlite3")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public MigrationResult Import(string sourcePath, bool replaceCurrent)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("دیتابیس نسخه 3.4 پیدا نشد.", sourcePath);
        ValidateSource(sourcePath);
        if (!replaceCurrent && HasNativeBusinessData())
            throw new InvalidOperationException("نسخه v4 فعلی دارای اطلاعات است. برای انتقال کامل باید گزینه جایگزینی اطلاعات فعلی را تأیید کنید.");

        var result = new MigrationResult { SafetyBackupPath = _backup.CreateBackup("before-v34-import") };
        var sourceBuilder = new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly };
        using var source = new SqliteConnection(sourceBuilder.ToString());
        source.Open();
        using var target = Database.Open();
        using var tx = target.BeginTransaction();

        ClearTarget(target, tx);
        var pinned = LoadPinnedState(source);
        var categoryNames = LoadCategoryNames(source);
        var productMap = new Dictionary<string, long>(StringComparer.Ordinal);
        var productBarcodes = new Dictionary<string, string>(StringComparer.Ordinal);
        var partyMap = new Dictionary<string, long>(StringComparer.Ordinal);
        var invoiceMap = new Dictionary<string, long>(StringComparer.Ordinal);

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = @"SELECT p.id,p.name,COALESCE(p.producer,''),p.category_id,COALESCE(NULLIF(p.base_unit,''),'دانه'),
COALESCE(NULLIF(p.purchase_unit,''),COALESCE(NULLIF(p.base_unit,''),'دانه')),p.unit_conversion_enabled,
CASE WHEN p.units_per_purchase<=0 THEN 1 ELSE p.units_per_purchase END,p.average_unit_cost,p.sell_price,p.stock,p.min_stock,COALESCE(p.image_path,''),p.is_active,p.created_at
FROM products p ORDER BY p.created_at,p.id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var legacyId = r.GetString(0);
                var baseUnit = r.GetString(4);
                var purchaseUnit = r.GetString(5);
                var conversion = r.GetInt64(6) == 1;
                var unitsPerPurchase = Math.Max(1, r.GetDouble(7));
                var unitCost = Money(r.GetDouble(8));
                var packageCost = conversion ? Money(r.GetDouble(8) * unitsPerPurchase) : unitCost;
                var image = ImportProductImage(r.GetString(12), legacyId);
                using var ins = target.CreateCommand(); ins.Transaction = tx;
                ins.CommandText = @"INSERT INTO products(name,producer,barcode,category,unit,base_unit,purchase_unit,unit_conversion_enabled,units_per_purchase,package_buy_price,image_path,purchase_price,sale_price,stock,min_stock,is_active,created_at)
VALUES($n,$producer,NULL,$c,$u,$base,$purchase,$conv,$units,$pack,$img,$pp,$sp,$st,$ms,$active,$at); SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$n", r.GetString(1));
                ins.Parameters.AddWithValue("$producer", r.GetString(2));
                var categoryId = r.IsDBNull(3) ? "" : r.GetString(3);
                ins.Parameters.AddWithValue("$c", categoryNames.TryGetValue(categoryId, out var cn) ? cn : "عمومی");
                ins.Parameters.AddWithValue("$u", baseUnit);
                ins.Parameters.AddWithValue("$base", baseUnit);
                ins.Parameters.AddWithValue("$purchase", purchaseUnit);
                ins.Parameters.AddWithValue("$conv", conversion ? 1 : 0);
                ins.Parameters.AddWithValue("$units", unitsPerPurchase);
                ins.Parameters.AddWithValue("$pack", packageCost);
                ins.Parameters.AddWithValue("$img", string.IsNullOrWhiteSpace(image) ? DBNull.Value : image);
                ins.Parameters.AddWithValue("$pp", unitCost);
                ins.Parameters.AddWithValue("$sp", Money(r.GetDouble(9)));
                ins.Parameters.AddWithValue("$st", r.GetDouble(10));
                ins.Parameters.AddWithValue("$ms", r.GetDouble(11));
                ins.Parameters.AddWithValue("$active", r.GetInt64(13));
                ins.Parameters.AddWithValue("$at", r.GetString(14));
                var nativeId = Convert.ToInt64(ins.ExecuteScalar() ?? 0L);
                productMap[legacyId] = nativeId;
                AddLegacyMap(target, tx, "product", legacyId, nativeId);
                result.Products++;
            }
        }

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = "SELECT product_id,barcode,is_primary FROM product_barcodes ORDER BY product_id,is_primary DESC,id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var legacyProduct = r.GetString(0);
                if (!productMap.TryGetValue(legacyProduct, out var nativeProduct)) continue;
                var barcode = r.GetString(1).Trim();
                if (barcode.Length == 0) continue;
                var isPrimary = r.GetInt64(2) == 1;
                using var ins = target.CreateCommand(); ins.Transaction = tx;
                ins.CommandText = "INSERT OR IGNORE INTO product_barcodes(product_id,barcode,is_primary) VALUES($p,$b,$primary)";
                ins.Parameters.AddWithValue("$p", nativeProduct); ins.Parameters.AddWithValue("$b", barcode); ins.Parameters.AddWithValue("$primary", isPrimary ? 1 : 0); ins.ExecuteNonQuery();
                if (isPrimary || !productBarcodes.ContainsKey(legacyProduct)) productBarcodes[legacyProduct] = barcode;
            }
        }
        foreach (var pair in productMap)
        {
            if (!productBarcodes.TryGetValue(pair.Key, out var primary)) continue;
            using var update = target.CreateCommand(); update.Transaction = tx;
            update.CommandText = "UPDATE products SET barcode=$b WHERE id=$id"; update.Parameters.AddWithValue("$b", primary); update.Parameters.AddWithValue("$id", pair.Value); update.ExecuteNonQuery();
        }

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = "SELECT id,kind,name,COALESCE(phone,''),balance,created_at FROM parties ORDER BY created_at,id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var legacyId = r.GetString(0);
                var type = string.Equals(r.GetString(1), "supplier", StringComparison.OrdinalIgnoreCase) ? "COMPANY" : "CUSTOMER";
                using var ins = target.CreateCommand(); ins.Transaction = tx;
                ins.CommandText = @"INSERT INTO parties(type,name,phone,address,debt,is_pinned,created_at)
VALUES($t,$n,$p,'',$d,$pin,$at); SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$t", type);
                ins.Parameters.AddWithValue("$n", r.GetString(2));
                ins.Parameters.AddWithValue("$p", r.GetString(3));
                ins.Parameters.AddWithValue("$d", Money(Math.Max(0, r.GetDouble(4))));
                ins.Parameters.AddWithValue("$pin", pinned.Contains(legacyId) || r.GetString(2) == "مشتری عمومی" ? 1 : 0);
                ins.Parameters.AddWithValue("$at", r.GetString(5));
                var nativeId = Convert.ToInt64(ins.ExecuteScalar() ?? 0L);
                partyMap[legacyId] = nativeId;
                AddLegacyMap(target, tx, "party", legacyId, nativeId);
                result.Parties++;
            }
        }
        EnsureGeneralCustomer(target, tx);

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = @"SELECT id,invoice_no,sold_at,customer_id,payment_method,subtotal,discount,total,created_at
FROM sales WHERE status='active' ORDER BY sold_at,invoice_no";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var legacyId = r.GetString(0);
                var legacyCustomer = r.IsDBNull(3) ? "" : r.GetString(3);
                long? customerId = partyMap.TryGetValue(legacyCustomer, out var cid) ? cid : null;
                var customerName = CustomerName(target, tx, customerId) ?? "مشتری عمومی";
                var payment = PaymentType(r.GetString(4));
                using var ins = target.CreateCommand(); ins.Transaction = tx;
                ins.CommandText = @"INSERT INTO invoices(invoice_no,customer_id,customer_name,payment_type,subtotal,discount,total,paid,created_at)
VALUES($no,$cid,$cn,$pt,$sub,$dis,$tot,$paid,$at); SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$no", r.GetInt64(1));
                ins.Parameters.AddWithValue("$cid", customerId.HasValue ? customerId.Value : DBNull.Value);
                ins.Parameters.AddWithValue("$cn", customerName);
                ins.Parameters.AddWithValue("$pt", payment);
                ins.Parameters.AddWithValue("$sub", Money(r.GetDouble(5)));
                ins.Parameters.AddWithValue("$dis", Money(r.GetDouble(6)));
                var total = Money(r.GetDouble(7));
                ins.Parameters.AddWithValue("$tot", total);
                ins.Parameters.AddWithValue("$paid", payment == "CASH" ? total : 0);
                ins.Parameters.AddWithValue("$at", r.GetString(2));
                var nativeId = Convert.ToInt64(ins.ExecuteScalar() ?? 0L);
                invoiceMap[legacyId] = nativeId;
                AddLegacyMap(target, tx, "invoice", legacyId, nativeId);
                result.Invoices++;
            }
        }

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = "SELECT sale_id,product_id,product_name,unit,qty,sell_price,unit_cost FROM sale_items ORDER BY id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var saleId = r.GetString(0); if (!invoiceMap.TryGetValue(saleId, out var invoiceId)) continue;
                var legacyProduct = r.GetString(1); long? productId = productMap.TryGetValue(legacyProduct, out var pid) ? pid : null;
                var unit = r.IsDBNull(3) ? "دانه" : r.GetString(3); var qty = r.GetDouble(4); var unitPrice = Money(r.GetDouble(5));
                using var ins = target.CreateCommand(); ins.Transaction = tx;
                ins.CommandText = @"INSERT INTO invoice_items(invoice_id,product_id,product_name,barcode,qty,unit_price,purchase_price,line_total,unit)
VALUES($iid,$pid,$n,$b,$q,$up,$pp,$lt,$unit)";
                ins.Parameters.AddWithValue("$iid", invoiceId);
                ins.Parameters.AddWithValue("$pid", productId.HasValue ? productId.Value : DBNull.Value);
                ins.Parameters.AddWithValue("$n", r.GetString(2));
                ins.Parameters.AddWithValue("$b", productBarcodes.TryGetValue(legacyProduct, out var bc) && !string.IsNullOrWhiteSpace(bc) ? bc : DBNull.Value);
                ins.Parameters.AddWithValue("$q", qty);
                ins.Parameters.AddWithValue("$up", unitPrice);
                ins.Parameters.AddWithValue("$pp", Money(r.GetDouble(6)));
                ins.Parameters.AddWithValue("$lt", Money(qty * r.GetDouble(5)));
                ins.Parameters.AddWithValue("$unit", unit);
                ins.ExecuteNonQuery(); result.InvoiceItems++;
            }
        }

        var runningBalance = new Dictionary<long, double>();
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = "SELECT product_id,occurred_at,movement_type,qty,reference_type,reference_id,COALESCE(note,'') FROM inventory_ledger ORDER BY occurred_at,id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var legacyProduct = r.GetString(0); if (!productMap.TryGetValue(legacyProduct, out var productId)) continue;
                var qty = r.GetDouble(3); runningBalance[productId] = runningBalance.GetValueOrDefault(productId) + qty;
                using var ins = target.CreateCommand(); ins.Transaction = tx;
                ins.CommandText = @"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at)
VALUES($pid,$mt,$q,$bal,$rt,NULL,$note,$at)";
                ins.Parameters.AddWithValue("$pid", productId); ins.Parameters.AddWithValue("$mt", r.GetString(2)); ins.Parameters.AddWithValue("$q", qty);
                ins.Parameters.AddWithValue("$bal", runningBalance[productId]); ins.Parameters.AddWithValue("$rt", r.IsDBNull(4) ? "" : r.GetString(4));
                ins.Parameters.AddWithValue("$note", r.GetString(6)); ins.Parameters.AddWithValue("$at", r.GetString(1)); ins.ExecuteNonQuery(); result.InventoryRows++;
            }
        }

        CopyExpenses(source, target, tx, result);
        CopyReceipts(source, target, tx, partyMap, result);
        CopySupplierPayments(source, target, tx, partyMap, result);
        CopyPurchases(source, target, tx, productMap, partyMap, result);
        CopyFinancialPeriods(source, target, tx);
        CopyAudit(source, target, tx, invoiceMap);
        CopyRevisions(source, target, tx, invoiceMap);
        CopySettings(source, target, tx);

        tx.Commit();
        Database.Initialize();
        if (!string.Equals(Database.QuickCheck(), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("پس از انتقال، بررسی سلامت دیتابیس موفق نبود.");
        using (var auditDb = Database.Open()) AuditService.Write(auditDb, null, "V34_MIGRATION", "DATABASE", "main", System.Text.Json.JsonSerializer.Serialize(result));
        return result;
    }

    private static void ValidateSource(string path)
    {
        var b = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly };
        using var db = new SqliteConnection(b.ToString()); db.Open();
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('products','sales','sale_items','parties','app_state')";
        if (Convert.ToInt32(cmd.ExecuteScalar() ?? 0) < 5) throw new InvalidDataException("فایل انتخاب‌شده دیتابیس معتبر نسخه 3.4 نیست.");
    }

    private static bool HasNativeBusinessData()
    {
        using var db = Database.Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT (SELECT COUNT(*) FROM products)+(SELECT COUNT(*) FROM invoices)+(SELECT COUNT(*) FROM expenses)+(SELECT COUNT(*) FROM parties WHERE name<>'مشتری عمومی')";
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) > 0;
    }

    private static void ClearTarget(SqliteConnection db, SqliteTransaction tx)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = @"DELETE FROM invoice_revisions; DELETE FROM invoice_items; DELETE FROM invoices; DELETE FROM customer_receipts;
DELETE FROM supplier_payments; DELETE FROM purchases; DELETE FROM inventory_ledger; DELETE FROM expenses; DELETE FROM product_barcodes; DELETE FROM products;
DELETE FROM parties; DELETE FROM financial_periods; DELETE FROM audit_log; DELETE FROM legacy_id_map;";
        cmd.ExecuteNonQuery();
    }

    private static Dictionary<string,string> LoadCategoryNames(SqliteConnection source)
    {
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        using var cmd = source.CreateCommand(); cmd.CommandText = "SELECT id,name FROM categories"; using var r = cmd.ExecuteReader();
        while (r.Read()) result[r.GetString(0)] = r.GetString(1); return result;
    }

    private static HashSet<string> LoadPinnedState(SqliteConnection source)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var cmd = source.CreateCommand(); cmd.CommandText = "SELECT json FROM app_state WHERE id=1"; var raw = Convert.ToString(cmd.ExecuteScalar());
            if (string.IsNullOrWhiteSpace(raw)) return result;
            using var doc = JsonDocument.Parse(raw);
            foreach (var key in new[] { "customers", "suppliers" })
            {
                if (!doc.RootElement.TryGetProperty(key, out var list) || list.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in list.EnumerateArray())
                {
                    if (item.TryGetProperty("pinned", out var pin) && pin.ValueKind == JsonValueKind.True && item.TryGetProperty("id", out var id))
                        result.Add(id.GetString() ?? "");
                }
            }
        }
        catch { }
        return result;
    }

    private static void CopyExpenses(SqliteConnection source, SqliteConnection target, SqliteTransaction tx, MigrationResult result)
    {
        using var cmd = source.CreateCommand(); cmd.CommandText = "SELECT occurred_at,category,amount,COALESCE(note,'') FROM expenses ORDER BY occurred_at"; using var r = cmd.ExecuteReader();
        while (r.Read()) { using var ins=target.CreateCommand(); ins.Transaction=tx; ins.CommandText="INSERT INTO expenses(title,category,amount,note,created_at) VALUES($t,$c,$a,$n,$at)"; var cat=r.GetString(1); ins.Parameters.AddWithValue("$t",cat);ins.Parameters.AddWithValue("$c",cat);ins.Parameters.AddWithValue("$a",Money(r.GetDouble(2)));ins.Parameters.AddWithValue("$n",r.GetString(3));ins.Parameters.AddWithValue("$at",r.GetString(0));ins.ExecuteNonQuery();result.Expenses++; }
    }

    private static void CopyReceipts(SqliteConnection source, SqliteConnection target, SqliteTransaction tx, Dictionary<string,long> parties, MigrationResult result)
    {
        using var cmd=source.CreateCommand();cmd.CommandText="SELECT occurred_at,customer_id,amount,COALESCE(note,'') FROM customer_receipts ORDER BY occurred_at";using var r=cmd.ExecuteReader();
        while(r.Read()){if(!parties.TryGetValue(r.GetString(1),out var pid))continue;using var ins=target.CreateCommand();ins.Transaction=tx;ins.CommandText="INSERT INTO customer_receipts(party_id,amount,note,created_at) VALUES($p,$a,$n,$at)";ins.Parameters.AddWithValue("$p",pid);ins.Parameters.AddWithValue("$a",Money(r.GetDouble(2)));ins.Parameters.AddWithValue("$n",r.GetString(3));ins.Parameters.AddWithValue("$at",r.GetString(0));ins.ExecuteNonQuery();result.Receipts++;}
    }

    private static void CopySupplierPayments(SqliteConnection source, SqliteConnection target, SqliteTransaction tx, Dictionary<string,long> parties, MigrationResult result)
    {
        using var cmd=source.CreateCommand();cmd.CommandText="SELECT occurred_at,supplier_id,amount,COALESCE(note,'') FROM supplier_payments ORDER BY occurred_at";using var r=cmd.ExecuteReader();
        while(r.Read()){if(!parties.TryGetValue(r.GetString(1),out var pid))continue;using var ins=target.CreateCommand();ins.Transaction=tx;ins.CommandText="INSERT INTO supplier_payments(party_id,amount,note,created_at) VALUES($p,$a,$n,$at)";ins.Parameters.AddWithValue("$p",pid);ins.Parameters.AddWithValue("$a",Money(r.GetDouble(2)));ins.Parameters.AddWithValue("$n",r.GetString(3));ins.Parameters.AddWithValue("$at",r.GetString(0));ins.ExecuteNonQuery();result.SupplierPayments++;}
    }

    private static void CopyPurchases(SqliteConnection source, SqliteConnection target, SqliteTransaction tx, Dictionary<string,long> products, Dictionary<string,long> parties, MigrationResult result)
    {
        using var cmd=source.CreateCommand();
        cmd.CommandText=@"SELECT purchased_at,product_id,product_name,COALESCE(mode,'base'),quantity,COALESCE(purchase_unit,'دانه'),
CASE WHEN units_per_purchase<=0 THEN 1 ELSE units_per_purchase END,units,pack_cost,unit_cost,average_cost,total,payment_method,supplier_id
FROM purchases ORDER BY purchased_at";
        using var r=cmd.ExecuteReader();
        while(r.Read())
        {
            var legacyProduct=r.GetString(1); long? productId=products.TryGetValue(legacyProduct,out var p)?p:null;
            var legacySupplier=r.IsDBNull(13)?"":r.GetString(13); long? supplierId=parties.TryGetValue(legacySupplier,out var s)?s:null;
            var rawMode=r.GetString(3); var mode=rawMode.Contains("purchase",StringComparison.OrdinalIgnoreCase)||rawMode.Contains("pack",StringComparison.OrdinalIgnoreCase)?"PURCHASE_UNIT":"BASE";
            using var ins=target.CreateCommand(); ins.Transaction=tx;
            ins.CommandText=@"INSERT INTO purchases(product_id,product_name,quantity,units,unit_cost,total,payment_type,supplier_id,note,mode,purchase_unit,units_per_purchase,pack_cost,average_cost,created_at)
VALUES($p,$n,$q,$u,$uc,$t,$pt,$s,'',$mode,$purchaseUnit,$upp,$pack,$avg,$at)";
            ins.Parameters.AddWithValue("$p",productId.HasValue?productId.Value:DBNull.Value); ins.Parameters.AddWithValue("$n",r.GetString(2));
            ins.Parameters.AddWithValue("$q",r.GetDouble(4)); ins.Parameters.AddWithValue("$u",r.GetDouble(7)); ins.Parameters.AddWithValue("$uc",Money(r.GetDouble(9)));
            ins.Parameters.AddWithValue("$t",Money(r.GetDouble(11))); ins.Parameters.AddWithValue("$pt",PaymentType(r.GetString(12))); ins.Parameters.AddWithValue("$s",supplierId.HasValue?supplierId.Value:DBNull.Value);
            ins.Parameters.AddWithValue("$mode",mode); ins.Parameters.AddWithValue("$purchaseUnit",r.GetString(5)); ins.Parameters.AddWithValue("$upp",r.GetDouble(6));
            ins.Parameters.AddWithValue("$pack",Money(r.GetDouble(8))); ins.Parameters.AddWithValue("$avg",Money(r.GetDouble(10))); ins.Parameters.AddWithValue("$at",r.GetString(0));
            ins.ExecuteNonQuery(); result.Purchases++;
        }
    }

    private static void CopyFinancialPeriods(SqliteConnection source, SqliteConnection target, SqliteTransaction tx)
    {
        using var cmd=source.CreateCommand();cmd.CommandText="SELECT period_no,started_at,ended_at,summary_json FROM financial_periods ORDER BY period_no";using var r=cmd.ExecuteReader();
        while(r.Read()){using var ins=target.CreateCommand();ins.Transaction=tx;ins.CommandText="INSERT INTO financial_periods(period_no,started_at,ended_at,summary_json) VALUES($n,$s,$e,$j)";ins.Parameters.AddWithValue("$n",r.GetInt64(0));ins.Parameters.AddWithValue("$s",r.GetString(1));ins.Parameters.AddWithValue("$e",r.IsDBNull(2)?DBNull.Value:r.GetString(2));ins.Parameters.AddWithValue("$j",r.IsDBNull(3)?DBNull.Value:r.GetString(3));ins.ExecuteNonQuery();}
    }

    private static void CopyAudit(SqliteConnection source, SqliteConnection target, SqliteTransaction tx, Dictionary<string,long> invoiceMap)
    {
        try{using var cmd=source.CreateCommand();cmd.CommandText="SELECT created_at,action,entity_type,entity_id,payload_json,actor,invoice_no FROM audit_log ORDER BY id";using var r=cmd.ExecuteReader();while(r.Read()){using var ins=target.CreateCommand();ins.Transaction=tx;ins.CommandText="INSERT INTO audit_log(created_at,action,entity_type,entity_id,payload_json,actor,invoice_no) VALUES($at,$a,$et,$eid,$p,$actor,$no)";ins.Parameters.AddWithValue("$at",r.GetString(0));ins.Parameters.AddWithValue("$a",r.GetString(1));ins.Parameters.AddWithValue("$et",r.IsDBNull(2)?DBNull.Value:r.GetString(2));ins.Parameters.AddWithValue("$eid",r.IsDBNull(3)?DBNull.Value:r.GetString(3));ins.Parameters.AddWithValue("$p",r.IsDBNull(4)?DBNull.Value:r.GetString(4));ins.Parameters.AddWithValue("$actor",r.IsDBNull(5)?"Admin":r.GetString(5));ins.Parameters.AddWithValue("$no",r.IsDBNull(6)?DBNull.Value:r.GetInt64(6));ins.ExecuteNonQuery();}}catch(SqliteException){}
    }

    private static void CopyRevisions(SqliteConnection source, SqliteConnection target, SqliteTransaction tx, Dictionary<string,long> invoiceMap)
    {
        try{using var cmd=source.CreateCommand();cmd.CommandText="SELECT sale_id,invoice_no,revision_no,changed_at,action,actor,snapshot_json FROM invoice_revisions ORDER BY id";using var r=cmd.ExecuteReader();while(r.Read()){var sale=r.GetString(0);long? native=invoiceMap.TryGetValue(sale,out var iid)?iid:null;using var ins=target.CreateCommand();ins.Transaction=tx;ins.CommandText="INSERT INTO invoice_revisions(invoice_id,invoice_no,revision_no,changed_at,action,actor,snapshot_json) VALUES($iid,$no,$rev,$at,$a,$actor,$snap)";ins.Parameters.AddWithValue("$iid",native.HasValue?native.Value:DBNull.Value);ins.Parameters.AddWithValue("$no",r.GetInt64(1));ins.Parameters.AddWithValue("$rev",r.GetInt64(2));ins.Parameters.AddWithValue("$at",r.GetString(3));ins.Parameters.AddWithValue("$a",r.GetString(4));ins.Parameters.AddWithValue("$actor",r.GetString(5));ins.Parameters.AddWithValue("$snap",r.GetString(6));ins.ExecuteNonQuery();}}catch(SqliteException){}
    }


    private static void CopySettings(SqliteConnection source, SqliteConnection target, SqliteTransaction tx)
    {
        try
        {
            using var cmd=source.CreateCommand();cmd.CommandText="SELECT json FROM app_state WHERE id=1";var raw=Convert.ToString(cmd.ExecuteScalar());if(string.IsNullOrWhiteSpace(raw))return;
            using var doc=JsonDocument.Parse(raw);if(!doc.RootElement.TryGetProperty("settings",out var settings)||settings.ValueKind!=JsonValueKind.Object)return;
            string S(string key){return settings.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";}
            bool B(string key){return settings.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;}
            double N(string key,double fallback){return settings.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetDouble(out var n)?n:fallback;}
            UpsertSetting(target,tx,"manager_name",string.IsNullOrWhiteSpace(S("managerName"))?"مدیر سیستم":S("managerName"));
            UpsertSetting(target,tx,"shop_name",string.IsNullOrWhiteSpace(S("storeName"))?"فروشگاه من":S("storeName"));
            UpsertSetting(target,tx,"shop_phone",S("storePhone"));
            UpsertSetting(target,tx,"shop_address",S("storeAddress"));
            UpsertSetting(target,tx,"printer_name",S("printerName"));
            UpsertSetting(target,tx,"print_after_sale",B("autoPrint")?"1":"0");
            UpsertSetting(target,tx,"theme",string.Equals(S("theme"),"dark",StringComparison.OrdinalIgnoreCase)?"Dark":"Light");
            UpsertSetting(target,tx,"ui_scale",Math.Clamp(N("uiTextScale",1),1,2).ToString(System.Globalization.CultureInfo.InvariantCulture));
            UpsertSetting(target,tx,"auto_backup_per_day",Math.Clamp((int)Math.Round(N("autoBackupPerDay",4)),1,6).ToString());
            var manager=SaveDataImage(S("managerPhoto"),"manager");if(!string.IsNullOrWhiteSpace(manager))UpsertSetting(target,tx,"manager_image",manager);
            var logo=SaveDataImage(S("storeLogo"),"store-logo");if(!string.IsNullOrWhiteSpace(logo))UpsertSetting(target,tx,"shop_logo",logo);
        }
        catch { }
    }

    private static string ImportProductImage(string value,string legacyId)
    {
        if(string.IsNullOrWhiteSpace(value)) return "";
        if(value.StartsWith("data:image/",StringComparison.OrdinalIgnoreCase)) return SaveDataImage(value,$"product-{legacyId}");
        if(!File.Exists(value)) return "";
        try
        {
            var ext=Path.GetExtension(value); if(string.IsNullOrWhiteSpace(ext)) ext=".jpg";
            var dir=Path.Combine(Database.AppDataDir,"Media","Products"); Directory.CreateDirectory(dir);
            var safe=new string(legacyId.Where(ch=>char.IsLetterOrDigit(ch)||ch=='-'||ch=='_').ToArray()); if(string.IsNullOrWhiteSpace(safe)) safe=Guid.NewGuid().ToString("N");
            var dest=Path.Combine(dir,$"import-{safe}{ext.ToLowerInvariant()}"); File.Copy(value,dest,true); return dest;
        }
        catch{return "";}
    }

    private static string SaveDataImage(string value,string name)
    {
        if(string.IsNullOrWhiteSpace(value))return "";
        if(File.Exists(value))return value;
        if(!value.StartsWith("data:image/",StringComparison.OrdinalIgnoreCase))return "";
        var comma=value.IndexOf(',');if(comma<0)return "";
        try
        {
            var meta=value[..comma];var bytes=Convert.FromBase64String(value[(comma+1)..]);var ext=meta.Contains("png",StringComparison.OrdinalIgnoreCase)?"png":"jpg";
            var dir=Path.Combine(Database.AppDataDir,"ImportedAssets");Directory.CreateDirectory(dir);var path=Path.Combine(dir,$"{name}.{ext}");File.WriteAllBytes(path,bytes);return path;
        }
        catch{return "";}
    }

    private static void UpsertSetting(SqliteConnection db,SqliteTransaction tx,string key,string value)
    {
        using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";cmd.Parameters.AddWithValue("$k",key);cmd.Parameters.AddWithValue("$v",value);cmd.ExecuteNonQuery();
    }

    private static void EnsureGeneralCustomer(SqliteConnection db, SqliteTransaction tx)
    {
        using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText=@"INSERT INTO parties(type,name,phone,address,debt,is_pinned,created_at)
SELECT 'CUSTOMER','مشتری عمومی','','',0,1,$at WHERE NOT EXISTS(SELECT 1 FROM parties WHERE type='CUSTOMER' AND name='مشتری عمومی')";cmd.Parameters.AddWithValue("$at",DateTime.Now.ToString("O"));cmd.ExecuteNonQuery();
    }

    private static string? CustomerName(SqliteConnection db, SqliteTransaction tx, long? id)
    {
        if(!id.HasValue)return null;using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="SELECT name FROM parties WHERE id=$id";cmd.Parameters.AddWithValue("$id",id.Value);return Convert.ToString(cmd.ExecuteScalar());
    }

    private static void AddLegacyMap(SqliteConnection db, SqliteTransaction tx, string type, string legacyId, long nativeId)
    {
        using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT OR REPLACE INTO legacy_id_map(entity_type,legacy_id,native_id) VALUES($t,$l,$n)";cmd.Parameters.AddWithValue("$t",type);cmd.Parameters.AddWithValue("$l",legacyId);cmd.Parameters.AddWithValue("$n",nativeId);cmd.ExecuteNonQuery();
    }

    private static string PaymentType(string raw) => raw.Contains("نسیه",StringComparison.OrdinalIgnoreCase) || raw.Contains("قرض",StringComparison.OrdinalIgnoreCase) || raw.Equals("CREDIT",StringComparison.OrdinalIgnoreCase) ? "CREDIT" : "CASH";
    private static long Money(double value) => checked((long)Math.Round(value, MidpointRounding.AwayFromZero));
}
