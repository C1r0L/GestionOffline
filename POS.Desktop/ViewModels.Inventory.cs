using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POS.Core;
namespace POS.Desktop;
public sealed record InventoryView(InventoryRow Row,SaleUnit Unit,string Category="")
{
    public string Name=>Row.Name;
    public bool IsManual=>Row.Mode==StockMode.MANUAL;
    public string Quantity=>Row.QuantityBase is long q?Display.ShortQuantity(q,Unit):"—";
    public string Minimum=>Row.MinimumBase is long m&&m>0?Display.ShortQuantity(m,Unit):"—";
    public string ModeLabel=>Display.Mode(Row.Mode);
    /// <summary>REPONER, BAJO o SUFICIENTE: el estado manual, o el calculado con existencias y mínimo.</summary>
    public string Status=>IsManual?Row.Status.ToString():Row.QuantityBase==0?"REPONER":Row.MinimumBase is long min&&Row.QuantityBase<=min?"BAJO":"SUFICIENTE";
    public string StatusLabel=>Status switch{"REPONER"=>"Reponer","BAJO"=>"Bajo",_=>"Suficiente"};
    /// <summary>ok, low u out para la etiqueta de color.</summary>
    public string StatusKind=>Status switch{"REPONER"=>"out","BAJO"=>"low",_=>"ok"};
    public string Subtitle=>(Category.Length>0?Category+" · ":"")+ModeLabel;
}
public sealed record MovementView(Movement Row,SaleUnit Unit)
{
    public string Date=>Display.Date(Row.CreatedAt);
    public string Type=>Row.Type switch{"SALE"=>"Venta","RESTOCK"=>"Reposición","ADJUSTMENT"=>"Ajuste",_=>"Anulación de venta"};
    public string Title=>Row.SaleId is long id?$"{Type} n.º {id}":Type;
    public string Quantity=>(Row.DeltaBase>0?"+":Row.DeltaBase<0?"−":"")+Display.ShortQuantity(Math.Abs(Row.DeltaBase),Unit);
    public string Detail=>string.IsNullOrWhiteSpace(Row.Note)||Row.Type is "SALE"||string.Equals(Row.Note.Trim(),Type,StringComparison.OrdinalIgnoreCase)?Date:$"{Date} · {Row.Note}";
    /// <summary>in, out o zero: color del movimiento.</summary>
    public string Kind=>Row.DeltaBase>0?"in":Row.DeltaBase<0?"out":"zero";
}
public sealed record FilterOption(string Key,string Label);

public sealed partial class MainViewModel
{
    const string RestockNote="Reposición",CountNote="Recuento";
    string inventorySearch="",inventoryFilter="Todos",stockQuantity="",stockNote=RestockNote,minimumQuantity="";bool isAbsoluteStock;InventoryView? selectedInventory;
    ICommand? stockCommand,minimumCommand,manualStatusCommand,pickInventoryFilterCommand;
    public ObservableCollection<InventoryView> Inventory{get;private set;}=[];
    public ObservableCollection<InventoryView> FilteredInventory{get;private set;}=[];
    public IReadOnlyList<FilterOption> InventoryFilters{get;private set;}=[];
    public FilterOption? InventoryFilterOption{get=>InventoryFilters.FirstOrDefault(x=>x.Key==inventoryFilter);set{if(value!=null&&value.Key!=inventoryFilter){inventoryFilter=value.Key;Changed();ApplyInventoryFilter();}}}
    public string InventorySearch{get=>inventorySearch;set{inventorySearch=value;Changed();ApplyInventoryFilter();}}
    public int ReplenishCount=>Inventory.Count(x=>x.Status=="REPONER");public int LowCount=>Inventory.Count(x=>x.Status=="BAJO");public int EnoughCount=>Inventory.Count(x=>x.Status=="SUFICIENTE");

