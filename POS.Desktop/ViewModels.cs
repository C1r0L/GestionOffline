using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using POS.Core;
using InfoBarSeverity=Wpf.Ui.Controls.InfoBarSeverity;
using SymbolRegular=Wpf.Ui.Controls.SymbolRegular;

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
public sealed class ParameterCommand(Action<object?> action) : ICommand
{
    public event EventHandler? CanExecuteChanged {add{} remove{}}
    public bool CanExecute(object? parameter)=>true;
    public void Execute(object? parameter)=>action(parameter);
}
public static class Display
{
    public static readonly CultureInfo Culture=CultureInfo.GetCultureInfo("es-AR");
    public static string Money(long value)=>(value/100m).ToString("C2",Culture);
    public static string Unit(SaleUnit unit)=>unit switch{SaleUnit.KG=>"kg",SaleUnit.PACK=>"pack",_=>"unidad"};
    public static string Quantity(long value,SaleUnit unit)=>(value/(unit==SaleUnit.KG?1000m:1m)).ToString("0.###",Culture)+" "+Unit(unit);
    public static decimal Parse(string value)=>decimal.Parse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite,Culture);
    public static string Date(string value)=>DateTime.Parse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToLocalTime().ToString("dd/MM/yyyy HH:mm",Culture);
    public static string Mode(StockMode mode)=>mode switch{StockMode.EXACT=>"Exacto",StockMode.WEIGHT=>"Por peso",_=>"Manual"};
    public static string UnitTitle(SaleUnit unit)=>unit switch{SaleUnit.KG=>"Kilo",SaleUnit.PACK=>"Pack",_=>"Unidad"};
    /// <summary>Ganancia sobre el costo, redondeada al entero: (precio − costo) / costo.</summary>
    public static string Margin(long price,long? cost)=>cost is long c&&c>0?decimal.Round((price-c)*100m/c,0,MidpointRounding.AwayFromZero).ToString(Culture)+" %":"—";
    /// <summary>Importe sin decimales cuando es un valor entero de pesos: "$ 4.500".</summary>
    public static string MoneyShort(long value)=>(value/100m).ToString(value%100==0?"C0":"C2",Culture);
    /// <summary>Cantidad compacta para tarjetas y carrito: "2 u.", "3 packs", "0,5 kg".</summary>
    public static string ShortQuantity(long value,SaleUnit unit)=>unit switch{SaleUnit.KG=>(value/1000m).ToString("0.###",Culture)+" kg",SaleUnit.PACK=>value==1?"1 pack":value.ToString(Culture)+" packs",_=>value.ToString(Culture)+" u."};
    public static string Today{get{var t=DateTime.Today.ToString("dddd d 'de' MMMM",Culture);return char.ToUpper(t[0],Culture)+t[1..];}}
    public static string Payment(PaymentMethod method)=>method switch{PaymentMethod.CASH=>"Efectivo",PaymentMethod.TRANSFER=>"Transferencia",PaymentMethod.CARD=>"Tarjeta",_=>"Otro"};
}
public sealed record ProductView(Product Product)
{
    public string Name=>Product.Name;public string Price=>Display.Money(Product.PriceMinor);public string UnitLabel=>Display.Unit(Product.Unit);public string ModeLabel=>Display.Mode(Product.Mode);
    public string Code=>string.IsNullOrWhiteSpace(Product.Code)?"Sin código":Product.Code;public string Category=>Product.Category.Length==0?"Sin categoría":Product.Category;
    public string SellLabel=>Display.UnitTitle(Product.Unit)+" · "+Product.Mode switch{StockMode.EXACT=>"stock exacto",StockMode.WEIGHT=>"por peso",_=>"manual"};
    public string Margin=>Display.Margin(Product.PriceMinor,Product.CostMinor);
    public string StateLabel=>Product.Active?"Activo":"Inactivo";public string ToggleLabel=>Product.Active?"Desactivar":"Activar";
}
public sealed record CartView(Product Product,long QuantityBase)
{
    public string Name=>Product.Name;public string QuantityLabel=>Display.Quantity(QuantityBase,Product.Unit);public string TotalLabel=>Display.Money(Pricing.Total(Product.PriceMinor,QuantityBase,Product.Unit));
    public string ShortQuantity=>Display.ShortQuantity(QuantityBase,Product.Unit);public string UnitPriceLabel=>Display.MoneyShort(Product.PriceMinor)+" / "+Display.Unit(Product.Unit);
    /// <summary>Paso de los botones +/−: una unidad o pack, o 100 g en productos por peso.</summary>
    public long Step=>Product.Unit==SaleUnit.KG?100:1;
}
/// <summary>Tarjeta de producto en Nueva venta, con su estado de stock.</summary>
public sealed record SaleTile(Product Product,InventoryView? Stock)
{
    public string Name=>Product.Name;public string Category=>Product.Category;public string Price=>Display.MoneyShort(Product.PriceMinor);public string UnitLabel=>Display.Unit(Product.Unit);
    /// <summary>ok, low, out o none: elige el color de la etiqueta de stock.</summary>
    public string StockKind=>Stock==null?"none":Stock.Status switch{"REPONER"=>"out","BAJO"=>"low",_=>Product.Mode==StockMode.MANUAL?"none":"ok"};
    public string StockLabel=>Stock==null?"":Stock.Row.QuantityBase is long q?(q==0?"Sin stock":Stock.Status=="BAJO"?"Bajo · "+Display.ShortQuantity(q,Product.Unit):Display.ShortQuantity(q,Product.Unit)):Stock.Status switch{"REPONER"=>"Reponer","BAJO"=>"Bajo",_=>""};
}
public sealed record PreviewView(PricePreview Row){public string Name=>Row.Name;public string OldPrice=>Display.MoneyShort(Row.OldPriceMinor);public string NewPrice=>Display.MoneyShort(Row.NewPriceMinor);}
public sealed record RoundingOption(long Minor,string Label);
public sealed record PaymentOption(PaymentMethod Value,string Label);

