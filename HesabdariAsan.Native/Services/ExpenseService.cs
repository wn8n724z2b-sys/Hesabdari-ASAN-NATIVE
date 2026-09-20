using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class ExpenseService
{
    public PagedResult<Expense> Search(string? query,int page,int pageSize) => Search(query,"ALL",page,pageSize);

    public PagedResult<Expense> Search(string? query,string period,int page,int pageSize)
    {
        page=Math.Max(1,page); pageSize=pageSize is 10 or 20 or 50 ? pageSize : 10; query=(query??"").Trim();
        var (from,to)=ResolvePeriod(period);
        var filters=new List<string>();
        if(query.Length>0) filters.Add("(title LIKE $like OR category LIKE $like OR note LIKE $like)");
        if(from is not null) filters.Add("created_at >= $from");
        if(to is not null) filters.Add("created_at < $to");
        var where=filters.Count==0?"":"WHERE "+string.Join(" AND ",filters);
        using var db=Database.Open();
        void AddParams(Microsoft.Data.Sqlite.SqliteCommand cmd)
        {
            if(query.Length>0)cmd.Parameters.AddWithValue("$like",$"%{query}%");
            if(from is not null)cmd.Parameters.AddWithValue("$from",from.Value.ToString("O"));
            if(to is not null)cmd.Parameters.AddWithValue("$to",to.Value.ToString("O"));
        }
        int total;
        using(var count=db.CreateCommand()){count.CommandText=$"SELECT COUNT(*) FROM expenses {where}";AddParams(count);total=Convert.ToInt32(count.ExecuteScalar()??0);}
        var items=new List<Expense>();
        using var cmd=db.CreateCommand(); cmd.CommandText=$@"SELECT id,title,category,amount,COALESCE(note,''),created_at FROM expenses {where} ORDER BY created_at DESC,id DESC LIMIT $l OFFSET $o"; AddParams(cmd);cmd.Parameters.AddWithValue("$l",pageSize);cmd.Parameters.AddWithValue("$o",(page-1)*pageSize);
        using var r=cmd.ExecuteReader();var i=0;while(r.Read())items.Add(new Expense{Id=r.GetInt64(0),RowNumber=(page-1)*pageSize+ ++i,Title=r.GetString(1),Category=r.GetString(2),Amount=r.GetInt64(3),Note=r.GetString(4),CreatedAt=DateTime.Parse(r.GetString(5))});
        return new PagedResult<Expense>{Items=items,TotalCount=total,Page=page,PageSize=pageSize};
    }

    public long Save(Expense e)
    {
        if(string.IsNullOrWhiteSpace(e.Title))throw new InvalidOperationException("عنوان هزینه الزامی است.");if(e.Amount<0)throw new InvalidOperationException("مبلغ هزینه معتبر نیست.");
        using var db=Database.Open();using var tx=db.BeginTransaction();long id;string action;
        if(e.Id==0){using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText=@"INSERT INTO expenses(title,category,amount,note,created_at) VALUES($t,$c,$a,$n,$at);SELECT last_insert_rowid();";Fill(cmd,e);cmd.Parameters.AddWithValue("$at",(e.CreatedAt==default?DateTime.Now:e.CreatedAt).ToString("O"));id=Convert.ToInt64(cmd.ExecuteScalar()??0L);action="EXPENSE_CREATE";}
        else{using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText="UPDATE expenses SET title=$t,category=$c,amount=$a,note=$n WHERE id=$id";Fill(cmd,e);cmd.Parameters.AddWithValue("$id",e.Id);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("هزینه پیدا نشد.");id=e.Id;action="EXPENSE_EDIT";}
        AuditService.Write(db,tx,action,"EXPENSE",id.ToString(),JsonSerializer.Serialize(new{e.Title,e.Category,e.Amount,e.Note}));tx.Commit();return id;
    }
    public void Delete(long id){using var db=Database.Open();using var tx=db.BeginTransaction();string payload;using(var q=db.CreateCommand()){q.Transaction=tx;q.CommandText="SELECT title,category,amount,COALESCE(note,''),created_at FROM expenses WHERE id=$id";q.Parameters.AddWithValue("$id",id);using var r=q.ExecuteReader();if(!r.Read())throw new InvalidOperationException("هزینه پیدا نشد.");payload=JsonSerializer.Serialize(new{title=r.GetString(0),category=r.GetString(1),amount=r.GetInt64(2),note=r.GetString(3),createdAt=r.GetString(4)});}using(var cmd=db.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="DELETE FROM expenses WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();}AuditService.Write(db,tx,"EXPENSE_DELETE","EXPENSE",id.ToString(),payload);tx.Commit();}
    public (long Today,long Month,long Max,string MaxTitle,int Count) GetSummary(){var today=DateTime.Today;var month=new DateTime(today.Year,today.Month,1);using var db=Database.Open();long Scalar(string sql,string p,DateTime dt){using var c=db.CreateCommand();c.CommandText=sql;c.Parameters.AddWithValue(p,dt.ToString("O"));return Convert.ToInt64(c.ExecuteScalar()??0L);}var t=Scalar("SELECT COALESCE(SUM(amount),0) FROM expenses WHERE created_at >= $d","$d",today);var m=Scalar("SELECT COALESCE(SUM(amount),0) FROM expenses WHERE created_at >= $d","$d",month);long max=0;string title="—";using(var c=db.CreateCommand()){c.CommandText="SELECT title,amount FROM expenses ORDER BY amount DESC LIMIT 1";using var r=c.ExecuteReader();if(r.Read()){title=r.GetString(0);max=r.GetInt64(1);}}using var cc=db.CreateCommand();cc.CommandText="SELECT COUNT(*) FROM expenses";var count=Convert.ToInt32(cc.ExecuteScalar()??0);return(t,m,max,title,count);}
    private static void Fill(Microsoft.Data.Sqlite.SqliteCommand cmd,Expense e){cmd.Parameters.AddWithValue("$t",e.Title.Trim());cmd.Parameters.AddWithValue("$c",string.IsNullOrWhiteSpace(e.Category)?"هزینه":e.Category.Trim());cmd.Parameters.AddWithValue("$a",Math.Max(0,e.Amount));cmd.Parameters.AddWithValue("$n",e.Note?.Trim()??"");}

    private static (DateTime? From,DateTime? To) ResolvePeriod(string period)
    {
        var today=DateTime.Today;
        return period switch
        {
            "TODAY" => (today,today.AddDays(1)),
            "LAST7" => (today.AddDays(-6),today.AddDays(1)),
            "MONTH" => (new DateTime(today.Year,today.Month,1),new DateTime(today.Year,today.Month,1).AddMonths(1)),
            "PREVMONTH" => (new DateTime(today.Year,today.Month,1).AddMonths(-1),new DateTime(today.Year,today.Month,1)),
            _ => (null,null)
        };
    }
}