    /// <summary>Al elegir un producto se cargan sus movimientos y se precargan mínimo y estado.</summary>
    public InventoryView? SelectedInventory
    {
        get=>selectedInventory;
        set
        {
            selectedInventory=value;Changed();
            Movements=value==null?[]:new(service.Movements(value.Row.ProductId).Take(30).Select(x=>new MovementView(x,value.Unit)));
            minimumQuantity=value?.Row.MinimumBase is long m&&m>0?(m/(value.Unit==SaleUnit.KG?1000m:1m)).ToString("0.###",Display.Culture):"";
            stockQuantity="";
            foreach(var n in new[]{nameof(Movements),nameof(MinimumQuantity),nameof(StockQuantity),nameof(StockActionLabel),nameof(DetailVisibility),nameof(NoDetailVisibility),nameof(MeasurableVisibility),nameof(ManualVisibility),nameof(StockUnitLabel),nameof(NoMovementsVisibility)})Changed(n);
        }
    }
    public ObservableCollection<MovementView> Movements{get;private set;}=[];
    public Visibility NoInventoryVisibility=>FilteredInventory.Count==0?Visibility.Visible:Visibility.Collapsed;
    public Visibility DetailVisibility=>SelectedInventory!=null?Visibility.Visible:Visibility.Collapsed;
    public Visibility NoDetailVisibility=>SelectedInventory==null?Visibility.Visible:Visibility.Collapsed;
    public Visibility MeasurableVisibility=>SelectedInventory is {IsManual:false}?Visibility.Visible:Visibility.Collapsed;
    public Visibility ManualVisibility=>SelectedInventory is {IsManual:true}?Visibility.Visible:Visibility.Collapsed;
    public Visibility NoMovementsVisibility=>Movements.Count==0?Visibility.Visible:Visibility.Collapsed;
    public string StockUnitLabel=>SelectedInventory?.Unit switch{SaleUnit.KG=>"kg",SaleUnit.PACK=>"packs",_=>"unidades"};

    /// <summary>Reponer suma a las existencias; fijar valor las reemplaza (recuento).</summary>
    public bool IsAbsoluteStock{get=>isAbsoluteStock;set{isAbsoluteStock=value;if(stockNote==(value?RestockNote:CountNote))stockNote=value?CountNote:RestockNote;Changed();Changed(nameof(IsRestock));Changed(nameof(StockNote));Changed(nameof(StockActionLabel));}}
    public bool IsRestock{get=>!isAbsoluteStock;set=>IsAbsoluteStock=!value;}
    public string StockQuantity{get=>stockQuantity;set{stockQuantity=value;Changed();Changed(nameof(StockActionLabel));}}
    public string StockNote{get=>stockNote;set{stockNote=value;Changed();}}
    public string MinimumQuantity{get=>minimumQuantity;set{minimumQuantity=value;Changed();}}
    /// <summary>Texto del botón con el resultado previsto: "Reponer 3 kg · quedarán 5,55 kg".</summary>
    public string StockActionLabel
    {
        get
        {
            var verb=IsAbsoluteStock?"Fijar existencias":"Reponer";
            if(SelectedInventory is not {Row.QuantityBase: long current} row||!TryQuantity(StockQuantity,row.Unit,IsAbsoluteStock,out var q))return verb;
            var next=IsAbsoluteStock?q:current+q;
            return IsAbsoluteStock?$"{verb} en {Display.ShortQuantity(next,row.Unit)}":$"{verb} {Display.ShortQuantity(q,row.Unit)} · quedarán {Display.ShortQuantity(next,row.Unit)}";
        }
    }
    static bool TryQuantity(string text,SaleUnit unit,bool allowZero,out long q)
    {
        q=0;try{var v=Display.Parse(text);if(allowZero&&v==0)return true;q=Pricing.Quantity(v,unit);return true;}catch(Exception e) when(e is FormatException or ArgumentException or OverflowException){return false;}
    }

