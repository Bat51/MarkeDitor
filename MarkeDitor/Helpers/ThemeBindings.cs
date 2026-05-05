using Avalonia;
using Avalonia.Controls;

namespace MarkeDitor.Helpers;

public static class ThemeBindings
{
    public static T BindToResource<T>(this T element, AvaloniaProperty property, string resourceKey)
        where T : Control
    {
        element.Bind(property, element.GetResourceObservable(resourceKey));
        return element;
    }
}
