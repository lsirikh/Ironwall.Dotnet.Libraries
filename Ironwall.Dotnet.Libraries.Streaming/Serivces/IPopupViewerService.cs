using Ironwall.Dotnet.Libraries.Streaming.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
public interface IPopupViewerService
{
    void ClosePopup();
    void ShowPopup(params ICameraModel[] connections);
    void PositionWindow(EnumDisplayPosition? position = null);
    bool IsPopupOpen { get; }

}