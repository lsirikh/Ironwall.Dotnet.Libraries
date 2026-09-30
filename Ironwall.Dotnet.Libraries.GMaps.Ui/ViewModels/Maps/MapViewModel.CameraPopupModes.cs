using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.CameraPopup;
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

    #region - 지도 하단 호스트 안내(FR-25) -
    private string _cameraPopupHostNoticeMessage = string.Empty;
    private bool _isCameraPopupHostNoticeVisible;
    private ICameraPopupHost? _cameraPopupNoticeHost;
    private CameraPopupHostState? _cameraPopupNoticeState;

    /// <summary>계속 떠 있는 안내 한 줄("영상 기능 일시 중지") — 옆에 [다시 시작].</summary>
    public string CameraPopupHostNoticeMessage
    {
        get => _cameraPopupHostNoticeMessage;
        private set { _cameraPopupHostNoticeMessage = value; NotifyOfPropertyChange(nameof(CameraPopupHostNoticeMessage)); }
    }

    /// <summary>호스트가 일시 중지(Suspended)인 동안 참 — 오버레이가 하나도 없어도 지도 하단에 보인다.</summary>
    public bool IsCameraPopupHostNoticeVisible
    {
        get => _isCameraPopupHostNoticeVisible;
        private set { _isCameraPopupHostNoticeVisible = value; NotifyOfPropertyChange(nameof(IsCameraPopupHostNoticeVisible)); }
    }

    /// <summary>
    /// 활성화 때 한 번 — 호스트 상태 변화를 구독하고, 이미 멈춰 있으면 지금 안내한다. 호스트가 없으면(미등록) 아무것도 안 한다.
    /// 던지지 않는다 — 안내가 실패해도 지도는 뜬다.
    /// </summary>
    private void StartCameraPopupHostNotice()
    {
        try
        {
            if (_cameraPopupNoticeHost != null) return;
            var host = ResolveCameraPopupHost();
            if (host == null) return;
            _cameraPopupNoticeHost = host;
            host.StateChanged += OnCameraPopupHostStateChanged;
            ApplyCameraPopupHostState(host.State);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopup] 호스트 안내 구독 실패(GIS 는 정상): {ex.Message}");
        }
    }

    private void StopCameraPopupHostNotice()
    {
        var host = _cameraPopupNoticeHost;
        _cameraPopupNoticeHost = null;
        if (host == null) return;
        try { host.StateChanged -= OnCameraPopupHostStateChanged; }
        catch (Exception ex) { _log?.Warning($"[CameraPopup] 호스트 안내 구독 해제 실패(무시): {ex.Message}"); }
    }

    /// <summary>감시자의 배경 스레드에서 온다 — UI 로 옮기고, 무엇이 실패해도 던지지 않는다(FR-27).</summary>
    private void OnCameraPopupHostStateChanged(object? sender, CameraPopupHostStateChangedEventArgs e)
    {
        try { _ = OnUiAsync(() => ApplyCameraPopupHostState(e.NewState)); }
        catch (Exception ex) { _log?.Warning($"[CameraPopup] 호스트 안내 갱신 실패(무시): {ex.Message}"); }
    }

    /// <summary>
    /// 상태 → 안내: 일시 중지 = 계속 떠 있는 "영상 기능 일시 중지 · [다시 시작]", 사용할 수 없음 = 토스트 한 번,
    /// 그 밖(정상 · 시작 중 · 재시작 중) = 안내를 지운다. UI 스레드에서 부른다.
    /// </summary>
    internal void ApplyCameraPopupHostState(CameraPopupHostState state)
    {
        var previous = _cameraPopupNoticeState;
        _cameraPopupNoticeState = state;
        switch (CameraPopupHostNotices.KindOf(state))
        {
            case CameraPopupHostNoticeKind.Persistent:
                CameraPopupHostNoticeMessage = CameraPopupHostNotices.Suspended;
                IsCameraPopupHostNoticeVisible = true;
                break;
            case CameraPopupHostNoticeKind.Toast:
                IsCameraPopupHostNoticeVisible = false;
                // 같은 상태가 다시 알려져도 한 번만 띄운다.
                if (previous != state) ShowCameraPopupToast(CameraPopupHostNotices.Unavailable, pending: false);
                break;
            default:
                IsCameraPopupHostNoticeVisible = false;
                break;
        }
    }

    /// <summary>[다시 시작] — 감시자의 재시작 예산을 비우고 호스트를 다시 띄운다. 기다리지 않고 던지지 않는다.</summary>
    public void RestartCameraPopupHost()
    {
        try
        {
            var host = _cameraPopupNoticeHost ?? ResolveCameraPopupHost();
            _log?.Info("[CameraPopup] 지도 하단 [다시 시작]");
            host?.Restart();
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopup] 호스트 다시 시작 실패(GIS 는 정상): {ex.Message}");
        }
    }
    #endregion
}
