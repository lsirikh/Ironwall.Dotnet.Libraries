using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 장비 한 대의 부품 구성 · 관측을 <b>지도 아이콘이 그릴 만큼만</b> 줄인 요약 — 순수 함수 <see cref="Build"/> 의 결과.
/// </summary>
/// <remarks>
/// <para><b>줄 · 이름 · 한글은 공용 사전</b>(<see cref="ComponentSnapshot"/> · <see cref="ComponentDisplay"/>, component-display-unify FR-01)이
/// 만든다 — 장비 콘솔과 같은 말을 쓴다. 이 클래스는 그 위에 지도 전용 판단(배지 · 칸 줄 · 문 위치 · 툴팁)만 얹는다.</para>
/// <para><b>입력</b>: 장비 모델의 축 묶음(<see cref="IDeviceAxesModel"/>). <c>SYNC_DEVICE</c> 재조회 때마다 묶음 참조가 통째로 갈리므로
/// 요약도 "새 참조를 받았을 때 한 번" 다시 만들면 된다(프레임 · 타이머 구동 아님 — NFR-02).</para>
/// <para><b>규칙</b>(docs/analyses/device-components-map-icon-analysis.md §4): ① 최악 건강 FAULT &gt; DEGRADED &gt; 미상 &gt; OK.
/// ② <c>in_service=false</c> 는 집계에서 뺀다. ③ 모르는 것은 그리지 않는다 — 축 미수신은 <see cref="IsReceived"/>=false, 배지 · 칸 줄 없음.
/// ④ 심볼 DB 에 영속하지 않는다(런타임 전용 — 진실은 서버).</para>
/// </remarks>
public sealed class ComponentHealthSummary
{
    /// <summary>툴팁에 싣는 최대 부품 행 수 — 넘치면 "외 n개".</summary>
    public const int MaxToolTipRows = 6;

    /// <summary>축이 아예 없다(6.3 서버 · 장비 미연결) — 아무것도 그리지도 적지도 않는다(무회귀).</summary>
    public static readonly ComponentHealthSummary None = new(ComponentSnapshot.None, DoorMotionState.Unknown);

    private ComponentHealthSummary(ComponentSnapshot snapshot, DoorMotionState doorMotion)
    {
        Snapshot = snapshot;
        DoorMotion = doorMotion;
        Rows = ComponentStripRules.CardOrder(snapshot);
        Strip = ComponentStripRules.Build(snapshot);
    }

    /// <summary>아이콘 아래 부품 칸 줄(L2) — 조립 카드 순서의 앞 4칸. 6.3 · 미수신이면 빈 줄.</summary>
    public ComponentStrip Strip { get; }

    /// <summary>공용 부품 표(선언 순서 줄 · 수 · 요약 줄) — 조립 카드 · 상세 보기 부품 탭이 그대로 쓴다.</summary>
    public ComponentSnapshot Snapshot { get; }

    /// <summary>장비 모델에 축 묶음이 있었는가(7.0+ 응답). false 면 이 요약은 <see cref="None"/> 이다.</summary>
    public bool HasAxes => Snapshot.HasAxes;

    /// <summary>관측 축(<c>device_status</c>)을 받았는가. false 면 건강을 모른다 — 툴팁에 "미수신"만 적는다.</summary>
    public bool IsReceived => Snapshot.IsReceived;

    /// <summary>사용 중인 부품 가운데 최악 건강.</summary>
    public ComponentHealthLevel Health => Snapshot.WorstHealth;

    public int FaultCount => Snapshot.FaultCount;
    public int DegradedCount => Snapshot.DegradedCount;
    public int OutOfServiceCount => Snapshot.OutOfServiceCount;

    /// <summary>형상 축에 선언된 부품 수.</summary>
    public int DeclaredCount => Snapshot.DeclaredCount;

    /// <summary>행 — 조립 카드 순서: 고장 → 저하 → 나머지(선언 순서) → 사용 안 함.</summary>
    public IReadOnlyList<ComponentRowInfo> Rows { get; }

    /// <summary>문 부품 관측(<c>DOOR_ACTUATOR</c> 우선, 없으면 <c>DOOR_SENSOR</c>).</summary>
    public DoorMotionState DoorMotion { get; }

