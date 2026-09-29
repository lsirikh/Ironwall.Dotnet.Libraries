using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 장비 한 대의 부품 구성 · 관측을 <b>지도 아이콘이 그릴 만큼만</b> 줄인 요약 — 순수 함수 <see cref="Build"/> 의 결과.
/// </summary>
/// <remarks>
/// <para><b>입력</b>: 장비 모델의 축 묶음(<see cref="IDeviceAxesModel"/>) — 형상(<c>hardware_spec.components[]</c>)이 무엇이 달렸는지,
/// 관측(<c>device_status.components{}</c>)이 지금 어떤지를 준다. <c>SYNC_DEVICE</c> 재조회 때마다 묶음 참조가 통째로 갈리므로
/// 요약도 "새 참조를 받았을 때 한 번" 다시 만들면 된다(프레임 · 타이머 구동 아님).</para>
/// <para><b>규칙</b>(docs/analyses/device-components-map-icon-analysis.md §4):
/// ① 최악 건강 FAULT &gt; DEGRADED &gt; 확인 안 됨 &gt; OK. ② <c>in_service=false</c> 는 집계에서 뺀다.
/// ③ 모르는 것은 그리지 않는다 — 축 미수신은 <see cref="IsReceived"/>=false, 배지 없음.
/// ④ 심볼 DB 에 영속하지 않는다(런타임 전용 — 진실은 서버).</para>
/// </remarks>
public sealed class ComponentHealthSummary
{
    /// <summary>툴팁에 싣는 최대 부품 행 수 — 넘치면 "외 n개".</summary>
    public const int MaxToolTipRows = 6;

    /// <summary>축이 아예 없다(6.3 서버 · 장비 미연결) — 아무것도 그리지도 적지도 않는다(무회귀).</summary>
    public static readonly ComponentHealthSummary None = new(false, false, ComponentHealthLevel.None, 0, 0, 0, 0, Array.Empty<ComponentHealthRow>(), DoorMotionState.Unknown);

    private ComponentHealthSummary(bool hasAxes, bool isReceived, ComponentHealthLevel health,
        int faultCount, int degradedCount, int outOfServiceCount, int declaredCount,
        IReadOnlyList<ComponentHealthRow> rows, DoorMotionState doorMotion)
    {
        HasAxes = hasAxes;
        IsReceived = isReceived;
        Health = health;
        FaultCount = faultCount;
        DegradedCount = degradedCount;
        OutOfServiceCount = outOfServiceCount;
        DeclaredCount = declaredCount;
        Rows = rows;
        DoorMotion = doorMotion;
    }

    /// <summary>장비 모델에 축 묶음이 있었는가(7.0+ 응답). false 면 이 요약은 <see cref="None"/> 이다.</summary>
    public bool HasAxes { get; }

    /// <summary>관측 축(<c>device_status</c>)을 받았는가. false 면 건강을 모른다 — 툴팁에 "미수신"만 적는다.</summary>
    public bool IsReceived { get; }

    /// <summary>사용 중인 부품 가운데 최악 건강.</summary>
    public ComponentHealthLevel Health { get; }

    public int FaultCount { get; }
    public int DegradedCount { get; }
    public int OutOfServiceCount { get; }

    /// <summary>형상 축에 선언된 부품 수.</summary>
    public int DeclaredCount { get; }

    /// <summary>행 — 고장 → 저하 → 확인 안 됨 → 정상 → 사용 안 함 순.</summary>
    public IReadOnlyList<ComponentHealthRow> Rows { get; }

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
    public static ComponentHealthSummary Build(IDeviceAxesModel? axes)
    {
        if (axes is null) return None;

        var declared = axes.HardwareSpec?.Components ?? (IList<ComponentDefinitionModel>)Array.Empty<ComponentDefinitionModel>();
        var observed = axes.DeviceStatus?.Components;
        var isReceived = observed != null || axes.IsSectionReceived("device_status");

        var rows = new List<ComponentHealthRow>(declared.Count + (observed?.Count ?? 0));
        var declaredKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var def in declared)
        {
            if (def is null) continue;
            var key = def.Key ?? string.Empty;
            declaredKeys.Add(key);
            ComponentStatusModel? status = null;
            var hasStatus = observed != null && observed.TryGetValue(key, out status) && status != null;
            rows.Add(new ComponentHealthRow(
                Key: key,
                Type: def.Type ?? string.Empty,
                Label: LabelOf(def.Label, def.Type, key),
                State: hasStatus ? status!.State : null,
                Health: hasStatus ? ComponentVocabulary.ParseHealth(status!.Health) : ComponentHealthLevel.Unknown,
                FaultReason: hasStatus ? status!.FaultReason : null,
                InService: def.InService != false,
                IsObserved: hasStatus));
        }

        // 선언 없이 관측만 온 key — 버리지 않는다(형상 미입력 장비도 고장은 보여야 한다).
        if (observed != null)
        {
            foreach (var (key, status) in observed)
            {
                if (status is null || declaredKeys.Contains(key)) continue;
                rows.Add(new ComponentHealthRow(key, string.Empty, key, status.State,
                    ComponentVocabulary.ParseHealth(status.Health), status.FaultReason, InService: true, IsObserved: true));
            }
        }

        var active = rows.Where(r => r.InService).ToList();
        // enum 값이 나쁜 순서로 커진다(Ok < Unknown < Degraded < Fault) — 최악 = Max.
        var health = !isReceived || active.Count == 0 ? ComponentHealthLevel.None : active.Max(r => r.Health);

        var ordered = rows
            .Select((r, i) => (r, i))
            .OrderBy(x => x.r.SortRank).ThenBy(x => x.i)
            .Select(x => x.r)
            .ToList();

        return new ComponentHealthSummary(
            hasAxes: true,
            isReceived: isReceived,
            health: health,
            faultCount: isReceived ? active.Count(r => r.Health == ComponentHealthLevel.Fault) : 0,
            degradedCount: isReceived ? active.Count(r => r.Health == ComponentHealthLevel.Degraded) : 0,
            outOfServiceCount: rows.Count(r => !r.InService),
            declaredCount: declaredKeys.Count,
            rows: ordered,
            doorMotion: ResolveDoorMotion(axes));
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
        var used = Rows.Count(r => r.InService);
        return Health switch
        {
            ComponentHealthLevel.Fault or ComponentHealthLevel.Degraded =>
                $"부품 고장 {FaultCount} · 저하 {DegradedCount} / {used}",
            ComponentHealthLevel.Ok => $"부품 정상 {used}",
            ComponentHealthLevel.Unknown => $"부품 확인 안 됨 / {used}",
            _ => "부품 사용 안 함",
        };
    }

    private string HeaderLine() => "부품 상태 — " + (ShortText() ?? string.Empty).Replace("부품 ", string.Empty, StringComparison.Ordinal);

    private static string LabelOf(string? label, string? type, string key)
    {
        if (!string.IsNullOrWhiteSpace(label)) return label.Trim();
        if (!string.IsNullOrWhiteSpace(type)) return ComponentVocabulary.TypeName(type);
        return string.IsNullOrWhiteSpace(key) ? "부품" : key;
    }

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