    public ICommand StockCommand=>stockCommand??=Command(()=>
    {
        var row=SelectedInventory??throw new ArgumentException("Seleccioná un producto del inventario.");
        if(StockQuantity.Trim().Length==0)throw new ArgumentException("Ingresá una cantidad.");
        var value=Display.Parse(StockQuantity);var q=IsAbsoluteStock&&value==0?0:Pricing.Quantity(value,row.Unit);
        if(IsAbsoluteStock&&!ConfirmDialog.Ask("Fijar existencias",$"Las existencias de «{row.Name}» pasarán a {Display.ShortQuantity(q,row.Unit)}.","Fijar"))return;
        service.AdjustStock(row.Row.ProductId,q,IsAbsoluteStock,StockNote);RefreshKeepingSelection();
        Message=IsAbsoluteStock?$"Existencias de «{row.Name}» fijadas en {Display.ShortQuantity(q,row.Unit)}.":$"Se repusieron {Display.ShortQuantity(q,row.Unit)} de «{row.Name}».";
    });
    public ICommand MinimumCommand=>minimumCommand??=Command(()=>
    {
        var row=SelectedInventory??throw new ArgumentException("Seleccioná un producto del inventario.");
        var n=MinimumQuantity.Trim().Length==0?0:Display.Parse(MinimumQuantity);
        service.SetMinimumStock(row.Row.ProductId,n==0?0:Pricing.Quantity(n,row.Unit));RefreshKeepingSelection();
        Message=n==0?$"«{row.Name}» ya no tiene mínimo de reposición.":"Mínimo de reposición actualizado.";
    });
    public ICommand ManualStatusCommand=>manualStatusCommand??=ParamCommand(p=>
    {
        var row=SelectedInventory??throw new ArgumentException("Seleccioná un producto del inventario.");
        if(p is not string s||!Enum.TryParse<ReplenishmentStatus>(s,out var status))return;
        service.SetManualStatus(row.Row.ProductId,status);RefreshKeepingSelection();Message=$"«{row.Name}» marcado como {status.ToString().ToLower(Display.Culture)}.";
    });
    public ICommand PickInventoryFilterCommand=>pickInventoryFilterCommand??=ParamCommand(p=>{if(p is string key)InventoryFilterOption=InventoryFilters.FirstOrDefault(x=>x.Key==key);});

    void RefreshInventory(IReadOnlyDictionary<long,Product> products)
    {
        Inventory=new(service.Inventory().Select(x=>new InventoryView(x,products[x.ProductId].Unit,products[x.ProductId].Category)));Changed(nameof(Inventory));
        InventoryFilters=[new("Todos",$"Todos · {Inventory.Count}"),new("REPONER",$"Reponer · {ReplenishCount}"),new("BAJO",$"Bajo · {LowCount}"),new("SUFICIENTE",$"Suficiente · {EnoughCount}")];
        Changed(nameof(InventoryFilters));Changed(nameof(InventoryFilterOption));Changed(nameof(ReplenishCount));Changed(nameof(LowCount));Changed(nameof(EnoughCount));
        ApplyInventoryFilter();
    }
    /// <summary>Ordena por urgencia (reponer, bajo, suficiente) y aplica filtro y búsqueda.</summary>
    void ApplyInventoryFilter()
    {
        var keep=SelectedInventory?.Row.ProductId;
        FilteredInventory=new(Inventory.Where(x=>inventoryFilter=="Todos"||x.Status==inventoryFilter).Where(x=>InventorySearch.Trim().Length==0||Display.Culture.CompareInfo.IndexOf(x.Name,InventorySearch.Trim(),System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0)
            .OrderBy(x=>x.Status switch{"REPONER"=>0,"BAJO"=>1,_=>2}).ThenBy(x=>x.Name,StringComparer.Create(Display.Culture,true)));
        Changed(nameof(FilteredInventory));Changed(nameof(NoInventoryVisibility));
        SelectedInventory=FilteredInventory.FirstOrDefault(x=>x.Row.ProductId==keep);
    }
    void RefreshKeepingSelection(){var keep=SelectedInventory?.Row.ProductId;Refresh();SelectedInventory=FilteredInventory.FirstOrDefault(x=>x.Row.ProductId==keep);}
}
