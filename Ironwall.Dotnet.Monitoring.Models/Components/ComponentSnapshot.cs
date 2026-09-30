using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Components;

/// <summary>
/// 장비 한 대의 부품 표 — 선언 · 관측 · 설정을 합친 줄들과 건강 수 · 요약 줄. 순수 함수 <see cref="Build"/> 의 결과.
/// </summary>
/// <remarks>
/// <para><b>입력</b>: 장비 모델의 축 묶음(<see cref="IDeviceAxesModel"/>). <c>SYNC_DEVICE</c> 재조회 때마다 묶음 참조가 통째로
/// 갈리므로 "새 참조를 받았을 때 한 번" 다시 만들면 된다(프레임 · 타이머 구동 아님 — NFR-02).</para>
/// <para><b>규칙</b>: ① 최악 건강 FAULT &gt; DEGRADED &gt; 미상 &gt; OK. ② <c>in_service=false</c>(선언 또는 설정 덮어쓰기)는
/// 집계에서 뺀다. ③ 모르는 것은 그리지 않는다 — 축 미수신이면 <see cref="IsReceived"/>=false 이고 요약은 "부품 정보 없음".
/// ④ 선언 없이 관측만 온 key 도 버리지 않는다(형상 미입력 장비도 고장은 보여야 한다).</para>
/// </remarks>
public sealed class ComponentSnapshot
{
    /// <summary>축이 아예 없다(6.3 서버 · 장비 미연결) — 아무것도 그리지 않는다(무회귀).</summary>
    public static readonly ComponentSnapshot None = new(false, false, false, Array.Empty<ComponentRowInfo>());

    private ComponentSnapshot(bool hasAxes, bool isDeclarationReceived, bool isReceived, IReadOnlyList<ComponentRowInfo> rows)
    {
        HasAxes = hasAxes;
        IsDeclarationReceived = isDeclarationReceived;
        IsReceived = isReceived;
        Rows = rows;

        foreach (var row in rows)
        {
            if (!row.InService) { OutOfServiceCount++; continue; }
            if (!isReceived) continue;
            switch (row.Health)
            {
                case ComponentHealthLevel.Fault: FaultCount++; break;
                case ComponentHealthLevel.Degraded: DegradedCount++; break;
                case ComponentHealthLevel.Ok: OkCount++; break;
                default: UnknownCount++; break;
            }
        }
        InServiceCount = rows.Count - OutOfServiceCount;
        DeclaredCount = rows.Count(r => r.IsDeclared);
        WorstHealth = !isReceived || InServiceCount == 0
            ? ComponentHealthLevel.None
            : rows.Where(r => r.InService).Max(r => r.Health);   // enum 값이 나쁜 순서로 커진다 — 최악 = Max

        LastObserved = rows.Select(r => (Row: r, At: ComponentDisplay.ParseObservedAt(r.ObservedAt)))
            .Where(x => x.At.HasValue)
            .OrderByDescending(x => x.At!.Value)
            .Select(x => x.Row)
            .FirstOrDefault();
    }

    /// <summary>장비 모델에 축 묶음이 있었는가(7.0+ 응답).</summary>
    public bool HasAxes { get; }

    /// <summary>형상 축(부품 선언)을 받았는가.</summary>
    public bool IsDeclarationReceived { get; }

    /// <summary>관측 축(<c>device_status</c>)을 받았는가. false 면 건강을 모른다.</summary>
    public bool IsReceived { get; }

    /// <summary>줄 — 선언 순서(관측만 온 줄은 뒤).</summary>
    public IReadOnlyList<ComponentRowInfo> Rows { get; }

    public int FaultCount { get; }
    public int DegradedCount { get; }
    public int OkCount { get; }
    public int UnknownCount { get; }
    public int OutOfServiceCount { get; }
    public int InServiceCount { get; }

    /// <summary>형상 축에 선언된 부품 수.</summary>
    public int DeclaredCount { get; }

    /// <summary>사용 중인 부품 가운데 최악 건강. 모르면 <see cref="ComponentHealthLevel.None"/>.</summary>
    public ComponentHealthLevel WorstHealth { get; }

    /// <summary>가장 최근에 바뀐 줄(관측 시각 기준). 없으면 null — 카드 머리의 "마지막 변화".</summary>
    public ComponentRowInfo? LastObserved { get; }

    /// <summary>부품 표를 보일 수 있는가 — 축을 받았고 관측 축도 받았다. false 면 "부품 정보 없음"(FR-08).</summary>
    public bool IsAvailable => HasAxes && IsReceived;

