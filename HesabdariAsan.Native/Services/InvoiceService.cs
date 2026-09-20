using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class InvoiceService
{
    public PagedResult<InvoiceSummary> Search(string? query,int page,int pageSize) => Search(query,"ALL",null,null,page,pageSize);

    public PagedResult<InvoiceSummary> Search(string? query,string paymentType,DateTime? from,DateTime? to,int page,int pageSize)
    {
        page=Math.Max(1,page);pageSize=pageSize is 10 or 20 or 50? pageSize:10;query=(query??"").Trim();
        var filters=new List<string>();
        if(query.Length>0)filters.Add("(customer_name LIKE $like OR CAST(invoice_no AS TEXT) LIKE $like OR CAST(total AS TEXT) LIKE $like)");
        if(paymentType is "CASH" or "CREDIT")filters.Add("payment_type=$payment");
        if(from is not null)filters.Add("created_at >= $from");if(to is not null)filters.Add("created_at < $to");
        var where=filters.Count==0?"":"WHERE "+string.Join(" AND ",filters);
        using var db=Database.Open();
        void Add(Microsoft.Data.Sqlite.SqliteCommand c){if(query.Length>0)c.Parameters.AddWithValue("$like",$"%{query}%");if(paymentType is "CASH" or "CREDIT")c.Parameters.AddWithValue("$payment",paymentType);if(from is not null)c.Parameters.AddWithValue("$from",from.Value.ToString("O"));if(to is not null)c.Parameters.AddWithValue("$to",to.Value.ToString("O"));}
        int total;using(var c=db.CreateCommand()){c.CommandText=$"SELECT COUNT(*) FROM invoices {where}";Add(c);total=Convert.ToInt32(c.ExecuteScalar()??0);}
        var list=new List<InvoiceSummary>();using var cmd=db.CreateCommand();cmd.CommandText=$@"SELECT id,invoice_no,customer_name,payment_type,total,COALESCE(status,'ACTIVE'),created_at FROM invoices {where} ORDER BY created_at DESC,id DESC LIMIT $l OFFSET $o";Add(cmd);cmd.Parameters.AddWithValue("$l",pageSize);cmd.Parameters.AddWithValue("$o",(page-1)*pageSize);
        using var r=cmd.ExecuteReader();var i=0;while(r.Read())list.Add(new InvoiceSummary{Id=r.GetInt64(0),RowNumber=(page-1)*pageSize+ ++i,InvoiceNo=r.GetInt64(1),CustomerName=r.GetString(2),PaymentType=r.GetString(3),Total=r.GetInt64(4),Status=r.GetString(5),CreatedAt=DateTime.Parse(r.GetString(6))});
        return new PagedResult<InvoiceSummary>{Items=list,TotalCount=total,Page=page,PageSize=pageSize};
    }

    public InvoiceDetail? GetByInvoiceNo(long invoiceNo)
    {
        using var db=Database.Open();
        InvoiceDetail? detail=null;
        using(var cmd=db.CreateCommand())
        {
            cmd.CommandText=@"SELECT id,invoice_no,customer_id,customer_name,payment_type,subtotal,discount,total,paid,COALESCE(status,'ACTIVE'),COALESCE(cancel_reason,''),created_at
FROM invoices WHERE invoice_no=$no LIMIT 1";
            cmd.Parameters.AddWithValue("$no",invoiceNo);
            using var r=cmd.ExecuteReader();
            if(!r.Read())return null;
            detail=new InvoiceDetail
            {
                Id=r.GetInt64(0),InvoiceNo=r.GetInt64(1),CustomerId=r.IsDBNull(2)?null:r.GetInt64(2),CustomerName=r.GetString(3),PaymentType=r.GetString(4),
                Subtotal=r.GetInt64(5),Discount=r.GetInt64(6),Total=r.GetInt64(7),Paid=r.GetInt64(8),Status=r.GetString(9),CancelReason=r.GetString(10),CreatedAt=DateTime.Parse(r.GetString(11))
            };
        }
        using(var cmd=db.CreateCommand())
        {
            cmd.CommandText=@"SELECT product_id,product_name,COALESCE(barcode,''),qty,unit_price,purchase_price,line_total
FROM invoice_items WHERE invoice_id=$id ORDER BY id";
            cmd.Parameters.AddWithValue("$id",detail.Id);
            using var r=cmd.ExecuteReader();var row=0;
            while(r.Read())detail.Items.Add(new InvoiceLine{RowNumber=++row,ProductId=r.IsDBNull(0)?null:r.GetInt64(0),ProductName=r.GetString(1),Barcode=r.GetString(2),Quantity=r.GetDouble(3),UnitPrice=r.GetInt64(4),PurchasePrice=r.GetInt64(5),LineTotal=r.GetInt64(6)});
        }
        return detail;
    }
}
