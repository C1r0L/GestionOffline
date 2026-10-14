using System.IO;
using System.Windows;
using POS.Infrastructure;
namespace POS.Desktop;
/// <summary>Muestra la versión nueva y sus novedades; con la confirmación del usuario descarga y verifica el instalador.</summary>
public partial class UpdateDialog : Wpf.Ui.Controls.FluentWindow
{
    readonly Func<IProgress<double>,CancellationToken,Task<string>> download;
    CancellationTokenSource? cancel;
    /// <summary>Instalador descargado y verificado; null si el usuario eligió "Más tarde".</summary>
    public string? InstallerPath{get;private set;}
    public UpdateDialog(ReleaseInfo release,Version current,bool cartHasItems,Func<IProgress<double>,CancellationToken,Task<string>> download)
    {
        InitializeComponent();this.download=download;
        NewVersion.Text=$"Versión {release.Version.ToString(3)}";
        CurrentVersion.Text=$"Tenés la {current.ToString(3)}"+(release.Published>DateTimeOffset.MinValue?$" · publicada el {release.Published.LocalDateTime.ToString("dd/MM/yyyy",Display.Culture)}":"")+(release.Size>0?$" · {release.Size/1024d/1024d:0.#} MB":"");
        Notes.Text=release.Notes.Length>0?release.Notes:"Mejoras y correcciones.";
        if(cartHasItems){CartWarning.Visibility=Visibility.Visible;InstallButton.IsEnabled=false;}
        Loaded+=(_,_)=>DialogHelper.FitHeight(this,Root);
    }
    async void InstallClick(object sender,RoutedEventArgs e)
    {
        if(!InstallButton.IsEnabled)return;
        InstallButton.IsEnabled=false;LaterButton.Content="Cancelar";Error.Text="";ProgressPanel.Visibility=Visibility.Visible;Progress.Value=0;
        cancel=new CancellationTokenSource();
        var progress=new Progress<double>(v=>{Progress.Value=v;ProgressText.Text=v.ToString("0%",Display.Culture);});
        try{InstallerPath=await download(progress,cancel.Token);DialogResult=true;}
        catch(OperationCanceledException){ProgressPanel.Visibility=Visibility.Hidden;}
        catch(InvalidDataException ex){Error.Text=ex.Message;ProgressPanel.Visibility=Visibility.Hidden;}
        catch(Exception){Error.Text="No se pudo descargar la actualización. Revisá la conexión a internet y probá de nuevo.";ProgressPanel.Visibility=Visibility.Hidden;}
        finally{cancel.Dispose();cancel=null;if(InstallerPath==null){InstallButton.IsEnabled=true;LaterButton.Content="Más tarde";}}
    }
    void LaterClick(object sender,RoutedEventArgs e){if(cancel!=null){cancel.Cancel();return;}DialogResult=false;}
    /// <summary>Esc cancela la descarga en curso o cierra el diálogo.</summary>
    void DialogKeyDown(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape){LaterClick(sender,e);e.Handled=true;}}
    /// <summary>Cerrar la ventana durante la descarga también la cancela.</summary>
    void DialogClosing(object? sender,System.ComponentModel.CancelEventArgs e)=>cancel?.Cancel();
}
