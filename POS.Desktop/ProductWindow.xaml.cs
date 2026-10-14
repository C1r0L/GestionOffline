using System.Windows;
using POS.Core;
namespace POS.Desktop;
public sealed record ChoiceOption<T>(T Value,string Title,string Detail,bool Enabled);
/// <summary>Estado editable del diálogo de producto, con validación por campo.</summary>
public sealed class ProductEditor : Observable
{
    string name="",price="",cost="",nameError="",priceError="",costError="",error="";SaleUnit unit=SaleUnit.UNIT;StockMode mode=StockMode.EXACT;
    public long Id{get;init;}public bool IsNew=>Id==0;
    public string Title=>IsNew?"Nuevo producto":"Editar producto";
    public Visibility LockedVisibility=>IsNew?Visibility.Collapsed:Visibility.Visible;
    public string Name{get=>name;set{name=value;Changed();NameError="";}}
    public string Code{get;set;}="";public string Category{get;set;}="";public bool Active{get;set;}=true;
    public IReadOnlyList<string> Categories{get;init;}=[];
    /// <summary>Al cambiar la unidad se ajusta el control de stock a una combinación válida: kilo ↔ por peso, unidad o pack ↔ exacto.</summary>
    public SaleUnit Unit{get=>unit;set{unit=value;if(value==SaleUnit.KG&&mode==StockMode.EXACT)mode=StockMode.WEIGHT;else if(value!=SaleUnit.KG&&mode==StockMode.WEIGHT)mode=StockMode.EXACT;Changed();Changed(nameof(Modes));Changed(nameof(Mode));Changed(nameof(PriceLabel));}}
    public StockMode Mode{get=>mode;set{mode=value;Changed();}}
    public IReadOnlyList<ChoiceOption<SaleUnit>> Units=>[new(SaleUnit.UNIT,"Unidad","Se cuenta de a 1",IsNew),new(SaleUnit.KG,"Kilo","Se pesa (gramos)",IsNew),new(SaleUnit.PACK,"Pack","Se vende cerrado",IsNew)];
    public IReadOnlyList<ChoiceOption<StockMode>> Modes=>[new(StockMode.EXACT,"Exacto","Descuenta al vender",IsNew&&Unit!=SaleUnit.KG),new(StockMode.WEIGHT,"Por peso","Descuenta en kg",IsNew&&Unit==SaleUnit.KG),new(StockMode.MANUAL,"Manual","Solo estado",IsNew)];
    public string PriceLabel=>"Precio por "+Display.Unit(Unit);
    public string Price{get=>price;set{price=value;Changed();PriceError="";Changed(nameof(MarginLabel));}}
    public string Cost{get=>cost;set{cost=value;Changed();CostError="";Changed(nameof(MarginLabel));}}
    /// <summary>Ganancia sobre el costo calculada en vivo; vacía si falta algún importe.</summary>
    public string MarginLabel{get{try{return Price.Trim().Length>0&&Cost.Trim().Length>0?"Margen "+Display.Margin(Pricing.Money(Display.Parse(Price)),Pricing.Money(Display.Parse(Cost))):"Margen —";}catch(Exception e) when(e is FormatException or OverflowException){return "Margen —";}}}
    public string NameError{get=>nameError;set{nameError=value;Changed();}}
    public string PriceError{get=>priceError;set{priceError=value;Changed();}}
    public string CostError{get=>costError;set{costError=value;Changed();}}
    public string Error{get=>error;set{error=value;Changed();}}
    static long? Amount(string text,Action<string> fail,bool required)
    {
        if(text.Trim().Length==0){if(required)fail("Ingresá un precio.");return null;}
        try{var v=Pricing.Money(Display.Parse(text));if(v<0){fail("El importe no puede ser negativo.");return null;}return v;}
        catch(FormatException){fail("Revisá el importe. Usá coma para los decimales.");}catch(OverflowException){fail("El importe es demasiado grande.");}
        return null;
    }
    /// <summary>Valida todos los campos a la vez y devuelve el producto, o null marcando los errores.</summary>
    public Product? Build()
    {
        Error="";var ok=true;
        if(Name.Trim().Length==0){NameError="El nombre es obligatorio.";ok=false;}
        var p=Amount(Price,e=>{PriceError=e;ok=false;},true);var c=Amount(Cost,e=>{CostError=e;ok=false;},false);
        return ok&&p!=null?new(Id,Code,Name,MatchCategory(Category),Unit,Mode,p.Value,c,Active):null;
    }
    /// <summary>Si la categoría escrita coincide con una existente sin importar mayúsculas ni tildes, usa la existente para no duplicarla.</summary>
    string MatchCategory(string text)
    {
        var compare=Display.Culture.CompareInfo;
        return Categories.FirstOrDefault(x=>compare.Compare(x,text.Trim(),System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)==0)??text.Trim();
    }
}
public partial class ProductWindow : Wpf.Ui.Controls.FluentWindow
{
    readonly ProductEditor editor;
    readonly Func<Product,long> save;
    public Product? Result{get;private set;}
    public ProductWindow(Product? p,IReadOnlyList<string> categories,Func<Product,long> save)
    {
        InitializeComponent();this.save=save;
        editor=p==null?new(){Categories=categories}:new(){Id=p.Id,Name=p.Name,Code=p.Code??"",Category=p.Category,Unit=p.Unit,Mode=p.Mode,Price=(p.PriceMinor/100m).ToString("0.##",Display.Culture),Cost=p.CostMinor is long cost?(cost/100m).ToString("0.##",Display.Culture):"",Active=p.Active,Categories=categories};
        editor.NameError="";editor.PriceError="";DataContext=editor;
        Loaded+=(_,_)=>{DialogHelper.FitHeight(this,Root);NameBox.Focus();NameBox.SelectAll();};
    }
    void SaveClick(object sender,RoutedEventArgs e)
    {
        var product=editor.Build();if(product==null)return;
        try{Pricing.Validate(product);save(product);Result=product;DialogResult=true;}
        catch(ArgumentException ex){editor.Error=ex.Message;}
        catch(Exception){editor.Error="No se pudo guardar el producto. Revisá el acceso al archivo; tus datos no se borraron.";}
    }
}
