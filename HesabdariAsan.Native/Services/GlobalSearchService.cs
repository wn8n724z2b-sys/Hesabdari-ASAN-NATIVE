using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class GlobalSearchService
{
    public IReadOnlyList<SearchHit> Search(string query,int limit=10)
    {
        query=(query??"").Trim();if(query.Length<1)return Array.Empty<SearchHit>();
        var hits=new List<SearchHit>();using var db=Database.Open();
        using(var cmd=db.CreateCommand())
        {
            cmd.CommandText=@"SELECT name,COALESCE(barcode,''),sale_price FROM products WHERE is_active=1 AND (name LIKE $q OR barcode LIKE $q) ORDER BY name LIMIT $l";cmd.Parameters.AddWithValue("$q",$"%{query}%");cmd.Parameters.AddWithValue("$l",limit);using var r=cmd.ExecuteReader();while(r.Read())hits.Add(new SearchHit{Kind="کالا",Title=r.GetString(0),Subtitle=$"{r.GetString(1)} · {r.GetInt64(2):N0} ؋",TargetPage="Products"});
        }
        if(hits.Count<limit)using(var cmd=db.CreateCommand())
        {
            cmd.CommandText=@"SELECT name,COALESCE(phone,''),type FROM parties WHERE name LIKE $q OR phone LIKE $q ORDER BY is_pinned DESC,name LIMIT $l";cmd.Parameters.AddWithValue("$q",$"%{query}%");cmd.Parameters.AddWithValue("$l",limit-hits.Count);using var r=cmd.ExecuteReader();while(r.Read())hits.Add(new SearchHit{Kind=r.GetString(2)=="COMPANY"?"شرکت":"مشتری",Title=r.GetString(0),Subtitle=r.GetString(1),TargetPage="Parties"});
        }
        if(hits.Count<limit)using(var cmd=db.CreateCommand())
        {
            cmd.CommandText=@"SELECT invoice_no,customer_name,total FROM invoices WHERE CAST(invoice_no AS TEXT) LIKE $q OR customer_name LIKE $q ORDER BY id DESC LIMIT $l";cmd.Parameters.AddWithValue("$q",$"%{query}%");cmd.Parameters.AddWithValue("$l",limit-hits.Count);using var r=cmd.ExecuteReader();while(r.Read())hits.Add(new SearchHit{Kind="فاکتور",Title=$"فاکتور {r.GetInt64(0)}",Subtitle=$"{r.GetString(1)} · {r.GetInt64(2):N0} ؋",TargetPage="Reports"});
        }
        return hits;
    }
}
