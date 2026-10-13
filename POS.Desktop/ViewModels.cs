using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using POS.Core;

namespace POS.Desktop;
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
public sealed class ActionCommand(Action action) : ICommand
{
    public event EventHandler? CanExecuteChanged {add{} remove{}}
    public bool CanExecute(object? parameter)=>true;
    public void Execute(object? parameter)=>action();
}
public static class Display
{
    public static readonly CultureInfo Culture=CultureInfo.GetCultureInfo("es-AR");
    public static string Money(long value)=>(value/100m).ToString("C2",Culture);
    public static string Unit(SaleUnit unit)=>unit switch{SaleUnit.KG=>"kg",SaleUnit.PACK=>"pack",_=>"unidad"};
    public static string Quantity(long value,SaleUnit unit)=>(value/(unit==SaleUnit.KG?1000m:1m)).ToString("0.###",Culture)+" "+Unit(unit);
    public static decimal Parse(string value)=>decimal.Parse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite,Culture);
    public static string Date(string value)=>DateTime.Parse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToLocalTime().ToString("g",Culture);
    public static string Mode(StockMode mode)=>mode switch{StockMode.EXACT=>"Exacto",StockMode.WEIGHT=>"Por peso",_=>"Manual"};
    public static string Payment(PaymentMethod method)=>method switch{PaymentMethod.CASH=>"Efectivo",PaymentMethod.TRANSFER=>"Transferencia",PaymentMethod.CARD=>"Tarjeta",_=>"Otro"};
}
public sealed record ProductView(Product Product)
{
    public string Name=>Product.Name;public string Price=>Display.Money(Product.PriceMinor);public string UnitLabel=>Display.Unit(Product.Unit);public string ModeLabel=>Display.Mode(Product.Mode);
}
public sealed record CartView(Product Product,long QuantityBase)
{
    public string Name=>Product.Name;public string QuantityLabel=>Display.Quantity(QuantityBase,Product.Unit);public string TotalLabel=>Display.Money(Pricing.Total(Product.PriceMinor,QuantityBase,Product.Unit));
}
public sealed record InventoryView(InventoryRow Row,SaleUnit Unit)
{
    public string Quantity=>Row.QuantityBase is long q?Display.Quantity(q,Unit):"Control manual";
    public string Status=>Row.Mode==StockMode.MANUAL?Row.Status.ToString():Row.QuantityBase==0?"REPONER":Row.MinimumBase is long min&&Row.QuantityBase<=min?"BAJO":"SUFICIENTE";
}
public sealed record PreviewView(PricePreview Row){public string Name=>Row.Name;public string OldPrice=>Display.Money(Row.OldPriceMinor);public string NewPrice=>Display.Money(Row.NewPriceMinor);}
public sealed record SaleView(SaleSummary Sale)
{
    public string Date=>Display.Date(Sale.CreatedAt);public string Status=>Sale.Status=="COMPLETED"?"Completada":"Anulada";public string Payment=>Display.Payment(Enum.Parse<PaymentMethod>(Sale.PaymentMethod));public string Total=>Display.Money(Sale.TotalMinor);
}
public sealed record DetailView(SaleItem Item){public string Name=>Item.Name;public string Quantity=>Display.Quantity(Item.QuantityBase,Item.Unit);public string Price=>Display.Money(Item.UnitPriceMinor)+" / "+Display.Unit(Item.Unit);public string Total=>Display.Money(Item.TotalMinor);}
public sealed record MovementView(Movement Row,SaleUnit Unit)
{
    public string Date=>Display.Date(Row.CreatedAt);public string Type=>Row.Type switch{"SALE"=>"Venta","RESTOCK"=>"Reposición","ADJUSTMENT"=>"Ajuste",_=>"Anulación"};public string Quantity=>Display.Quantity(Row.DeltaBase,Unit);public string? Note=>Row.Note;
}
public sealed record PaymentOption(PaymentMethod Value,string Label);

