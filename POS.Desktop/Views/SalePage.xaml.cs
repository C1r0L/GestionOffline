using System.Windows.Controls;
namespace POS.Desktop.Views;
public partial class SalePage : UserControl
{
    public SalePage(){InitializeComponent();}
    public void FocusSearch()=>SaleSearch.Focus();
}
