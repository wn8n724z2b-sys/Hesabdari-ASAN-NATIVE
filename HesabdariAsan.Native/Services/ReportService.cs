using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class ReportService
{
    public IReadOnlyList<DailyFinancePoint> GetDailyTrend(int days=14)
    {
        days=Math.Clamp(days,7,90);var start=DateTime.Today.AddDays(-(days-1));
        var map=Enumerable.Range(0,days).ToDictionary(i=>start.AddDays(i).Date,d=>new DailyFinancePoint{Date=start.AddDays(d).Date});
        using var db=Database.Open();
        void Fill(string sql,Action<DailyFinancePoint,long> set)
        {
            using var c=db.CreateCommand();c.CommandText=sql;c.Parameters.AddWithValue("$s",start.ToString("O"));using var r=c.ExecuteReader();while(r.Read()){if(DateTime.TryParse(r.GetString(0),out var dt)&&map.TryGetValue(dt.Date,out var p))set(p,r.GetInt64(1));}
        }
        Fill("SELECT substr(created_at,1,10),COALESCE(SUM(total),0) FROM invoices WHERE COALESCE(status,'ACTIVE')='ACTIVE' AND created_at >= $s GROUP BY substr(created_at,1,10)",(p,v)=>p.Sales=v);
        Fill("SELECT substr(created_at,1,10),COALESCE(SUM(amount),0) FROM expenses WHERE created_at >= $s GROUP BY substr(created_at,1,10)",(p,v)=>p.Expenses=v);
        Fill("SELECT substr(created_at,1,10),COALESCE(SUM(total),0) FROM invoices WHERE COALESCE(status,'ACTIVE')='ACTIVE' AND payment_type='CREDIT' AND created_at >= $s GROUP BY substr(created_at,1,10)",(p,v)=>p.Debt=v);
        Fill("SELECT substr(created_at,1,10),COALESCE(SUM(amount),0) FROM customer_receipts WHERE created_at >= $s GROUP BY substr(created_at,1,10)",(p,v)=>p.Payments=v);
        var profit=new Dictionary<DateTime,long>();using(var c=db.CreateCommand()){c.CommandText=@"SELECT substr(i.created_at,1,10),COALESCE(SUM(CAST((ii.unit_price-ii.purchase_price)*ii.qty AS INTEGER)),0) FROM invoice_items ii JOIN invoices i ON i.id=ii.invoice_id WHERE COALESCE(i.status,'ACTIVE')='ACTIVE' AND i.created_at >= $s GROUP BY substr(i.created_at,1,10)";c.Parameters.AddWithValue("$s",start.ToString("O"));using var r=c.ExecuteReader();while(r.Read())if(DateTime.TryParse(r.GetString(0),out var dt))profit[dt.Date]=r.GetInt64(1);}
        foreach(var p in map.Values)p.NetProfit=profit.GetValueOrDefault(p.Date)-p.Expenses;
        return map.Values.OrderBy(x=>x.Date).ToList();
    }
}
