using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;
using Microsoft.Data.Sqlite;

namespace HesabdariAsan.Native.Services;

public sealed class ProductService
{
    private const char BarcodeSeparator = '\u001F';

    public PagedResult<Product> Search(string? query, int page, int pageSize) => Search(query, "ALL", "ALL", page, pageSize);

    public PagedResult<Product> Search(string? query, string category, string stockFilter, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = pageSize is 10 or 20 or 50 ? pageSize : 10;
        query = (query ?? "").Trim();
        var filters = new List<string> { "p.is_active=1" };
        if (query.Length > 0)
            filters.Add("(p.name LIKE $like OR p.producer LIKE $like OR p.category LIKE $like OR p.barcode LIKE $like OR EXISTS(SELECT 1 FROM product_barcodes pb WHERE pb.product_id=p.id AND pb.barcode LIKE $like))");
        if (!string.IsNullOrWhiteSpace(category) && category != "ALL") filters.Add("p.category=$category");
        if (stockFilter == "LOW") filters.Add("p.stock<=p.min_stock");
        if (stockFilter == "PACKAGED") filters.Add("p.unit_conversion_enabled=1");
        var where = "WHERE " + string.Join(" AND ", filters);

        using var db = Database.Open();
        void AddFilters(SqliteCommand cmd)
        {
            if (query.Length > 0) cmd.Parameters.AddWithValue("$like", $"%{query}%");
            if (!string.IsNullOrWhiteSpace(category) && category != "ALL") cmd.Parameters.AddWithValue("$category", category);
        }

        int total;
        using (var count = db.CreateCommand())
        {
            count.CommandText = $"SELECT COUNT(*) FROM products p {where}";
            AddFilters(count);
            total = Convert.ToInt32(count.ExecuteScalar() ?? 0);
        }

        var items = new List<Product>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = $@"SELECT p.id,p.name,COALESCE(p.producer,''),COALESCE(p.barcode,''),p.category,
COALESCE(NULLIF(p.base_unit,''),NULLIF(p.unit,''),'دانه'),COALESCE(NULLIF(p.purchase_unit,''),COALESCE(NULLIF(p.base_unit,''),NULLIF(p.unit,''),'دانه')),
p.unit_conversion_enabled,p.units_per_purchase,p.package_buy_price,COALESCE(p.image_path,''),p.purchase_price,p.sale_price,p.stock,p.min_stock,p.is_active,
COALESCE((SELECT group_concat(x.barcode,char(31)) FROM (SELECT barcode FROM product_barcodes WHERE product_id=p.id ORDER BY is_primary DESC,id) x),'')
FROM products p {where} ORDER BY p.id DESC LIMIT $limit OFFSET $offset";
            AddFilters(cmd);
            cmd.Parameters.AddWithValue("$limit", pageSize);
            cmd.Parameters.AddWithValue("$offset", (page - 1) * pageSize);
            using var r = cmd.ExecuteReader();
            var i = 0;
            while (r.Read()) items.Add(ReadProduct(r, (page - 1) * pageSize + ++i));
        }
        return new PagedResult<Product> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Product? Get(long id)
    {
        using var db = Database.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"SELECT p.id,p.name,COALESCE(p.producer,''),COALESCE(p.barcode,''),p.category,
COALESCE(NULLIF(p.base_unit,''),NULLIF(p.unit,''),'دانه'),COALESCE(NULLIF(p.purchase_unit,''),COALESCE(NULLIF(p.base_unit,''),NULLIF(p.unit,''),'دانه')),
p.unit_conversion_enabled,p.units_per_purchase,p.package_buy_price,COALESCE(p.image_path,''),p.purchase_price,p.sale_price,p.stock,p.min_stock,p.is_active,
COALESCE((SELECT group_concat(x.barcode,char(31)) FROM (SELECT barcode FROM product_barcodes WHERE product_id=p.id ORDER BY is_primary DESC,id) x),'')
FROM products p WHERE p.id=$id LIMIT 1";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadProduct(r, 0) : null;
    }

    public Product? FindByBarcode(string barcode)
    {
        barcode = (barcode ?? "").Trim();
        if (barcode.Length == 0) return null;
        using var db = Database.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"SELECT p.id,p.name,COALESCE(p.producer,''),COALESCE(p.barcode,''),p.category,
COALESCE(NULLIF(p.base_unit,''),NULLIF(p.unit,''),'دانه'),COALESCE(NULLIF(p.purchase_unit,''),COALESCE(NULLIF(p.base_unit,''),NULLIF(p.unit,''),'دانه')),
p.unit_conversion_enabled,p.units_per_purchase,p.package_buy_price,COALESCE(p.image_path,''),p.purchase_price,p.sale_price,p.stock,p.min_stock,p.is_active,
COALESCE((SELECT group_concat(x.barcode,char(31)) FROM (SELECT barcode FROM product_barcodes WHERE product_id=p.id ORDER BY is_primary DESC,id) x),'')
FROM product_barcodes hit
JOIN products p ON p.id=hit.product_id
WHERE p.is_active=1 AND hit.barcode=$b
LIMIT 1";
        cmd.Parameters.AddWithValue("$b", barcode);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadProduct(r, 0) : null;
    }

