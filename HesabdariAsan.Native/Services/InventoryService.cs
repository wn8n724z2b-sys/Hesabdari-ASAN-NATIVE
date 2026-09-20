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
}
