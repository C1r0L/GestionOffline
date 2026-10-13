using System.Windows;
using Wpf.Ui.Controls;
namespace POS.Desktop;
public partial class ConfirmDialog : FluentWindow
{
    ConfirmDialog(string title,string message,string confirmText,bool danger)
    {
        InitializeComponent();Title=title;Heading.Text=title;Body.Text=message;ConfirmButton.Content=confirmText;
        if(danger){ConfirmButton.Appearance=ControlAppearance.Danger;IconBadge.Background=(System.Windows.Media.Brush)FindResource("DangerSoftBrush");BadgeIcon.Symbol=SymbolRegular.Warning24;BadgeIcon.Foreground=(System.Windows.Media.Brush)FindResource("DangerInkBrush");}
        Loaded+=(_,_)=>{Root.Measure(new Size(Width,double.PositiveInfinity));var h=Math.Ceiling(Root.DesiredSize.Height);Top+=(Height-h)/2;Height=h;(danger?CancelButton:ConfirmButton).Focus();};
    }
    void ConfirmClick(object sender,RoutedEventArgs e)=>DialogResult=true;
    void CancelClick(object sender,RoutedEventArgs e)=>DialogResult=false;
    /// <summary>Pide confirmación con el estilo de la aplicación. En acciones destructivas el foco inicial queda en Cancelar.</summary>
    public static bool Ask(string title,string message,string confirmText="Confirmar",bool danger=false)
    {
        var dialog=new ConfirmDialog(title,message,confirmText,danger);
        var owner=Application.Current?.MainWindow;if(owner!=null&&owner.IsLoaded)dialog.Owner=owner;
        return dialog.ShowDialog()==true;
    }
}
