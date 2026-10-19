using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 장비 1개 = 마커 1개 — 다른 마커(A)에 연결된 장비를 마커 B 에 고르면 확인 팝업 → "확인" 때만 A 를 비우고 B 에 연결(2026-10-19).
   Note         : ★ 이벤트 조회표(SymbolLifecycleCoordinator)는 장비당 심볼 하나라 중복 연결이면 한 마커만 이벤트를 받고,
                    부팅 재바인딩은 첫 마커에만 장비 객체를 붙여 다른 마커의 카메라 팝업 · 문 제어가 죽는다.
                  ★ 표준 확인 팝업은 '확인' 때만 콜백한다 → 고르는 즉시 B 를 원래 값(콤보 포함)으로 되돌리고 대기만 기록.
                  ★ 저장 순서: A 비움 → B 연결. B 실패 시 A 를 다시 연결해 저장(한쪽만 바뀌지 않게). Undo = 두 마커 배치 1단위.
                  ★ 이미 중복된 자료는 자동으로 고치지 않는다 — 부팅 로그 경고 + 속성 판 경고 글(LinkedDeviceSharedWarning)만.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel : IHandle<CallReassignLinkedDeviceProcessMessageModel>
{
    /// <summary>확인 대기 중인 재할당 — (장비를 가진 마커, 고른 마커, 장비, 고른 마커의 원래 장비).</summary>
    private (GMapPidsMarker Holder, GMapPidsMarker Target, IBaseDeviceModel Device, IBaseDeviceModel? TargetBefore)? _pendingDeviceReassign;

    /// <summary>재할당이 마커 · 속성 판 연결을 직접 맞추는 중 — 속성 판 에코(OnMarkerPropertyChanged)를 저장 · 기록하지 않는다.</summary>
    private int _deviceLinkSyncDepth;

    /// <summary>마커가 이 장비에 연결돼 있나 — 같은 객체, 또는 같은 Id · 같은 종류(장비 Id 는 종류마다 따로라 Id 만으로는 안 된다).</summary>
    internal static bool IsLinkedTo(GMapPidsMarker marker, IBaseDeviceModel device)
        => !marker.IsDisposed
           && (ReferenceEquals(marker.LinkedDevice, device)
               || (marker.LinkedDeviceId == device.Id && (marker.LinkedDevice?.DeviceType ?? marker.DeviceType) == device.DeviceType));

    /// <summary>지도에서 self 말고 이 장비에 연결된 마커들(지도 순서 = 불러온 순서).</summary>
    private List<GMapPidsMarker> OtherDeviceHolders(GMapPidsMarker self, IBaseDeviceModel device)
        => MainMap?.Markers.OfType<GMapPidsMarker>().Where(m => !ReferenceEquals(m, self) && IsLinkedTo(m, device)).ToList()
           ?? new List<GMapPidsMarker>();

    private static string MarkerName(GMapPidsMarker marker) => string.IsNullOrWhiteSpace(marker.Title) ? $"#{marker.Id}" : marker.Title;

    private static string SymbolKindName(EnumDeviceType type) => type switch
    {
        EnumDeviceType.IpCamera => "카메라",
        EnumDeviceType.Controller => "제어기",
        EnumDeviceType.Enclosure => "함체",
        EnumDeviceType.Gate => "통문",
        EnumDeviceType.IpSpeaker => "스피커",
        EnumDeviceType.Lamp => "경광등",
        EnumDeviceType.NONE => "장비",
        _ => "센서",
    };

    /// <summary>확인 팝업 문구 — 「이 장비(이름)는 'A' 카메라 심볼에 할당되어 있습니다. 재할당하시겠습니까?」.</summary>
    internal static string DeviceReassignQuestion(IBaseDeviceModel device, GMapPidsMarker holder)
        => $"이 장비({device.DeviceName})는 '{MarkerName(holder)}' {SymbolKindName(holder.DeviceType)} 심볼에 할당되어 있습니다. 재할당하시겠습니까?\n"
           + $"(예 = '{MarkerName(holder)}' 의 할당은 비워집니다)";

    /// <summary>
    /// 속성 판 연결 변경 직후(저장 전) — 다른 마커가 가진 장비면 B 를 원래 값으로 되돌리고 확인 팝업을 띄운다.
    /// </summary>
    /// <returns>true = 여기서 처리함(호출자는 저장 · 기록하지 않는다).</returns>
    private async Task<bool> TryRequestDeviceReassignAsync(GMapPidsMarker target, IBaseDeviceModel device, IBaseDeviceModel? before)
    {
        var holder = OtherDeviceHolders(target, device).FirstOrDefault();
        if (holder == null) return false;

        SetDeviceLinkSilently(target, before);   // '아니오' · 닫기 = 원래 값 — 팝업은 '확인' 때만 콜백한다
        _pendingDeviceReassign = (holder, target, device, before);
        _log?.Info($"[장비 재할당] '{device.DeviceName}'({device.Id},{device.DeviceType}) 은 '{MarkerName(holder)}' 에 연결됨 — '{MarkerName(target)}' 로 옮길지 확인");
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = "장비 재할당",
                Explain = DeviceReassignQuestion(device, holder),
                MessageModel = new CallReassignLinkedDeviceProcessMessageModel(),
            });
        }
        catch (Exception ex) { _log?.Error($"[장비 재할당] 확인 창 실패: {ex.Message}"); }
        return true;
    }

    /// <summary>"확인" — A 비움 저장 → B 연결 저장. 하나라도 실패하면 둘 다 원래대로(저장도 되돌림). 성공 시 Undo 배치 1단위.</summary>
    public async Task HandleAsync(CallReassignLinkedDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        var pending = _pendingDeviceReassign;
        _pendingDeviceReassign = null;
        try
        {
            if (pending is not { } p) return;
            var (holder, target, device, before) = p;
            if (target.IsDisposed || holder.IsDisposed || !IsLinkedTo(holder, device) || !ReferenceEquals(target.LinkedDevice, before))
            {
                _log?.Warning($"[장비 재할당] 확인 사이에 상태가 바뀌어 취소: '{device.DeviceName}'");
                return;
            }

            _deviceLinkSyncDepth++;
            try
            {
                SetDeviceLinkSilently(holder, null);
                try { await DbUpdateProcess(holder); }
                catch (Exception ex)
                {
                    SetDeviceLinkSilently(holder, device);
                    _log?.Error($"[장비 재할당] '{MarkerName(holder)}' 비움 저장 실패 — 바꾸지 않음: {ex.Message}");
                    SetAimStatus("장비 재할당을 저장하지 못했습니다.", autoHide: true);
                    return;
                }

                SetDeviceLinkSilently(target, device);
                try { await DbUpdateProcess(target); }
                catch (Exception ex)
                {
                    SetDeviceLinkSilently(target, before);
                    SetDeviceLinkSilently(holder, device);
                    try { await DbUpdateProcess(holder); }
                    catch (Exception ex2) { _log?.Error($"[장비 재할당] '{MarkerName(holder)}' 원복 저장 실패: {ex2.Message}"); }
                    _log?.Error($"[장비 재할당] '{MarkerName(target)}' 연결 저장 실패 — 둘 다 원래대로: {ex.Message}");
                    SetAimStatus("장비 재할당을 저장하지 못했습니다.", autoHide: true);
                    return;
                }
            }
            finally { _deviceLinkSyncDepth--; }

            using (_editRecorder?.BeginBatch($"장비 재할당: {device.DeviceName}"))
            {
                _editRecorder?.RecordPropertyChange(holder, "LinkedDevice", device, null);
                _editRecorder?.RecordPropertyChange(target, "LinkedDevice", before, device);
            }

            holder.ResetLiveState();                                     // 이제 이 장비의 이벤트를 받지 않는다
            SymbolLifecycle.RegisterDevice(device, target.Model);         // 조회표를 새 마커로
            SymbolLifecycle.ReconcileDevice(device.Id, device.DeviceType);
            RefreshDeviceAssignmentNotes();
            _log?.Info($"[장비 재할당] '{device.DeviceName}' : '{MarkerName(holder)}' → '{MarkerName(target)}'");
            SetAimStatus($"장비 '{device.DeviceName}' 을(를) '{MarkerName(target)}' 에 할당했습니다. '{MarkerName(holder)}' 의 할당은 비웠습니다.", autoHide: true);
        }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }

    /// <summary>마커 연결을 직접 바꾸고 열린 속성 판(그 마커)도 맞춘다 — 판 에코는 저장 · 기록하지 않는다.</summary>
    private void SetDeviceLinkSilently(GMapPidsMarker marker, IBaseDeviceModel? device)
    {
        _deviceLinkSyncDepth++;
        try
        {
            marker.LinkedDevice = device;   // LinkedDeviceId 동기(null = 0, 기존 '연결 안 됨')
            if (PropertyPanel is GMapPropertyPidsControl panel && ReferenceEquals(panel.SelectedMarker, marker))
                panel.SetCurrentValue(GMapPropertyPidsControl.LinkedDeviceProperty, device);
        }
        finally { _deviceLinkSyncDepth--; }
    }

    /// <summary>
    /// 열린 PIDS 속성 판의 콤보 항목 글(다른 마커에 할당됨)과 중복 경고 글을 지금 지도 기준으로 다시 채운다.
    /// </summary>
    private void RefreshDeviceAssignmentNotes()
    {
        if (PropertyPanel is not GMapPropertyPidsControl panel || panel.SelectedMarker is not GMapPidsMarker self) return;

        var devices = panel.FilteredDeviceList ?? Enumerable.Empty<IBaseDeviceModel>();
        var notes = new Dictionary<IBaseDeviceModel, string>(ReferenceEqualityComparer.Instance);
        foreach (var device in devices)
            if (OtherDeviceHolders(self, device).FirstOrDefault() is { } holder)
                notes[device] = $"· {MarkerName(holder)} 에 할당됨";
        panel.DeviceAssignmentNotes = notes;

        // 이미 중복된 자료 — 부팅 재바인딩이 장비 객체를 첫 마커에만 붙이므로 Id 로도 찾는다
        var linked = self.LinkedDevice
                     ?? (self.LinkedDeviceId > 0 ? devices.FirstOrDefault(d => d.Id == self.LinkedDeviceId) : null);
        var shared = linked == null ? new List<GMapPidsMarker>() : OtherDeviceHolders(self, linked);
        panel.LinkedDeviceSharedWarning = shared.Count == 0
            ? null
            : $"이 장비는 다른 마커({string.Join(", ", shared.Select(m => $"'{MarkerName(m)}'"))})에도 연결되어 있습니다. 한 마커에만 연결하세요.";
    }
}
