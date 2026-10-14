using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
namespace POS.Desktop;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.DefaultThreadCurrentCulture=CultureInfo.DefaultThreadCurrentUICulture=Display.Culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(Display.Culture.IetfLanguageTag)));
        ApplicationThemeManager.Apply(ApplicationTheme.Light,WindowBackdropType.None,false);
        ApplicationAccentColorManager.Apply(Color.FromRgb(0x0B,0x6E,0x8A),Color.FromRgb(0x0B,0x6E,0x8A),Color.FromRgb(0x09,0x5F,0x77),Color.FromRgb(0x07,0x50,0x65));
        base.OnStartup(e);
    }
}
