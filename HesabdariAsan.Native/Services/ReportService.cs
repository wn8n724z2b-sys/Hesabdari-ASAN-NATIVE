using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;
using Microsoft.Data.Sqlite;

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
        var map=Enumerable.Range(0,days).ToDictionary(i=>start.AddDays(i).Date,i=>new DailyFinancePoint{Date=start.AddDays(i).Date});
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
        using var db=Database.Open();
        var result=new ReportPeriodSummary();
        var invoiceWhere=DateWhere("i",from,to,"COALESCE(i.status,'ACTIVE')='ACTIVE'");
        var plainWhere=DateWhere("",from,to,"1=1");

        using(var c=db.CreateCommand())
        {
            c.CommandText=$@"SELECT COALESCE(SUM(i.total),0),COUNT(*),
COALESCE(SUM(CASE WHEN i.payment_type='CASH' THEN i.total ELSE 0 END),0),
COALESCE(SUM(CASE WHEN i.payment_type='CREDIT' THEN i.total ELSE 0 END),0),
COALESCE(SUM(i.discount),0)
FROM invoices i WHERE {invoiceWhere}";
            AddDates(c,from,to);using var r=c.ExecuteReader();if(r.Read()){result.Sales=r.GetInt64(0);result.InvoiceCount=r.GetInt32(1);result.CashSales=r.GetInt64(2);result.CreditSales=r.GetInt64(3);result.DebtSales=result.CreditSales;result.Discounts=r.GetInt64(4);}
        }
        result.Expenses=ScalarLong(db,$"SELECT COALESCE(SUM(amount),0) FROM expenses WHERE {plainWhere}",from,to);
        result.CustomerReceipts=ScalarLong(db,$"SELECT COALESCE(SUM(amount),0) FROM customer_receipts WHERE {plainWhere}",from,to);
        result.Payments=result.CustomerReceipts;
        result.SupplierPayments=ScalarLong(db,$"SELECT COALESCE(SUM(amount),0) FROM supplier_payments WHERE {plainWhere}",from,to);
        result.Purchases=ScalarLong(db,$"SELECT COALESCE(SUM(total),0) FROM purchases WHERE {plainWhere}",from,to);
        var purchaseWhere=DateWhere("p",from,to,"p.payment_type='CASH'");
        result.CashPurchases=ScalarLong(db,$"SELECT COALESCE(SUM(p.total),0) FROM purchases p WHERE {purchaseWhere}",from,to);

        var profitWhere=DateWhere("i",from,to,"COALESCE(i.status,'ACTIVE')='ACTIVE'");
        using(var c=db.CreateCommand())
        {
            c.CommandText=$@"SELECT
COALESCE(SUM(CAST(ii.purchase_price*ii.qty AS INTEGER)),0),
COALESCE(SUM(CAST((ii.unit_price-ii.purchase_price)*ii.qty AS INTEGER)),0)
FROM invoice_items ii JOIN invoices i ON i.id=ii.invoice_id WHERE {profitWhere}";
            AddDates(c,from,to);using var r=c.ExecuteReader();if(r.Read()){result.CostOfGoods=r.GetInt64(0);result.GrossProfit=r.GetInt64(1);}
        }
        result.NetProfit=result.GrossProfit-result.Expenses;
        result.CashIn=result.CashSales+result.CustomerReceipts;
        result.CashOut=result.Expenses+result.SupplierPayments+result.CashPurchases;
        result.NetCash=result.CashIn-result.CashOut;
        return result;
    }

    public ReportBalanceSnapshot GetCurrentBalances()
    {
        using var db=Database.Open();
        var now=DateTime.Today;var month=new DateTime(now.Year,now.Month,1);
        var monthSummary=GetSummary(month,now.AddDays(1));
        var todaySummary=GetSummary(now,now.AddDays(1));
        return new ReportBalanceSnapshot
        {
            CustomerDebt=ScalarLong(db,"SELECT COALESCE(SUM(debt),0) FROM parties WHERE type='CUSTOMER'",null,null),
            SupplierDebt=ScalarLong(db,"SELECT COALESCE(SUM(debt),0) FROM parties WHERE type='COMPANY'",null,null),
            InventoryValue=ScalarRounded(db,"SELECT COALESCE(SUM(stock*purchase_price),0) FROM products WHERE is_active=1"),
            CashMonth=monthSummary.NetCash,
            CashToday=todaySummary.NetCash
        };
    }

    public FinancialPeriodInfo GetCurrentFinancialPeriod()
    {
        EnsureOpenPeriod();
        using var db=Database.Open();using var c=db.CreateCommand();c.CommandText="SELECT id,period_no,started_at,ended_at FROM financial_periods WHERE ended_at IS NULL ORDER BY period_no DESC LIMIT 1";using var r=c.ExecuteReader();
        if(!r.Read())return new FinancialPeriodInfo();
        var start=DateTime.TryParse(r.GetString(2),out var d)?d:DateTime.Today;
        var months=MonthCount(start,DateTime.Now);var due=start.AddMonths(6);var total=Math.Max(1,(due-start).TotalSeconds);var elapsed=(DateTime.Now-start).TotalSeconds;
        return new FinancialPeriodInfo{Id=r.GetInt64(0),PeriodNo=r.GetInt32(1),StartedAt=start,EndedAt=r.IsDBNull(3)?null:DateTime.Parse(r.GetString(3)),MonthNumber=months,ProgressPercent=Math.Clamp(elapsed/total*100,0,100),IsPastRecommendedLength=DateTime.Now>=due};
    }

    public FinancialPeriodInfo StartNewFinancialPeriod()
    {
        var current=GetCurrentFinancialPeriod();var summary=GetSummary(current.StartedAt,DateTime.Now.AddTicks(1));var balances=GetCurrentBalances();
        var snapshot=JsonSerializer.Serialize(new{sales=summary.Sales,netProfit=summary.NetProfit,cashFlow=summary.NetCash,customerDebt=balances.CustomerDebt,supplierDebt=balances.SupplierDebt,months=current.MonthNumber});
        using var db=Database.Open();using var tx=db.BeginTransaction();var now=DateTime.Now.ToString("O");
        using(var close=db.CreateCommand()){close.Transaction=tx;close.CommandText="UPDATE financial_periods SET ended_at=$e,summary_json=$j WHERE id=$id AND ended_at IS NULL";close.Parameters.AddWithValue("$e",now);close.Parameters.AddWithValue("$j",snapshot);close.Parameters.AddWithValue("$id",current.Id);close.ExecuteNonQuery();}
        var nextNo=current.PeriodNo+1;using(var ins=db.CreateCommand()){ins.Transaction=tx;ins.CommandText="INSERT INTO financial_periods(period_no,started_at,ended_at,summary_json) VALUES($n,$s,NULL,NULL)";ins.Parameters.AddWithValue("$n",nextNo);ins.Parameters.AddWithValue("$s",now);ins.ExecuteNonQuery();}
        AuditService.Write(db,tx,"FINANCIAL_PERIOD_START","FINANCIAL_PERIOD",nextNo.ToString(),JsonSerializer.Serialize(new{previous=current.PeriodNo,newPeriod=nextNo}));tx.Commit();return GetCurrentFinancialPeriod();
    }

    public DateTime? CurrentFinancialPeriodStart() => GetCurrentFinancialPeriod().StartedAt;

    private static void EnsureOpenPeriod()
    {
        using var db=Database.Open();using var c=db.CreateCommand();c.CommandText=@"INSERT OR IGNORE INTO financial_periods(period_no,started_at,ended_at,summary_json)
SELECT COALESCE((SELECT MAX(period_no) FROM financial_periods),0)+1,$s,NULL,NULL
WHERE NOT EXISTS(SELECT 1 FROM financial_periods WHERE ended_at IS NULL);";c.Parameters.AddWithValue("$s",DateTime.Now.ToString("O"));c.ExecuteNonQuery();
    }

    private static string DateWhere(string alias,DateTime? from,DateTime? to,string initial)
    {
        var prefix=string.IsNullOrWhiteSpace(alias)?"":alias+".";var q=initial;if(from is not null)q+=$" AND {prefix}created_at >= $s";if(to is not null)q+=$" AND {prefix}created_at < $e";return q;
    }
    private static void AddDates(SqliteCommand c,DateTime? from,DateTime? to){if(from is not null)c.Parameters.AddWithValue("$s",from.Value.ToString("O"));if(to is not null)c.Parameters.AddWithValue("$e",to.Value.ToString("O"));}
    private static long ScalarLong(SqliteConnection db,string sql,DateTime? from,DateTime? to){using var c=db.CreateCommand();c.CommandText=sql;AddDates(c,from,to);return Convert.ToInt64(c.ExecuteScalar()??0L);}
    private static long ScalarRounded(SqliteConnection db,string sql){using var c=db.CreateCommand();c.CommandText=sql;return Convert.ToInt64(Math.Round(Convert.ToDouble(c.ExecuteScalar()??0d)));}
    private static int MonthCount(DateTime start,DateTime now){var months=(now.Year-start.Year)*12+(now.Month-start.Month);if(now.Day<start.Day)months--;return Math.Max(1,months+1);}
}
