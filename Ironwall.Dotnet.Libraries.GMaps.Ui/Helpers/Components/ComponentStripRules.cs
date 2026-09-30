using System.Windows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 지도 부품 칸 줄(L2) · 조립 카드(L3)의 순수 규칙 — 대표 칸 고르기 · 칸 모양 · LOD(48px) · 밀도(30개) · 라벨 피하기.
/// </summary>
/// <remarks>
/// <para><b>대표 칸</b>(스토리보드 v0.1 카테고리별 대표 표): 고장 · 저하가 먼저, 그다음은 장비 종류별 대표 순서
/// (<see cref="RepresentativeTypes"/>), 표에 없는 부품은 그 뒤에 선언 순서, 사용 안 함은 맨 뒤. 앞 4칸 + "+n".</para>
/// <para><b>카드 순서</b>(조립 카드 표) = 고장 → 저하 → 나머지(선언 순서) → 사용 안 함(<see cref="ComponentSortMode.FaultFirst"/>).</para>
/// <para><b>LOD</b>: 아이콘이 화면에 <see cref="MinMarkerPixels"/>(48px) 이상일 때만. 작게 보이면 아예 그리지 않는다.</para>
/// <para><b>밀도</b>(PRD FR-04): 칸 줄을 그릴 만큼 크게 보이는 아이콘이 한 화면에 <see cref="CrowdedThreshold"/>(30)개를 <b>넘으면</b>,
/// <b>고장 · 선택 · 호버</b>한 아이콘에만 줄을 붙인다(저하는 이 예외에 들지 않는다). 판정은 지도가 뷰포트가 바뀔 때 한 번 센다.</para>
/// </remarks>
public static class ComponentStripRules
{
    /// <summary>줄에 싣는 최대 칸 수.</summary>
    public const int MaxChips = 4;

    /// <summary>이 크기(px, 짧은 변 × 디지털 배율) 이상일 때만 칸 줄을 그린다.</summary>
    public const double MinMarkerPixels = 48.0;

    /// <summary>크게 보이는 아이콘이 이 수를 <b>넘으면</b> 밀집 — 고장 · 선택 · 호버만 줄을 그린다.</summary>
    public const int CrowdedThreshold = 30;

    // ── 카테고리별 대표 표(스토리보드 v0.1, 사용자 확정 2026-10-01). 한 칸에 후보가 여럿이면(열화상) 먼저 선언된 것이 그 자리를 갖는다 ──
    private static readonly string[] Vibration = { "VIBRATION_SENSOR" };
    private static readonly string[] Pir = { "PIR_SENSOR" };
    private static readonly string[] Thermal = { "THERMAL_CAMERA", "THERMAL_SENSOR" };

    private static readonly IReadOnlyDictionary<EnumDeviceType, string[][]> Table = new Dictionary<EnumDeviceType, string[][]>
    {
        [EnumDeviceType.Fence] = new[] { Vibration },
        [EnumDeviceType.Multi] = new[] { Vibration, Pir, Thermal },
        [EnumDeviceType.SmartSensor] = new[] { Vibration, new[] { "ULTRASONIC_SENSOR" }, Pir },
        [EnumDeviceType.SmartSensor2] = new[] { Vibration, Pir, new[] { "RADAR_UNIT" }, new[] { "EO_CAMERA" } },
        [EnumDeviceType.SmartMultisensor2] = new[] { Vibration, Pir, new[] { "RADAR_UNIT" }, new[] { "EO_CAMERA" } },
        [EnumDeviceType.Enclosure] = new[] { new[] { "DOOR_SENSOR" }, new[] { "DOOR_LOCK" }, new[] { "HEATER" }, new[] { "FAN" } },
        [EnumDeviceType.Gate] = new[] { new[] { "DOOR_ACTUATOR" }, new[] { "DOOR_SENSOR" }, new[] { "DOOR_LOCK" }, new[] { "LIMIT_SWITCH" } },
        [EnumDeviceType.IpCamera] = new[] { new[] { "PTZ_UNIT" }, new[] { "TRACKER" }, new[] { "IR_LED" }, new[] { "WIPER" } },
        [EnumDeviceType.Lamp] = new[] { new[] { "LAMP_LIGHT" }, new[] { "BUZZER" } },
        [EnumDeviceType.IpSpeaker] = new[] { new[] { "AMPLIFIER" }, new[] { "MIC" } },
        [EnumDeviceType.Controller] = new[] { new[] { "NETWORK_INTERFACE" }, new[] { "CONTACT_INPUT" } },
        [EnumDeviceType.IoController] = new[] { new[] { "NETWORK_INTERFACE" }, new[] { "CONTACT_INPUT" } },
    };

    /// <summary>장비 종류의 대표 칸 순서(유형 코드 묶음). 표에 없는 종류면 빈 목록 — 선언 순서를 따른다.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> RepresentativeTypes(EnumDeviceType deviceType)
        => Table.TryGetValue(deviceType, out var slots) ? slots : Array.Empty<string[]>();

    /// <summary>조립 카드 순서 — 고장 먼저(같은 단계 안에서는 선언 순서).</summary>
    public static IReadOnlyList<ComponentRowInfo> CardOrder(ComponentSnapshot snapshot)
        => snapshot.Sorted(ComponentSortMode.FaultFirst);

