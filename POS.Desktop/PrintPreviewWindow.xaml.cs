using System.Windows;
namespace POS.Desktop;
/// <summary>Muestra el cierre tal como se va a imprimir; "Imprimir…" abre el diálogo de impresión de Windows.</summary>
public partial class PrintPreviewWindow : Wpf.Ui.Controls.FluentWindow
{
    readonly ClosingSnapshot closing;
    public bool Printed{get;private set;}
    public PrintPreviewWindow(ClosingSnapshot closing)
    {
        InitializeComponent();this.closing=closing;
        var doc=ClosingPrinter.Build(closing);doc.PagePadding=new Thickness(40,32,40,32);Viewer.Document=doc;
    }
    void PrintClick(object sender,RoutedEventArgs e)
    {
        try{if(ClosingPrinter.Print(closing)){Printed=true;DialogResult=true;}}
        catch(Exception){ConfirmDialog.Ask("No se pudo imprimir","Revisá que haya una impresora instalada y encendida. Podés elegir «Microsoft Print to PDF» para guardar el cierre como PDF.","Entendido");}
    }
}
