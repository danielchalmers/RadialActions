using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace RadialActions;

/// <summary>
/// Shows a <see cref="Version"/> as three parts in bindings.
/// </summary>
public sealed class VersionTextConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Version version ? AppInfoFormatter.FormatVersion(version) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
