using System.Windows;
using System.Windows.Input;
using POS.Core;
namespace POS.Desktop;
/// <summary>Cobros de un medio de pago en el día; Share (0 a 1) dibuja la barra de proporción.</summary>
public sealed record PaymentSummary(string Label,int Count,long TotalMinor,double Share)
{
    public string CountLabel=>Count==1?"1 venta":$"{Count} ventas";
    public string Total=>Display.Money(TotalMinor);
    public string Percent=>Share.ToString("0%",Display.Culture);
}
public sealed record SoldItem(string Name,SaleUnit Unit,long QuantityBase,long TotalMinor)
{
    public string Quantity=>Display.ShortQuantity(QuantityBase,Unit);
    public string Total=>Display.Money(TotalMinor);
}
/// <summary>Datos de un cierre, también usados para imprimirlo.</summary>
public sealed record ClosingSnapshot(DateTime Day,long TotalMinor,int Count,int CancelledCount,long CancelledMinor,IReadOnlyList<PaymentSummary> Payments,IReadOnlyList<SoldItem> Items,long CashMinor,long? CountedCashMinor,string FirstLast)
{
    public string Average=>Count==0?Display.Money(0):Display.Money((long)Math.Round((decimal)TotalMinor/Count,MidpointRounding.AwayFromZero));
}

public sealed partial class MainViewModel
{
    DateTime closingDay=DateTime.Today;string countedCash="";ClosingSnapshot closing=new(DateTime.Today,0,0,0,0,[],[],0,null,"");
    ICommand? previousDayCommand,nextDayCommand,closingTodayCommand,printClosingCommand;
    public ClosingSnapshot Closing{get=>closing;private set{closing=value;foreach(var n in new[]{nameof(Closing),nameof(ClosingDayLabel),nameof(IsClosingToday),nameof(NoClosingSalesVisibility),nameof(NoSoldItemsVisibility)})Changed(n);UpdateCashDifference();}}
    /// <summary>Día del cierre (nunca futuro). Al cambiarlo se descarta el efectivo contado, que corresponde a otro día.</summary>
    public DateTime ClosingDay{get=>closingDay;set{var day=value.Date>DateTime.Today?DateTime.Today:value.Date;if(day!=closingDay){countedCash="";Changed(nameof(CountedCash));}closingDay=day;Changed();Guard(LoadClosing);}}
    public string ClosingDayLabel{get{var t=ClosingDay.ToString("dddd dd/MM/yyyy",Display.Culture);return char.ToUpper(t[0],Display.Culture)+t[1..];}}
    public bool IsClosingToday=>ClosingDay==DateTime.Today;
    public Visibility NoClosingSalesVisibility=>Closing.Count==0&&Closing.CancelledCount==0?Visibility.Visible:Visibility.Collapsed;
    public Visibility NoSoldItemsVisibility=>Closing.Items.Count==0?Visibility.Visible:Visibility.Collapsed;

    /// <summary>Efectivo contado en la caja; solo se usa para mostrar la diferencia, no se guarda.</summary>
    public string CountedCash{get=>countedCash;set{countedCash=value;Changed();UpdateCashDifference();}}
    public string CashDifferenceLabel{get;private set;}="";
    public string CashDifferenceAmount{get;private set;}="";
    /// <summary>ok (coincide), over (sobra), short (falta) o none (sin dato).</summary>
    public string CashDifferenceKind{get;private set;}="none";
    long? CountedCashMinor{get{if(CountedCash.Trim().Length==0)return null;try{return Pricing.Money(Display.Parse(CountedCash));}catch(Exception e) when(e is FormatException or OverflowException){return -1;}}}
    void UpdateCashDifference()
    {
        var counted=CountedCashMinor;
        (CashDifferenceKind,CashDifferenceLabel,CashDifferenceAmount)=counted switch
        {
            null=>("none","Ingresá el efectivo contado para controlar la caja.",""),
            < 0=>("short","Revisá el importe contado.",""),
            var c when c==Closing.CashMinor=>("ok","La caja coincide",Display.Money(0)),
            var c when c>Closing.CashMinor=>("over","Sobra en caja",Display.Money(c.Value-Closing.CashMinor)),
            var c=>("short","Falta en caja",Display.Money(Closing.CashMinor-c!.Value)),
        };
        Changed(nameof(CashDifferenceKind));Changed(nameof(CashDifferenceLabel));Changed(nameof(CashDifferenceAmount));
    }

    public ICommand PreviousDayCommand=>previousDayCommand??=Command(()=>ClosingDay=ClosingDay.AddDays(-1));
    public ICommand NextDayCommand=>nextDayCommand??=Command(()=>ClosingDay=ClosingDay.AddDays(1));
    public ICommand ClosingTodayCommand=>closingTodayCommand??=Command(()=>ClosingDay=DateTime.Today);
    public ICommand PrintClosingCommand=>printClosingCommand??=Command(()=>
    {
        long? counted=CountedCashMinor is long c&&c>=0?c:null;
        var preview=new PrintPreviewWindow(Closing with{CountedCashMinor=counted});DialogHelper.SetOwner(preview);
        preview.ShowDialog();if(preview.Printed)Message="Resumen del cierre enviado a la impresora.";
    });

    /// <summary>Arma el cierre del día elegido: ventas completadas por medio de pago y productos vendidos; las anuladas solo se cuentan aparte.</summary>
    void LoadClosing()
    {
        var sales=service.Sales(ClosingDay,ClosingDay.AddDays(1));
        var done=sales.Where(x=>x.Status=="COMPLETED").ToList();var cancelled=sales.Where(x=>x.Status!="COMPLETED").ToList();
        var total=done.Sum(x=>x.TotalMinor);
        var payments=Enum.GetValues<PaymentMethod>().Select(m=>{var list=done.Where(x=>x.PaymentMethod==m.ToString()).ToList();var sum=list.Sum(x=>x.TotalMinor);return new PaymentSummary(Display.Payment(m),list.Count,sum,total==0?0:(double)sum/total);}).ToList();
        var items=done.SelectMany(x=>service.SaleItems(x.Id)).GroupBy(x=>(x.Name,x.Unit)).Select(g=>new SoldItem(g.Key.Name,g.Key.Unit,g.Sum(x=>x.QuantityBase),g.Sum(x=>x.TotalMinor))).OrderByDescending(x=>x.TotalMinor).ThenBy(x=>x.Name).ToList();
        var times=sales.Select(x=>DateTime.Parse(x.CreatedAt,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime()).Order().ToList();
        var firstLast=times.Count==0?"Sin ventas en el día":times.Count==1?$"Única venta {times[0]:HH:mm}":$"Primera venta {times[0]:HH:mm} · Última venta {times[^1]:HH:mm}";
        Closing=new(ClosingDay,total,done.Count,cancelled.Count,cancelled.Sum(x=>x.TotalMinor),payments,items,payments[(int)PaymentMethod.CASH].TotalMinor,null,firstLast);
    }
}
