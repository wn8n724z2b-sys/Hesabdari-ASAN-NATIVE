using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class DashboardService
{
    public DashboardMetrics Load()
    {
        var start=DateTime.Today.ToString("O");using var db=Database.Open();
        long Scalar(string sql,bool day=false){using var c=db.CreateCommand();c.CommandText=sql;if(day)c.Parameters.AddWithValue("$d",start);return Convert.ToInt64(c.ExecuteScalar()??0L);}        
        var sales=Scalar("SELECT COALESCE(SUM(total),0) FROM invoices WHERE COALESCE(status,'ACTIVE')='ACTIVE' AND created_at >= $d",true);
        var expenses=Scalar("SELECT COALESCE(SUM(amount),0) FROM expenses WHERE created_at >= $d",true);
        var debt=Scalar("SELECT COALESCE(SUM(CASE WHEN debt>0 THEN debt ELSE 0 END),0) FROM parties WHERE type='CUSTOMER'");
        var inventory=Scalar("SELECT COALESCE(SUM(CAST(stock*purchase_price AS INTEGER)),0) FROM products WHERE is_active=1");
        var gross=Scalar(@"SELECT COALESCE(SUM(CAST((ii.unit_price-ii.purchase_price)*ii.qty AS INTEGER)),0)
FROM invoice_items ii JOIN invoices i ON i.id=ii.invoice_id WHERE COALESCE(i.status,'ACTIVE')='ACTIVE' AND i.created_at >= $d",true);
        return new DashboardMetrics{TodaySales=sales,TodayExpenses=expenses,CustomerDebt=debt,InventoryValue=inventory,TodayNetProfit=gross-expenses};
    }

    public IReadOnlyList<InvoiceSummary> RecentInvoices(int limit=10)
    {
        using var db=Database.Open();using var cmd=db.CreateCommand();cmd.CommandText=@"SELECT id,invoice_no,customer_name,payment_type,total,COALESCE(status,'ACTIVE'),created_at FROM invoices ORDER BY created_at DESC,id DESC LIMIT $l";cmd.Parameters.AddWithValue("$l",Math.Clamp(limit,1,50));var list=new List<InvoiceSummary>();using var r=cmd.ExecuteReader();var i=0;while(r.Read())list.Add(new InvoiceSummary{Id=r.GetInt64(0),RowNumber=++i,InvoiceNo=r.GetInt64(1),CustomerName=r.GetString(2),PaymentType=r.GetString(3),Total=r.GetInt64(4),Status=r.GetString(5),CreatedAt=DateTime.Parse(r.GetString(6))});return list;
    }
}