    /// <summary>
    /// 칸 줄 순서 — ① 고장 ② 저하(각각 대표 순서 → 선언 순서) ③ 대표 표의 자리 순서(한 자리에는 먼저 선언된 부품 하나)
    /// ④ 표에 없는(또는 자리를 못 얻은) 부품은 선언 순서 ⑤ 사용 안 함은 맨 뒤.
    /// </summary>
    public static IReadOnlyList<ComponentRowInfo> RepresentativeOrder(ComponentSnapshot snapshot, EnumDeviceType deviceType)
    {
        var slots = RepresentativeTypes(deviceType);
        var slotOf = new Dictionary<ComponentRowInfo, int>();
        var claimed = new HashSet<int>();
        foreach (var row in snapshot.Rows.OrderBy(r => r.DeclaredIndex))
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (claimed.Contains(i) || !slots[i].Contains(row.Type, StringComparer.OrdinalIgnoreCase)) continue;
                slotOf[row] = i;
                claimed.Add(i);
                break;
            }
        }

        int Stage(ComponentRowInfo r) => !r.InService ? 9 : r.Health switch
        {
            ComponentHealthLevel.Fault => 0,
            ComponentHealthLevel.Degraded => 1,
            _ => slotOf.ContainsKey(r) ? 2 : 3,
        };

        return snapshot.Rows
            .OrderBy(Stage)
            .ThenBy(r => slotOf.TryGetValue(r, out var slot) ? slot : int.MaxValue)
            .ThenBy(r => r.DeclaredIndex)
            .ToList();
    }

    /// <summary>
    /// 부품 표 → 칸 줄. 관측 축을 못 받았으면(6.3 · 미수신) 빈 줄 — 모르는 것은 그리지 않는다(FR-08).
    /// </summary>
    public static ComponentStrip Build(ComponentSnapshot snapshot, EnumDeviceType deviceType = EnumDeviceType.NONE)
    {
        if (snapshot is null || !snapshot.IsAvailable || snapshot.Rows.Count == 0) return ComponentStrip.Empty;

        var ordered = RepresentativeOrder(snapshot, deviceType);
        var chips = ordered.Take(MaxChips).Select(r => new ComponentChip(r.Key, r.Name, KindOf(r))).ToList();
        return new ComponentStrip(chips, Math.Max(0, ordered.Count - MaxChips), hasFault: snapshot.FaultCount > 0);
    }

    /// <summary>칸 모양 — 사용 안 함 &gt; 고장 &gt; 저하 &gt; 가동 &gt; 미상 &gt; 정상.</summary>
    public static ComponentChipKind KindOf(ComponentRowInfo row)
    {
        if (!row.InService) return ComponentChipKind.OutOfService;
        return row.Health switch
        {
            ComponentHealthLevel.Fault => ComponentChipKind.Fault,
            ComponentHealthLevel.Degraded => ComponentChipKind.Degraded,
            _ when row.IsActive => ComponentChipKind.Active,
            ComponentHealthLevel.Ok => ComponentChipKind.Normal,
            _ => ComponentChipKind.Unknown,
        };
    }

    /// <summary>아이콘이 칸 줄을 그릴 만큼 크게 보이는가(48px 이상).</summary>
    public static bool IsLarge(double screenPixels) => screenPixels >= MinMarkerPixels;

    /// <summary>밀도 판정 — 크게 보이는(칸 줄 대상) 아이콘 수가 30개를 넘는가.</summary>
    public static bool IsCrowded(int qualifyingCount) => qualifyingCount > CrowdedThreshold;

    /// <summary>밀도 집계 대상인가 — 그릴 줄이 있고 크게 보인다.</summary>
    public static bool Qualifies(ComponentStrip? strip, double screenPixels)
        => strip is { IsEmpty: false } && IsLarge(screenPixels);

    /// <summary>
    /// 이 아이콘에 칸 줄을 그리는가 — 크게 보이고, (밀집이 아니거나) 고장 · 선택 · 호버일 때(PRD FR-04).
    /// </summary>
    /// <param name="strip">칸 줄.</param>
    /// <param name="screenPixels">아이콘의 화면 크기.</param>
    /// <param name="crowded">지도가 센 밀집 여부.</param>
    /// <param name="emphasized">선택 · 호버 · 카드 대상.</param>
    public static bool ShowsStrip(ComponentStrip? strip, double screenPixels, bool crowded, bool emphasized)
        => Qualifies(strip, screenPixels) && (!crowded || strip!.HasFault || emphasized);

    /// <summary>
    /// 칸 줄 윗변 — 기본 자리(<paramref name="defaultStrip"/>)가 제목 라벨 상자와 겹치면 라벨 아래로 내린다.
    /// 라벨이 없거나(제목 숨김 · 빈 제목) 옮겨져서 겹치지 않으면 기본 자리 그대로.
    /// </summary>
    /// <param name="defaultStrip">아이콘 바로 아래의 기본 칸 줄 사각형(요소 좌표).</param>
    /// <param name="labelBox">제목 라벨 상자(같은 좌표). 없으면 <see cref="Rect.Empty"/>.</param>
    /// <param name="gap">라벨과 줄 사이 틈.</param>
    public static double StripTop(Rect defaultStrip, Rect labelBox, double gap)
        => labelBox.IsEmpty || !labelBox.IntersectsWith(defaultStrip) ? defaultStrip.Top : labelBox.Bottom + gap;
}
