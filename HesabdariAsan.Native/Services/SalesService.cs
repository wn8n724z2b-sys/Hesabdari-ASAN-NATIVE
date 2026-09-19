using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class SalesService
{
    public long Checkout(IReadOnlyList<CartItem> items, Party? customer, string paymentType, long discount)
    {
        if(items.Count==0) throw new InvalidOperationException("سبد فروش خالی است.");
        if(paymentType is not ("CASH" or "CREDIT")) throw new InvalidOperationException("روش پرداخت را انتخاب کنید.");
        if(paymentType=="CREDIT" && (customer is null || customer.Name=="مشتری عمومی")) throw new InvalidOperationException("برای فروش نسیه یک مشتری مشخص انتخاب کنید.");
        discount=Math.Max(0,discount);
        using var db=Database.Open(); using var tx=db.BeginTransaction();

        foreach(var item in items)
        {
            using var stockCmd=db.CreateCommand();stockCmd.Transaction=tx;stockCmd.CommandText="SELECT stock FROM products WHERE id=$id AND is_active=1";stockCmd.Parameters.AddWithValue("$id",item.ProductId);
            var stockObj=stockCmd.ExecuteScalar();if(stockObj is null) throw new InvalidOperationException($"کالای «{item.Name}» پیدا نشد.");
            var stock=Convert.ToDouble(stockObj);if(item.Quantity<=0||stock+0.000001<item.Quantity) throw new InvalidOperationException($"موجودی «{item.Name}» کافی نیست.");
        }

        long nextNo; using(var no=db.CreateCommand()){no.Transaction=tx;no.CommandText="SELECT COALESCE(MAX(invoice_no),0)+1 FROM invoices";nextNo=Convert.ToInt64(no.ExecuteScalar()??1L);}
        var subtotal=items.Sum(x=>x.LineTotal); if(discount>subtotal) discount=subtotal; var total=subtotal-discount;
        long invoiceId; var createdAt=DateTime.Now.ToString("O");
        using(var inv=db.CreateCommand())
        {
            inv.Transaction=tx;inv.CommandText=@"INSERT INTO invoices(invoice_no,customer_id,customer_name,payment_type,subtotal,discount,total,paid,status,created_at)
VALUES($no,$cid,$cn,$pt,$sub,$dis,$tot,$paid,'ACTIVE',$at);SELECT last_insert_rowid();";
            inv.Parameters.AddWithValue("$no",nextNo);inv.Parameters.AddWithValue("$cid",customer is null?DBNull.Value:customer.Id);inv.Parameters.AddWithValue("$cn",customer?.Name??"مشتری عمومی");inv.Parameters.AddWithValue("$pt",paymentType);inv.Parameters.AddWithValue("$sub",subtotal);inv.Parameters.AddWithValue("$dis",discount);inv.Parameters.AddWithValue("$tot",total);inv.Parameters.AddWithValue("$paid",paymentType=="CASH"?total:0);inv.Parameters.AddWithValue("$at",createdAt);invoiceId=Convert.ToInt64(inv.ExecuteScalar()??0L);
        }

        foreach(var item in items)
        {
            using(var line=db.CreateCommand()){line.Transaction=tx;line.CommandText=@"INSERT INTO invoice_items(invoice_id,product_id,product_name,barcode,qty,unit_price,purchase_price,line_total)
VALUES($iid,$pid,$n,$b,$q,$up,$pp,$lt)";line.Parameters.AddWithValue("$iid",invoiceId);line.Parameters.AddWithValue("$pid",item.ProductId);line.Parameters.AddWithValue("$n",item.Name);line.Parameters.AddWithValue("$b",item.Barcode);line.Parameters.AddWithValue("$q",item.Quantity);line.Parameters.AddWithValue("$up",item.UnitPrice);line.Parameters.AddWithValue("$pp",item.PurchasePrice);line.Parameters.AddWithValue("$lt",item.LineTotal);line.ExecuteNonQuery();}
            double balance;using(var update=db.CreateCommand()){update.Transaction=tx;update.CommandText="UPDATE products SET stock=stock-$q WHERE id=$id";update.Parameters.AddWithValue("$q",item.Quantity);update.Parameters.AddWithValue("$id",item.ProductId);update.ExecuteNonQuery();}
            using(var readBalance=db.CreateCommand()){readBalance.Transaction=tx;readBalance.CommandText="SELECT stock FROM products WHERE id=$id";readBalance.Parameters.AddWithValue("$id",item.ProductId);balance=Convert.ToDouble(readBalance.ExecuteScalar()??0d);}
            using(var ledger=db.CreateCommand()){ledger.Transaction=tx;ledger.CommandText=@"INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at)
VALUES($pid,'SALE',$q,$bal,'INVOICE',$iid,'فروش',$at)";ledger.Parameters.AddWithValue("$pid",item.ProductId);ledger.Parameters.AddWithValue("$q",-item.Quantity);ledger.Parameters.AddWithValue("$bal",balance);ledger.Parameters.AddWithValue("$iid",invoiceId);ledger.Parameters.AddWithValue("$at",createdAt);ledger.ExecuteNonQuery();}
        }

        if(paymentType=="CREDIT"&&customer is not null)
        {
            using var debt=db.CreateCommand();debt.Transaction=tx;debt.CommandText="UPDATE parties SET debt=debt+$d WHERE id=$id";debt.Parameters.AddWithValue("$d",total);debt.Parameters.AddWithValue("$id",customer.Id);if(debt.ExecuteNonQuery()!=1)throw new InvalidOperationException("حساب مشتری پیدا نشد.");
        }

        var snapshot=JsonSerializer.Serialize(new{invoiceId,invoiceNo=nextNo,customerId=customer?.Id,customerName=customer?.Name??"مشتری عمومی",paymentType,subtotal,discount,total,status="ACTIVE",createdAt,items=items.Select(x=>new{x.ProductId,x.Name,x.Barcode,x.Quantity,x.UnitPrice,x.PurchasePrice,lineTotal=x.LineTotal}).ToArray()});
        using(var rev=db.CreateCommand()){rev.Transaction=tx;rev.CommandText=@"INSERT INTO invoice_revisions(invoice_id,invoice_no,revision_no,changed_at,action,actor,snapshot_json)
VALUES($id,$no,1,$at,'CREATE','Admin',$json)";rev.Parameters.AddWithValue("$id",invoiceId);rev.Parameters.AddWithValue("$no",nextNo);rev.Parameters.AddWithValue("$at",createdAt);rev.Parameters.AddWithValue("$json",snapshot);rev.ExecuteNonQuery();}
        AuditService.Write(db,tx,"INVOICE_CREATE","INVOICE",invoiceId.ToString(),snapshot,nextNo);
        tx.Commit();return nextNo;
    }
}
