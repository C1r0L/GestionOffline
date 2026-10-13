using System.Windows;
using POS.Infrastructure;
namespace POS.Desktop;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        try
        {
            var root=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiPOS");
            var service=new PosService(System.IO.Path.Combine(root,"data","pos.db"));
            var vm=new MainViewModel(service);DataContext=vm;
            try { service.DailyBackup(System.IO.Path.Combine(root,"backups")); }
            catch(Exception){vm.Message="No se pudo crear el respaldo diario. Exportá un respaldo a una unidad externa.";}
            Loaded+=(_,_)=>SaleSearch.Focus();
        }
        catch(Exception){MessageBox.Show("No se pudo abrir la base de datos. Conservá tus archivos y contactá al soporte.","MiPOS",MessageBoxButton.OK,MessageBoxImage.Error);Close();}
    }
}
