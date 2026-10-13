using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace POS.Desktop.Views;
public partial class SalePage : UserControl
{
    public SalePage()
    {
        InitializeComponent();
        DataContextChanged+=(_,e)=>{if(e.OldValue is MainViewModel old)old.FocusSearchRequested-=RequestFocus;if(e.NewValue is MainViewModel vm)vm.FocusSearchRequested+=RequestFocus;};
    }
    void RequestFocus()=>Dispatcher.BeginInvoke(FocusSearch,System.Windows.Threading.DispatcherPriority.Input);
    MainViewModel Vm=>(MainViewModel)DataContext;
    public static readonly DependencyProperty TileWidthProperty=DependencyProperty.Register(nameof(TileWidth),typeof(double),typeof(SalePage),new(196d));
    public double TileWidth{get=>(double)GetValue(TileWidthProperty);set=>SetValue(TileWidthProperty,value);}
    /// <summary>Reparte el ancho del catálogo en columnas de al menos 190 px para que las tarjetas ocupen toda la fila.</summary>
    void CatalogScrollChanged(object sender,ScrollChangedEventArgs e)
    {
        if(e.ViewportWidthChange==0)return;
        var width=((ScrollViewer)sender).ViewportWidth;if(width<=0)return;
        var columns=Math.Max(1,(int)(width/(190+12)));TileWidth=Math.Floor(width/columns)-12;
    }
    /// <summary>Lleva el foco a la búsqueda. También la registra como foco de la ventana, porque al cerrarse un diálogo WPF restaura el último elemento enfocado.</summary>
    public void FocusSearch(){if(Window.GetWindow(this) is Window w)FocusManager.SetFocusedElement(w,SaleSearch);SaleSearch.Focus();SaleSearch.SelectAll();}
    /// <summary>Enter agrega el primer resultado (o el código exacto escaneado); Esc limpia la búsqueda; flecha abajo pasa al carrito.</summary>
    void SearchKeyDown(object sender,KeyEventArgs e)
    {
        switch(e.Key)
        {
            case Key.Enter:Vm.QuickAddCommand.Execute(null);e.Handled=true;break;
            case Key.Escape when Vm.SaleSearch.Length>0:Vm.SaleSearch="";e.Handled=true;break;
            case Key.Down when Vm.Cart.Count>0:Vm.SelectedCart??=Vm.Cart[^1];(CartList.ItemContainerGenerator.ContainerFromItem(Vm.SelectedCart) as ListBoxItem)?.Focus();e.Handled=true;break;
        }
    }
    /// <summary>Atajos sobre la línea seleccionada del carrito: +, − y Suprimir.</summary>
    void CartKeyDown(object sender,KeyEventArgs e)
    {
        switch(e.Key)
        {
            case Key.Add or Key.OemPlus:Vm.ChangeSelectedLine(+1);e.Handled=true;break;
            case Key.Subtract or Key.OemMinus:Vm.ChangeSelectedLine(-1);e.Handled=true;break;
            case Key.Delete or Key.Back:Vm.RemoveSelectedLine();e.Handled=true;break;
            case Key.Escape:FocusSearch();e.Handled=true;break;
        }
        if(e.Handled&&Vm.SelectedCart!=null)Dispatcher.BeginInvoke(()=>(CartList.ItemContainerGenerator.ContainerFromItem(Vm.SelectedCart) as ListBoxItem)?.Focus());
    }
}
