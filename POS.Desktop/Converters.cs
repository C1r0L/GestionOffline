using System.Globalization;
using System.Windows.Data;
namespace POS.Desktop;
/// <summary>Centavos (long) a importe en pesos: 1050000 → "$ 10.500,00".</summary>
public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is long minor?Display.Money(minor):"";
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is not true;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>value is not true;
}
