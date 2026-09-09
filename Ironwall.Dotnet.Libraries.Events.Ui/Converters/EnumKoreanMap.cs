using System;
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Converters;
/****************************************************************************
   Purpose      : 이벤트 UI 표시 전용 — enum 값을 한글 라벨로 변환하는 단일 정본(SSOT) 맵.
                  ⚠ 표시 계층 전용: enum 정의/DTO/NATS/DB/직렬화 값은 절대 변경하지 않는다.
                  매핑에 없는 값은 원문(ToString())으로 폴백 — 신규 enum 값 추가 시 여기만 갱신.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public static class EnumKoreanMap
{
    // ── 이벤트 종류 (MessageType) ─────────────────────────────────────────
    private static readonly IReadOnlyDictionary<EnumEventType, string> _eventType = new Dictionary<EnumEventType, string>
    {
        [EnumEventType.None]       = "없음",
        [EnumEventType.Intrusion]  = "침입",
        [EnumEventType.ContactOn]  = "접점 ON",
        [EnumEventType.ContactOff] = "접점 OFF",
        [EnumEventType.Connection] = "연결보고",
        [EnumEventType.Action]     = "조치보고",
        [EnumEventType.Fault]      = "장애",
        [EnumEventType.WindyMode]  = "풍량모드",
    };

    // ── 탐지 결과 (Result) ────────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<EnumDetectionType, string> _detectionType = new Dictionary<EnumDetectionType, string>
    {
        [EnumDetectionType.NONE]             = "없음",
        [EnumDetectionType.CABLE_CUTTING]    = "케이블 절단",
        [EnumDetectionType.CABLE_CONNECTED]  = "케이블 복구",
        [EnumDetectionType.PIR_SENSOR]       = "PIR 감지",
        [EnumDetectionType.THERMAL_SENSOR]   = "열선 감지",
        [EnumDetectionType.VIBRATION_SENSOR] = "진동 감지",
        [EnumDetectionType.CONTACT_SENSOR]   = "접점 감지",
        [EnumDetectionType.DISTANCE_SENSOR]  = "거리 감지",
        [EnumDetectionType.AI_DETECT]        = "AI 탐지",
    };

    // ── 장애 원인 (Reason) ────────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<EnumFaultType, string> _faultType = new Dictionary<EnumFaultType, string>
    {
        [EnumFaultType.FAULT_CONTROLLER]    = "제어기 장애",
        [EnumFaultType.FAULT_FENCE]         = "펜스 장애",
        [EnumFaultType.FAULT_MULTI]         = "복합센서 장애",
        [EnumFaultType.FAULT_CABLE_CUTTING] = "케이블 절단",
        [EnumFaultType.FAULT_ETC]           = "기타 장애",
    };

    // ── 장비 종류 (DeviceTypeText — "Fence" 등) ───────────────────────────
    private static readonly IReadOnlyDictionary<EnumDeviceType, string> _deviceType = new Dictionary<EnumDeviceType, string>
    {
        [EnumDeviceType.NONE]               = "없음",
        [EnumDeviceType.Controller]         = "제어기",
        [EnumDeviceType.Multi]              = "복합센서",
        [EnumDeviceType.Fence]              = "펜스센서",
        [EnumDeviceType.Underground]        = "지중센서",
        [EnumDeviceType.Contact]            = "접점센서",
        [EnumDeviceType.PIR]                = "PIR센서",
        [EnumDeviceType.IoController]       = "IO제어기",
        [EnumDeviceType.Laser]              = "레이저센서",
        [EnumDeviceType.Cable]              = "케이블",
        [EnumDeviceType.IpCamera]           = "카메라",
        [EnumDeviceType.SmartSensor]        = "스마트센서",
        [EnumDeviceType.SmartSensor2]       = "스마트센서2",
        [EnumDeviceType.SmartCompound]      = "스마트복합센서",
        [EnumDeviceType.IpSpeaker]          = "스피커",
        [EnumDeviceType.Radar]              = "레이더",
        [EnumDeviceType.OpticalCable]       = "광케이블",
        [EnumDeviceType.Fence_Group]        = "펜스그룹",
        [EnumDeviceType.Lamp]               = "경고등",
        [EnumDeviceType.Enclosure]          = "함체",
        [EnumDeviceType.SmartMultisensor2]  = "스마트멀티센서2",
        [EnumDeviceType.Gate]               = "통문",
    };

    // ── 조치 여부 (Status) — 이벤트 문맥: True=조치완료 / False=미조치
    //    (코드 IsActioned = Status == EnumTrueFalse.True). 일반 참/거짓이 아닌 조치 상태 표기.
    private static readonly IReadOnlyDictionary<EnumTrueFalse, string> _trueFalse = new Dictionary<EnumTrueFalse, string>
    {
        [EnumTrueFalse.False] = "미조치",
        [EnumTrueFalse.True]  = "조치완료",
    };

    /// <summary>
    /// enum 값 → 한글 표시 라벨. 매핑에 없거나 다른 타입이면 원문(ToString())으로 폴백,
    /// null이면 빈 문자열. 표시 전용이며 원본 값은 어떤 경우에도 변경하지 않는다.
    /// </summary>
    public static string To(object? value)
    {
        return value switch
        {
            null                     => string.Empty,
            EnumEventType v          => _eventType.TryGetValue(v, out var s) ? s : v.ToString(),
            EnumDetectionType v      => _detectionType.TryGetValue(v, out var s) ? s : v.ToString(),
            EnumFaultType v          => _faultType.TryGetValue(v, out var s) ? s : v.ToString(),
            EnumDeviceType v         => _deviceType.TryGetValue(v, out var s) ? s : v.ToString(),
            EnumTrueFalse v          => _trueFalse.TryGetValue(v, out var s) ? s : v.ToString(),
            _                        => value.ToString() ?? string.Empty,
        };
    }
}
