using System.Globalization;
using System.Windows;
using System.Windows.Data;
using POS.Infrastructure;
using Wpf.Ui.Controls;
namespace POS.Desktop;
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        try
        {
            var root=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiPOS");
            var service=new PosService(System.IO.Path.Combine(root,"data","pos.db"));
            var vm=new MainViewModel(service);DataContext=vm;
            var backups=System.IO.Path.Combine(root,"backups");
            try { service.DailyBackup(backups);vm.SetDailyBackup(System.IO.File.GetLastWriteTime(System.IO.Path.Combine(backups,$"pos-{DateTime.Now:yyyy-MM-dd}.db"))); }
            catch(Exception){vm.SetDailyBackup(null);vm.Fail("No se pudo crear el respaldo diario. Exportá un respaldo a una unidad externa.");}
            Loaded+=(_,_)=>SalePageView.FocusSearch();
        }
        catch(Exception){System.Windows.MessageBox.Show("No se pudo abrir la base de datos. Conservá tus archivos y contactá al soporte.","MiPOS",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Error);Close();}
    }
    /// <summary>F1 abre Nueva venta y F2 lleva el foco a la búsqueda de productos.</summary>
    void WindowKeyDown(object sender,System.Windows.Input.KeyEventArgs e)
    {
        if(e.Key is not (System.Windows.Input.Key.F1 or System.Windows.Input.Key.F2)||DataContext is not MainViewModel vm)return;
        vm.Section=0;Dispatcher.BeginInvoke(SalePageView.FocusSearch,System.Windows.Threading.DispatcherPriority.Input);e.Handled=true;
    }
    void BackupMenuClick(object sender,RoutedEventArgs e)
    {
        var menu=BackupMenuButton.ContextMenu;menu.DataContext=DataContext;menu.PlacementTarget=BackupMenuButton;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Top;menu.IsOpen=true;
    }
}
public sealed class SectionVisibilityConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is int section&&section.ToString(CultureInfo.InvariantCulture)==(string)parameter?Visibility.Visible:Visibility.Collapsed;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
