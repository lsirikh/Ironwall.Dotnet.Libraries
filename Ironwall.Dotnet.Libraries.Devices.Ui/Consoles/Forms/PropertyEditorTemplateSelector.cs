using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;

/// <summary>
/// Picks the editor template from the field's effective editor (a locked field is always drawn read-only).
/// </summary>
public sealed class PropertyEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Text { get; set; }
    public DataTemplate? Number { get; set; }
    public DataTemplate? Boolean { get; set; }
    public DataTemplate? Choice { get; set; }
    public DataTemplate? Password { get; set; }
    public DataTemplate? ReadOnly { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not PropertyFieldViewModel field) return ReadOnly;

        return field.EffectiveEditor switch
        {
            DevicePropertyEditor.Text => Text,
            DevicePropertyEditor.Integer => Number,
            DevicePropertyEditor.Number => Number,
            DevicePropertyEditor.Boolean => Boolean,
            // A choice without options cannot be picked from - fall back to free text so the value stays editable.
            DevicePropertyEditor.Choice => field.Options.Count > 0 ? Choice : Text,
            DevicePropertyEditor.Password => Password,
            _ => ReadOnly,
        } ?? ReadOnly;
    }
}
