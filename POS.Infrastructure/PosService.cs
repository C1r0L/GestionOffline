using Dapper;
using Microsoft.Data.Sqlite;
using POS.Core;

namespace POS.Infrastructure;

public sealed class PosService : IPosService
{
    readonly string path;
    static string Now => DateTime.UtcNow.ToString("O");
    public PosService(string databasePath)
    {
        path = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var c = Open();
        var version = c.ExecuteScalar<int>("PRAGMA user_version");
        if (version > 1) throw new InvalidOperationException("Esta base requiere una versión más reciente de MiPOS.");
        if (version == 0)
        {
            using var t = c.BeginTransaction();
            using var stream = typeof(PosService).Assembly.GetManifestResourceStream("POS.Infrastructure.schema.sql")!;
            using var reader = new StreamReader(stream);
            c.Execute(reader.ReadToEnd(), transaction:t); t.Commit();
        }
    }
    SqliteConnection Open()
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path, Pooling=false, ForeignKeys=true, DefaultTimeout=10 }.ToString());
        c.Open(); return c;
    }
    static Product Map(dynamic p) => new((long)p.id, (string?)p.code, (string)p.name, (string)p.category, Enum.Parse<SaleUnit>((string)p.sale_unit), Enum.Parse<StockMode>((string)p.stock_mode), (long)p.price_minor, (long?)p.cost_minor, (long)p.is_active == 1);
    static Product Get(SqliteConnection c,long id,SqliteTransaction t) => Map(c.QuerySingle("SELECT * FROM products WHERE id=@id",new{id},t));
    public IReadOnlyList<Product> Products(string search="",bool includeInactive=false)
    {
        using var c=Open(); return c.Query("SELECT * FROM products WHERE (@includeInactive OR is_active=1) AND (instr(lower(name),lower(@search))>0 OR instr(lower(coalesce(code,'')),lower(@search))>0) ORDER BY name",new{search,includeInactive}).Select(Map).ToList();
    }
    public long SaveProduct(Product p)
    {
        Pricing.Validate(p); using var c=Open(); using var t=c.BeginTransaction(); var now=Now;
        if (!string.IsNullOrWhiteSpace(p.Code) && c.ExecuteScalar<long>("SELECT count(*) FROM products WHERE code=@code AND id<>@id",new{code=p.Code.Trim(),id=p.Id},t)>0) throw new ArgumentException("Ese código ya pertenece a otro producto.");
        var args=new {p.Id,code=string.IsNullOrWhiteSpace(p.Code)?null:p.Code.Trim(),name=p.Name.Trim(),category=p.Category.Trim(),unit=p.Unit.ToString(),mode=p.Mode.ToString(),price=p.PriceMinor,cost=p.CostMinor,active=p.Active,now};
        long id=p.Id;
        if(id==0)
        {
            id=c.ExecuteScalar<long>("INSERT INTO products(code,name,category,sale_unit,stock_mode,price_minor,cost_minor,is_active,created_at,updated_at) VALUES(@code,@name,@category,@unit,@mode,@price,@cost,@active,@now,@now); SELECT last_insert_rowid();",args,t);
            c.Execute("INSERT INTO inventory VALUES(@id,@quantity,NULL,'SUFICIENTE',@now)",new{id,quantity=p.Mode==StockMode.MANUAL?(long?)null:0,now},t);
        }
        else
        {
            var old=Get(c,id,t);
            if(old.Mode!=p.Mode || old.Unit!=p.Unit) throw new ArgumentException("No se puede cambiar la unidad ni el modo de stock de un producto existente.");
            c.Execute("UPDATE products SET code=@code,name=@name,category=@category,price_minor=@price,cost_minor=@cost,is_active=@active,updated_at=@now WHERE id=@Id",args,t);
            if(old.PriceMinor!=p.PriceMinor) AuditPrice(c,t,id,old.PriceMinor,p.PriceMinor,"Edición individual");
        }
        t.Commit(); return id;
    }
    public void SetActive(long id,bool active) { using var c=Open(); c.Execute("UPDATE products SET is_active=@active,updated_at=@now WHERE id=@id",new{id,active,now=Now}); }
    public IReadOnlyList<InventoryRow> Inventory()
    {
        using var c=Open();return c.Query("SELECT p.id,p.name,p.stock_mode,i.* FROM products p JOIN inventory i ON p.id=i.product_id WHERE p.is_active=1 ORDER BY p.name").Select(x=>new InventoryRow((long)x.id,(string)x.name,Enum.Parse<StockMode>((string)x.stock_mode),(long?)x.quantity_base,(long?)x.minimum_base,Enum.Parse<ReplenishmentStatus>((string)x.replenishment_status))).ToList();
    }
    public void AdjustStock(long productId,long value,bool absolute,string note)
    {
        if(string.IsNullOrWhiteSpace(note)) throw new ArgumentException("Indicá el motivo del movimiento.");
        using var c=Open();using var t=c.BeginTransaction();var p=Get(c,productId,t);
        if(p.Mode==StockMode.MANUAL) throw new ArgumentException("Este producto utiliza estado manual.");
        var old=c.ExecuteScalar<long>("SELECT quantity_base FROM inventory WHERE product_id=@productId",new{productId},t);
        var next=absolute?value:checked(old+value);if(next<0) throw new ArgumentException("El stock no puede ser negativo.");
        var delta=checked(next-old);
        c.Execute("UPDATE inventory SET quantity_base=@next,updated_at=@now WHERE product_id=@productId",new{next,now=Now,productId},t);
        Movement(c,t,productId,null,absolute?"ADJUSTMENT":"RESTOCK",delta,note);t.Commit();
    }
    public void SetMinimumStock(long productId,long? minimumBase)
    {
        if(minimumBase<0) throw new ArgumentException("El mínimo no puede ser negativo.");
        using var c=Open();
        if(c.Execute("UPDATE inventory SET minimum_base=@minimumBase,updated_at=@now WHERE product_id=@productId AND quantity_base IS NOT NULL",new{productId,minimumBase,now=Now})!=1) throw new ArgumentException("Este producto no tiene stock medible.");
    }
    public void SetManualStatus(long productId,ReplenishmentStatus status)
    {
        if(!Enum.IsDefined(status)) throw new ArgumentException("Estado inválido.");
        using var c=Open();if(c.Execute("UPDATE inventory SET replenishment_status=@status,updated_at=@now WHERE product_id=@productId AND quantity_base IS NULL",new{status=status.ToString(),now=Now,productId})!=1) throw new ArgumentException("El producto no utiliza estado manual.");
    }
    static void Movement(SqliteConnection c,SqliteTransaction t,long productId,long? saleId,string type,long delta,string? note=null) => c.Execute("INSERT INTO stock_movements(product_id,sale_id,movement_type,delta_base,created_at,note) VALUES(@productId,@saleId,@type,@delta,@now,@note)",new{productId,saleId,type,delta,now=Now,note},t);
    public IReadOnlyList<Movement> Movements(long productId)
    {
        using var c=Open();return c.Query("SELECT * FROM stock_movements WHERE product_id=@productId ORDER BY id DESC",new{productId}).Select(x=>new Movement((long)x.id,(long)x.product_id,(long?)x.sale_id,(string)x.movement_type,(long)x.delta_base,(string)x.created_at,(string?)x.note)).ToList();
    }
    public long CompleteSale(IReadOnlyList<CartLine> lines,PaymentMethod payment)
    {
        if(lines.Count==0 || !Enum.IsDefined(payment)) throw new ArgumentException("Agregá productos y elegí un medio de pago.");
        if(lines.Any(x=>x.QuantityBase<=0)) throw new ArgumentException("La cantidad debe ser positiva.");
        using var c=Open();using var t=c.BeginTransaction();
        var items=lines.GroupBy(x=>x.ProductId).Select(g=>{var p=Get(c,g.Key,t);if(!p.Active) throw new ArgumentException("Hay un producto inactivo en la venta.");var q=g.Aggregate(0L,(s,x)=>checked(s+x.QuantityBase));return (p,q,total:Pricing.Total(p.PriceMinor,q,p.Unit));}).ToList();
        var total=items.Aggregate(0L,(s,x)=>checked(s+x.total));
        var id=c.ExecuteScalar<long>("INSERT INTO sales(created_at,status,payment_method,total_minor) VALUES(@now,'COMPLETED',@payment,@total); SELECT last_insert_rowid();",new{now=Now,payment=payment.ToString(),total},t);
        foreach(var (p,q,lineTotal) in items)
        {
            c.Execute("INSERT INTO sale_items(sale_id,product_id,product_name_snapshot,sale_unit_snapshot,quantity_base,unit_price_minor,line_total_minor) VALUES(@id,@pid,@name,@unit,@q,@price,@lineTotal)",new{id,pid=p.Id,name=p.Name,unit=p.Unit.ToString(),q,price=p.PriceMinor,lineTotal},t);
            if(p.Mode!=StockMode.MANUAL)
            {
                if(c.Execute("UPDATE inventory SET quantity_base=quantity_base-@q,updated_at=@now WHERE product_id=@pid AND quantity_base>=@q",new{q,now=Now,pid=p.Id},t)!=1) throw new ArgumentException($"Stock insuficiente: {p.Name}. Corregí el inventario antes de cobrar.");
                Movement(c,t,p.Id,id,"SALE",-q);
            }
        }
        t.Commit();return id;
    }
    public IReadOnlyList<SaleSummary> Sales(DateTime from,DateTime to)
    {
        using var c=Open();return c.Query("SELECT * FROM sales WHERE created_at>=@from AND created_at<@to ORDER BY id DESC",new{from=from.ToUniversalTime().ToString("O"),to=to.ToUniversalTime().ToString("O")}).Select(x=>new SaleSummary((long)x.id,(string)x.created_at,(string)x.status,(string)x.payment_method,(long)x.total_minor)).ToList();
    }
    public IReadOnlyList<SaleItem> SaleItems(long saleId)
    {
        using var c=Open();return c.Query("SELECT * FROM sale_items WHERE sale_id=@saleId ORDER BY id",new{saleId}).Select(x=>new SaleItem((long)x.product_id,(string)x.product_name_snapshot,Enum.Parse<SaleUnit>((string)x.sale_unit_snapshot),(long)x.quantity_base,(long)x.unit_price_minor,(long)x.line_total_minor)).ToList();
    }
    public void CancelSale(long saleId,string reason)
    {
        if(string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Indicá un motivo para anular.");
        using var c=Open();using var t=c.BeginTransaction();
        if(c.Execute("UPDATE sales SET status='CANCELLED',cancelled_at=@now,cancellation_reason=@reason WHERE id=@saleId AND status='COMPLETED'",new{saleId,now=Now,reason},t)!=1) throw new ArgumentException("La venta no existe o ya está anulada.");
        foreach(var m in c.Query("SELECT product_id,delta_base FROM stock_movements WHERE sale_id=@saleId AND movement_type='SALE'",new{saleId},t).ToList())
        {
            long pid=m.product_id,delta=checked(-(long)m.delta_base);
            c.Execute("UPDATE inventory SET quantity_base=quantity_base+@delta,updated_at=@now WHERE product_id=@pid",new{delta,now=Now,pid},t);Movement(c,t,pid,saleId,"SALE_REVERSAL",delta,reason);
        }
        t.Commit();
    }
    public IReadOnlyList<PricePreview> PreviewPrices(string category,decimal amount,bool percentage,long roundToMinor)
    {
        if(roundToMinor<=0) throw new ArgumentException("El redondeo debe ser positivo.");
        return Products().Where(x=>category.Length==0 || x.Category==category).Select(p=>{
            var raw=percentage?p.PriceMinor*(1m+amount/100m):p.PriceMinor+amount*100m;
            var price=checked((long)(decimal.Round(raw/roundToMinor,0,MidpointRounding.AwayFromZero)*roundToMinor));
            if(price<0) throw new ArgumentException("El cambio genera precios negativos.");return new PricePreview(p.Id,p.Name,p.PriceMinor,price);
        }).ToList();
    }
    static void AuditPrice(SqliteConnection c,SqliteTransaction t,long id,long old,long next,string reason) => c.Execute("INSERT INTO price_changes(product_id,old_price_minor,new_price_minor,changed_at,reason) VALUES(@id,@old,@next,@now,@reason)",new{id,old,next,now=Now,reason},t);
    public void ApplyPrices(IReadOnlyList<PricePreview> preview)
    {
        if(preview.Select(x=>x.ProductId).Distinct().Count()!=preview.Count || preview.Any(x=>x.NewPriceMinor<0)) throw new ArgumentException("Vista previa inválida.");
        using var c=Open();using var t=c.BeginTransaction();foreach(var p in preview)
        {
            if(c.Execute("UPDATE products SET price_minor=@next,updated_at=@now WHERE id=@id AND price_minor=@old AND is_active=1",new{next=p.NewPriceMinor,now=Now,id=p.ProductId,old=p.OldPriceMinor},t)!=1) throw new ArgumentException("Los productos cambiaron. Generá nuevamente la vista previa.");
            AuditPrice(c,t,p.ProductId,p.OldPriceMinor,p.NewPriceMinor,"Cambio masivo");
        }t.Commit();
    }
    public void Backup(string destination)
    {
        var target=Path.GetFullPath(destination);if(target==path || File.Exists(target)) throw new ArgumentException("Elegí un archivo nuevo para el respaldo.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var c=Open();using var backup=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=target,Pooling=false}.ToString());backup.Open();c.BackupDatabase(backup);ValidateBackup(backup);
    }
    static void ValidateBackup(SqliteConnection c)
    {
        if(c.ExecuteScalar<string>("PRAGMA integrity_check")!="ok" || c.Query("PRAGMA foreign_key_check").Any() || c.ExecuteScalar<int>("PRAGMA user_version")!=1) throw new ArgumentException("El respaldo no es válido o su versión no es compatible.");
        using var expected=new SqliteConnection("Data Source=:memory:");expected.Open();
        using var stream=typeof(PosService).Assembly.GetManifestResourceStream("POS.Infrastructure.schema.sql")!;
        using var reader=new StreamReader(stream);expected.Execute(reader.ReadToEnd());
        const string schemaQuery="SELECT type || ':' || name || ':' || coalesce(sql,'') FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name";
        if(!c.Query<string>(schemaQuery).SequenceEqual(expected.Query<string>(schemaQuery))) throw new ArgumentException("El archivo no tiene el esquema de un respaldo MiPOS compatible.");
    }
    public void Restore(string source)
    {
        var src=Path.GetFullPath(source);if(src==path) throw new ArgumentException("Elegí un respaldo externo.");
        using var backup=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=src,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());backup.Open();ValidateBackup(backup);
        Backup(path+".before-restore-"+Guid.NewGuid().ToString("N")+".db");
        using var destination=Open();backup.BackupDatabase(destination);
    }
    public void DailyBackup(string directory,int retention=7)
    {
        Directory.CreateDirectory(directory);var destination=Path.Combine(directory,$"pos-{DateTime.Now:yyyy-MM-dd}.db");
        if(!File.Exists(destination)) Backup(destination);
        foreach(var f in Directory.GetFiles(directory,"pos-????-??-??.db").OrderByDescending(Path.GetFileName).Skip(retention)) File.Delete(f);
    }
}
