using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>한 제어기에 붙은 센서의 제품군 — 한도 표의 행을 고른다.</summary>
public enum WiringFamily
{
    /// <summary>센서가 없거나 종류를 모른다.</summary>
    None = 0,
    /// <summary>스마트 센서만(스마트 링).</summary>
    Smart = 1,
    /// <summary>복합 · 펜스 · 지진동만(펜스 경계 제어기 — "PIDS" 는 울타리 감지 센서의 통칭).</summary>
    Perimeter = 2,
    /// <summary>둘이 섞였다 — 정상인 현장이 있다(윤형철조망 = 펜스센서 101번~ · 철조망 = 스마트 1번~). 기준 길이는 O-11.</summary>
    Mixed = 3,
}

/// <summary>
/// 제어기 한 대의 기준 길이 · 대수(PRD v0.4 §1-C 한도 표 · 브로셔) — <b>경고만</b> 하고 저장을 막지 않는다.
/// </summary>
/// <remarks>
/// 스마트 링 200m · 34대(브로셔 5쪽) / 펜스 경계 제어기 1대 = 펜스 500m(좌우 250m) · 복합 25 + 펜스센서 200
/// (브로셔 2쪽: 1km = 제어기 2 + 복합 50 + 펜스 400, 제어기 500m 간격). 섞으면 기준 길이가 줄어든다(사용자) — 산식 미확정(O-11)이라 숫자를 두지 않는다.
/// </remarks>
public sealed record WiringLimitTable(
    double SmartLengthMetres = 200,
    int SmartMaxSensors = WiringTopology.SMART_MAX_SENSORS,
    double PerimeterLengthMetres = 500,
    int PerimeterMaxMulti = 25,
    int PerimeterMaxFence = 200)
{
    public static WiringLimitTable Default { get; } = new();

    /// <summary>섞였을 때의 알림(정보 · 숫자 없음).</summary>
    /// <remarks>운영자 화면 문구 — 설계 질문 번호(O-11)는 싣지 않는다(주석에만).</remarks>
    public const string MIXED_INFO = "스마트 센서와 펜스 계열 센서(복합 · 펜스 · 지진동)를 한 제어기에 섞어 쓰고 있습니다(정상). 섞어 쓸 때의 기준 길이 · 대수는 아직 정해지지 않아 경고하지 않습니다.";

    /// <summary>머리 · 아래 띠에 쓰는 짧은 말(섞였을 때 기준 자리 — "기준 — 섞임(미정)").</summary>
    public const string MIXED_SHORT = "섞임(미정)";

    /// <summary>센서 종류들의 제품군. 모르는 종류(<see cref="EnumDeviceType.NONE"/> 등)는 판정에서 뺀다.</summary>
    public static WiringFamily FamilyOf(IEnumerable<EnumDeviceType> types)
    {
        var list = (types ?? Enumerable.Empty<EnumDeviceType>()).ToList();
        var smart = list.Any(WiringTopology.IsSmartSensor);
        var perimeter = list.Any(WiringTopology.IsPidsSensor);
        return smart && perimeter ? WiringFamily.Mixed : smart ? WiringFamily.Smart : perimeter ? WiringFamily.Perimeter : WiringFamily.None;
    }

    /// <summary>기준 길이(m) — 섞였거나 모르면 <c>null</c>.</summary>
    public double? ReferenceLength(WiringFamily family) => family switch
    {
        WiringFamily.Smart => SmartLengthMetres,
        WiringFamily.Perimeter => PerimeterLengthMetres,
        _ => null,
    };

    /// <summary>
    /// 체인(<paramref name="chainTypes"/> · 길이 <paramref name="lengthMetres"/>)이 기준을 넘었을 때의 경고 문장. 넘지 않았거나 섞였으면 빈 목록.
    /// </summary>
    public IReadOnlyList<string> Warnings(IReadOnlyList<EnumDeviceType> chainTypes, double lengthMetres)
    {
        var result = new List<string>();
        var types = chainTypes ?? new List<EnumDeviceType>();
        switch (FamilyOf(types))
        {
            case WiringFamily.Smart:
                if (types.Count > SmartMaxSensors)
                    result.Add($"센서가 {types.Count}대입니다 — 스마트 링 기준({SmartMaxSensors}대)을 넘었습니다.");
                if (lengthMetres > SmartLengthMetres)
                    result.Add($"체인 길이 약 {lengthMetres:0}m — 스마트 링 기준 길이({SmartLengthMetres:0}m)를 넘었습니다.");
                break;
            case WiringFamily.Perimeter:
                var multi = types.Count(t => t == EnumDeviceType.Multi);
                var fence = types.Count(t => t == EnumDeviceType.Fence);
                if (multi > PerimeterMaxMulti)
                    result.Add($"복합센서가 {multi}대입니다 — 펜스 경계 제어기 기준({PerimeterMaxMulti}대)을 넘었습니다.");
                if (fence > PerimeterMaxFence)
                    result.Add($"펜스센서가 {fence}대입니다 — 펜스 경계 제어기 기준({PerimeterMaxFence}대)을 넘었습니다.");
                if (lengthMetres > PerimeterLengthMetres)
                    result.Add($"체인 길이 약 {lengthMetres:0}m — 펜스 경계 제어기 기준 길이({PerimeterLengthMetres:0}m · 좌우 {PerimeterLengthMetres / 2:0}m)를 넘었습니다.");
                break;
        }
        return result;
    }
}
