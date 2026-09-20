using System.Text.Json;
using HesabdariAsan.Native.Data;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Services;

public sealed class CategoryService
{
    private const string Key="product_categories_v1";

    public IReadOnlyList<string> Names()
    {
        using var db=Database.Open();
        var names=LoadNames(db);
        var changed=false;
        using(var cmd=db.CreateCommand())
        {
            cmd.CommandText="SELECT DISTINCT category FROM products WHERE is_active=1 AND trim(category)<>'' ORDER BY category";
            using var r=cmd.ExecuteReader();
            while(r.Read())
            {
                var name=r.GetString(0).Trim();
                if(name.Length>0 && !names.Contains(name,StringComparer.OrdinalIgnoreCase)){names.Add(name);changed=true;}
            }
        }
        if(!names.Contains("عمومی",StringComparer.OrdinalIgnoreCase)){names.Insert(0,"عمومی");changed=true;}
        var general=names.FindIndex(x=>x=="عمومی");if(general>0){names.RemoveAt(general);names.Insert(0,"عمومی");changed=true;}
        if(changed)SaveNames(db,names);
        return names;
    }

    public PagedResult<CategoryItem> Search(string? query,int page,int pageSize)
    {
        page=Math.Max(1,page);pageSize=pageSize is 10 or 20 or 50?pageSize:10;query=(query??"").Trim();
        var names=Names();
        using var db=Database.Open();
        var counts=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        using(var c=db.CreateCommand()){c.CommandText="SELECT category,COUNT(*) FROM products WHERE is_active=1 GROUP BY category";using var r=c.ExecuteReader();while(r.Read())counts[r.GetString(0)]=r.GetInt32(1);}
        var all=names.Select((n,i)=>new CategoryItem{Name=n,ProductCount=counts.GetValueOrDefault(n),SortOrder=i+1}).Where(x=>query.Length==0||x.Name.Contains(query,StringComparison.CurrentCultureIgnoreCase)).ToList();
        var total=all.Count;var items=all.Skip((page-1)*pageSize).Take(pageSize).ToList();for(var i=0;i<items.Count;i++)items[i].RowNumber=(page-1)*pageSize+i+1;
        return new PagedResult<CategoryItem>{Items=items,TotalCount=total,Page=page,PageSize=pageSize};
    }

    public void Add(string name)
    {
        name=Normalize(name);using var db=Database.Open();using var tx=db.BeginTransaction();var names=LoadNames(db);
        if(names.Contains(name,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("این دسته قبلاً وجود دارد.");
        names.Add(name);SaveNames(db,names,tx);AuditService.Write(db,tx,"CATEGORY_CREATE","CATEGORY",name,JsonSerializer.Serialize(new{name}));tx.Commit();
    }

    public void Rename(string oldName,string newName)
    {
        oldName=Normalize(oldName);newName=Normalize(newName);if(oldName=="عمومی")throw new InvalidOperationException("نام دسته عمومی قابل تغییر نیست.");
        using var db=Database.Open();using var tx=db.BeginTransaction();var names=LoadNames(db);var index=names.FindIndex(x=>string.Equals(x,oldName,StringComparison.OrdinalIgnoreCase));if(index<0)throw new InvalidOperationException("دسته پیدا نشد.");
        if(names.Any(x=>!string.Equals(x,oldName,StringComparison.OrdinalIgnoreCase)&&string.Equals(x,newName,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("این نام قبلاً وجود دارد.");
        using(var cmd=db.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="UPDATE products SET category=$new WHERE category=$old";cmd.Parameters.AddWithValue("$new",newName);cmd.Parameters.AddWithValue("$old",oldName);cmd.ExecuteNonQuery();}
        names[index]=newName;SaveNames(db,names,tx);AuditService.Write(db,tx,"CATEGORY_RENAME","CATEGORY",oldName,JsonSerializer.Serialize(new{oldName,newName}));tx.Commit();
    }

    public void Delete(string name)
    {
        name=Normalize(name);if(name=="عمومی")throw new InvalidOperationException("دسته عمومی قابل حذف نیست.");
        using var db=Database.Open();using var tx=db.BeginTransaction();
        using(var c=db.CreateCommand()){c.Transaction=tx;c.CommandText="UPDATE products SET category='عمومی' WHERE is_active=1 AND category=$n";c.Parameters.AddWithValue("$n",name);c.ExecuteNonQuery();}
        var names=LoadNames(db);names.RemoveAll(x=>string.Equals(x,name,StringComparison.OrdinalIgnoreCase));SaveNames(db,names,tx);AuditService.Write(db,tx,"CATEGORY_DELETE","CATEGORY",name);tx.Commit();
    }

    public void Move(string name,int delta)
    {
        if(name=="عمومی")return;using var db=Database.Open();using var tx=db.BeginTransaction();var names=LoadNames(db);var i=names.FindIndex(x=>string.Equals(x,name,StringComparison.OrdinalIgnoreCase));if(i<0)return;var j=Math.Clamp(i+delta,1,names.Count-1);if(i==j)return;(names[i],names[j])=(names[j],names[i]);SaveNames(db,names,tx);AuditService.Write(db,tx,"CATEGORY_REORDER","CATEGORY",name,JsonSerializer.Serialize(new{from=i+1,to=j+1}));tx.Commit();
    }

    private static string Normalize(string name){name=(name??"").Trim();if(name.Length==0)throw new InvalidOperationException("نام دسته را وارد کنید.");if(name.Length>80)throw new InvalidOperationException("نام دسته بیش از حد طولانی است.");return name;}
    private static List<string> LoadNames(Microsoft.Data.Sqlite.SqliteConnection db)
    {
        using var c=db.CreateCommand();c.CommandText="SELECT value FROM settings WHERE key=$k LIMIT 1";c.Parameters.AddWithValue("$k",Key);var raw=Convert.ToString(c.ExecuteScalar());
        try{var list=string.IsNullOrWhiteSpace(raw)?new List<string>():JsonSerializer.Deserialize<List<string>>(raw!)??new List<string>();list=list.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(!list.Contains("عمومی",StringComparer.OrdinalIgnoreCase))list.Insert(0,"عمومی");return list;}catch{return new List<string>{"عمومی"};}
    }
    private static void SaveNames(Microsoft.Data.Sqlite.SqliteConnection db,List<string> names,Microsoft.Data.Sqlite.SqliteTransaction? tx=null)
    {
        using var c=db.CreateCommand();c.Transaction=tx;c.CommandText=@"INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";c.Parameters.AddWithValue("$k",Key);c.Parameters.AddWithValue("$v",JsonSerializer.Serialize(names));c.ExecuteNonQuery();
    }
}
