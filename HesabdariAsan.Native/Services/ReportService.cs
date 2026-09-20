using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class ReportService
{
    public IReadOnlyList<DailyFinancePoint> GetDailyTrend(int days=14)
    {
        days=Math.Clamp(days,7,90);var from=DateTime.Today.AddDays(-(days-1));var to=DateTime.Today.AddDays(1);return GetDailyTrend(from,to);
    }

    public IReadOnlyList<DailyFinancePoint> GetDailyTrend(DateTime from,DateTime to)
    {
        from=from.Date;to=to.Date;if(to<=from)to=from.AddDays(1);var days=Math.Clamp((int)Math.Ceiling((to-from).TotalDays),1,90);var start=to.AddDays(-days);
        var map=Enumerable.Range(0,days).ToDictionary(i=>start.AddDays(i).Date,d=>new DailyFinancePoint{Date=start.AddDays(d).Date});
        using var db=Database.Open();
        void Fill(string sql,Action<DailyFinancePoint,long> set)
        {
            using var c=db.CreateCommand();c.CommandText=sql;c.Parameters.AddWithValue("$s",start.ToString("O"));c.Parameters.AddWithValue("$e",to.ToString("O"));using var r=c.ExecuteReader();while(r.Read()){if(DateTime.TryParse(r.GetString(0),out var dt)&&map.TryGetValue(dt.Date,out var p))set(p,r.GetInt64(1));}
        }
        Fill("SELECT substr(created_at,1,10),COALESCE(SUM(total),0) FROM invoices WHERE COALESCE(status,'ACTIVE')='ACTIVE' AND created_at >= $s AND created_at < $e GROUP BY substr(created_at,1,10)",(p,v)=>p.Sales=v);
        Fill("SELECT substr(created_at,1,10),COALESCE(SUM(amount),0) FROM expenses WHERE created_at >= $s AND created_at < $e GROUP BY substr(created_at,1,10)",(p,v)=>p.Expenses=v);
        Fill("SELECT substr(created_at,1,10),COALESCE(SUM(total),0) FROM invoices WHERE COALESCE(status,'ACTIVE')='ACTIVE' AND payment_type='CREDIT' AND created_at >= $s AND created_at < $e GROUP BY substr(created_at,1,10)",(p,v)=>p.Debt=v);
        Fill("SELECT substr(created_at,1,10),COALESCE(SUM(amount),0) FROM customer_receipts WHERE created_at >= $s AND created_at < $e GROUP BY substr(created_at,1,10)",(p,v)=>p.Payments=v);
        var profit=new Dictionary<DateTime,long>();using(var c=db.CreateCommand()){c.CommandText=@"SELECT substr(i.created_at,1,10),COALESCE(SUM(CAST((ii.unit_price-ii.purchase_price)*ii.qty AS INTEGER)),0) FROM invoice_items ii JOIN invoices i ON i.id=ii.invoice_id WHERE COALESCE(i.status,'ACTIVE')='ACTIVE' AND i.created_at >= $s AND i.created_at < $e GROUP BY substr(i.created_at,1,10)";c.Parameters.AddWithValue("$s",start.ToString("O"));c.Parameters.AddWithValue("$e",to.ToString("O"));using var r=c.ExecuteReader();while(r.Read())if(DateTime.TryParse(r.GetString(0),out var dt))profit[dt.Date]=r.GetInt64(1);}
        foreach(var p in map.Values)p.NetProfit=profit.GetValueOrDefault(p.Date)-p.Expenses;
        return map.Values.OrderBy(x=>x.Date).ToList();
    }

    public ReportPeriodSummary GetSummary(DateTime? from,DateTime? to)
    {
        using var db=Database.Open();var result=new ReportPeriodSummary();var conditions="COALESCE(status,'ACTIVE')='ACTIVE'";if(from is not null)conditions+=" AND created_at >= $s";if(to is not null)conditions+=" AND created_at < $e";
        void Dates(Microsoft.Data.Sqlite.SqliteCommand c){if(from is not null)c.Parameters.AddWithValue("$s",from.Value.ToString("O"));if(to is not null)c.Parameters.AddWithValue("$e",to.Value.ToString("O"));}
        using(var c=db.CreateCommand()){c.CommandText=$"SELECT COALESCE(SUM(total),0),COUNT(*) FROM invoices WHERE {conditions}";Dates(c);using var r=c.ExecuteReader();if(r.Read()){result.Sales=r.GetInt64(0);result.InvoiceCount=r.GetInt32(1);}}
        using(var c=db.CreateCommand()){var q="1=1";if(from is not null)q+=" AND created_at >= $s";if(to is not null)q+=" AND created_at < $e";c.CommandText=$"SELECT COALESCE(SUM(amount),0) FROM expenses WHERE {q}";Dates(c);result.Expenses=Convert.ToInt64(c.ExecuteScalar()??0L);}
        using(var c=db.CreateCommand()){c.CommandText=$"SELECT COALESCE(SUM(total),0) FROM invoices WHERE {conditions} AND payment_type='CREDIT'";Dates(c);result.DebtSales=Convert.ToInt64(c.ExecuteScalar()??0L);}
        using(var c=db.CreateCommand()){var q="1=1";if(from is not null)q+=" AND created_at >= $s";if(to is not null)q+=" AND created_at < $e";c.CommandText=$"SELECT COALESCE(SUM(amount),0) FROM customer_receipts WHERE {q}";Dates(c);result.Payments=Convert.ToInt64(c.ExecuteScalar()??0L);}
        using(var c=db.CreateCommand()){var q="COALESCE(i.status,'ACTIVE')='ACTIVE'";if(from is not null)q+=" AND i.created_at >= $s";if(to is not null)q+=" AND i.created_at < $e";c.CommandText=$"SELECT COALESCE(SUM(CAST((ii.unit_price-ii.purchase_price)*ii.qty AS INTEGER)),0) FROM invoice_items ii JOIN invoices i ON i.id=ii.invoice_id WHERE {q}";Dates(c);var gross=Convert.ToInt64(c.ExecuteScalar()??0L);result.NetProfit=gross-result.Expenses;}
        return result;
    }

    public DateTime? CurrentFinancialPeriodStart()
    {
        using var db=Database.Open();using var c=db.CreateCommand();c.CommandText="SELECT started_at FROM financial_periods WHERE ended_at IS NULL ORDER BY period_no DESC LIMIT 1";var v=Convert.ToString(c.ExecuteScalar());return DateTime.TryParse(v,out var d)?d:null;
    }
}
