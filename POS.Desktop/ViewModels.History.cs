using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POS.Core;
namespace POS.Desktop;
public sealed record SaleView(SaleSummary Sale)
{
    public bool IsCancelled=>Sale.Status!="COMPLETED";
    public string Date=>Display.Date(Sale.CreatedAt);
    public string Status=>IsCancelled?"Anulada":"Completada";
    /// <summary>ok u out para la etiqueta de color.</summary>
    public string StatusKind=>IsCancelled?"out":"ok";
    public string Payment=>Display.Payment(Enum.Parse<PaymentMethod>(Sale.PaymentMethod));
    public string Total=>Display.Money(Sale.TotalMinor);
}
public sealed record DetailView(SaleItem Item)
{
    public string Name=>Item.Name;public string Quantity=>Display.Quantity(Item.QuantityBase,Item.Unit);public string Price=>Display.Money(Item.UnitPriceMinor)+" / "+Display.Unit(Item.Unit);public string Total=>Display.Money(Item.TotalMinor);
    /// <summary>Cantidad por precio histórico: "0,25 kg × $ 14.500 / kg" o "2 × $ 4.500".</summary>
    public string Detail=>(Item.Unit==SaleUnit.KG?Display.ShortQuantity(Item.QuantityBase,Item.Unit):Item.QuantityBase.ToString(Display.Culture))+" × "+Display.MoneyShort(Item.UnitPriceMinor)+(Item.Unit==SaleUnit.KG?" / kg":Item.Unit==SaleUnit.PACK?" / pack":"");
}

public sealed partial class MainViewModel
{
    public const string RangeToday="Hoy",RangeYesterday="Ayer",RangeWeek="7 días",RangeMonth="Mes",RangeCustom="Elegir fechas";
    string historyRange=RangeToday,cancellationReason="";DateTime? from=DateTime.Today,to=DateTime.Today;SaleView? selectedSale;bool loadingRange;
    ICommand? cancelSaleCommand;
    public IReadOnlyList<string> HistoryRanges{get;}=[RangeToday,RangeYesterday,RangeWeek,RangeMonth,RangeCustom];
    /// <summary>Atajos de período; "Elegir fechas" habilita los selectores de fecha.</summary>
    public string HistoryRange
    {
        get=>historyRange;
        set
        {
            historyRange=value;Changed();Changed(nameof(CustomRangeVisibility));
            var today=DateTime.Today;
            (DateTime f,DateTime t)? range=value switch{RangeToday=>(today,today),RangeYesterday=>(today.AddDays(-1),today.AddDays(-1)),RangeWeek=>(today.AddDays(-6),today),RangeMonth=>(new DateTime(today.Year,today.Month,1),today),_=>null};
            if(range is var (f,t)){loadingRange=true;From=f;To=t;loadingRange=false;}
            Guard(LoadHistory);
        }
    }
    public Visibility CustomRangeVisibility=>HistoryRange==RangeCustom?Visibility.Visible:Visibility.Collapsed;
    public DateTime? From{get=>from;set{from=value;Changed();Changed(nameof(RangeLabel));if(!loadingRange)Guard(LoadHistory);}}
    public DateTime? To{get=>to;set{to=value;Changed();Changed(nameof(RangeLabel));if(!loadingRange)Guard(LoadHistory);}}
    public string RangeLabel=>From==null||To==null?"":From==To?From.Value.ToString("dddd d 'de' MMMM",Display.Culture):$"{From:dd/MM} al {To:dd/MM/yyyy}";

    public ObservableCollection<SaleView> Sales{get;private set;}=[];
    public string TotalSold=>Display.Money(Sales.Where(x=>!x.IsCancelled).Sum(x=>x.Sale.TotalMinor));
    public int CompletedCount=>Sales.Count(x=>!x.IsCancelled);
    public int CancelledCount=>Sales.Count(x=>x.IsCancelled);
    public string CancelledTotal=>CancelledCount==0?"":Display.Money(Sales.Where(x=>x.IsCancelled).Sum(x=>x.Sale.TotalMinor));
    public Visibility NoSalesVisibility=>Sales.Count==0?Visibility.Visible:Visibility.Collapsed;

    /// <summary>Al elegir una venta se carga su detalle.</summary>
    public SaleView? SelectedSale
    {
        get=>selectedSale;
        set
        {
            selectedSale=value;Changed();cancellationReason="";
            SaleDetails=value==null?[]:new(service.SaleItems(value.Sale.Id).Select(x=>new DetailView(x)));
            foreach(var n in new[]{nameof(SaleDetails),nameof(CancellationReason),nameof(SaleDetailVisibility),nameof(NoSaleDetailVisibility),nameof(CancelVisibility),nameof(CancelledNoticeVisibility)})Changed(n);
        }
    }
    public ObservableCollection<DetailView> SaleDetails{get;private set;}=[];
    public Visibility SaleDetailVisibility=>SelectedSale!=null?Visibility.Visible:Visibility.Collapsed;
    public Visibility NoSaleDetailVisibility=>SelectedSale==null?Visibility.Visible:Visibility.Collapsed;
    public Visibility CancelVisibility=>SelectedSale is {IsCancelled:false}?Visibility.Visible:Visibility.Collapsed;
    public Visibility CancelledNoticeVisibility=>SelectedSale is {IsCancelled:true}?Visibility.Visible:Visibility.Collapsed;
    public string CancellationReason{get=>cancellationReason;set{cancellationReason=value;Changed();}}

    public ICommand CancelSaleCommand=>cancelSaleCommand??=Command(()=>
    {
        var s=SelectedSale??throw new ArgumentException("Seleccioná una venta.");
        if(CancellationReason.Trim().Length==0)throw new ArgumentException("Indicá el motivo de la anulación.");
        if(!ConfirmDialog.Ask($"Anular la venta n.º {s.Sale.Id}",$"La venta de {s.Total} quedará registrada como anulada y sus productos volverán al inventario. No se puede deshacer.","Anular venta",danger:true))return;
        service.CancelSale(s.Sale.Id,CancellationReason);Refresh();LoadHistory();Message=$"Venta n.º {s.Sale.Id} anulada. El stock volvió al inventario.";
    });

    void LoadHistory()
    {
        if(From==null||To==null||From>To)throw new ArgumentException("Revisá las fechas del historial: «Desde» debe ser anterior a «Hasta».");
        var keep=SelectedSale?.Sale.Id;
        Sales=new(service.Sales(From.Value.Date,To.Value.Date.AddDays(1)).Select(x=>new SaleView(x)));
        foreach(var n in new[]{nameof(Sales),nameof(TotalSold),nameof(CompletedCount),nameof(CancelledCount),nameof(CancelledTotal),nameof(NoSalesVisibility)})Changed(n);
        SelectedSale=Sales.FirstOrDefault(x=>x.Sale.Id==keep);
    }
}
