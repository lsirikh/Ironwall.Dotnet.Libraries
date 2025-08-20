using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
public interface IMarkerControl
{
    IEditableMarker EditableMarker { get; }
    FrameworkElement VisualElement { get; }
    void RefreshFromMarker();
}
