using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class InventoryService
{
    public PagedResult<InventoryRow> Search(string? query,int page,int pageSize)
    {
        page=Math.Max(1,page);pageSize=pageSize is 10 or 20 or 50? pageSize:10;query=(query??"").Trim();
        var where=query.Length==0?"WHERE is_active=1":"WHERE is_active=1 AND (name LIKE $like OR barcode LIKE $like OR category LIKE $like)";
        using var db=Database.Open();int total;using(var c=db.CreateCommand()){c.CommandText=$"SELECT COUNT(*) FROM products {where}";if(query.Length>0)c.Parameters.AddWithValue("$like",$"%{query}%");total=Convert.ToInt32(c.ExecuteScalar()??0);}
        var list=new List<InventoryRow>();using var cmd=db.CreateCommand();cmd.CommandText=$@"SELECT id,name,COALESCE(barcode,''),category,stock,min_stock,purchase_price FROM products {where} ORDER BY stock ASC,name LIMIT $l OFFSET $o";if(query.Length>0)cmd.Parameters.AddWithValue("$like",$"%{query}%");cmd.Parameters.AddWithValue("$l",pageSize);cmd.Parameters.AddWithValue("$o",(page-1)*pageSize);
        using var r=cmd.ExecuteReader();var i=0;while(r.Read())list.Add(new InventoryRow{RowNumber=(page-1)*pageSize+ ++i,ProductId=r.GetInt64(0),ProductName=r.GetString(1),Barcode=r.GetString(2),Category=r.GetString(3),Stock=r.GetDouble(4),MinStock=r.GetDouble(5),PurchasePrice=r.GetInt64(6)});
        return new PagedResult<InventoryRow>{Items=list,TotalCount=total,Page=page,PageSize=pageSize};
    }

    public (int TotalProducts,int LowStock,int OutOfStock,long InventoryValue) Summary()
    {
        using var db=Database.Open();
        long S(string sql){using var c=db.CreateCommand();c.CommandText=sql;return Convert.ToInt64(c.ExecuteScalar()??0L);}        
        return ((int)S("SELECT COUNT(*) FROM products WHERE is_active=1"),(int)S("SELECT COUNT(*) FROM products WHERE is_active=1 AND stock>0 AND stock<=min_stock"),(int)S("SELECT COUNT(*) FROM products WHERE is_active=1 AND stock<=0"),S("SELECT COALESCE(SUM(CAST(stock*purchase_price AS INTEGER)),0) FROM products WHERE is_active=1"));
    }
}
