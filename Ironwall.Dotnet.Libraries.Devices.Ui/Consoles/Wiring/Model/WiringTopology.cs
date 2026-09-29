using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>결선의 모양 — <b>제어기 종류가 정한다</b>(PRD FR-16).</summary>
public enum WiringShape
{
    /// <summary>링 — 한 줄 체인(1…N) + 가운데 함체(Sensor A · Sensor B) + 리턴케이블 2가닥(PRD §1-A).</summary>
    Ring = 0,

    /// <summary>
    /// 옛 양쪽 가지 — 제어기 기준 왼쪽 · 오른쪽 체인(각 가지는 제어기 쪽에서 바깥으로 1, 2, 3…). <b>폐기</b>(v0.4 · 모든 제어기는 링) —
    /// <c>"shape": "branch"</c> 로 저장된 값을 읽어 링으로 이어 붙이기 위해서만 남는다.
    /// </summary>
    TwoBranch = 1,

    /// <summary>옛 한 줄 — 제어기 쪽 끝이 1. 판정에는 쓰지 않는다(지진동만 붙은 제어기도 O-9 전까지 링) · 옛 저장값 읽기용.</summary>
    Line = 2,
}

/// <summary>
/// 제어기 종류 — 서버 <c>type_controller</c> 원값(<c>Controller</c>|<c>SmartController</c>|<c>IoController</c>)을 옮긴 것.
/// </summary>
/// <remarks>
/// 클라 <see cref="EnumDeviceType"/> 에는 <c>SmartController</c> 가 없다(종류축이 클라 enum 보다 앞서 간다 —
/// <c>DeviceAxesMapper</c> · <c>DeviceTypeText</c>). 그래서 결선 모양 판정은 <b>문자열 원값</b>에서 이 enum 으로 옮겨 한다.
/// </remarks>
public enum WiringControllerKind
{
    /// <summary>원값이 없거나 모르는 값 — 붙은 센서 종류로 추정한다.</summary>
    Unknown = 0,
    /// <summary><c>SmartController</c> — 스마트 제어기(링).</summary>
    Smart = 1,
    /// <summary><c>Controller</c> — PIDS 제어기(복합 · 펜스 · 지진동).</summary>
    Pids = 2,
    /// <summary><c>IoController</c> — 접점 IO. 한 줄로 본다.</summary>
    Io = 3,
}

