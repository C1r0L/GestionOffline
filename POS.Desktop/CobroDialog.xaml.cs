using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using POS.Core;
using Wpf.Ui.Controls;
namespace POS.Desktop;
public sealed record CobroResult(bool Confirmed,long? Change);
/// <summary>Confirma el cobro. En efectivo calcula el vuelto; el monto recibido solo se muestra, no se guarda.</summary>
public partial class CobroDialog : FluentWindow
{
    readonly long total;
    long? change;
    CobroDialog(long total,PaymentOption payment,int lines)
    {
        InitializeComponent();this.total=total;
        Summary.Text=lines==1?"Total a cobrar · 1 producto":$"Total a cobrar · {lines} productos";TotalText.Text=Display.Money(total);Method.Text=payment.Label;
        var cash=payment.Value==PaymentMethod.CASH;
        CashPanel.Visibility=cash?Visibility.Visible:Visibility.Collapsed;
        OtherHint.Text=cash?"Ingresá con cuánto paga para ver el vuelto, o confirmá directamente.":$"Confirmá cuando hayas recibido el pago por {payment.Label.ToLower(Display.Culture)}.";
        foreach(var amount in QuickValues(total))
        {
            var button=new Wpf.Ui.Controls.Button{Content=amount==total?"Justo":Display.MoneyShort(amount),Margin=new Thickness(0,0,6,6),MinHeight=36,Tag=amount};
            button.Click+=(_,_)=>{Paid.Text=((long)button.Tag/100m).ToString("0.##",Display.Culture);Paid.Focus();Paid.CaretIndex=Paid.Text.Length;};
            QuickAmounts.Children.Add(button);
        }
        UpdateChange();
        Loaded+=(_,_)=>{DialogHelper.FitHeight(this,Root);if(cash)Paid.Focus();else ConfirmButton.Focus();};
    }
    /// <summary>Monto justo y los siguientes múltiplos redondos de $1.000, $5.000, $10.000 y $20.000.</summary>
    static IEnumerable<long> QuickValues(long total)=>new[]{total}.Concat(new long[]{100000,500000,1000000,2000000}.Select(step=>(total/step+1)*step)).Distinct().Take(5);
    void PaidChanged(object sender,TextChangedEventArgs e)=>UpdateChange();
    void UpdateChange()
    {
        change=null;ConfirmButton.IsEnabled=true;
        if(Paid.Text.Trim().Length==0){ChangeBox.Visibility=Visibility.Hidden;return;}
        ChangeBox.Visibility=Visibility.Visible;
        long paid;try{paid=Pricing.Money(Display.Parse(Paid.Text));}catch(Exception e) when(e is FormatException or OverflowException){SetChange("Revisá el importe","",false);ConfirmButton.IsEnabled=false;return;}
        if(paid<total){SetChange("Faltan",Display.Money(total-paid),false);ConfirmButton.IsEnabled=false;return;}
        change=paid-total;SetChange("Vuelto",Display.Money(change.Value),true);
    }
    void SetChange(string label,string amount,bool ok)
    {
        ChangeLabel.Text=label;ChangeText.Text=amount;
        ChangeBox.Background=(Brush)FindResource(ok?"SuccessSoftBrush":"DangerSoftBrush");
        var ink=(Brush)FindResource(ok?"SuccessInkBrush":"DangerInkBrush");ChangeLabel.Foreground=ink;ChangeText.Foreground=ink;
    }
    void ConfirmClick(object sender,RoutedEventArgs e){if(ConfirmButton.IsEnabled)DialogResult=true;}
    void CancelClick(object sender,RoutedEventArgs e)=>DialogResult=false;
    public static CobroResult Ask(long total,PaymentOption payment,int lines)
    {
        var dialog=new CobroDialog(total,payment,lines);DialogHelper.SetOwner(dialog);
        return dialog.ShowDialog()==true?new(true,dialog.change):new(false,null);
    }
}
