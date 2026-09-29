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
    /// 양쪽 가지 — 제어기 기준 왼쪽 체인 · 오른쪽 체인, 각 가지는 <b>제어기 쪽에서 바깥으로</b> 1, 2, 3…
    /// <b>잠정(O-6)</b>: PIDS 제어기가 링처럼 양 끝이 돌아오는지 아직 모른다.
    /// </summary>
    TwoBranch = 1,

    /// <summary>한 줄 — 제어기 쪽 끝이 1(지중 · 지진동 센서만 붙은 제어기).</summary>
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
    /// 제품 한도를 넘었을 때의 경고(PRD FR-14 ④). 한도를 모르거나 안 넘었으면 <c>null</c>.
    /// </summary>
    public string? LimitWarning(int sensorCount)
        => MaxSensors is { } max && sensorCount > max
            ? $"센서가 {sensorCount}대입니다 — 이 제어기의 제품 한도({max}대)를 넘었습니다."
            : null;

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

    /// <summary>PIDS 제품군 센서인가(복합 · 펜스 · 지진동).</summary>
    public static bool IsPidsSensor(EnumDeviceType type) => type is
        EnumDeviceType.Multi or EnumDeviceType.Fence or EnumDeviceType.Underground;

    /// <summary>원값 문자열 편의 겹 — <see cref="For(string?, IReadOnlyList{EnumDeviceType})"/>.</summary>
    public static WiringTopology For(string? typeController, IEnumerable<string?> sensorTypeTexts)
        => For(typeController, (sensorTypeTexts ?? Enumerable.Empty<string?>()).Select(ParseSensorType).ToList());

    /// <summary>
    /// 결선 모양 판정(PRD FR-16). 규칙:
    /// <list type="bullet">
    /// <item><c>SmartController</c> → 링(한도 34).</item>
    /// <item><c>Controller</c>(PIDS) → 붙은 센서가 <b>모두 지진동</b>이면 한 줄, 아니면 양쪽 가지(잠정 O-6).</item>
    /// <item><c>IoController</c> → 한 줄.</item>
    /// <item>모름 → 센서로 추정: 스마트 센서가 하나라도 있으면 링 · 전부 지진동이면 한 줄 · 복합/펜스가 있으면 양쪽 가지 · 아무 단서가 없으면 링(주력 제품).</item>
    /// </list>
    /// </summary>
    public static WiringTopology For(string? typeController, IReadOnlyList<EnumDeviceType>? sensorTypes)
    {
        var kind = ParseControllerKind(typeController);
        var types = sensorTypes ?? Array.Empty<EnumDeviceType>();

        var hasSmart = types.Any(IsSmartSensor);
        var hasPids = types.Any(IsPidsSensor);
        var allUnderground = types.Count > 0 && types.All(t => t == EnumDeviceType.Underground);
        var hasFenceLine = types.Any(t => t is EnumDeviceType.Multi or EnumDeviceType.Fence);

        var inferred = kind == WiringControllerKind.Unknown;
        var shape = kind switch
        {
            WiringControllerKind.Smart => WiringShape.Ring,
            WiringControllerKind.Pids => allUnderground ? WiringShape.Line : WiringShape.TwoBranch,
            WiringControllerKind.Io => WiringShape.Line,
            _ => hasSmart ? WiringShape.Ring
               : allUnderground ? WiringShape.Line
               : hasFenceLine ? WiringShape.TwoBranch
               : WiringShape.Ring,
        };

        int? max = shape == WiringShape.Ring ? SMART_MAX_SENSORS : null;     // 그 밖은 O-7 — 모른다
        return new WiringTopology(shape, kind, max, shape == WiringShape.TwoBranch, inferred, MixWarningOf(kind, hasSmart, hasPids));
    }

    /// <summary>
    /// 섞임 경고(O-8) — 한 제어기에 스마트 · PIDS 센서가 함께 있거나, 제어기 종류와 센서 제품군이 맞지 않을 때.
    /// </summary>
    private static string? MixWarningOf(WiringControllerKind kind, bool hasSmart, bool hasPids)
    {
        if (hasSmart && hasPids)
            return "스마트 센서와 PIDS 센서(복합 · 펜스 · 지진동)가 한 제어기에 섞여 있습니다 — 결선 모양을 확인해 주세요.";
        if (hasSmart && kind is WiringControllerKind.Pids or WiringControllerKind.Io)
            return "스마트 센서가 스마트 제어기가 아닌 제어기에 붙어 있습니다 — 링 결선이 아닐 수 있습니다.";
        if (hasPids && kind == WiringControllerKind.Smart)
            return "PIDS 센서(복합 · 펜스 · 지진동)가 스마트 제어기에 붙어 있습니다 — 링 결선이 아닐 수 있습니다.";
        return null;
    }
}
