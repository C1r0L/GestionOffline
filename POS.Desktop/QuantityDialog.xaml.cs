using System.Windows;
using System.Windows.Controls;
using POS.Core;
using Wpf.Ui.Controls;
namespace POS.Desktop;
/// <summary>Pide la cantidad de un producto: kg con precisión de un gramo, o unidades y packs enteros.</summary>
public partial class QuantityDialog : FluentWindow
{
    readonly Product product;
    long? quantity;
    QuantityDialog(Product product,long? current)
    {
        InitializeComponent();this.product=product;
        var kg=product.Unit==SaleUnit.KG;
        ProductName.Text=product.Name;UnitPrice.Text=Display.MoneyShort(product.PriceMinor)+" / "+Display.Unit(product.Unit);
        FieldName.Text=kg?"Peso":"Cantidad";Suffix.Text=kg?"kg":product.Unit==SaleUnit.PACK?"packs":"unidades";
        Help.Text=kg?"Ej.: 0,5 es medio kilo. Precisión mínima: 1 gramo.":"Solo números enteros.";
        ConfirmButton.Content=current==null?"Agregar":"Guardar";
        Input.Text=current is long q?(q/(kg?1000m:1m)).ToString("0.###",Display.Culture):kg?"":"1";
        Update();
        Loaded+=(_,_)=>{DialogHelper.FitHeight(this,Root);Input.Focus();Input.SelectAll();};
    }
    void InputChanged(object sender,TextChangedEventArgs e)=>Update();
    void Update()
    {
        quantity=null;Error.Visibility=Visibility.Hidden;
        if(Input.Text.Trim().Length==0){Amount.Text="—";return;}
        try{quantity=Pricing.Quantity(Display.Parse(Input.Text),product.Unit);Amount.Text=Display.Money(Pricing.Total(product.PriceMinor,quantity.Value,product.Unit));}
        catch(Exception e) when(e is ArgumentException or FormatException or OverflowException){Amount.Text="—";Error.Text=e is ArgumentException?e.Message:"Revisá el número. Usá coma para los decimales.";Error.Visibility=Visibility.Visible;}
    }
    void ConfirmClick(object sender,RoutedEventArgs e){if(quantity!=null)DialogResult=true;else{Error.Text=Error.Visibility==Visibility.Visible?Error.Text:"Ingresá una cantidad.";Error.Visibility=Visibility.Visible;}}
    void CancelClick(object sender,RoutedEventArgs e)=>DialogResult=false;
    /// <summary>Devuelve la cantidad en unidad base (gramos, unidades o packs), o null si se canceló.</summary>
    public static long? Ask(Product product,long? current)
    {
        var dialog=new QuantityDialog(product,current);DialogHelper.SetOwner(dialog);
        return dialog.ShowDialog()==true?dialog.quantity:null;
    }
}