    public long Save(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name)) throw new InvalidOperationException("نام کالا الزامی است.");
        product.Barcodes = product.Barcodes
            .Select(x => (x ?? "").Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(15).ToList();
        if (product.Barcodes.Count == 0 && !string.IsNullOrWhiteSpace(product.Barcode)) product.Barcodes.Add(product.Barcode.Trim());
        product.Barcode = product.Barcodes.FirstOrDefault() ?? "";
        product.BaseUnit = string.IsNullOrWhiteSpace(product.BaseUnit) ? (string.IsNullOrWhiteSpace(product.Unit) ? "دانه" : product.Unit.Trim()) : product.BaseUnit.Trim();
        product.Unit = product.BaseUnit;
        product.UnitsPerPurchase = product.UnitConversionEnabled ? Math.Max(1, product.UnitsPerPurchase) : 1;
        product.PurchaseUnit = product.UnitConversionEnabled ? (string.IsNullOrWhiteSpace(product.PurchaseUnit) ? "بسته" : product.PurchaseUnit.Trim()) : product.BaseUnit;
        product.PackageBuyPrice = product.UnitConversionEnabled ? Math.Max(0, product.PackageBuyPrice) : Math.Max(0, product.PurchasePrice);
        if (product.UnitConversionEnabled && product.PackageBuyPrice > 0)
            product.PurchasePrice = Math.Max(0, (long)Math.Round(product.PackageBuyPrice / product.UnitsPerPurchase, MidpointRounding.AwayFromZero));

        using var db = Database.Open();
        using var tx = db.BeginTransaction();
        EnsureBarcodeUniqueness(db, tx, product);
        long id;
        string action;
        if (product.Id == 0)
        {
            using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO products(name,producer,barcode,category,unit,base_unit,purchase_unit,unit_conversion_enabled,units_per_purchase,package_buy_price,image_path,purchase_price,sale_price,stock,min_stock,is_active,created_at)
VALUES($n,$producer,$b,$c,$u,$base,$purchase,$conv,$units,$pack,$img,$pp,$sp,$st,$ms,1,$at); SELECT last_insert_rowid();";
            Fill(cmd, product);
            cmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("O"));
            id = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
            action = "PRODUCT_CREATE";
            if (product.Stock > 0)
            {
                using var ledger = db.CreateCommand(); ledger.Transaction = tx;
                ledger.CommandText = @"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,note,created_at) VALUES($pid,'OPENING',$q,$q,'PRODUCT','موجودی اولیه',$at)";
                ledger.Parameters.AddWithValue("$pid", id); ledger.Parameters.AddWithValue("$q", product.Stock); ledger.Parameters.AddWithValue("$at", DateTime.Now.ToString("O")); ledger.ExecuteNonQuery();
            }
        }
        else
        {
            double oldStock;
            using (var old = db.CreateCommand()) { old.Transaction = tx; old.CommandText = "SELECT stock FROM products WHERE id=$id"; old.Parameters.AddWithValue("$id", product.Id); var value = old.ExecuteScalar(); if (value is null) throw new InvalidOperationException("کالا پیدا نشد."); oldStock = Convert.ToDouble(value); }
            using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = @"UPDATE products SET name=$n,producer=$producer,barcode=$b,category=$c,unit=$u,base_unit=$base,purchase_unit=$purchase,unit_conversion_enabled=$conv,units_per_purchase=$units,package_buy_price=$pack,image_path=$img,purchase_price=$pp,sale_price=$sp,stock=$st,min_stock=$ms WHERE id=$id";
            Fill(cmd, product); cmd.Parameters.AddWithValue("$id", product.Id); cmd.ExecuteNonQuery();
            id = product.Id; action = "PRODUCT_EDIT";
            var delta = product.Stock - oldStock;
            if (Math.Abs(delta) > 0.000001)
            {
                using var ledger = db.CreateCommand(); ledger.Transaction = tx;
                ledger.CommandText = @"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at) VALUES($pid,'ADJUSTMENT',$q,$bal,'PRODUCT',$pid,'ویرایش موجودی کالا',$at)";
                ledger.Parameters.AddWithValue("$pid", id); ledger.Parameters.AddWithValue("$q", delta); ledger.Parameters.AddWithValue("$bal", product.Stock); ledger.Parameters.AddWithValue("$at", DateTime.Now.ToString("O")); ledger.ExecuteNonQuery();
            }
        }

        using (var clear = db.CreateCommand()) { clear.Transaction = tx; clear.CommandText = "DELETE FROM product_barcodes WHERE product_id=$p"; clear.Parameters.AddWithValue("$p", id); clear.ExecuteNonQuery(); }
        for (var i = 0; i < product.Barcodes.Count; i++)
        {
            using var bc = db.CreateCommand(); bc.Transaction = tx;
            bc.CommandText = "INSERT INTO product_barcodes(product_id,barcode,is_primary) VALUES($p,$b,$primary)";
            bc.Parameters.AddWithValue("$p", id); bc.Parameters.AddWithValue("$b", product.Barcodes[i]); bc.Parameters.AddWithValue("$primary", i == 0 ? 1 : 0); bc.ExecuteNonQuery();
        }

        AuditService.Write(db, tx, action, "PRODUCT", id.ToString(), JsonSerializer.Serialize(new
        {
            product.Name, product.Producer, product.Barcodes, product.Category, product.BaseUnit, product.PurchaseUnit,
            product.UnitConversionEnabled, product.UnitsPerPurchase, product.PackageBuyPrice, product.PurchasePrice,
            product.SalePrice, product.Stock, product.MinStock
        }));
        tx.Commit();
        return id;
    }

    public void Delete(long id)
    {
        using var db = Database.Open(); using var tx = db.BeginTransaction(); string name;
        using (var q = db.CreateCommand()) { q.Transaction = tx; q.CommandText = "SELECT name FROM products WHERE id=$id"; q.Parameters.AddWithValue("$id", id); name = Convert.ToString(q.ExecuteScalar()) ?? ""; if (name.Length == 0) throw new InvalidOperationException("کالا پیدا نشد."); }
        using (var cmd = db.CreateCommand()) { cmd.Transaction = tx; cmd.CommandText = "UPDATE products SET is_active=0 WHERE id=$id"; cmd.Parameters.AddWithValue("$id", id); cmd.ExecuteNonQuery(); }
        AuditService.Write(db, tx, "PRODUCT_ARCHIVE", "PRODUCT", id.ToString(), JsonSerializer.Serialize(new { name })); tx.Commit();
    }

    private static void EnsureBarcodeUniqueness(SqliteConnection db, SqliteTransaction tx, Product product)
    {
        foreach (var barcode in product.Barcodes)
        {
            using var dup = db.CreateCommand(); dup.Transaction = tx;
            dup.CommandText = @"SELECT COUNT(*) FROM product_barcodes pb
JOIN products p ON p.id=pb.product_id
WHERE p.is_active=1 AND p.id<>$id AND pb.barcode=$b";
            dup.Parameters.AddWithValue("$b", barcode); dup.Parameters.AddWithValue("$id", product.Id);
            if (Convert.ToInt32(dup.ExecuteScalar() ?? 0) > 0) throw new InvalidOperationException($"بارکد «{barcode}» قبلاً برای کالای دیگری ثبت شده است.");
        }
    }

    private static void Fill(SqliteCommand cmd, Product p)
    {
        cmd.Parameters.AddWithValue("$n", p.Name.Trim());
        cmd.Parameters.AddWithValue("$producer", (p.Producer ?? "").Trim());
        cmd.Parameters.AddWithValue("$b", string.IsNullOrWhiteSpace(p.Barcode) ? DBNull.Value : p.Barcode.Trim());
        cmd.Parameters.AddWithValue("$c", string.IsNullOrWhiteSpace(p.Category) ? "عمومی" : p.Category.Trim());
        cmd.Parameters.AddWithValue("$u", p.BaseUnit);
        cmd.Parameters.AddWithValue("$base", p.BaseUnit);
        cmd.Parameters.AddWithValue("$purchase", p.PurchaseUnit);
        cmd.Parameters.AddWithValue("$conv", p.UnitConversionEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$units", Math.Max(1, p.UnitsPerPurchase));
        cmd.Parameters.AddWithValue("$pack", Math.Max(0, p.PackageBuyPrice));
        cmd.Parameters.AddWithValue("$img", string.IsNullOrWhiteSpace(p.ImagePath) ? DBNull.Value : p.ImagePath);
        cmd.Parameters.AddWithValue("$pp", Math.Max(0, p.PurchasePrice));
        cmd.Parameters.AddWithValue("$sp", Math.Max(0, p.SalePrice));
        cmd.Parameters.AddWithValue("$st", Math.Max(0, p.Stock));
        cmd.Parameters.AddWithValue("$ms", Math.Max(0, p.MinStock));
    }

    private static Product ReadProduct(SqliteDataReader r, int rowNumber)
    {
        var raw = r.GetString(16);
        var barcodes = string.IsNullOrWhiteSpace(raw) ? new List<string>() : raw.Split(BarcodeSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var primary = r.GetString(3);
        if (barcodes.Count == 0 && !string.IsNullOrWhiteSpace(primary)) barcodes.Add(primary);
        return new Product
        {
            Id = r.GetInt64(0), RowNumber = rowNumber, Name = r.GetString(1), Producer = r.GetString(2), Barcode = primary,
            Barcodes = barcodes, Category = r.GetString(4), Unit = r.GetString(5), BaseUnit = r.GetString(5), PurchaseUnit = r.GetString(6),
            UnitConversionEnabled = r.GetInt64(7) == 1, UnitsPerPurchase = r.GetDouble(8), PackageBuyPrice = r.GetInt64(9), ImagePath = r.GetString(10),
            PurchasePrice = r.GetInt64(11), SalePrice = r.GetInt64(12), Stock = r.GetDouble(13), MinStock = r.GetDouble(14), IsActive = r.GetInt64(15) == 1
        };
    }
}
