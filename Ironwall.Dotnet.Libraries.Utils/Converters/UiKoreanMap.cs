using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Utils.Converters;
/****************************************************************************
   Purpose      : UI 표시 전용 — enum 값을 한글 라벨로 변환하는 **공용** 맵.
                  Events.Ui 전용 EnumKoreanMap(Events 도메인 enum 중심)과 달리,
                  Devices.Ui 등 하위 UI 프로젝트에서도 쓸 수 있도록 Utils에 둔다
                  (Utils→Enums 참조만 있으므로 순환 없음).
   ⚠ 표시 계층 전용   : enum 정의/DTO/NATS/DB/직렬화 값은 절대 변경하지 않는다.
                  매핑에 없는 값은 원문(ToString())으로 폴백 — 신규 값 추가 시 여기만 갱신.
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public static class UiKoreanMap
{
    // ── 장비 종류 (EnumDeviceType) ────────────────────────────────────────
    private static readonly IReadOnlyDictionary<EnumDeviceType, string> _deviceType = new Dictionary<EnumDeviceType, string>
    {
        [EnumDeviceType.NONE]              = "없음",
        [EnumDeviceType.Controller]        = "제어기",
        [EnumDeviceType.Multi]             = "복합센서",
        [EnumDeviceType.Fence]             = "펜스센서",
        [EnumDeviceType.Underground]       = "지중센서",
        [EnumDeviceType.Contact]           = "접점센서",
        [EnumDeviceType.PIR]               = "PIR센서",
        [EnumDeviceType.IoController]      = "IO제어기",
        [EnumDeviceType.Laser]             = "레이저센서",
        [EnumDeviceType.Cable]             = "케이블",
        [EnumDeviceType.IpCamera]          = "카메라",
        [EnumDeviceType.SmartSensor]       = "스마트센서",
        [EnumDeviceType.SmartSensor2]      = "스마트센서2",
        [EnumDeviceType.SmartCompound]     = "스마트복합센서",
        [EnumDeviceType.IpSpeaker]         = "스피커",
        [EnumDeviceType.Radar]             = "레이더",
        [EnumDeviceType.OpticalCable]      = "광케이블",
        [EnumDeviceType.Fence_Group]       = "펜스그룹",
        [EnumDeviceType.Lamp]              = "경고등",
        [EnumDeviceType.Enclosure]         = "함체",
        [EnumDeviceType.SmartMultisensor2] = "스마트멀티센서2",
    };

    // ── 장비 상태 (EnumDeviceStatus) — 장비 그리드 Status 컬럼 ────────────
    private static readonly IReadOnlyDictionary<EnumDeviceStatus, string> _deviceStatus = new Dictionary<EnumDeviceStatus, string>
    {
        [EnumDeviceStatus.ACTIVATED]   = "정상",
        [EnumDeviceStatus.ERROR]       = "장애",
        [EnumDeviceStatus.DEACTIVATED] = "비활성",
    };

    // ── 스피커 종류 (EnumSpeakerType) ─────────────────────────────────────
    private static readonly IReadOnlyDictionary<EnumSpeakerType, string> _speakerType = new Dictionary<EnumSpeakerType, string>
    {
        [EnumSpeakerType.NORMAL]  = "일반",
        [EnumSpeakerType.ADMIN]   = "관리자",
        [EnumSpeakerType.MONITOR] = "모니터",
        [EnumSpeakerType.DEV]     = "개발",
    };

    // ── 함체 도어 상태 (EnumDoorStatus) ───────────────────────────────────
    private static readonly IReadOnlyDictionary<EnumDoorStatus, string> _doorStatus = new Dictionary<EnumDoorStatus, string>
    {
        [EnumDoorStatus.CLOSED] = "닫힘",
        [EnumDoorStatus.OPEN]   = "열림",
    };

    /// <summary>
    /// enum 값 → 한글 표시 라벨. 매핑에 없거나 다른 타입이면 원문(ToString())으로 폴백,
    /// null이면 빈 문자열. 표시 전용이며 원본 값은 어떤 경우에도 변경하지 않는다.
    /// </summary>
    public static string To(object? value)
    {
        return value switch
        {
            null                 => string.Empty,
            EnumDeviceType v     => _deviceType.TryGetValue(v, out var s) ? s : v.ToString(),
            EnumDeviceStatus v   => _deviceStatus.TryGetValue(v, out var s) ? s : v.ToString(),
            EnumSpeakerType v    => _speakerType.TryGetValue(v, out var s) ? s : v.ToString(),
            EnumDoorStatus v     => _doorStatus.TryGetValue(v, out var s) ? s : v.ToString(),
            _                    => value.ToString() ?? string.Empty,
        };
    }
}