    /// <summary>아이콘에 건강 배지를 그리는가 — 고장 · 저하일 때만.</summary>
    public bool ShowsBadge => Health is ComponentHealthLevel.Fault or ComponentHealthLevel.Degraded;

    /// <summary>배지에 적을 숫자 — 고장이면 고장 수, 저하면 저하 수. 배지가 없으면 0.</summary>
    public int BadgeCount => Health switch
    {
        ComponentHealthLevel.Fault => FaultCount,
        ComponentHealthLevel.Degraded => DegradedCount,
        _ => 0,
    };

    /// <summary>
    /// 축 묶음 → 요약. <paramref name="axes"/> 가 null 이면 <see cref="None"/>.
    /// </summary>
    /// <param name="axes">장비 모델의 축 묶음.</param>
    /// <param name="catalog">서버 카탈로그 한글 이름(FR-07). null 이면 내장 사전.</param>
    public static ComponentHealthSummary Build(IDeviceAxesModel? axes, IComponentTypeLabels? catalog = null)
    {
        if (axes is null) return None;
        return new ComponentHealthSummary(ComponentSnapshot.Build(axes, catalog), ResolveDoorMotion(axes));
    }

    /// <summary>
    /// 툴팁의 부품 절 — 축이 없으면(6.3) null(아무것도 적지 않는다).
    /// </summary>
    public string? ToToolTipSection(int maxRows = MaxToolTipRows)
    {
        if (!HasAxes) return null;
        if (!IsReceived) return "부품 상태: 미수신";
        if (Rows.Count == 0) return "부품: 선언 없음";

        var lines = new List<string>(maxRows + 2) { HeaderLine() };
        foreach (var row in Rows.Take(maxRows)) lines.Add("· " + row.ToLine());
        if (Rows.Count > maxRows) lines.Add($"· 외 {Rows.Count - maxRows}개");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>한 줄 요약 — 자동화 이름 · 툴팁 머리줄. 예: <c>부품 고장 2 · 저하 1 / 5</c>.</summary>
    public string? ShortText()
    {
        if (!HasAxes) return null;
        if (!IsReceived) return "부품 미수신";
        if (Rows.Count == 0) return "부품 없음";
        var used = Snapshot.InServiceCount;
        return Health switch
        {
            ComponentHealthLevel.Fault or ComponentHealthLevel.Degraded =>
                $"부품 고장 {FaultCount} · 저하 {DegradedCount} / {used}",
            ComponentHealthLevel.Ok => $"부품 정상 {used}",
            ComponentHealthLevel.Unknown => $"부품 미상 / {used}",
            _ => "부품 사용 안 함",
        };
    }

    private string HeaderLine() => "부품 상태 — " + (ShortText() ?? string.Empty).Replace("부품 ", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// 문 위치 — 구동부(<c>DOOR_ACTUATOR</c>: OPEN/CLOSED/RUNNING)를 먼저, 없으면 센서(<c>DOOR_SENSOR</c>: OPEN/CLOSED).
    /// 선언이 없으면 관례 key(<c>actuator</c> · <c>door</c>)로 폴백한다(<c>BaseDeviceDto.ResolveComponentStatus</c> 와 같은 규칙).
    /// </summary>
    private static DoorMotionState ResolveDoorMotion(IDeviceAxesModel axes)
    {
        var actuator = FindStatus(axes, "DOOR_ACTUATOR", "actuator");
        var fromActuator = ParseDoor(actuator?.State);
        if (fromActuator != DoorMotionState.Unknown) return fromActuator;
        return ParseDoor(FindStatus(axes, "DOOR_SENSOR", "door")?.State);
    }

    private static ComponentStatusModel? FindStatus(IDeviceAxesModel axes, string type, string conventionalKey)
    {
        var byType = axes.FindStatusByType(type);
        if (byType != null) return byType;
        var components = axes.DeviceStatus?.Components;
        return components != null && components.TryGetValue(conventionalKey, out var byKey) ? byKey : null;
    }

    private static DoorMotionState ParseDoor(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return DoorMotionState.Unknown;
        return state.Trim().ToUpperInvariant() switch
        {
            "OPEN" => DoorMotionState.Open,
            "CLOSED" => DoorMotionState.Closed,
            "RUNNING" => DoorMotionState.Running,
            _ => DoorMotionState.Unknown,
        };
    }
}
