using System.Windows;
using POS.Core;
namespace POS.Desktop;
public sealed record UnitOption(SaleUnit Value,string Label);
public sealed record ModeOption(StockMode Value,string Label);
public sealed class ProductEditor : Observable
{
    public long Id{get;init;}public bool IsNew=>Id==0;
    public string Name{get;set;}="";public string Code{get;set;}="";public string Category{get;set;}="";public SaleUnit Unit{get;set;}public StockMode Mode{get;set;}
    public string Price{get;set;}="0";public string Cost{get;set;}="";public bool Active{get;set;}=true;
    string error="";public string Error{get=>error;set{error=value;Changed();}}
    public IReadOnlyList<UnitOption> Units{get;}=Enum.GetValues<SaleUnit>().Select(x=>new UnitOption(x,Display.Unit(x))).ToList();
    public IReadOnlyList<ModeOption> Modes{get;}=Enum.GetValues<StockMode>().Select(x=>new ModeOption(x,Display.Mode(x))).ToList();
}
public partial class ProductWindow : Wpf.Ui.Controls.FluentWindow
{
    readonly ProductEditor editor;
    public Product? Result{get;private set;}
    public ProductWindow(Product? p)
    {
        InitializeComponent();editor=p==null?new():new(){Id=p.Id,Name=p.Name,Code=p.Code??"",Category=p.Category,Unit=p.Unit,Mode=p.Mode,Price=(p.PriceMinor/100m).ToString(Display.Culture),Cost=p.CostMinor is long cost?(cost/100m).ToString(Display.Culture):"",Active=p.Active};DataContext=editor;
    }
    void SaveClick(object sender,RoutedEventArgs e)
    {
        try{Result=new(editor.Id,editor.Code,editor.Name,editor.Category,editor.Unit,editor.Mode,Pricing.Money(Display.Parse(editor.Price)),string.IsNullOrWhiteSpace(editor.Cost)?null:Pricing.Money(Display.Parse(editor.Cost)),editor.Active);Pricing.Validate(Result);DialogResult=true;}
        catch(ArgumentException ex){editor.Error=ex.Message;}catch(FormatException){editor.Error="Revisá los importes. Usá coma para los decimales.";}catch(OverflowException){editor.Error="El importe es demasiado grande.";}
    }
}
