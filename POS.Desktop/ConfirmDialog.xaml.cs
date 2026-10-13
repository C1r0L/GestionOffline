using System.Windows;
using Wpf.Ui.Controls;
namespace POS.Desktop;
public partial class ConfirmDialog : FluentWindow
{
    ConfirmDialog(string title,string message,string confirmText,bool danger)
    {
        InitializeComponent();Title=title;Heading.Text=title;Body.Text=message;ConfirmButton.Content=confirmText;
        if(danger){ConfirmButton.Appearance=ControlAppearance.Danger;IconBadge.Background=(System.Windows.Media.Brush)FindResource("DangerSoftBrush");BadgeIcon.Symbol=SymbolRegular.Warning24;BadgeIcon.Foreground=(System.Windows.Media.Brush)FindResource("DangerInkBrush");}
        Loaded+=(_,_)=>{DialogHelper.FitHeight(this,Root);(danger?CancelButton:ConfirmButton).Focus();};
    }
    void ConfirmClick(object sender,RoutedEventArgs e)=>DialogResult=true;
    void CancelClick(object sender,RoutedEventArgs e)=>DialogResult=false;
    /// <summary>Pide confirmación con el estilo de la aplicación. En acciones destructivas el foco inicial queda en Cancelar.</summary>
    public static bool Ask(string title,string message,string confirmText="Confirmar",bool danger=false)
    {
        var dialog=new ConfirmDialog(title,message,confirmText,danger);DialogHelper.SetOwner(dialog);
        return dialog.ShowDialog()==true;
    }
}
static class DialogHelper
{
    public static void SetOwner(Window dialog){var owner=Application.Current?.MainWindow;if(owner!=null&&owner!=dialog&&owner.IsLoaded)dialog.Owner=owner;}
    /// <summary>Activa el diálogo y ajusta su alto al contenido, también cuando cambia (por ejemplo al mostrar errores); SizeToContent no es confiable con la barra de título extendida.</summary>
    public static void FitHeight(Window window,FrameworkElement root)
    {
        window.Activate();root.VerticalAlignment=VerticalAlignment.Top;
        root.Measure(new Size(window.Width,double.PositiveInfinity));var h=Math.Ceiling(root.DesiredSize.Height);window.Top+=(window.Height-h)/2;window.Height=h;
        root.SizeChanged+=(_,e)=>{if(e.HeightChanged)window.Height=Math.Ceiling(e.NewSize.Height);};
    }
}
