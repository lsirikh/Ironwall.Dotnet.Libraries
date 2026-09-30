using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.CameraPopup;

/****************************************************************************
   Purpose      : 지도 카메라 더블클릭 — 팝업 방식(자체 · 브로커 · 사용 안 함)으로 가르는 한 곳
                  (camera-popup-modes T-07 · FR-01~03 · FR-05/06 · FR-27/28)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>더블클릭이 간 길.</summary>
public enum CameraPopupDoubleClickRoute
{
    /// <summary>카메라가 아니거나 열 것이 없어 아무것도 하지 않았다.</summary>
    Ignored,

    /// <summary>GIS 자체 팝업 — 지도 위 상자(호스트 프로세스 영상, T-02 경로 그대로).</summary>
    SelfOverlay,

    /// <summary>사용 안 함 — 영상 대신 카메라 상세(속성)를 열었다(FR-03).</summary>
    ShowDetail,

    /// <summary>브로커 요청 — <c>CAMERA_POPUP_OPEN</c> 을 보냈다(결과는 토스트, FR-05/06).</summary>
    BrokerRequest,
}

/// <summary>
/// 옛 게이트(<c>IsCameraPopupUsed</c>) 자리를 대신한다 — 설정 <see cref="CameraPopupSettings.Mode"/> 로 길을 고르고,
/// 실제 동작은 지도 뷰모델이 넘긴 콜백이 한다(뷰모델 없이 시험하려고 떼어 냈다).
/// <para>설정 창구가 없으면(등록 안 됨 · 읽기 실패) 옛 기본 동작 = 자체 팝업.</para>
/// <para>이 안에서는 아무것도 기다리지 않는다(FR-28) — 브로커 요청은 <see cref="LastBrokerTask"/> 로 떠나보낸다.</para>
/// </summary>
public sealed class CameraPopupDoubleClickDispatcher
{
    private readonly Func<ICameraPopupOverlaySettings?> _settings;
    private readonly Action<IPidsEditableMarker, ICameraDeviceModel, CameraPopupSettings> _openSelf;
    private readonly Action<IPidsEditableMarker> _showDetail;
    private readonly Func<ICameraPopupBrokerService?> _broker;
    private readonly Func<string?> _requestedBy;
    private readonly Action<CameraPopupBrokerNotice> _toast;
    private readonly ILogService? _log;

    /// <param name="settings">라이브 설정 창구(매번 다시 읽는다).</param>
    /// <param name="openSelf">자체 팝업 열기(지도 위 상자).</param>
    /// <param name="showDetail">카메라 상세(속성) 열기 — 우클릭 `상세 보기`와 같은 경로.</param>
    /// <param name="broker">브로커 낱말 서비스(없으면 null).</param>
    /// <param name="requestedBy">요청자 표시(<c>requested_by</c>).</param>
    /// <param name="toast">지도 하단 토스트(어느 스레드에서나 불린다 — 받는 쪽이 UI 로 옮긴다).</param>
    public CameraPopupDoubleClickDispatcher(
        Func<ICameraPopupOverlaySettings?> settings,
        Action<IPidsEditableMarker, ICameraDeviceModel, CameraPopupSettings> openSelf,
        Action<IPidsEditableMarker> showDetail,
        Func<ICameraPopupBrokerService?> broker,
        Func<string?> requestedBy,
        Action<CameraPopupBrokerNotice> toast,
        ILogService? log = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _openSelf = openSelf ?? throw new ArgumentNullException(nameof(openSelf));
        _showDetail = showDetail ?? throw new ArgumentNullException(nameof(showDetail));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _requestedBy = requestedBy ?? throw new ArgumentNullException(nameof(requestedBy));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        _log = log;
    }

    /// <summary>마지막 브로커 요청(시험 · 종료 대기용). 요청이 없었으면 끝난 작업.</summary>
    public Task<CameraPopupBrokerOutcome?> LastBrokerTask { get; private set; } = Task.FromResult<CameraPopupBrokerOutcome?>(null);

    /// <summary>모드 → 길(순수 함수).</summary>
    public static CameraPopupDoubleClickRoute RouteFor(CameraPopupMode mode) => mode switch
    {
        CameraPopupMode.Broker => CameraPopupDoubleClickRoute.BrokerRequest,
        CameraPopupMode.None => CameraPopupDoubleClickRoute.ShowDetail,
        _ => CameraPopupDoubleClickRoute.SelfOverlay,
    };

    /// <summary>더블클릭 한 번. 예외를 밖으로 내지 않는다(FR-27).</summary>
    public CameraPopupDoubleClickRoute Handle(IEditableMarker? marker)
    {
        try
        {
            if (marker is not IPidsEditableMarker pids || pids.DeviceType != EnumDeviceType.IpCamera)
                return CameraPopupDoubleClickRoute.Ignored;

            var source = SafeSettingsSource();
            var settings = SafeSettings(source);
            var route = RouteFor(settings.Mode);

            switch (route)
            {
                case CameraPopupDoubleClickRoute.ShowDetail:
                    _log?.Info($"[CameraPopup] 사용 안 함 — {pids.Title} 상세 보기를 연다");
                    _showDetail(pids);
                    return route;

                case CameraPopupDoubleClickRoute.BrokerRequest:
                    StartBrokerRequest(pids, source, settings);
                    return route;

                default:
                    if (pids.LinkedDevice is not ICameraDeviceModel camera)
                    {
                        _log?.Warning($"[CameraPopup] 카메라 모델 없음(LinkedDevice null): {pids.Title}");
                        return CameraPopupDoubleClickRoute.Ignored;
                    }
                    _openSelf(pids, camera, settings);
                    return route;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[CameraPopup] 더블클릭 처리 실패: {ex.Message}");
            return CameraPopupDoubleClickRoute.Ignored;
        }
    }

    private void StartBrokerRequest(IPidsEditableMarker marker, ICameraPopupOverlaySettings? source, CameraPopupSettings settings)
    {
        var broker = SafeBroker();
        if (broker is null)
        {
            _log?.Warning("[CameraPopup] 브로커 모드인데 브로커 서비스가 등록되지 않았다");
            _toast(new CameraPopupBrokerNotice(CameraPopupBrokerToasts.NoBroker, false));
            return;
        }

        var linkedId = marker.LinkedDevice?.Id ?? 0;
        var cameraId = linkedId > 0 ? linkedId : marker.LinkedDeviceId;
        var name = !string.IsNullOrWhiteSpace(marker.Title) ? marker.Title : marker.LinkedDevice?.DeviceName;
        var request = CameraPopupOpenRequest.From(settings, cameraId, name, SafeClientId(source), SafeRequestedBy());
        LastBrokerTask = RunBrokerAsync(broker, request);
    }

    private async Task<CameraPopupBrokerOutcome?> RunBrokerAsync(ICameraPopupBrokerService broker, CameraPopupOpenRequest request)
    {
        try
        {
            return await broker.RequestOpenAsync(request, _toast).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Error($"[CameraPopup] 브로커 요청 실패: {ex.Message}");
            try { _toast(new CameraPopupBrokerNotice(CameraPopupBrokerToasts.Failed, false)); } catch { /* 토스트 실패는 삼킨다 */ }
            return null;
        }
    }

    private ICameraPopupOverlaySettings? SafeSettingsSource()
    {
        try { return _settings(); }
        catch (Exception ex) { _log?.Warning($"[CameraPopup] 설정 창구 읽기 실패(자체 팝업으로): {ex.Message}"); return null; }
    }

    private CameraPopupSettings SafeSettings(ICameraPopupOverlaySettings? source)
    {
        try { return source?.Settings ?? new CameraPopupSettings(); }
        catch (Exception ex) { _log?.Warning($"[CameraPopup] 팝업 설정 읽기 실패(자체 팝업으로): {ex.Message}"); return new CameraPopupSettings(); }
    }

    private ICameraPopupBrokerService? SafeBroker()
    {
        try { return _broker(); }
        catch (Exception ex) { _log?.Warning($"[CameraPopup] 브로커 서비스 해석 실패: {ex.Message}"); return null; }
    }

    private string SafeClientId(ICameraPopupOverlaySettings? source)
    {
        try { return source?.ClientId ?? string.Empty; }
        catch { return string.Empty; }
    }

    private string? SafeRequestedBy()
    {
        try { return _requestedBy(); }
        catch { return null; }
    }
}
