using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.ViewModel;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
public interface IPopupViewerService
{
    void ClosePopup();
    void ShowPopup(params ICameraModel[] connections);
    void PositionWindow(EnumDisplayPosition? position = null);
    bool IsPopupOpen { get; }

}