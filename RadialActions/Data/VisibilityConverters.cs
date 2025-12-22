using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace RadialActions;

/// <summary>
/// Converts a value comparison into a Visibility value.
/// </summary>
public sealed class MatchToVisibilityConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter == null)
            return Visibility.Collapsed;

        return Equals(value, parameter) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
