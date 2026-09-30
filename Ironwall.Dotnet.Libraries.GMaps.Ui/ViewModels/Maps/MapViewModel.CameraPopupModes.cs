using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : 카메라 팝업 방식(자체 · 브로커 · 사용 안 함) 더블클릭 분기 배선 + 지도 하단 토스트
                  (camera-popup-modes T-07 · FR-01~03 · FR-05/06 · FR-27/28). MapViewModel partial 분리.
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public partial class MapViewModel
{
    /// <summary>결과 토스트가 떠 있는 시간.</summary>
    private static readonly TimeSpan CameraPopupToastHold = TimeSpan.FromSeconds(5);

    /// <summary>대기 토스트("요청했습니다")의 상한 — 결과가 반드시 오지만(응답 기다림 + 여유) 그래도 남지 않게.</summary>
    private static readonly TimeSpan CameraPopupPendingToastHold = TimeSpan.FromSeconds(75);

    private CameraPopupDoubleClickDispatcher? _cameraPopupDoubleClick;
    private ICameraPopupBrokerService? _cameraPopupBroker;
    private bool _cameraPopupBrokerResolved;

    private string _cameraPopupToastMessage = string.Empty;
    private bool _isCameraPopupToastVisible;
    private int _cameraPopupToastGeneration;

    /// <summary>
    /// 더블클릭 분기기 — 처음 쓸 때 만든다(시험은 GetUninitializedObject 로 뷰모델을 만들므로 생성자에서 만들지 않는다).
    /// </summary>
    private CameraPopupDoubleClickDispatcher CameraPopupDoubleClick
        => _cameraPopupDoubleClick ??= new CameraPopupDoubleClickDispatcher(
            ResolveOverlaySettings,
            OpenSelfCameraPopup,
            ShowSymbolDetail,                 // 사용 안 함 — 우클릭 `상세 보기`와 같은 경로(FR-03)
            ResolveCameraPopupBroker,
            CurrentRequestedBy,
            notice => ShowCameraPopupToast(notice.Text, notice.IsPending),
            _log);

    /// <summary>브로커 낱말 서비스 — GMapUiModule 등록. 미등록이면 null(토스트로 안내).</summary>
    private ICameraPopupBrokerService? ResolveCameraPopupBroker()
    {
        if (_cameraPopupBrokerResolved) return _cameraPopupBroker;
        _cameraPopupBrokerResolved = true;
        try { _cameraPopupBroker = IoC.Get<ICameraPopupBrokerService>(); }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopup] 브로커 서비스 미등록: {ex.Message}");
            _cameraPopupBroker = null;
        }
        return _cameraPopupBroker;
    }

    #region - 지도 하단 토스트(FR-06) -
    /// <summary>지도 하단 토스트 한 줄(브로커 요청 결과).</summary>
    public string CameraPopupToastMessage
    {
        get => _cameraPopupToastMessage;
        private set { _cameraPopupToastMessage = value; NotifyOfPropertyChange(nameof(CameraPopupToastMessage)); }
    }

    public bool IsCameraPopupToastVisible
    {
        get => _isCameraPopupToastVisible;
        private set { _isCameraPopupToastVisible = value; NotifyOfPropertyChange(nameof(IsCameraPopupToastVisible)); }
    }

    /// <summary>
    /// 토스트를 띄운다(어느 스레드에서나 — UI 로 옮긴다). 새 토스트가 옛 것을 덮는다.
    /// 대기(<paramref name="pending"/>) 토스트는 결과가 올 때까지 둔다.
    /// </summary>
    private void ShowCameraPopupToast(string message, bool pending)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _log?.Info($"[CameraPopup] 토스트: {message}");
        _ = OnUiAsync(() =>
        {
            var gen = ++_cameraPopupToastGeneration;
            CameraPopupToastMessage = message;
            IsCameraPopupToastVisible = true;
            _ = HideCameraPopupToastLaterAsync(gen, pending ? CameraPopupPendingToastHold : CameraPopupToastHold);
        });
    }

    private async Task HideCameraPopupToastLaterAsync(int gen, TimeSpan hold)
    {
        try
        {
            await Task.Delay(hold).ConfigureAwait(false);
            await OnUiAsync(() =>
            {
                if (gen == _cameraPopupToastGeneration) IsCameraPopupToastVisible = false;
            }).ConfigureAwait(false);
        }
        catch { /* 종료 중 Dispatcher 없음 — 무시 */ }
    }
    #endregion
}
