using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : 서버에서 지워진 이벤트(SYNC_DETECTION DELETED)를 카드 목록이 닫았다 → 지도 하단 토스트로 잠깐 알린다.
                  토스트는 카메라 팝업 결과 토스트와 같은 자리 · 같은 시간(5초)을 쓴다. MapViewModel partial 분리.
   Created By   : Claude
   Created On   : 2026-10-04
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public partial class MapViewModel : IHandle<EventDeletedOnServerNotice>
{
    /// <summary>카드 목록이 서버 삭제로 카드 · 큐 엔트리를 닫았을 때만 온다(닫을 것이 없었으면 오지 않는다).</summary>
    public Task HandleAsync(EventDeletedOnServerNotice message, CancellationToken cancellationToken)
    {
        if (message is null) return Task.CompletedTask;
        ShowCameraPopupToast(message.Text, pending: false, MaterialDesignThemes.Wpf.PackIconKind.DeleteOutline);   // 어느 스레드에서나 — UI 로 옮긴다
        return Task.CompletedTask;
    }
}
