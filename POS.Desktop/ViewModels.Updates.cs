using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using POS.Infrastructure;
using InfoBarSeverity=Wpf.Ui.Controls.InfoBarSeverity;
namespace POS.Desktop;

public sealed partial class MainViewModel
{
    static readonly HttpClient updateHttp=new(){Timeout=TimeSpan.FromMinutes(10)};
    UpdateSchedule? updateSchedule;string updatesDirectory="",backupsDirectory="";ReleaseInfo? availableUpdate;bool checkingUpdates;
    ICommand? checkUpdatesCommand,showUpdateCommand;
    public static Version CurrentVersion=>Assembly.GetEntryAssembly()?.GetName().Version??new Version(0,0,0);
    public string VersionLabel=>$"Versión {CurrentVersion.ToString(3)}";
    public ReleaseInfo? AvailableUpdate{get=>availableUpdate;private set{availableUpdate=value;Changed();Changed(nameof(UpdateVisibility));Changed(nameof(UpdateLabel));}}
    public Visibility UpdateVisibility=>AvailableUpdate!=null?Visibility.Visible:Visibility.Collapsed;
    public string UpdateLabel=>AvailableUpdate==null?"":$"Versión {AvailableUpdate.Version.ToString(3)} disponible";
    public bool CartHasItems=>Cart.Count>0;

    /// <summary>Configura dónde se guardan la fecha de la última búsqueda, las descargas y el respaldo previo a actualizar.</summary>
    public void ConfigureUpdates(string root){updateSchedule=new(Path.Combine(root,"update.json"));updatesDirectory=Path.Combine(root,"updates");backupsDirectory=Path.Combine(root,"backups");}

    /// <summary>Búsqueda automática al abrir la app: como máximo una vez por día y en silencio si no hay conexión.</summary>
    public async Task CheckForUpdatesOnStartupAsync()
    {
        if(updateSchedule==null||!updateSchedule.ShouldCheck(DateTime.Now))return;
        try{await CheckForUpdatesAsync();updateSchedule.MarkChecked(DateTime.Now);}
        // Cualquier falla (sin conexión, respuesta inesperada, disco) se ignora: la búsqueda automática nunca debe interrumpir ni cerrar la app.
        catch(Exception){}
    }
    async Task CheckForUpdatesAsync()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var release=await new UpdateChecker(updateHttp).GetLatestAsync(timeout.Token);
        AvailableUpdate=release!=null&&UpdateChecker.IsNewer(CurrentVersion,release)?release:null;
    }

    /// <summary>"Buscar actualizaciones" desde el menú lateral: informa el resultado, también si no hay conexión.</summary>
    public ICommand CheckUpdatesCommand=>checkUpdatesCommand??=new ActionCommand(async()=>
    {
        if(checkingUpdates)return;checkingUpdates=true;
        try
        {
            await CheckForUpdatesAsync();updateSchedule?.MarkChecked(DateTime.Now);
            if(AvailableUpdate==null)Notify($"Tenés la última versión ({CurrentVersion.ToString(3)}).",InfoBarSeverity.Success);else ShowUpdateCommand.Execute(null);
        }
        catch(Exception){Fail("No se pudo buscar actualizaciones. Revisá la conexión a internet; MiPOS sigue funcionando sin ella.");}
        finally{checkingUpdates=false;}
    });

    public ICommand ShowUpdateCommand=>showUpdateCommand??=Command(()=>
    {
        if(AvailableUpdate is not ReleaseInfo release)return;
        var dialog=new UpdateDialog(release,CurrentVersion,CartHasItems,(progress,ct)=>new UpdateChecker(updateHttp).DownloadAsync(release,updatesDirectory,progress,ct));
        DialogHelper.SetOwner(dialog);
        if(dialog.ShowDialog()!=true||dialog.InstallerPath is not string installer)return;
        Install(release,installer);
    });

    /// <summary>Respalda los datos, ejecuta el instalador en modo silencioso y cierra MiPOS; el instalador vuelve a abrirla al terminar.</summary>
    void Install(ReleaseInfo release,string installer)
    {
        Directory.CreateDirectory(backupsDirectory);
        service.Backup(Path.Combine(backupsDirectory,$"pos-antes-de-actualizar-a-{release.Version.ToString(3)}-{DateTime.Now:yyyyMMdd-HHmmss}.db"));
        Process.Start(new ProcessStartInfo(installer,"/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS"){UseShellExecute=true});
        Application.Current.Shutdown();
    }
}
