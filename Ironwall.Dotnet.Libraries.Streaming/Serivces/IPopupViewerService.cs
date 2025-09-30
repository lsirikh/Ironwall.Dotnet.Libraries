using Ironwall.Dotnet.Libraries.Streaming.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
public interface IPopupViewerService
{
    void ClosePopup();
    void ShowPopup(params RtspConnectionInfo[] connections);
    bool IsPopupOpen { get; }

}