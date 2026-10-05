using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace RadialActions;

/// <summary>
/// Converts an <see cref="ActionType"/> into its display name.
/// </summary>
public sealed class ActionTypeNameConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => ActionDisplayText.GetTypeName(value is ActionType type ? type : ActionType.None);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Converts an action's name, type and enabled state (in that order) into its accessible list item name.
/// </summary>
public sealed class ActionListItemNameConverter : MarkupExtension, IMultiValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var name = values.Length > 0 ? values[0] as string : null;
        var type = values.Length > 1 && values[1] is ActionType actionType ? actionType : ActionType.None;
        var isEnabled = values.Length <= 2 || values[2] is not false;
        return ActionDisplayText.GetListItemName(name, type, isEnabled);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => null;
}

/// <summary>
/// Joins the non-empty strings of a multi-binding in order with spaces, such as a status message followed by a field's help text.
/// </summary>
public sealed class TextJoinConverter : MarkupExtension, IMultiValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var parts = (values ?? []).OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim());
        return string.Join(" ", parts);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => null;
}
