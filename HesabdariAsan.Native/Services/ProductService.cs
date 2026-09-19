using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class ProductService
{
    public PagedResult<Product> Search(string? query, int page, int pageSize)
    {
        page = Math.Max(1, page); pageSize = pageSize is 10 or 20 or 50 ? pageSize : 10; query = (query ?? "").Trim();
        var where = string.IsNullOrWhiteSpace(query) ? "WHERE is_active=1" : "WHERE is_active=1 AND (name LIKE $like OR barcode LIKE $like OR category LIKE $like)";
        using var db = Database.Open();
        int total;
        using (var count = db.CreateCommand()) { count.CommandText = $"SELECT COUNT(*) FROM products {where}"; if (!string.IsNullOrWhiteSpace(query)) count.Parameters.AddWithValue("$like", $"%{query}%"); total = Convert.ToInt32(count.ExecuteScalar() ?? 0); }
        var items = new List<Product>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = $@"SELECT id,name,COALESCE(barcode,''),category,unit,purchase_price,sale_price,stock,min_stock,is_active FROM products {where} ORDER BY id DESC LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(query)) cmd.Parameters.AddWithValue("$like", $"%{query}%"); cmd.Parameters.AddWithValue("$limit", pageSize); cmd.Parameters.AddWithValue("$offset", (page - 1) * pageSize);
            using var r = cmd.ExecuteReader(); var i = 0;
            while (r.Read()) items.Add(new Product { Id=r.GetInt64(0), RowNumber=(page-1)*pageSize+ ++i, Name=r.GetString(1), Barcode=r.GetString(2), Category=r.GetString(3), Unit=r.GetString(4), PurchasePrice=r.GetInt64(5), SalePrice=r.GetInt64(6), Stock=r.GetDouble(7), MinStock=r.GetDouble(8), IsActive=r.GetInt64(9)==1 });
        }
        return new PagedResult<Product> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public Product? FindByBarcode(string barcode)
    {
        barcode = barcode.Trim(); if (barcode.Length == 0) return null;
        using var db = Database.Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = @"SELECT id,name,COALESCE(barcode,''),category,unit,purchase_price,sale_price,stock,min_stock,is_active FROM products WHERE is_active=1 AND barcode=$b LIMIT 1"; cmd.Parameters.AddWithValue("$b", barcode);
        using var r = cmd.ExecuteReader(); if (!r.Read()) return null;
        return new Product { Id=r.GetInt64(0),Name=r.GetString(1),Barcode=r.GetString(2),Category=r.GetString(3),Unit=r.GetString(4),PurchasePrice=r.GetInt64(5),SalePrice=r.GetInt64(6),Stock=r.GetDouble(7),MinStock=r.GetDouble(8),IsActive=r.GetInt64(9)==1 };
    }

    public long Save(Product product)
    {
        if(string.IsNullOrWhiteSpace(product.Name))throw new InvalidOperationException("نام کالا الزامی است.");
        using var db = Database.Open(); using var tx = db.BeginTransaction(); long id; string action;
        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            using var dup = db.CreateCommand(); dup.Transaction = tx;
            dup.CommandText = "SELECT COUNT(*) FROM products WHERE is_active=1 AND barcode=$b AND id<>$id";
            dup.Parameters.AddWithValue("$b", product.Barcode.Trim()); dup.Parameters.AddWithValue("$id", product.Id);
            if (Convert.ToInt32(dup.ExecuteScalar() ?? 0) > 0) throw new InvalidOperationException("این بارکد قبلاً برای کالای دیگری ثبت شده است.");
        }
        if (product.Id == 0)
        {
            using var cmd = db.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO products(name,barcode,category,unit,purchase_price,sale_price,stock,min_stock,is_active,created_at) VALUES($n,$b,$c,$u,$pp,$sp,$st,$ms,1,$at); SELECT last_insert_rowid();";
            Fill(cmd, product); cmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("O")); id = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L); action="PRODUCT_CREATE";
            using var ledger = db.CreateCommand(); ledger.Transaction = tx; ledger.CommandText = @"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,note,created_at) VALUES($pid,'OPENING',$q,$q,'PRODUCT','موجودی اولیه',$at)"; ledger.Parameters.AddWithValue("$pid", id); ledger.Parameters.AddWithValue("$q", product.Stock); ledger.Parameters.AddWithValue("$at", DateTime.Now.ToString("O")); ledger.ExecuteNonQuery();
        }
        else
        {
            double oldStock; using (var old = db.CreateCommand()) { old.Transaction=tx; old.CommandText="SELECT stock FROM products WHERE id=$id"; old.Parameters.AddWithValue("$id", product.Id); var value=old.ExecuteScalar(); if(value is null)throw new InvalidOperationException("کالا پیدا نشد."); oldStock=Convert.ToDouble(value); }
            using var cmd = db.CreateCommand(); cmd.Transaction=tx; cmd.CommandText=@"UPDATE products SET name=$n,barcode=$b,category=$c,unit=$u,purchase_price=$pp,sale_price=$sp,stock=$st,min_stock=$ms WHERE id=$id"; Fill(cmd,product); cmd.Parameters.AddWithValue("$id",product.Id); cmd.ExecuteNonQuery(); id=product.Id; action="PRODUCT_EDIT";
            var delta=product.Stock-oldStock; if(Math.Abs(delta)>0.000001){using var ledger=db.CreateCommand();ledger.Transaction=tx;ledger.CommandText=@"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at) VALUES($pid,'ADJUSTMENT',$q,$bal,'PRODUCT',$pid,'ویرایش موجودی کالا',$at)";ledger.Parameters.AddWithValue("$pid",id);ledger.Parameters.AddWithValue("$q",delta);ledger.Parameters.AddWithValue("$bal",product.Stock);ledger.Parameters.AddWithValue("$at",DateTime.Now.ToString("O"));ledger.ExecuteNonQuery();}
        }
        AuditService.Write(db,tx,action,"PRODUCT",id.ToString(),JsonSerializer.Serialize(new{product.Name,product.Barcode,product.Category,product.Unit,product.PurchasePrice,product.SalePrice,product.Stock,product.MinStock})); tx.Commit(); return id;
    }

    public void Delete(long id)
    {
        using var db=Database.Open();using var tx=db.BeginTransaction();string name;using(var q=db.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT name FROM products WHERE id=$id";q.Parameters.AddWithValue("$id",id);name=Convert.ToString(q.ExecuteScalar())??"";if(name.Length==0)throw new InvalidOperationException("کالا پیدا نشد.");}
        using(var cmd=db.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="UPDATE products SET is_active=0 WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();}
        AuditService.Write(db,tx,"PRODUCT_ARCHIVE","PRODUCT",id.ToString(),JsonSerializer.Serialize(new{name}));tx.Commit();
    }

    private static void Fill(Microsoft.Data.Sqlite.SqliteCommand cmd, Product p)
    {
        cmd.Parameters.AddWithValue("$n", p.Name.Trim()); cmd.Parameters.AddWithValue("$b", string.IsNullOrWhiteSpace(p.Barcode) ? DBNull.Value : p.Barcode.Trim()); cmd.Parameters.AddWithValue("$c", string.IsNullOrWhiteSpace(p.Category) ? "عمومی" : p.Category.Trim()); cmd.Parameters.AddWithValue("$u", string.IsNullOrWhiteSpace(p.Unit) ? "عدد" : p.Unit.Trim()); cmd.Parameters.AddWithValue("$pp", Math.Max(0, p.PurchasePrice)); cmd.Parameters.AddWithValue("$sp", Math.Max(0, p.SalePrice)); cmd.Parameters.AddWithValue("$st", Math.Max(0,p.Stock)); cmd.Parameters.AddWithValue("$ms", Math.Max(0, p.MinStock));
    }
}
