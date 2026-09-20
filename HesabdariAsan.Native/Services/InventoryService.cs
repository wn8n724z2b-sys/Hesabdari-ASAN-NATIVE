using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class InventoryService
{
    public PagedResult<InventoryRow> Search(string? query,int page,int pageSize) => Search(query,"ALL",page,pageSize);

    public PagedResult<InventoryRow> Search(string? query,string status,int page,int pageSize)
    {
        page=Math.Max(1,page);pageSize=pageSize is 10 or 20 or 50? pageSize:10;query=(query??"").Trim();
        var filters=new List<string>{"is_active=1"};
        if(query.Length>0)filters.Add("(name LIKE $like OR barcode LIKE $like OR category LIKE $like)");
        filters.Add(status switch{"OUT"=>"stock<=0","LOW"=>"stock>0 AND stock<=min_stock","OK"=>"stock>min_stock",_=>"1=1"});
        var where="WHERE "+string.Join(" AND ",filters);
        using var db=Database.Open();int total;using(var c=db.CreateCommand()){c.CommandText=$"SELECT COUNT(*) FROM products {where}";if(query.Length>0)c.Parameters.AddWithValue("$like",$"%{query}%");total=Convert.ToInt32(c.ExecuteScalar()??0);}
        var list=new List<InventoryRow>();using var cmd=db.CreateCommand();cmd.CommandText=$@"SELECT id,name,COALESCE(barcode,''),category,stock,min_stock,purchase_price,sale_price FROM products {where} ORDER BY stock ASC,name LIMIT $l OFFSET $o";if(query.Length>0)cmd.Parameters.AddWithValue("$like",$"%{query}%");cmd.Parameters.AddWithValue("$l",pageSize);cmd.Parameters.AddWithValue("$o",(page-1)*pageSize);
        using var r=cmd.ExecuteReader();var i=0;while(r.Read())list.Add(new InventoryRow{RowNumber=(page-1)*pageSize+ ++i,ProductId=r.GetInt64(0),ProductName=r.GetString(1),Barcode=r.GetString(2),Category=r.GetString(3),Stock=r.GetDouble(4),MinStock=r.GetDouble(5),PurchasePrice=r.GetInt64(6),SalePrice=r.GetInt64(7)});
        return new PagedResult<InventoryRow>{Items=list,TotalCount=total,Page=page,PageSize=pageSize};
    }

    public IReadOnlyList<InventoryLedgerEntry> RecentLedger(int limit=10)
    {
        limit=Math.Clamp(limit,1,50);
        using var db=Database.Open();
        using var cmd=db.CreateCommand();
        cmd.CommandText=@"SELECT l.created_at,p.name,l.movement_type,l.qty,COALESCE(l.reference_type,''),COALESCE(l.note,'')
FROM inventory_ledger l JOIN products p ON p.id=l.product_id
ORDER BY l.created_at DESC,l.id DESC LIMIT $l";
        cmd.Parameters.AddWithValue("$l",limit);
        var list=new List<InventoryLedgerEntry>();
        using var r=cmd.ExecuteReader();var i=0;
        while(r.Read())list.Add(new InventoryLedgerEntry{RowNumber=++i,CreatedAt=DateTime.Parse(r.GetString(0)),ProductName=r.GetString(1),MovementType=r.GetString(2),Quantity=r.GetDouble(3),ReferenceType=r.GetString(4),Note=r.GetString(5)});
        return list;
    }

    public (int TotalProducts,double TotalUnits,int LowStock,int OutOfStock,long InventoryValue) Summary()
    {
        using var db=Database.Open();
        long L(string sql){using var c=db.CreateCommand();c.CommandText=sql;return Convert.ToInt64(c.ExecuteScalar()??0L);}
        double D(string sql){using var c=db.CreateCommand();c.CommandText=sql;return Convert.ToDouble(c.ExecuteScalar()??0d);}
        return ((int)L("SELECT COUNT(*) FROM products WHERE is_active=1"),D("SELECT COALESCE(SUM(stock),0) FROM products WHERE is_active=1"),(int)L("SELECT COUNT(*) FROM products WHERE is_active=1 AND stock>0 AND stock<=min_stock"),(int)L("SELECT COUNT(*) FROM products WHERE is_active=1 AND stock<=0"),L("SELECT COALESCE(SUM(CAST(stock*purchase_price AS INTEGER)),0) FROM products WHERE is_active=1"));
    }
    public void Adjust(long productId, double newStock, string? reason)
    {
        if (newStock < 0) throw new InvalidOperationException("موجودی نمی‌تواند منفی باشد.");
        reason = (reason ?? "").Trim();
        if (reason.Length == 0) throw new InvalidOperationException("دلیل اصلاح موجودی را وارد کنید.");
        using var db = Database.Open();
        using var tx = db.BeginTransaction();
        string productName;
        double oldStock;
        using (var read = db.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT name,stock FROM products WHERE id=$id AND is_active=1";
            read.Parameters.AddWithValue("$id", productId);
            using var r = read.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("کالای انتخاب‌شده پیدا نشد.");
            productName = r.GetString(0); oldStock = r.GetDouble(1);
        }
        var delta = newStock - oldStock;
        if (Math.Abs(delta) < 0.000001) throw new InvalidOperationException("موجودی جدید با موجودی فعلی یکسان است.");
        using (var update = db.CreateCommand())
        {
            update.Transaction = tx; update.CommandText = "UPDATE products SET stock=$stock WHERE id=$id";
            update.Parameters.AddWithValue("$stock", newStock); update.Parameters.AddWithValue("$id", productId); update.ExecuteNonQuery();
        }
        using (var ledger = db.CreateCommand())
        {
            ledger.Transaction = tx;
            ledger.CommandText = @"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at)
VALUES($pid,'ADJUSTMENT',$qty,$bal,'MANUAL',NULL,$note,$at)";
            ledger.Parameters.AddWithValue("$pid", productId); ledger.Parameters.AddWithValue("$qty", delta); ledger.Parameters.AddWithValue("$bal", newStock);
            ledger.Parameters.AddWithValue("$note", reason); ledger.Parameters.AddWithValue("$at", DateTime.Now.ToString("O")); ledger.ExecuteNonQuery();
        }
        AuditService.Write(db, tx, "INVENTORY_ADJUST", "PRODUCT", productId.ToString(), JsonSerializer.Serialize(new { productId, productName, oldStock, newStock, delta, reason }));
        tx.Commit();
    }

}
