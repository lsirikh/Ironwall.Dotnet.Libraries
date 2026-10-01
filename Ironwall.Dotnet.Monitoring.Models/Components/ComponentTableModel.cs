using System.ComponentModel;
using System.Globalization;

namespace Ironwall.Dotnet.Monitoring.Models.Components;

/// <summary>부품 표 한 줄 — 화면이 그대로 묶는 글 · 분류(모두 생성 시 1회 확정).</summary>
public sealed class ComponentTableRow
{
    public ComponentTableRow(ComponentRowInfo info, DateTime? today)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        TimeText = info.ObservedTimeText(today);
    }

    public ComponentRowInfo Info { get; }
    public string Key => Info.Key;
    public string Name => Info.Name;
    public string TypeName => Info.TypeName;
    public string ChannelText => Info.Channel is { } channel ? channel.ToString(CultureInfo.InvariantCulture) : ComponentDisplay.Dash;
    public string PositionText => string.IsNullOrWhiteSpace(Info.Position) ? ComponentDisplay.Dash : Info.Position!;
    public string StateText => Info.StateText;
    public bool IsIntentMismatch => Info.IsIntentMismatch;
    public string HealthText => Info.HealthText;
    public ComponentHealthKind HealthKind => Info.HealthKind;
    public string FaultText => Info.FaultText;
    public bool InService => Info.InService;
    /// <summary>선언 절의 "사용" 칸 — 사용 안 함일 때만 글이 있다.</summary>
    public string ServiceText => Info.InService ? string.Empty : ComponentDisplay.OutOfServiceText;
    /// <summary>마지막 변화(오늘이면 시:분:초). 원문은 <see cref="ObservedAtRaw"/>.</summary>
    public string TimeText { get; }
    public string ObservedAtRaw => Info.ObservedAt ?? string.Empty;
    /// <summary>줄 툴팁 — 부품 key · 유형 코드(운영자가 서버 값과 대조할 때).</summary>
    public string DetailToolTip => string.IsNullOrEmpty(Info.Type) ? Info.Key : $"{Info.Key} · {Info.Type}";
}

/// <summary>
/// 부품 표 — 장비 콘솔 상세 "부품 · 부품 상태" 절, 지도 상세 보기 "부품" 탭, 조립 카드가 같은 모양으로 쓴다(FR-02 · FR-05 · FR-06).
/// 요약 줄 + 정렬 단추(선언 순서 / 고장 먼저) + 줄.
/// </summary>
/// <remarks>WPF 에 기대지 않는 순수 모델이라 헤드리스로 시험한다. 호출 스레드: UI(정렬 단추).</remarks>
public sealed class ComponentTableModel : INotifyPropertyChanged
{
    private readonly DateTime? _today;
    private ComponentSortMode _sortMode;
    private IReadOnlyList<ComponentTableRow> _rows;

    /// <param name="snapshot">부품 표.</param>
    /// <param name="sortMode">처음 정렬 — 콘솔 · 상세 탭은 선언 순서, 지도 카드는 고장 먼저.</param>
    /// <param name="today">"오늘"(마지막 변화 칸이 시각만 적을지) — 보통 <c>DateTime.Today</c>.</param>
    public ComponentTableModel(ComponentSnapshot snapshot, ComponentSortMode sortMode, DateTime? today)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _today = today;
        _sortMode = sortMode;
        _rows = Build();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ComponentSnapshot Snapshot { get; }

    /// <summary>표를 보일 수 있는가 — false 면 <see cref="SummaryText"/>("부품 정보 없음")만 보인다(FR-08).</summary>
    public bool IsAvailable => Snapshot.IsAvailable;

    /// <summary>줄이 하나라도 있는가.</summary>
    public bool HasRows => _rows.Count > 0;

    /// <summary>절 머리 요약 줄 — "고장 1 · 저하 1 · 정상 3" / "부품 이상 없음" / "부품 정보 없음".</summary>
    public string SummaryText => Snapshot.SummaryText;

    /// <summary>선언 절 머리 — "부품 5개" / "부품 5개 · 사용 안 함 1" / "부품 없음".</summary>
    public string DeclarationSummaryText
    {
        get
        {
            var declared = Snapshot.Rows.Where(r => r.IsDeclared).ToList();
            if (declared.Count == 0) return ComponentDisplay.NoComponentsText;
            var off = declared.Count(r => !r.InService);
            return off > 0 ? $"부품 {declared.Count}개 · {ComponentDisplay.OutOfServiceText} {off}" : $"부품 {declared.Count}개";
        }
    }

    /// <summary>선언된 줄만(선언 순서) — "부품" 절 표.</summary>
    public IReadOnlyList<ComponentTableRow> DeclaredRows => _rows.Where(r => r.Info.IsDeclared).OrderBy(r => r.Info.DeclaredIndex).ToList();

    /// <summary>선언된 줄이 하나라도 있는가 — 없으면 "부품" 절은 머리줄 없이 요약 한 줄("부품 없음")만.</summary>
    public bool HasDeclaredRows => _rows.Any(r => r.Info.IsDeclared);

    /// <summary>요약 줄의 분류 —고장이 있으면 Crit, 저하만 있으면 Warn, 전부 정상이면 Ok, 그 밖은 Unknown(점 색).</summary>
    public ComponentHealthKind SummaryKind => !Snapshot.IsAvailable ? ComponentHealthKind.Unknown
        : Snapshot.FaultCount > 0 ? ComponentHealthKind.Crit
        : Snapshot.DegradedCount > 0 ? ComponentHealthKind.Warn
        : Snapshot.OkCount > 0 && Snapshot.UnknownCount == 0 ? ComponentHealthKind.Ok
        : ComponentHealthKind.Unknown;

    /// <summary>가장 최근 변화 — 카드 머리 "마지막 변화 09:41:07 · 레이더". 없으면 빈 글.</summary>
    public string LastChangeText => Snapshot.LastObserved is { } last
        ? $"마지막 변화 {last.ObservedTimeText(_today)} · {last.Name}"
        : string.Empty;

    public ComponentSortMode SortMode
    {
        get => _sortMode;
        set
        {
            if (_sortMode == value) return;
            _sortMode = value;
            _rows = Build();
            Raise(nameof(SortMode));
            Raise(nameof(IsFaultFirst));
            Raise(nameof(SortText));
            Raise(nameof(Rows));
        }
    }

    /// <summary>정렬 단추(토글) — 켜면 고장 먼저.</summary>
    public bool IsFaultFirst
    {
        get => _sortMode == ComponentSortMode.FaultFirst;
        set => SortMode = value ? ComponentSortMode.FaultFirst : ComponentSortMode.Declared;
    }

    /// <summary>정렬 단추 글 — 지금 순서를 말한다.</summary>
    public string SortText => _sortMode == ComponentSortMode.FaultFirst ? "고장 먼저" : "선언 순서";

    public IReadOnlyList<ComponentTableRow> Rows => _rows;

    private IReadOnlyList<ComponentTableRow> Build()
        => Snapshot.Sorted(_sortMode).Select(r => new ComponentTableRow(r, _today)).ToList();

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
