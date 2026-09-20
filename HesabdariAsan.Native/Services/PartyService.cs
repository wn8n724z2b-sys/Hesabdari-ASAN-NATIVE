using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class PartyService
{
    public PagedResult<Party> Search(string? query, string? type, int page, int pageSize)
    {
        page = Math.Max(1, page); pageSize = pageSize is 10 or 20 or 50 ? pageSize : 10; query = (query ?? "").Trim(); type = type is "CUSTOMER" or "COMPANY" ? type : "ALL";
        var clauses = new List<string>(); if (type != "ALL") clauses.Add("type=$type"); if (query.Length > 0) clauses.Add("(name LIKE $like OR phone LIKE $like OR address LIKE $like)"); var where = clauses.Count == 0 ? "" : "WHERE " + string.Join(" AND ", clauses);
        using var db = Database.Open(); int total;
        using (var count = db.CreateCommand()) { count.CommandText = $"SELECT COUNT(*) FROM parties {where}"; AddFilters(count, query, type); total = Convert.ToInt32(count.ExecuteScalar() ?? 0); }
        var items = new List<Party>(); using var cmd = db.CreateCommand(); cmd.CommandText = $@"SELECT id,type,name,COALESCE(phone,''),COALESCE(address,''),debt,is_pinned FROM parties {where} ORDER BY is_pinned DESC, CASE WHEN name='مشتری عمومی' THEN 0 ELSE 1 END, name COLLATE NOCASE LIMIT $limit OFFSET $offset"; AddFilters(cmd, query, type); cmd.Parameters.AddWithValue("$limit", pageSize); cmd.Parameters.AddWithValue("$offset", (page - 1) * pageSize);
        using var r = cmd.ExecuteReader(); var i = 0; while (r.Read()) items.Add(new Party { Id=r.GetInt64(0),RowNumber=(page-1)*pageSize+ ++i,Type=r.GetString(1),Name=r.GetString(2),Phone=r.GetString(3),Address=r.GetString(4),Debt=r.GetInt64(5),IsPinned=r.GetInt64(6)==1 });
        return new PagedResult<Party> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public IReadOnlyList<Party> QuickSearch(string query, int limit = 10) => QuickSearchByType(query, "CUSTOMER", limit);
    public IReadOnlyList<Party> QuickSearchCompanies(string query, int limit = 10) => QuickSearchByType(query, "COMPANY", limit);

    private IReadOnlyList<Party> QuickSearchByType(string query, string type, int limit)
    {
        query = (query ?? "").Trim(); using var db = Database.Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = @"SELECT id,type,name,COALESCE(phone,''),COALESCE(address,''),debt,is_pinned FROM parties WHERE type=$type AND (name LIKE $like OR phone LIKE $like) ORDER BY is_pinned DESC, name LIMIT $limit";
        cmd.Parameters.AddWithValue("$type", type); cmd.Parameters.AddWithValue("$like", $"%{query}%"); cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 50));
        var list = new List<Party>(); using var r = cmd.ExecuteReader(); while (r.Read()) list.Add(new Party { Id=r.GetInt64(0),Type=r.GetString(1),Name=r.GetString(2),Phone=r.GetString(3),Address=r.GetString(4),Debt=r.GetInt64(5),IsPinned=r.GetInt64(6)==1 }); return list;
    }

    public long Save(Party p)
    {
        if (string.IsNullOrWhiteSpace(p.Name)) throw new InvalidOperationException("نام الزامی است.");
        using var db = Database.Open(); using var tx = db.BeginTransaction(); long id; string action;
        if (p.Id == 0)
        {
            using var cmd = db.CreateCommand(); cmd.Transaction=tx; cmd.CommandText = @"INSERT INTO parties(type,name,phone,address,debt,is_pinned,created_at) VALUES($t,$n,$ph,$a,$d,$pin,$at); SELECT last_insert_rowid();"; Fill(cmd,p); cmd.Parameters.AddWithValue("$at",DateTime.Now.ToString("O")); id=Convert.ToInt64(cmd.ExecuteScalar()??0L); action="PARTY_CREATE";
        }
        else
        {
            using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText=@"UPDATE parties SET type=$t,name=$n,phone=$ph,address=$a,debt=$d,is_pinned=$pin WHERE id=$id";Fill(cmd,p);cmd.Parameters.AddWithValue("$id",p.Id);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("مشتری / شرکت پیدا نشد.");id=p.Id;action="PARTY_EDIT";
        }
        AuditService.Write(db,tx,action,"PARTY",id.ToString(),JsonSerializer.Serialize(new{p.Type,p.Name,p.Phone,p.Address,p.Debt,p.IsPinned}));tx.Commit();return id;
    }

    public void RecordCustomerPayment(long partyId,long amount)
    {
        if(amount<=0) throw new InvalidOperationException("مبلغ پرداخت باید بیشتر از صفر باشد."); using var db=Database.Open();using var tx=db.BeginTransaction(); long debt;string type;string name;
        using(var c=db.CreateCommand()){c.Transaction=tx;c.CommandText="SELECT debt,type,name FROM parties WHERE id=$id";c.Parameters.AddWithValue("$id",partyId);using var r=c.ExecuteReader();if(!r.Read())throw new InvalidOperationException("مشتری پیدا نشد.");debt=r.GetInt64(0);type=r.GetString(1);name=r.GetString(2);} if(type!="CUSTOMER")throw new InvalidOperationException("این عملیات فقط برای مشتری است.");if(amount>debt)throw new InvalidOperationException("مبلغ پرداخت از قرض مشتری بیشتر است.");
        using(var c=db.CreateCommand()){c.Transaction=tx;c.CommandText="UPDATE parties SET debt=debt-$a WHERE id=$id";c.Parameters.AddWithValue("$a",amount);c.Parameters.AddWithValue("$id",partyId);c.ExecuteNonQuery();}
        long receiptId;using(var c=db.CreateCommand()){c.Transaction=tx;c.CommandText="INSERT INTO customer_receipts(party_id,amount,note,created_at) VALUES($id,$a,'پرداخت قرض',$at);SELECT last_insert_rowid();";c.Parameters.AddWithValue("$id",partyId);c.Parameters.AddWithValue("$a",amount);c.Parameters.AddWithValue("$at",DateTime.Now.ToString("O"));receiptId=Convert.ToInt64(c.ExecuteScalar()??0L);}
        AuditService.Write(db,tx,"CUSTOMER_DEBT_PAYMENT","CUSTOMER_RECEIPT",receiptId.ToString(),JsonSerializer.Serialize(new{partyId,name,amount,before=debt,after=debt-amount})); tx.Commit();
    }

    public void RecordCompanyPayment(long partyId,long amount)
    {
        if(amount<=0) throw new InvalidOperationException("مبلغ پرداخت باید بیشتر از صفر باشد."); using var db=Database.Open();using var tx=db.BeginTransaction(); long debt;string type;string name;
        using(var c=db.CreateCommand()){c.Transaction=tx;c.CommandText="SELECT debt,type,name FROM parties WHERE id=$id";c.Parameters.AddWithValue("$id",partyId);using var r=c.ExecuteReader();if(!r.Read())throw new InvalidOperationException("شرکت پیدا نشد.");debt=r.GetInt64(0);type=r.GetString(1);name=r.GetString(2);} if(type!="COMPANY")throw new InvalidOperationException("این عملیات فقط برای شرکت است.");if(amount>debt)throw new InvalidOperationException("مبلغ پرداخت از قرض شرکت بیشتر است.");
        using(var c=db.CreateCommand()){c.Transaction=tx;c.CommandText="UPDATE parties SET debt=debt-$a WHERE id=$id";c.Parameters.AddWithValue("$a",amount);c.Parameters.AddWithValue("$id",partyId);c.ExecuteNonQuery();}
        long paymentId;using(var c=db.CreateCommand()){c.Transaction=tx;c.CommandText="INSERT INTO supplier_payments(party_id,amount,note,created_at) VALUES($id,$a,'پرداخت قرض شرکت',$at);SELECT last_insert_rowid();";c.Parameters.AddWithValue("$id",partyId);c.Parameters.AddWithValue("$a",amount);c.Parameters.AddWithValue("$at",DateTime.Now.ToString("O"));paymentId=Convert.ToInt64(c.ExecuteScalar()??0L);}
        AuditService.Write(db,tx,"SUPPLIER_DEBT_PAYMENT","SUPPLIER_PAYMENT",paymentId.ToString(),JsonSerializer.Serialize(new{partyId,name,amount,before=debt,after=debt-amount})); tx.Commit();
    }

    public void TogglePin(long id)
    {
        using var db=Database.Open();using var tx=db.BeginTransaction();using(var cmd=db.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="UPDATE parties SET is_pinned=CASE is_pinned WHEN 1 THEN 0 ELSE 1 END WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("مورد پیدا نشد.");}AuditService.Write(db,tx,"PARTY_PIN_TOGGLE","PARTY",id.ToString());tx.Commit();
    }

    public void Delete(long id)
    {
        using var db=Database.Open();using var tx=db.BeginTransaction();string name;long debt;string type;
        using(var check=db.CreateCommand()){check.Transaction=tx;check.CommandText="SELECT name,debt,type FROM parties WHERE id=$id";check.Parameters.AddWithValue("$id",id);using var r=check.ExecuteReader();if(!r.Read())throw new InvalidOperationException("مورد پیدا نشد.");name=r.GetString(0);debt=r.GetInt64(1);type=r.GetString(2);}
        if(name=="مشتری عمومی")throw new InvalidOperationException("مشتری عمومی قابل حذف نیست.");
        if(debt!=0)throw new InvalidOperationException("این مورد مانده قرض دارد و قابل حذف نیست. ابتدا حساب آن را تسویه کنید.");
        using(var refs=db.CreateCommand()){refs.Transaction=tx;refs.CommandText=@"SELECT
(SELECT COUNT(*) FROM invoices WHERE customer_id=$id)+
(SELECT COUNT(*) FROM purchases WHERE supplier_id=$id)+
(SELECT COUNT(*) FROM customer_receipts WHERE party_id=$id)+
(SELECT COUNT(*) FROM supplier_payments WHERE party_id=$id)";refs.Parameters.AddWithValue("$id",id);if(Convert.ToInt64(refs.ExecuteScalar()??0L)>0)throw new InvalidOperationException("این مورد سابقه مالی دارد و برای حفظ Audit قابل حذف نیست؛ فقط اطلاعات آن را ویرایش کنید.");}
        using(var cmd=db.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="DELETE FROM parties WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();}
        AuditService.Write(db,tx,"PARTY_DELETE","PARTY",id.ToString(),JsonSerializer.Serialize(new{name,type}));tx.Commit();
    }

    private static void AddFilters(Microsoft.Data.Sqlite.SqliteCommand cmd,string query,string type){if(type!="ALL")cmd.Parameters.AddWithValue("$type",type);if(query.Length>0)cmd.Parameters.AddWithValue("$like",$"%{query}%");}
    private static void Fill(Microsoft.Data.Sqlite.SqliteCommand cmd, Party p){cmd.Parameters.AddWithValue("$t",p.Type is "COMPANY" ? "COMPANY" : "CUSTOMER");cmd.Parameters.AddWithValue("$n",p.Name.Trim());cmd.Parameters.AddWithValue("$ph",p.Phone?.Trim()??"");cmd.Parameters.AddWithValue("$a",p.Address?.Trim()??"");cmd.Parameters.AddWithValue("$d",Math.Max(0,p.Debt));cmd.Parameters.AddWithValue("$pin",p.IsPinned?1:0);}
}
