using System.Globalization;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using ContractProviderKind = Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol.VideoProviderKind;
using SettingsProviderKind = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.VideoProviderKind;

namespace Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;

/****************************************************************************
   Purpose      : 탐지 트리거의 순수 함수 — 어떤 이벤트에 창을 여는가 · 카메라 고르기 · 타일 · 머리 (PRD camera-popup-modes FR-07 · 09 · 10 · 13 · 14 · 16)
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>헤드리스 시험 대상 — 상태 없음.</summary>
public static class EventWindowPlanning
{
    /// <summary>
    /// 이 이벤트에 창을 여는가 — <b>자체 모드</b>에서만(브로커 모드의 탐지는 프록시 매니저 몫, FR-07 · 사용 안 함은 창 없음).
    /// 탐지 계열(침입 · 접점 켜짐 · 사전 경보)은 "탐지 팝업", 장애(Fault)는 "장애도 띄움"을 따른다.
    /// 접점 꺼짐 · 연결 · 조치 · 운영 · 풍량 같은 상태 알림은 창을 열지 않는다.
    /// </summary>
    public static bool ShouldOpen(CameraPopupSettings settings, EnumEventType eventType)
    {
        if (settings is null || settings.Mode != CameraPopupMode.Self) return false;
        return eventType switch
        {
            EnumEventType.Fault => settings.EventWindowOnMalfunction,
            EnumEventType.Intrusion or EnumEventType.ContactOn or EnumEventType.Alert => settings.EventWindowOnDetection,
            _ => false,
        };
    }

    /// <summary>큐 유형 → 창 종류(장애만 장애, 나머지는 탐지 — <c>EventCardKind.Of</c> 와 같은 규칙).</summary>
    public static EventWindowKind KindOf(EnumEventType eventType)
        => eventType == EnumEventType.Fault ? EventWindowKind.Malfunction : EventWindowKind.Detection;

    /// <summary>카드 종류 문자열("detection" · "malfunction") → 창 종류. 모르면 null.</summary>
    public static EventWindowKind? KindOf(string? cardKind) => cardKind switch
    {
        ActionReportKind.Detection => EventWindowKind.Detection,
        ActionReportKind.Malfunction => EventWindowKind.Malfunction,
        _ => null,
    };

    /// <summary>
    /// 매핑 배선 → 타일 순서: 켜진 배선만 · <c>priority</c> 오름차순(null 은 뒤) · 같으면 배선 id · 같은 카메라는 처음 것 하나.
    /// </summary>
    public static IReadOnlyList<MappingCameraEntry> Order(IEnumerable<MappingCameraEntry>? entries)
        => (entries ?? Enumerable.Empty<MappingCameraEntry>())
            .Where(e => e is not null && e.IsEnable && e.CameraId > 0)
            .OrderBy(e => e.Priority ?? int.MaxValue)
            .ThenBy(e => e.ConfigId)
            .DistinctBy(e => e.CameraId)
            .ToList();

    /// <summary>
    /// 순서대로 풀어(장비 캐시에 없는 카메라는 건너뜀) 창당 카메라 수만큼 담고, 나머지 수를 "+N" 으로 돌려준다(FR-10).
    /// </summary>
    public static (IReadOnlyList<T> Selected, int Extra) Select<T>(IEnumerable<MappingCameraEntry>? entries, int camerasPerWindow,
                                                                   Func<MappingCameraEntry, T?> resolve) where T : class
    {
        var take = CameraPopupGridLayouts.ClampCount(camerasPerWindow);
        var resolved = new List<T>();
        foreach (var entry in Order(entries))
        {
            T? item;
            try { item = resolve(entry); }
            catch (Exception) { item = null; }   // 카메라 하나를 못 풀어도 창은 연다
            if (item is not null) resolved.Add(item);
        }
        return (resolved.Take(take).ToList(), Math.Max(0, resolved.Count - take));
    }

    /// <summary>
    /// 카메라 장비 + 배선 → 타일 계약. 제공자는 설정(ONVIF · RTSP 주소), 계정은 장비 값.
    /// PTZ 는 장비 종류(<see cref="EnumCameraType.PTZ"/>)로 정하고, 권한은 부르는 쪽이 준다(없으면 PTZ 메뉴 비활성, FR-14).
    /// 프리셋 토큰은 서버 <c>preset_index</c> 를 그대로 문자열로 쓴다(ONVIF 토큰 = 번호인 카메라 기준 — 실기 확인 필요).
    /// </summary>
    public static EventWindowCamera BuildCamera(ICameraDeviceModel camera, MappingCameraEntry entry, CameraPopupSettings settings, bool ptzAllowed)
    {
        var isPtz = camera.Category == EnumCameraType.PTZ;
        var target = isPtz && entry.TargetPresetIndex is int t && t >= 0 ? t.ToString(CultureInfo.InvariantCulture) : null;
        var home = isPtz && entry.HomePresetIndex is int h && h >= 0 ? h.ToString(CultureInfo.InvariantCulture) : null;
        return new EventWindowCamera
        {
            CameraId = camera.Id.ToString(CultureInfo.InvariantCulture),
            Name = string.IsNullOrWhiteSpace(camera.DeviceName) ? $"카메라 {camera.Id}" : camera.DeviceName,
            Provider = BuildProvider(camera, settings),
            IsPtz = isPtz,
            PtzAllowed = ptzAllowed,
            TargetPresetToken = target,
            TargetPresetName = target is null ? null : (string.IsNullOrWhiteSpace(entry.TargetPresetName) ? $"P{target}" : entry.TargetPresetName),
            HomePresetToken = home,
            DelaySeconds = target is null ? 0 : Math.Max(0, entry.DelaySeconds),
        };
    }

    /// <summary>설정의 제공자 → 호스트 제공자 정보. RTSP 주소 제공자는 장비의 주 스트림(없으면 보조).</summary>
    public static VideoProviderInfo BuildProvider(ICameraDeviceModel camera, CameraPopupSettings settings)
    {
        if (settings.Provider == SettingsProviderKind.RtspUrl)
        {
            var rtsp = FirstNonEmpty(camera.Urls?.RtspMain, camera.Urls?.RtspSub);
            return new VideoProviderInfo
            {
                Kind = ContractProviderKind.Rtsp,
                Uri = rtsp,
                Username = camera.UserName,
                Password = camera.UserPassword,
            };
        }
        return new VideoProviderInfo
        {
            Kind = ContractProviderKind.Onvif,
            Uri = FirstNonEmpty(camera.Urls?.OnvifDeviceService) ?? DefaultOnvifService(camera.IpAddress, camera.IpPort),
            Username = camera.UserName,
            Password = camera.UserPassword,
        };
    }

    /// <summary>장비에 ONVIF 서비스 주소가 없을 때의 표준 주소.</summary>
    public static string? DefaultOnvifService(string? ip, int port)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var p = port > 0 ? port : 80;
        return string.Create(CultureInfo.InvariantCulture, $"http://{ip.Trim()}:{p}/onvif/device_service");
    }

    /// <summary>창 머리(FR-16) — 배지 · 구역 · 장비 · 종류 · 시각.</summary>
    public static EventWindowHeader BuildHeader(EnumEventType eventType, string? zoneName, string? deviceName, DateTime occurredLocal)
        => new()
        {
            KindLabel = eventType == EnumEventType.Fault ? "장애" : "탐지",
            ZoneName = zoneName,
            DeviceName = deviceName,
            EventTypeText = EventTypeText(eventType),
            OccurredAt = occurredLocal == default ? null : new DateTimeOffset(DateTime.SpecifyKind(occurredLocal, DateTimeKind.Local)),
        };

    public static string EventTypeText(EnumEventType eventType) => eventType switch
    {
        EnumEventType.Intrusion => "침입",
        EnumEventType.ContactOn => "접점 켜짐",
        EnumEventType.Alert => "사전 경보",
        EnumEventType.Fault => "장애",
        _ => eventType.ToString(),
    };

    /// <summary>창 제목(작업 표시줄 · Alt+Tab) — "탐지 · 펜스 센서 #104".</summary>
    public static string BuildTitle(EventWindowHeader header, string eventId)
        => string.IsNullOrWhiteSpace(header.DeviceName)
            ? $"{header.KindLabel} · 이벤트 {eventId}"
            : $"{header.KindLabel} · {header.DeviceName}";

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
