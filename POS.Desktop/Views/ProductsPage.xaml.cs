using System.Windows.Controls;
using System.Windows.Input;
namespace POS.Desktop.Views;
public partial class ProductsPage : UserControl
{
    public ProductsPage(){InitializeComponent();}
    MainViewModel Vm=>(MainViewModel)DataContext;
    void GridDoubleClick(object sender,MouseButtonEventArgs e){if(Grid.SelectedItem is ProductView p)Vm.EditProductCommand.Execute(p);}
    void GridKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&Grid.SelectedItem is ProductView p){Vm.EditProductCommand.Execute(p);e.Handled=true;}}
}