/// <summary>
/// 결선 모양 판정 결과 — <b>순수 값</b>. 링 · 양쪽 가지 · 한 줄의 차이(번호 · 리턴케이블 · 함체 · 한도 · 경고)를 여기 한 곳에 모은다.
/// </summary>
/// <param name="Shape">결선 모양.</param>
/// <param name="ControllerKind">판정에 쓴 제어기 종류.</param>
/// <param name="MaxSensors">제어기 한 대의 제품 한도 — 모르면 <c>null</c>(O-7: 복합 · 펜스 · 지진동 한도 미확정).</param>
/// <param name="IsProvisional">결정 대기 중인 규칙으로 정한 모양인가(O-6 · 양쪽 가지).</param>
/// <param name="IsInferred">제어기 종류를 몰라 <b>센서 종류로 추정</b>했는가.</param>
/// <param name="MixWarning">스마트 센서와 PIDS 센서가 섞였거나 제어기와 맞지 않을 때의 경고(O-8). 없으면 <c>null</c>.</param>
public sealed record WiringTopology(
    WiringShape Shape,
    WiringControllerKind ControllerKind,
    int? MaxSensors,
    bool IsProvisional,
    bool IsInferred,
    string? MixWarning)
{
    /// <summary>스마트 제어기 한 대의 센서 한도 — 200m 구간 · 약 6m 간격(브로슈어 5쪽).</summary>
    public const int SMART_MAX_SENSORS = 34;

    /// <summary>링의 리턴케이블 가닥 수 — 함체 Sensor A · Sensor B 에서 체인 양 끝으로.</summary>
    public const int RING_RETURN_CABLES = 2;

    /// <summary>VBUS 보상 유닛 표지를 함체에서 몇 칸 떨어뜨리는가 — 약 30m ≈ 6m × 5(브로슈어 · PRD FR-05).</summary>
    public const int VBUS_OFFSET_SLOTS = 5;

    public const string CONTROLLER_SMART = "SmartController";
    public const string CONTROLLER_PIDS = "Controller";
    public const string CONTROLLER_IO = "IoController";

    /// <summary>링인가 — A/B 두 번호 · 함체 · 리턴케이블이 있다.</summary>
    public bool IsRing => Shape == WiringShape.Ring;

    /// <summary>리턴케이블 가닥 수(링만 2).</summary>
    public int ReturnCableCount => IsRing ? RING_RETURN_CABLES : 0;

    /// <summary>센서 칩에 반대쪽 포트(Sensor B)에서 센 번호도 보이는가.</summary>
    public bool HasOppositeNumber => IsRing;

    /// <summary>VBUS 보상 유닛 표지를 그리는가(링만 — 스마트 제품군).</summary>
    public bool HasVbus => IsRing;

    /// <summary>
    /// VBUS 보상 유닛 표지를 놓을 <b>틈 번호</b>(0…<paramref name="count"/>) — 함체 틈에서 ±<see cref="VBUS_OFFSET_SLOTS"/>, 체인 끝으로 눌러 붙인다.
    /// 두 표지가 같은 틈에 떨어지면 하나만 돌려준다. 센서가 없으면 빈 목록.
    /// </summary>
    public static IReadOnlyList<int> VbusGaps(int enclosureGap, int count)
    {
        if (count <= 0) return Array.Empty<int>();
        var gap = Math.Clamp(enclosureGap, 0, count);
        var left = Math.Max(0, gap - VBUS_OFFSET_SLOTS);
        var right = Math.Min(count, gap + VBUS_OFFSET_SLOTS);
        return left == right ? new[] { left } : new[] { left, right };
    }

    /// <summary>서버 <c>type_controller</c> 원값 → 제어기 종류(대소문자 무시). 모르면 <see cref="WiringControllerKind.Unknown"/>.</summary>
    public static WiringControllerKind ParseControllerKind(string? typeController)
    {
        var text = typeController?.Trim();
        if (string.IsNullOrEmpty(text)) return WiringControllerKind.Unknown;
        if (text.Equals(CONTROLLER_SMART, StringComparison.OrdinalIgnoreCase)) return WiringControllerKind.Smart;
        if (text.Equals(CONTROLLER_PIDS, StringComparison.OrdinalIgnoreCase)) return WiringControllerKind.Pids;
        if (text.Equals(CONTROLLER_IO, StringComparison.OrdinalIgnoreCase)) return WiringControllerKind.Io;
        return WiringControllerKind.Unknown;
    }

    /// <summary>센서 종류 원값(<c>type_sensor</c> · 옛 <c>type_device</c>) → <see cref="EnumDeviceType"/>. 모르면 <see cref="EnumDeviceType.NONE"/>.</summary>
    public static EnumDeviceType ParseSensorType(string? typeText)
    {
        var text = typeText?.Trim();
        if (string.IsNullOrEmpty(text) || int.TryParse(text, out _)) return EnumDeviceType.NONE;   // 숫자 문자열은 enum 값으로 새지 않게
        return Enum.TryParse<EnumDeviceType>(text, ignoreCase: true, out var type) ? type : EnumDeviceType.NONE;
    }

    /// <summary>스마트 제품군 센서인가(링에 붙는 것).</summary>
    public static bool IsSmartSensor(EnumDeviceType type) => type is
        EnumDeviceType.SmartSensor or EnumDeviceType.SmartSensor2 or
        EnumDeviceType.SmartCompound or EnumDeviceType.SmartMultisensor2;

    /// <summary>
    /// 보는 쪽(앞 · 뒤)이 있는가(FR-20) — <b>기둥에 다는</b> 스마트 복합센서 · 복합센서만. 펜스센서(철망 가운데) · 지진동(땅속) · 모르는 종류는 없다.
    /// </summary>
    public static bool SupportsFacing(EnumDeviceType type) => IsSmartSensor(type) || type == EnumDeviceType.Multi;

    /// <summary>기둥에 다는가(FR-20) — 펜스센서는 기둥 사이 철망 가운데, 지진동은 땅속. 그 밖(스마트 · 복합 · 모름)은 기둥.</summary>
    public static bool IsPostMounted(EnumDeviceType type) => type is not (EnumDeviceType.Fence or EnumDeviceType.Underground);

    /// <summary>PIDS 제품군 센서인가(복합 · 펜스 · 지진동).</summary>
    public static bool IsPidsSensor(EnumDeviceType type) => type is
        EnumDeviceType.Multi or EnumDeviceType.Fence or EnumDeviceType.Underground;

    /// <summary>원값 문자열 편의 겹 — <see cref="For(string?, IReadOnlyList{EnumDeviceType})"/>.</summary>
    public static WiringTopology For(string? typeController, IEnumerable<string?> sensorTypeTexts)
        => For(typeController, (sensorTypeTexts ?? Enumerable.Empty<string?>()).Select(ParseSensorType).ToList());

    /// <summary>
    /// 결선 모양 판정(PRD v0.4 §1-C · 사용자 2026-09-30) — <b>모든 제어기가 링</b>이다. "PIDS" 는 울타리 감지 센서의 통칭일 뿐
    /// 양쪽 가지 결선은 없다(폐기). 지진동센서만 붙은 제어기도 확인(O-9) 전까지 링. 한도는 <see cref="WiringLimitTable"/> 이 맡고,
    /// 스마트 · 펜스 계열을 섞어 쓰는 것은 정상이라 섞임 경고(옛 O-8)는 없다.
    /// </summary>
    /// <remarks>
    /// <see cref="WiringShape.TwoBranch"/> · <see cref="WiringShape.Line"/> 은 옛 저장값(<c>"shape": "branch" | "line"</c>)을 읽기 위해서만 남는다 —
    /// 가지로 저장된 값은 불러올 때 링으로 이어 붙이는 <b>제안</b>이 된다(<see cref="WiringBoard.Load"/>).
    /// </remarks>
    public static WiringTopology For(string? typeController, IReadOnlyList<EnumDeviceType>? sensorTypes)
        => new(WiringShape.Ring, ParseControllerKind(typeController), null, IsProvisional: false, IsInferred: false, MixWarning: null);
}
