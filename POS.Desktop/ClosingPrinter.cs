using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace POS.Desktop;
/// <summary>Imprime el cierre del día en una impresora común (o "Microsoft Print to PDF") con el diálogo de Windows.</summary>
public static class ClosingPrinter
{
    /// <summary>Devuelve true si se envió a imprimir; false si se canceló el diálogo.</summary>
    public static bool Print(ClosingSnapshot c)
    {
        var dialog=new PrintDialog();
        if(dialog.ShowDialog()!=true)return false;
        var doc=Build(c);
        doc.PageWidth=dialog.PrintableAreaWidth;doc.PageHeight=dialog.PrintableAreaHeight;doc.ColumnWidth=dialog.PrintableAreaWidth;doc.PagePadding=new Thickness(56);
        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,$"MiPOS - Cierre {c.Day:dd-MM-yyyy}");
        return true;
    }
    /// <summary>Documento en blanco y negro, legible en cualquier impresora.</summary>
    public static FlowDocument Build(ClosingSnapshot c)
    {
        var doc=new FlowDocument{FontFamily=SystemFonts.MessageFontFamily,FontSize=12,Foreground=Brushes.Black};
        var day=c.Day.ToString("dddd d 'de' MMMM 'de' yyyy",Display.Culture);
        doc.Blocks.Add(new Paragraph(new Run("MiPOS · Cierre del día")){FontSize=20,FontWeight=FontWeights.Bold,Margin=new Thickness(0)});
        doc.Blocks.Add(new Paragraph(new Run($"{char.ToUpper(day[0],Display.Culture)}{day[1..]} · {c.FirstLast}")){Margin=new Thickness(0,2,0,2)});
        doc.Blocks.Add(new Paragraph(new Run($"Impreso el {DateTime.Now.ToString("dd/MM/yyyy HH:mm",Display.Culture)}")){FontSize=10,Foreground=Brushes.DimGray,Margin=new Thickness(0,0,0,14)});

        doc.Blocks.Add(Table("Resumen",[("Total vendido",Display.Money(c.TotalMinor)),("Ventas completadas",c.Count.ToString(Display.Culture)),("Ticket promedio",c.Average),("Ventas anuladas",c.CancelledCount==0?"0":$"{c.CancelledCount} ({Display.Money(c.CancelledMinor)})")],bold:0));
        doc.Blocks.Add(Table("Cobros por medio de pago",c.Payments.Select(p=>(p.Label+" · "+p.CountLabel,p.Total)).ToList()));
        var cash=new List<(string,string)>{("Efectivo esperado en caja",Display.Money(c.CashMinor))};
        if(c.CountedCashMinor is long counted){cash.Add(("Efectivo contado",Display.Money(counted)));var diff=counted-c.CashMinor;cash.Add((diff==0?"Diferencia":diff>0?"Sobrante":"Faltante",Display.Money(Math.Abs(diff))));}
        doc.Blocks.Add(Table("Caja",cash,bold:0));
        doc.Blocks.Add(Table("Productos vendidos",c.Items.Count==0?[("Sin productos vendidos","")]:c.Items.Select(i=>($"{i.Name} · {i.Quantity}",i.Total)).ToList()));
        return doc;
    }
    static Table Table(string title,IReadOnlyList<(string Label,string Value)> rows,int bold=-1)
    {
        var table=new Table{CellSpacing=0,Margin=new Thickness(0,0,0,14)};
        table.Columns.Add(new TableColumn{Width=new GridLength(3,GridUnitType.Star)});table.Columns.Add(new TableColumn{Width=new GridLength(1,GridUnitType.Star)});
        var group=new TableRowGroup();table.RowGroups.Add(group);
        var head=new TableRow();head.Cells.Add(new TableCell(new Paragraph(new Run(title)){FontSize=14,FontWeight=FontWeights.Bold}){ColumnSpan=2,BorderBrush=Brushes.Black,BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,0,0,4)});group.Rows.Add(head);
        for(var i=0;i<rows.Count;i++)
        {
            var row=new TableRow();var weight=i==bold?FontWeights.Bold:FontWeights.Normal;
            row.Cells.Add(new TableCell(new Paragraph(new Run(rows[i].Label)){FontWeight=weight}){Padding=new Thickness(0,3,0,3),BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(0,0,0,0.5)});
            row.Cells.Add(new TableCell(new Paragraph(new Run(rows[i].Value)){TextAlignment=TextAlignment.Right,FontWeight=weight}){Padding=new Thickness(0,3,0,3),BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(0,0,0,0.5)});
            group.Rows.Add(row);
        }
        return table;
    }
}