public sealed partial class MainViewModel : Observable
{
    readonly IPosService service;
    string search="",saleSearch="",saleCategory=AllCategories,message="";CartView? selectedCart;bool includeInactive,hasMessage;int section;InfoBarSeverity severity=InfoBarSeverity.Informational;
    public MainViewModel(IPosService service)
    {
        this.service=service;
        Payment=Payments[0];priceRounding=Roundings[0];
        AddProductCommand=ParamCommand(p=>{if(p is SaleTile t)AddToCart(t.Product);});QuickAddCommand=Command(QuickAdd);
        IncrementCommand=ParamCommand(p=>ChangeLine(p as CartView,+1));DecrementCommand=ParamCommand(p=>ChangeLine(p as CartView,-1));
        EditLineCommand=ParamCommand(p=>{if(p is CartView line&&QuantityDialog.Ask(line.Product,line.QuantityBase) is long q)ReplaceLine(line,q);});
        RemoveLineCommand=ParamCommand(p=>{if(p is CartView line){Cart.Remove(line);CartChanged();}});
        ClearCartCommand=Command(()=>{if(Cart.Count>0&&ConfirmDialog.Ask("Vaciar carrito",$"Se quitarán los {Cart.Count} productos del carrito.","Vaciar",danger:true)){Cart.Clear();CartChanged();}});
        PickCategoryCommand=ParamCommand(p=>{if(p is string c)SaleCategory=c;});PickPaymentCommand=ParamCommand(p=>{if(p is PaymentOption o)Payment=o;});
        CheckoutCommand=Command(()=>{if(Section==0)Checkout();});
        NavigateCommand=new ParameterCommand(p=>{if(int.TryParse(p as string,out var s))Section=s;});
        NewProductCommand=Command(()=>EditProduct(null));EditProductCommand=ParamCommand(x=>EditProduct(((x as ProductView)??RequireProduct()).Product));
        TogglePricePanelCommand=Command(()=>{ShowPricePanel=!ShowPricePanel;if(ShowPricePanel)UpdatePreview();});
        ToggleProductCommand=ParamCommand(x=>{var p=((x as ProductView)??RequireProduct()).Product;if(p.Active?ConfirmDialog.Ask("Desactivar producto",$"«{p.Name}» dejará de aparecer en la venta. Podés volver a activarlo cuando quieras.","Desactivar"):ConfirmDialog.Ask("Activar producto",$"«{p.Name}» volverá a estar disponible para la venta.","Activar")){service.SetActive(p.Id,!p.Active);Refresh();Message=p.Active?$"«{p.Name}» desactivado. Activá «Mostrar inactivos» para verlo.":$"«{p.Name}» activado.";}});
        ApplyPricesCommand=Command(()=>{if(Preview.Count==0)throw new ArgumentException(PreviewError.Length>0?PreviewError:"Con estos valores ningún precio cambia.");if(ConfirmDialog.Ask("Aplicar nuevos precios",$"Se actualizará el precio de {Preview.Count} productos según la vista previa.","Aplicar precios")){try{service.ApplyPrices(Preview.Select(x=>x.Row).ToList());ShowPricePanel=false;}finally{Refresh();UpdatePreview();}Message=$"Precios actualizados. El carrito se recalculará antes de cobrar.";}});
        BackupCommand=Command(()=>{var dialog=new SaveFileDialog{Filter="Respaldo SQLite (*.db)|*.db",FileName=$"MiPOS-{DateTime.Now:yyyyMMdd-HHmmss}.db",OverwritePrompt=true};if(dialog.ShowDialog()==true){service.Backup(dialog.FileName);Message="Respaldo exportado y verificado. Conservá una copia en una unidad externa.";}});
        RestoreCommand=Command(()=>{var dialog=new OpenFileDialog{Filter="Respaldo SQLite (*.db)|*.db"};if(dialog.ShowDialog()==true&&ConfirmDialog.Ask("Restaurar respaldo","Los datos actuales se reemplazarán por los del respaldo. Se conservará una copia previa y el carrito se vaciará.","Restaurar",danger:true)){service.Restore(dialog.FileName);Cart.Clear();CartChanged();Refresh();LoadHistory();Message="Respaldo restaurado. Se conservó una copia de los datos anteriores.";}});
        Refresh();LoadHistory();if(Products.Count==0)Notify("Todavía no hay productos. Abrí Productos y elegí Nuevo producto.",InfoBarSeverity.Informational);
    }
    ICommand Command(Action action)=>new ActionCommand(()=>Guard(action));
    ICommand ParamCommand(Action<object?> action)=>new ParameterCommand(p=>Guard(()=>action(p)));
    void Guard(Action action){try{action();}catch(ArgumentException e){Fail(e.Message);}catch(OverflowException){Fail("El importe o la cantidad es demasiado grande.");}catch(FormatException){Fail("Revisá el número ingresado. Usá coma para los decimales.");}catch(Exception){Fail("No se pudo completar la operación. Revisá los datos y el acceso al archivo; tus datos no se borraron.");}}
    ProductView RequireProduct()=>SelectedProduct??throw new ArgumentException("Seleccioná un producto.");
    /// <summary>Mensaje de resultado; asignarlo informa un éxito. Para errores usar <see cref="Fail"/>.</summary>
    public string Message{get=>message;set=>Notify(value,InfoBarSeverity.Success);}
    public InfoBarSeverity MessageSeverity=>severity;
    public string MessageTitle=>severity switch{InfoBarSeverity.Error=>"No se pudo completar",InfoBarSeverity.Warning=>"Atención",InfoBarSeverity.Success=>"Listo",_=>""};
    public bool HasMessage{get=>hasMessage;set{hasMessage=value;Changed();}}
    public void Fail(string text)=>Notify(text,InfoBarSeverity.Error);
    void Notify(string text,InfoBarSeverity kind){message=text;severity=kind;Changed(nameof(Message));Changed(nameof(MessageSeverity));Changed(nameof(MessageTitle));HasMessage=text.Length>0;}
    public int Section{get=>section;set{section=value;Changed();if(value==4)Guard(LoadClosing);}}
    public ICommand NavigateCommand{get;}
    public int AlertCount{get;private set;}
    public Visibility AlertVisibility=>AlertCount>0?Visibility.Visible:Visibility.Collapsed;
    public string BackupStatus{get;private set;}="";
    public SymbolRegular BackupSymbol{get;private set;}=SymbolRegular.CheckmarkCircle24;
    public Brush BackupBrush{get;private set;}=Brushes.Transparent;
    public void SetDailyBackup(DateTime? at)
    {
        var ok=at!=null;BackupStatus=ok?$"Respaldo diario · {at:HH:mm}":"Sin respaldo diario";
        BackupSymbol=ok?SymbolRegular.CheckmarkCircle24:SymbolRegular.Warning24;BackupBrush=new SolidColorBrush(ok?Color.FromRgb(0x12,0x80,0x5C):Color.FromRgb(0xA3,0x26,0x1F));
        Changed(nameof(BackupStatus));Changed(nameof(BackupSymbol));Changed(nameof(BackupBrush));
    }
    public string Search{get=>search;set{search=value;Changed();RefreshProducts();}}
    public const string AllProductCategories="Todas";
    string productCategory=AllProductCategories;
    public string ProductCategory{get=>productCategory;set{productCategory=value??AllProductCategories;Changed();RefreshProducts();}}
    public IReadOnlyList<string> ProductCategories{get;private set;}=[AllProductCategories];
    public string ProductCountLabel{get;private set;}="";
    public bool IncludeInactive{get=>includeInactive;set{includeInactive=value;Changed();RefreshProducts();}}
    public ObservableCollection<ProductView> Products{get;private set;}=[];
    public ProductView? SelectedProduct{get;set;}
    public const string AllCategories="Todos";
    /// <summary>Categorías existentes, para sugerir en el diálogo de producto.</summary>
    public IReadOnlyList<string> AllCategoryNames{get;private set;}=[];
    public string TodayLabel=>Display.Today;
    public string SaleSearch{get=>saleSearch;set{saleSearch=value;Changed();RefreshSaleProducts();}}
    public string SaleCategory{get=>saleCategory;set{saleCategory=value;Changed();RefreshSaleProducts();}}
    public IReadOnlyList<string> Categories{get;private set;}=[AllCategories];
    public ObservableCollection<SaleTile> SaleProducts{get;private set;}=[];
    public Visibility NoResultsVisibility=>SaleProducts.Count==0?Visibility.Visible:Visibility.Collapsed;
    /// <summary>Pide a la vista volver a la búsqueda tras agregar o cobrar, para seguir escaneando.</summary>
    public event Action? FocusSearchRequested;
    public ObservableCollection<CartView> Cart{get;}=[];
    public CartView? SelectedCart{get=>selectedCart;set{selectedCart=value;Changed();}}
    public string CartCountLabel=>Cart.Count switch{0=>"",1=>"· 1 producto",var n=>$"· {n} productos"};
    public string Total=>Display.Money(Cart.Aggregate(0L,(s,x)=>checked(s+Pricing.Total(x.Product.PriceMinor,x.QuantityBase,x.Product.Unit))));
    public Visibility EmptyCartVisibility=>Cart.Count==0?Visibility.Visible:Visibility.Collapsed;
    public IReadOnlyList<PaymentOption> Payments{get;}=Enum.GetValues<PaymentMethod>().Select(x=>new PaymentOption(x,Display.Payment(x))).ToList();
    PaymentOption payment=null!;
    public PaymentOption Payment{get=>payment;set{payment=value;Changed();}}
    bool showPricePanel,isPercentage=true;string priceCategory=AllProductCategories,priceAmount="10",previewError="";RoundingOption priceRounding=null!;
    public bool ShowPricePanel{get=>showPricePanel;set{showPricePanel=value;Changed();Changed(nameof(PricePanelVisibility));}}
    public Visibility PricePanelVisibility=>ShowPricePanel?Visibility.Visible:Visibility.Collapsed;
    public string PriceCategory{get=>priceCategory;set{priceCategory=value??AllProductCategories;Changed();UpdatePreview();}}
    public string PriceAmount{get=>priceAmount;set{priceAmount=value;Changed();UpdatePreview();}}
    public bool IsPercentage{get=>isPercentage;set{isPercentage=value;Changed();Changed(nameof(IsFixedAmount));Changed(nameof(AmountSuffix));UpdatePreview();}}
    public bool IsFixedAmount{get=>!isPercentage;set=>IsPercentage=!value;}
    public string AmountSuffix=>IsPercentage?"%":"$";
    public IReadOnlyList<RoundingOption> Roundings{get;}=[new(1,"$ 0,01"),new(100,"$ 1"),new(1000,"$ 10"),new(5000,"$ 50"),new(10000,"$ 100")];
    public RoundingOption PriceRounding{get=>priceRounding;set{priceRounding=value;Changed();UpdatePreview();}}
    public ObservableCollection<PreviewView> Preview{get;private set;}=[];
    public string PreviewCount=>Preview.Count==1?"Vista previa · 1 producto":$"Vista previa · {Preview.Count} productos";
    public string ApplyLabel=>Preview.Count==1?"Aplicar a 1 producto":$"Aplicar a {Preview.Count} productos";
    public string PreviewError{get=>previewError;private set{previewError=value;Changed();Changed(nameof(PreviewErrorVisibility));}}
    public Visibility PreviewErrorVisibility=>PreviewError.Length>0?Visibility.Visible:Visibility.Collapsed;
    /// <summary>Recalcula la vista previa del ajuste masivo cada vez que cambia un valor; los errores se muestran en el panel.</summary>
    void UpdatePreview()
    {
        if(!ShowPricePanel||priceRounding==null)return;
        try{Preview=new(service.PreviewPrices(PriceCategory==AllProductCategories?"":PriceCategory,Display.Parse(PriceAmount),IsPercentage,PriceRounding.Minor).Where(x=>x.NewPriceMinor!=x.OldPriceMinor).Select(x=>new PreviewView(x)));PreviewError="";}
        catch(Exception e) when(e is ArgumentException or FormatException or OverflowException){Preview=[];PreviewError=e is ArgumentException?e.Message:"Ingresá un número válido. Usá coma para los decimales.";}
        Changed(nameof(Preview));Changed(nameof(PreviewCount));Changed(nameof(ApplyLabel));
    }
    public ICommand AddProductCommand{get;}public ICommand QuickAddCommand{get;}public ICommand IncrementCommand{get;}public ICommand DecrementCommand{get;}public ICommand EditLineCommand{get;}public ICommand RemoveLineCommand{get;}
    public ICommand ClearCartCommand{get;}public ICommand PickCategoryCommand{get;}public ICommand PickPaymentCommand{get;}public ICommand CheckoutCommand{get;}
    public ICommand TogglePricePanelCommand{get;}public ICommand NewProductCommand{get;}public ICommand EditProductCommand{get;}public ICommand ToggleProductCommand{get;}
    public ICommand ApplyPricesCommand{get;}public ICommand BackupCommand{get;}public ICommand RestoreCommand{get;}
    void RefreshProducts()
    {
        Products=new(service.Products(Search,IncludeInactive).Where(x=>ProductCategory==AllProductCategories||x.Category==ProductCategory).Select(x=>new ProductView(x)));Changed(nameof(Products));
        var all=service.Products(includeInactive:true);var inactive=all.Count(x=>!x.Active);
        ProductCountLabel=$"{all.Count-inactive} activos · {inactive} inactivos";Changed(nameof(ProductCountLabel));
    }
    void Refresh(){RefreshProducts();var products=service.Products(includeInactive:true).ToDictionary(x=>x.Id);RefreshInventory(products);AlertCount=Inventory.Count(x=>x.Status!="SUFICIENTE");Changed(nameof(AlertCount));Changed(nameof(AlertVisibility));
        var names=products.Values.Where(x=>x.Category.Length>0).Select(x=>x.Category).Distinct().Order(StringComparer.Create(Display.Culture,true)).ToList();
        Categories=[AllCategories,..products.Values.Where(x=>x.Active&&x.Category.Length>0).Select(x=>x.Category).Distinct().Order(StringComparer.Create(Display.Culture,true))];Changed(nameof(Categories));
        ProductCategories=[AllProductCategories,..names];Changed(nameof(ProductCategories));AllCategoryNames=names;
        if(!ProductCategories.Contains(productCategory)){productCategory=AllProductCategories;Changed(nameof(ProductCategory));RefreshProducts();}
        if(!ProductCategories.Contains(priceCategory)){priceCategory=AllProductCategories;Changed(nameof(PriceCategory));}
        if(!Categories.Contains(saleCategory)){saleCategory=AllCategories;Changed(nameof(SaleCategory));}
        RefreshSaleProducts();}
    void RefreshSaleProducts()
    {
        var stock=Inventory.ToDictionary(x=>x.Row.ProductId);
        SaleProducts=new(service.Products(SaleSearch).Where(x=>SaleCategory==AllCategories||x.Category==SaleCategory).Select(x=>new SaleTile(x,stock.GetValueOrDefault(x.Id))));
        Changed(nameof(SaleProducts));Changed(nameof(NoResultsVisibility));
    }
    void CartChanged(){Changed(nameof(Total));Changed(nameof(EmptyCartVisibility));Changed(nameof(CartCountLabel));}
    /// <summary>Agrega una unidad o pack; los productos por peso piden la cantidad en kg.</summary>
    void AddToCart(Product p)
    {
        if(!p.Active)throw new ArgumentException("El producto está inactivo.");
        long q;if(p.Unit==SaleUnit.KG){if(QuantityDialog.Ask(p,null) is not long kg)return;q=kg;}else q=1;
        var existing=Cart.FirstOrDefault(x=>x.Product.Id==p.Id);
        if(existing!=null)ReplaceLine(existing,checked(existing.QuantityBase+q));else{Cart.Add(new(p,q));SelectedCart=Cart[^1];CartChanged();}
        FocusSearchRequested?.Invoke();
    }
    /// <summary>Enter en la búsqueda: prioriza el código exacto (lector de códigos) y si no, el primer resultado.</summary>
    void QuickAdd()
    {
        if(SaleSearch.Trim().Length==0)return;
        var tile=SaleProducts.FirstOrDefault(x=>string.Equals(x.Product.Code,SaleSearch.Trim(),StringComparison.OrdinalIgnoreCase))??SaleProducts.FirstOrDefault()??throw new ArgumentException($"No hay productos que coincidan con «{SaleSearch.Trim()}».");
        AddToCart(tile.Product);SaleSearch="";
    }
    void ChangeLine(CartView? line,int direction)
    {
        line??=SelectedCart;if(line==null)return;
        var q=line.QuantityBase+direction*line.Step;
        if(q<=0){Cart.Remove(line);CartChanged();}else ReplaceLine(line,q);
    }
    void ReplaceLine(CartView line,long quantity){var index=Cart.IndexOf(line);if(index<0)return;Cart[index]=new(line.Product,quantity);SelectedCart=Cart[index];CartChanged();}
    public void RemoveSelectedLine(){if(SelectedCart is CartView line){var index=Cart.IndexOf(line);Cart.Remove(line);CartChanged();if(Cart.Count>0)SelectedCart=Cart[Math.Min(index,Cart.Count-1)];}}
    public void ChangeSelectedLine(int direction)=>Guard(()=>ChangeLine(null,direction));
    void Checkout()
    {
        if(Cart.Count==0)throw new ArgumentException("El carrito está vacío.");
        var current=service.Products(includeInactive:true).ToDictionary(x=>x.Id);
        bool changed=Cart.Any(x=>!current.TryGetValue(x.Product.Id,out var p)||p.PriceMinor!=x.Product.PriceMinor||p.Name!=x.Product.Name);
        for(var i=0;i<Cart.Count;i++){var line=Cart[i];if(!current.TryGetValue(line.Product.Id,out var p)||!p.Active)throw new ArgumentException("Hay productos inactivos en el carrito. Quitalos antes de cobrar.");Cart[i]=new(p,line.QuantityBase);}CartChanged();
        if(changed){Notify("Los precios del carrito se actualizaron. Revisá el nuevo total y volvé a cobrar.",InfoBarSeverity.Warning);return;}
        var total=Cart.Aggregate(0L,(s,x)=>checked(s+Pricing.Total(x.Product.PriceMinor,x.QuantityBase,x.Product.Unit)));
        var result=CobroDialog.Ask(total,Payment,Cart.Count);if(!result.Confirmed)return;
        var id=service.CompleteSale(Cart.Select(x=>new CartLine(x.Product.Id,x.QuantityBase)).ToList(),Payment.Value);Cart.Clear();CartChanged();Refresh();LoadHistory();
        Message=result.Change is long change&&change>0?$"Venta n.º {id} registrada. Vuelto: {Display.Money(change)}.":$"Venta n.º {id} registrada.";
        FocusSearchRequested?.Invoke();
    }
    void EditProduct(Product? product){var dialog=new ProductWindow(product,AllCategoryNames,service.SaveProduct);DialogHelper.SetOwner(dialog);if(dialog.ShowDialog()==true){Refresh();UpdatePreview();Message=product==null?$"Producto «{dialog.Result!.Name.Trim()}» creado.":"Producto guardado.";}}
}