    /// <summary>
    /// 절 머리 요약 줄 — "고장 1 · 저하 1 · 정상 3" / "부품 이상 없음" / "부품 없음" / "부품 정보 없음"(축 · 관측 미수신).
    /// </summary>
    public string SummaryText => !IsAvailable
        ? ComponentDisplay.NoInfoText
        : ComponentDisplay.SummaryText(FaultCount, DegradedCount, OkCount, UnknownCount, OutOfServiceCount);

    /// <summary>정렬된 줄. 고장 먼저는 같은 단계 안에서 선언 순서를 지킨다(안정 정렬).</summary>
    public IReadOnlyList<ComponentRowInfo> Sorted(ComponentSortMode mode)
        => mode == ComponentSortMode.FaultFirst
            ? Rows.OrderBy(r => r.FaultFirstRank).ThenBy(r => r.DeclaredIndex).ToList()
            : Rows.OrderBy(r => r.DeclaredIndex).ToList();

    /// <summary>
    /// 축 묶음 → 부품 표. <paramref name="axes"/> 가 null 이면 <see cref="None"/>.
    /// </summary>
    /// <param name="axes">장비 모델의 축 묶음.</param>
    /// <param name="catalog">서버 카탈로그 한글(없으면 내장 사전).</param>
    public static ComponentSnapshot Build(IDeviceAxesModel? axes, IComponentTypeLabels? catalog = null)
    {
        if (axes is null) return None;

        var declared = axes.HardwareSpec?.Components ?? (IList<ComponentDefinitionModel>)Array.Empty<ComponentDefinitionModel>();
        var observed = axes.DeviceStatus?.Components;
        var overrides = axes.DeviceConfig?.ComponentOverrides;
        var isReceived = observed != null || axes.IsSectionReceived("device_status");
        var isDeclarationReceived = axes.HardwareSpec != null || axes.IsSectionReceived("hardware_spec") || axes.IsSectionReceived("components");

        var rows = new List<ComponentRowInfo>(declared.Count + (observed?.Count ?? 0));
        var declaredKeys = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var def in declared)
        {
            if (def is null) continue;
            var key = def.Key ?? string.Empty;
            declaredKeys.Add(key);
            ComponentStatusModel? status = null;
            var hasStatus = observed != null && observed.TryGetValue(key, out status) && status != null;
            var entry = overrides?[key] as JObject;
            rows.Add(new ComponentRowInfo(
                Key: key,
                Type: def.Type ?? string.Empty,
                Name: ComponentDisplay.ComponentName(def.Label, def.Type, key, catalog),
                TypeName: ComponentDisplay.TypeName(def.Type, catalog),
                DeclaredIndex: index++,
                IsDeclared: true,
                StateCode: hasStatus ? Trim(status!.State) : null,
                Health: hasStatus ? ComponentDisplay.ParseHealth(status!.Health) : ComponentHealthLevel.Unknown,
                HealthRaw: hasStatus ? status!.Health : null,
                FaultCode: hasStatus ? Trim(status!.FaultReason) : null,
                InService: def.InService != false && ReadBool(entry, "in_service") != false,
                IsObserved: hasStatus,
                ObservedAt: hasStatus ? status!.ObservedAt : null,
                IntentEnabled: ReadBool(entry, "enabled"),
                Channel: def.Channel,
                Position: def.Position));
        }

        if (observed != null)
        {
            foreach (var pair in observed.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var status = pair.Value;
                if (status is null || declaredKeys.Contains(pair.Key)) continue;
                var entry = overrides?[pair.Key] as JObject;
                rows.Add(new ComponentRowInfo(
                    Key: pair.Key,
                    Type: string.Empty,
                    Name: ComponentDisplay.ComponentName(null, null, pair.Key, catalog),
                    TypeName: ComponentDisplay.Dash,
                    DeclaredIndex: index++,
                    IsDeclared: false,
                    StateCode: Trim(status.State),
                    Health: ComponentDisplay.ParseHealth(status.Health),
                    HealthRaw: status.Health,
                    FaultCode: Trim(status.FaultReason),
                    InService: ReadBool(entry, "in_service") != false,
                    IsObserved: true,
                    ObservedAt: status.ObservedAt,
                    IntentEnabled: ReadBool(entry, "enabled")));
            }
        }

        return new ComponentSnapshot(true, isDeclarationReceived, isReceived, rows);
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool? ReadBool(JObject? entry, string name)
        => entry?[name] is JValue { Type: JTokenType.Boolean } value ? (bool)value : null;
}
