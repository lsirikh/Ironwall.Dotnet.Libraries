using System.Collections.Specialized;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 지도 심볼 ↔ 이벤트 조회표 등록 수명(WP-2 A1~A5) — 재등록 뒤 큐 기준 재칠 · 장비 삭제 해제 · 미등록 전이 관측.
                  MapViewModel partial 분리 — 본체는 등록 호출을 코디네이터로 바꾼 줄만 고친다.
   Created By   : GHLee
   Created On   : 9/30/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public partial class MapViewModel
{
    private SymbolLifecycleCoordinator? _symbolLifecycle;

    /// <summary>등록 수명 코디네이터(생성자에서 한 번 만든다).</summary>
    private SymbolLifecycleCoordinator SymbolLifecycle
        => _symbolLifecycle ??= new SymbolLifecycleCoordinator(_symbolEventManager, _eventQueueManager, _log);

    /// <summary>
    /// 생성자에서 한 번 — 큐 전이 관찰(A5)과 장비 공급자 삭제 관찰(A3)을 건다. 둘 다 앱 수명 싱글턴이라 해제하지 않는다.
    /// </summary>
    private void AttachSymbolLifecycle()
    {
        if (_eventQueueManager != null)
            _eventQueueManager.OnDeviceStateChanged += OnQueueDeviceStateChangedForLifecycle;
        if (DeviceProvider?.CollectionEntity != null)
            DeviceProvider.CollectionEntity.CollectionChanged += OnDeviceProviderChangedForLifecycle;
    }

    private void OnQueueDeviceStateChangedForLifecycle(int deviceId, EnumDeviceType deviceType, EnumCompositeEventStatus previous, EnumCompositeEventStatus next)
        => SymbolLifecycle.ObserveDeviceTransition(deviceId, deviceType, previous, next);

    /// <summary>
    /// 장비 공급자에서 장비가 빠졌다(호스트 SYNC_DEVICE DELETED → <c>RemoveDeviceByIdAsync</c>) — 그 장비의 지도 심볼을 해제한다.
    /// 전량 재조회(Reset)는 이어서 오는 전량 재등록이 맡으므로 건드리지 않는다. 호출 스레드: 공급자가 UI 로 넘긴다.
    /// </summary>
    private void OnDeviceProviderChangedForLifecycle(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Remove || e.OldItems is null) return;
        foreach (var removed in e.OldItems.OfType<IBaseDeviceModel>())
            UnregisterDevice(removed.Id, removed.DeviceType, removed);
    }

    /// <summary>
    /// [A3] 장비 삭제 — 조회표에서 끊고 지도 심볼의 실시간 상태(이벤트 색 · 동작 · 문 · 부품)를 비운다. 저장된 연결은 남긴다.
    /// </summary>
    /// <remarks>
    /// 장비 공급자 삭제를 스스로 관찰하므로 호스트가 따로 부를 필요는 없다. 다른 경로(예: 콘솔 삭제 직후)에서 즉시 반영이 필요하면 부른다.
    /// 호출 스레드: 아무 곳 — UI 로 넘긴다.
    /// </remarks>
    /// <returns>지도 심볼을 하나라도 비웠으면 true.</returns>
    public bool UnregisterDevice(int deviceId, EnumDeviceType deviceType, IBaseDeviceModel? removedDevice = null)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            return dispatcher.Invoke(() => UnregisterDevice(deviceId, deviceType, removedDevice));

        var detached = SymbolLifecycle.UnregisterDevice(deviceId, deviceType);
        var markers = MainMap?.Markers.OfType<GMapPidsMarker>()
            .Where(m => removedDevice != null
                ? ReferenceEquals(m.LinkedDevice, removedDevice) || ReferenceEquals(m.Model, detached)
                : ReferenceEquals(m.Model, detached) || (m.LinkedDeviceId == deviceId && m.DeviceType == deviceType))
            .ToList() ?? new List<GMapPidsMarker>();
        foreach (var marker in markers) marker.ResetLiveState();

        if (detached != null || markers.Count > 0)
            _log?.Info($"[장비 삭제→심볼] Device({deviceId},{deviceType}) 조회표 {(detached != null ? "해제" : "미등록")} · 심볼 {markers.Count}개 실시간 상태 비움");
        return markers.Count > 0;
    }
}