public sealed class MainViewModel : Observable
{
    readonly IPosService service;
    string search="",message="Todo listo. Buscá un producto para comenzar.";bool includeInactive;
    public MainViewModel(IPosService service)
    {
        this.service=service;
        AddCartCommand=Command(AddCart);EditCartCommand=Command(EditCart);RemoveCartCommand=Command(()=>{if(SelectedCart!=null)Cart.Remove(SelectedCart);CartChanged();});CheckoutCommand=Command(Checkout);
        NewProductCommand=Command(()=>EditProduct(null));EditProductCommand=Command(()=>EditProduct(RequireProduct().Product));ToggleProductCommand=Command(()=>{var p=RequireProduct().Product;if(Confirm(p.Active?"¿Desactivar este producto?":"¿Activar este producto?")){service.SetActive(p.Id,!p.Active);Refresh();}});
        MinimumStockCommand=Command(()=>{var row=RequireInventory();var n=Display.Parse(MinimumQuantity);service.SetMinimumStock(row.Row.ProductId,n==0?0:Pricing.Quantity(n,row.Unit));Refresh();Message="Mínimo de reposición actualizado.";});
        RestockCommand=Command(()=>Stock(false));AdjustStockCommand=Command(()=>Stock(true));ManualStatusCommand=Command(()=>{service.SetManualStatus(RequireInventory().Row.ProductId,ManualStatus);Refresh();});MovementsCommand=Command(LoadMovements);
        PreviewCommand=Command(()=>{Preview=new(service.PreviewPrices(PriceCategory,Display.Parse(PriceAmount),IsPercentage,Pricing.Money(Display.Parse(PriceRounding))).Select(x=>new PreviewView(x)));Changed(nameof(Preview));Changed(nameof(PreviewCount));});
        ApplyPricesCommand=Command(()=>{if(Preview.Count==0)throw new ArgumentException("Generá una vista previa primero.");if(Confirm($"¿Aplicar los nuevos precios a {Preview.Count} productos?")){service.ApplyPrices(Preview.Select(x=>x.Row).ToList());Preview.Clear();Refresh();Changed(nameof(PreviewCount));Message="Precios actualizados. El carrito se recalculará antes de cobrar.";}});
        HistoryCommand=Command(LoadHistory);SaleDetailsCommand=Command(LoadDetails);CancelSaleCommand=Command(()=>{var s=RequireSale();if(Confirm($"¿Anular la venta {s.Sale.Id} y devolver sus existencias?")){service.CancelSale(s.Sale.Id,CancellationReason);Refresh();LoadHistory();}});
        BackupCommand=Command(()=>{var dialog=new SaveFileDialog{Filter="Respaldo SQLite (*.db)|*.db",FileName=$"MiPOS-{DateTime.Now:yyyyMMdd-HHmmss}.db",OverwritePrompt=true};if(dialog.ShowDialog()==true){service.Backup(dialog.FileName);Message="Respaldo exportado y verificado. Conservá una copia en una unidad externa.";}});
        RestoreCommand=Command(()=>{var dialog=new OpenFileDialog{Filter="Respaldo SQLite (*.db)|*.db"};if(dialog.ShowDialog()==true&&Confirm("¿Restaurar este respaldo y reemplazar los datos actuales? Se conservará una copia previa. El carrito se vaciará.")){service.Restore(dialog.FileName);Cart.Clear();CartChanged();Refresh();LoadHistory();Message="Respaldo restaurado. Se conservó una copia de los datos anteriores.";}});
        Refresh();LoadHistory();if(Products.Count==0)Message="Todavía no hay productos. Abrí Productos y elegí Nuevo producto.";
    }
    ICommand Command(Action action)=>new ActionCommand(()=>{try{action();}catch(ArgumentException e){Message=e.Message;}catch(OverflowException){Message="El importe o la cantidad es demasiado grande.";}catch(FormatException){Message="Revisá el número ingresado. Usá coma para los decimales.";}catch(Exception){Message="No se pudo completar la operación. Revisá los datos y el acceso al archivo; tus datos no se borraron.";}});
    static bool Confirm(string text)=>MessageBox.Show(text,"Confirmación",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
    ProductView RequireProduct()=>SelectedProduct??throw new ArgumentException("Seleccioná un producto.");
    InventoryView RequireInventory()=>SelectedInventory??throw new ArgumentException("Seleccioná un producto del inventario.");
    SaleView RequireSale()=>SelectedSale??throw new ArgumentException("Seleccioná una venta.");
    public string Message{get=>message;set{message=value;Changed();}}
    public string Search{get=>search;set{search=value;Changed();RefreshProducts();}}
    public bool IncludeInactive{get=>includeInactive;set{includeInactive=value;Changed();RefreshProducts();}}
    public ObservableCollection<ProductView> Products{get;private set;}=[];
    public ProductView? SelectedProduct{get;set;}
    public ObservableCollection<CartView> Cart{get;}=[];
    public CartView? SelectedCart{get;set;}
    public string SaleQuantity{get;set;}="1";public string CartQuantity{get;set;}="1";
    public string Total=>Display.Money(Cart.Aggregate(0L,(s,x)=>checked(s+Pricing.Total(x.Product.PriceMinor,x.QuantityBase,x.Product.Unit))));
    public Visibility EmptyCartVisibility=>Cart.Count==0?Visibility.Visible:Visibility.Collapsed;
    public IReadOnlyList<PaymentOption> Payments{get;}=Enum.GetValues<PaymentMethod>().Select(x=>new PaymentOption(x,Display.Payment(x))).ToList();
    public PaymentOption Payment{get;set;}=new(PaymentMethod.CASH,"Efectivo");
    public ObservableCollection<InventoryView> Inventory{get;private set;}=[];public InventoryView? SelectedInventory{get;set;}
    public string MinimumQuantity{get;set;}="0";public string StockQuantity{get;set;}="1";public string StockNote{get;set;}="";
    public IReadOnlyList<ReplenishmentStatus> Statuses{get;}=Enum.GetValues<ReplenishmentStatus>();public ReplenishmentStatus ManualStatus{get;set;}
    public ObservableCollection<MovementView> Movements{get;private set;}=[];
    public string PriceCategory{get;set;}="";public string PriceAmount{get;set;}="10";public bool IsPercentage{get;set;}=true;public string PriceRounding{get;set;}="0,01";
    public ObservableCollection<PreviewView> Preview{get;private set;}=[];public string PreviewCount=>$"{Preview.Count} productos en la vista previa";
    public DateTime? From{get;set;}=DateTime.Today;public DateTime? To{get;set;}=DateTime.Today;
    public ObservableCollection<SaleView> Sales{get;private set;}=[];public SaleView? SelectedSale{get;set;}
    public ObservableCollection<DetailView> SaleDetails{get;private set;}=[];public string CancellationReason{get;set;}="";
    public ICommand AddCartCommand{get;}public ICommand EditCartCommand{get;}public ICommand RemoveCartCommand{get;}public ICommand CheckoutCommand{get;}
    public ICommand NewProductCommand{get;}public ICommand EditProductCommand{get;}public ICommand ToggleProductCommand{get;}
    public ICommand MinimumStockCommand{get;}public ICommand RestockCommand{get;}public ICommand AdjustStockCommand{get;}public ICommand ManualStatusCommand{get;}public ICommand MovementsCommand{get;}
    public ICommand PreviewCommand{get;}public ICommand ApplyPricesCommand{get;}public ICommand HistoryCommand{get;}public ICommand SaleDetailsCommand{get;}public ICommand CancelSaleCommand{get;}public ICommand BackupCommand{get;}public ICommand RestoreCommand{get;}
    void RefreshProducts(){Products=new(service.Products(Search,IncludeInactive).Select(x=>new ProductView(x)));Changed(nameof(Products));}
    void Refresh(){RefreshProducts();var products=service.Products(includeInactive:true).ToDictionary(x=>x.Id);Inventory=new(service.Inventory().Select(x=>new InventoryView(x,products[x.ProductId].Unit)));Changed(nameof(Inventory));}
    void CartChanged(){Changed(nameof(Total));Changed(nameof(EmptyCartVisibility));}
    void AddCart(){var p=RequireProduct().Product;if(!p.Active)throw new ArgumentException("El producto está inactivo.");var q=Pricing.Quantity(Display.Parse(SaleQuantity),p.Unit);var existing=Cart.FirstOrDefault(x=>x.Product.Id==p.Id);if(existing!=null){q=checked(q+existing.QuantityBase);Cart.Remove(existing);}Cart.Add(new(p,q));CartChanged();}
    void EditCart(){var item=SelectedCart??throw new ArgumentException("Seleccioná una línea del carrito.");var q=Pricing.Quantity(Display.Parse(CartQuantity),item.Product.Unit);var index=Cart.IndexOf(item);Cart[index]=new(item.Product,q);CartChanged();}
    void Checkout()
    {
        if(Cart.Count==0)throw new ArgumentException("El carrito está vacío.");
        var current=service.Products(includeInactive:true).ToDictionary(x=>x.Id);
        bool changed=Cart.Any(x=>!current.TryGetValue(x.Product.Id,out var p)||p.PriceMinor!=x.Product.PriceMinor||p.Name!=x.Product.Name);
        for(var i=0;i<Cart.Count;i++){var line=Cart[i];if(!current.TryGetValue(line.Product.Id,out var p)||!p.Active)throw new ArgumentException("Hay productos inactivos en el carrito. Quitalos antes de cobrar.");Cart[i]=new(p,line.QuantityBase);}CartChanged();
        if(changed){Message="Los precios del carrito se actualizaron. Revisá el nuevo total y volvé a confirmar.";return;}
        if(!Confirm($"¿Confirmar cobro de {Total} por {Payment.Label}?"))return;
        var id=service.CompleteSale(Cart.Select(x=>new CartLine(x.Product.Id,x.QuantityBase)).ToList(),Payment.Value);Cart.Clear();CartChanged();Refresh();LoadHistory();Message=$"Venta {id} registrada correctamente.";
    }
    void EditProduct(Product? product){var dialog=new ProductWindow(product){Owner=Application.Current.MainWindow};if(dialog.ShowDialog()==true){service.SaveProduct(dialog.Result!);Refresh();Message="Producto guardado.";}}
    void Stock(bool absolute)
    {
        var row=RequireInventory();var value=Display.Parse(StockQuantity);long q;
        if(absolute&&value==0)q=0;else q=Pricing.Quantity(value,row.Unit);
        if(absolute&&!Confirm("¿Establecer el stock al valor indicado?"))return;
        service.AdjustStock(row.Row.ProductId,q,absolute,StockNote);Refresh();Message="Inventario actualizado.";
    }
    void LoadMovements(){var p=RequireInventory();Movements=new(service.Movements(p.Row.ProductId).Select(x=>new MovementView(x,p.Unit)));Changed(nameof(Movements));}
    void LoadHistory(){if(From==null||To==null||From>To)throw new ArgumentException("Revisá las fechas del historial.");Sales=new(service.Sales(From.Value.Date,To.Value.Date.AddDays(1)).Select(x=>new SaleView(x)));SaleDetails.Clear();Changed(nameof(Sales));}
    void LoadDetails(){SaleDetails=new(service.SaleItems(RequireSale().Sale.Id).Select(x=>new DetailView(x)));Changed(nameof(SaleDetails));}
}
