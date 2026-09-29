using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>창이 보여 주는 단계(WS L334-337) — 제어기는 이미 있고, 사람이 오가는 쪽은 둘이다.</summary>
public enum WiringStep
{
    /// <summary>센서 표.</summary>
    Sensors = 0,
    /// <summary>결선맵.</summary>
    Wiring = 1,
}

/// <summary>결선 창이 다루는 제어기(WS L382, L395).</summary>
/// <param name="TypeController">서버 <c>type_controller</c> 원값(<c>SmartController</c>|<c>Controller</c>|<c>IoController</c>) — 결선 모양을 정한다(FR-16). 모르면 <c>null</c>.</param>
public sealed record WiringControllerInfo(int Id, int Number, string Name, string Address, string? TypeController = null)
{
    public string Subject => string.IsNullOrWhiteSpace(Address) ? Name : $"{Name} · {Address}";
}

/// <summary>서버에서 받은 센서 한 대 — 창은 모델 타입을 모른다(헤드리스 테스트).</summary>
public sealed record WiringSensorSeed(int Id, int? Channel, SensorFacts Facts, WiringPlacement? Placement = null, string? Issue = null,
                                     IReadOnlyList<int>? Groups = null);

/// <summary>고를 수 있는 그룹 한 개(W2) — 창은 그룹 모델 타입을 모른다.</summary>
public sealed record WiringGroupInfo(int Id, string Name);

/// <summary>
/// 장비 셋업 · 결선맵 — 제어기 <b>한 대</b>의 센서 표와 결선 체인(WS 전체 · PRD N-04 · wiring-fence-view F-2).
/// </summary>
/// <remarks>
/// <para><b>저장 전까지 Draft 다.</b> 끌어 놓기 · 만들기 · 붙여넣기 · 되돌리기는 서버를 부르지 않는다.
/// 서버 호출은 [저장하기] 때 <b>바뀐 센서마다 한 번</b>이다(WS L475, L528 — 제어기 단위라 폭발반경이 묶인다).</para>
/// <para>판정은 전부 <c>Model/</c> 의 순수 함수에 있다. 드래그는 UIA 로 단언할 수 없으므로(그 패턴이 .NET 8 WPF 에 없다)
/// 회귀망은 그 함수들의 헤드리스 테스트와 <b>키보드 폴백 경로</b>다.</para>
/// </remarks>
public sealed class WiringViewModel : Screen, IDragDropHandler
{
    /// <summary>칸 드롭존.</summary>
    public const string SlotZoneKey = "wiring-slot";
    /// <summary>빼는 곳 드롭존.</summary>
    public const string BinZoneKey = "wiring-bin";

    private readonly WiringBoard _board = new();
    private readonly WiringApplyService? _apply;
    private readonly IWiringDialogs _dialogs;
    private readonly Dictionary<int, SensorRowViewModel> _rowsByKey = new();

    private WiringStep _step = WiringStep.Sensors;
    private string _statusText = string.Empty;
    private string _progressText = string.Empty;
    private bool _isBusy;
    private bool _closeWithoutAsking;
    private IReadOnlyList<SensorRowViewModel> _selectedRows = Array.Empty<SensorRowViewModel>();

    private readonly Dictionary<int, bool> _touchedGroups = new();
    private string? _editNumber;
    private string? _editName;
    private string? _editType;
    private string? _editZone;

    private WiringViewModel(WiringControllerInfo controller, IReadOnlyList<string> sensorTypes, IReadOnlyList<WiringGroupInfo> groups,
                            WiringApplyService? apply, IWiringDialogs dialogs)
    {
        Controller = controller ?? throw new ArgumentNullException(nameof(controller));
        SensorTypes = sensorTypes ?? Array.Empty<string>();
        AvailableGroups = groups ?? Array.Empty<WiringGroupInfo>();
        _apply = apply;
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        Rows = new ObservableCollection<SensorRowViewModel>();
        Palette = new ObservableCollection<SensorRowViewModel>();
        Line1 = new ObservableCollection<WiringSlotViewModel>();
        Line2 = new ObservableCollection<WiringSlotViewModel>();
        Issues = new ObservableCollection<WiringIssue>();
        SaveResults = new ObservableCollection<WiringRowResult>();
        GroupChecks = new ObservableCollection<WiringGroupCheckViewModel>();
        foreach (var group in AvailableGroups) GroupChecks.Add(new WiringGroupCheckViewModel(group));
        DisplayName = "장비 셋업 · 결선맵";
    }

    /// <summary>제어기 한 대와 그 센서로 창을 연다.</summary>
    public static WiringViewModel ForController(WiringControllerInfo controller,
                                                IEnumerable<WiringSensorSeed> sensors,
                                                IReadOnlyList<string> sensorTypes,
                                                WiringApplyService? apply,
                                                IWiringDialogs dialogs,
                                                IReadOnlyList<WiringGroupInfo>? groups = null)
    {
        var vm = new WiringViewModel(controller, sensorTypes, groups ?? Array.Empty<WiringGroupInfo>(), apply, dialogs);
        vm.Load(sensors);
        return vm;
    }

    #region - Head -
    public WiringControllerInfo Controller { get; }
    public IReadOnlyList<string> SensorTypes { get; }

    /// <summary>고를 수 있는 그룹(W2).</summary>
    public IReadOnlyList<WiringGroupInfo> AvailableGroups { get; }

    public string Subject => Controller.Subject;

    public WiringStep Step
    {
        get => _step;
        private set
        {
            if (_step == value) return;
            _step = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsSensorStep));
            NotifyOfPropertyChange(nameof(IsWiringStep));
            RefreshSteps();
        }
    }

    public bool IsSensorStep => _step == WiringStep.Sensors;
    public bool IsWiringStep => _step == WiringStep.Wiring;

    /// <summary>① 제어기 — 이 창은 제어기 한 대를 받아 열리므로 늘 끝난 단계다.</summary>
    public string StepControllerGlyph => "✓";

    /// <summary>② 센서 — 지금 보고 있으면 ▸, 센서가 있으면 ✓.</summary>
    public string StepSensorsGlyph => IsSensorStep ? "▸" : SensorCount > 0 ? "✓" : "·";

    /// <summary>③ 결선 — 지금 보고 있으면 ▸, 전부 붙고 치명 문제가 없으면 ✓.</summary>
    public string StepWiringGlyph => IsWiringStep ? "▸" : IsWiringDone ? "✓" : "·";

    /// <summary>
    /// ④ 확인 — 결선 쪽의 확인 칸에 <b>실제로 볼 것이 있을 때</b>만 켠다(보낼 것이 있고 막는 문제가 없다).
    /// </summary>
    public string StepConfirmGlyph => IsConfirmActive ? "▸" : "·";

    public bool IsConfirmActive => IsWiringStep && HasChanges && !WiringValidation.BlocksSave(Issues);

    public bool IsSensorStepDone => !IsSensorStep && SensorCount > 0;
    public bool IsWiringStepDone => !IsWiringStep && IsWiringDone;

    public void GoSensors() => Step = WiringStep.Sensors;
    public void GoWiring() => Step = WiringStep.Wiring;

    /// <summary>단계 띠의 "결선" 이 끝났다고 볼 수 있는가 — 치명 문제가 없고 미배치가 없다.</summary>
    public bool IsWiringDone => Issues.All(i => i.Level != WiringIssueLevel.Critical) && _board.Unplaced.Count == 0 && _board.Rows.Count > 0
                                && !_board.HasSuggestion;

    public string DraftText => $"바뀐 줄 {_board.UnsavedChangeCount}";

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public string ProgressText
    {
        get => _progressText;
        private set { _progressText = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasProgress)); }
    }

    public bool HasProgress => !string.IsNullOrEmpty(_progressText);

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; NotifyOfPropertyChange(); RefreshCommands(); }
    }
    #endregion

    #region - Collections -
    public ObservableCollection<SensorRowViewModel> Rows { get; }
    public ObservableCollection<SensorRowViewModel> Palette { get; }
    public ObservableCollection<WiringSlotViewModel> Line1 { get; }
    public ObservableCollection<WiringSlotViewModel> Line2 { get; }
    public ObservableCollection<WiringIssue> Issues { get; }

    /// <summary>마지막 저장의 줄별 결과 — 실패한 줄만 남는다(WS L450).</summary>
    public ObservableCollection<WiringRowResult> SaveResults { get; }

    /// <summary>그룹 3상태 칸(WS L614-618).</summary>
    public ObservableCollection<WiringGroupCheckViewModel> GroupChecks { get; }

    public bool HasGroups => GroupChecks.Count > 0;

    public bool HasSaveResults => SaveResults.Count > 0;

    public int SensorCount => _board.Rows.Count;
    public int UnplacedCount => _board.Unplaced.Count;
    public bool IsPaletteEmpty => Palette.Count == 0;

    /// <summary>표가 비었는가 — 팔레트가 빈 것("전부 붙였다")과 뜻이 다르다.</summary>
    public bool IsNoSensors => _board.Rows.Count == 0;
    public string PaletteEmptyText => _board.Rows.Count == 0 ? "센서가 없습니다 — [센서 여러 개 만들기] 로 먼저 만드세요." : "전부 결선에 붙였습니다 ✓";
    public string ListStatusText => $"센서 {SensorCount} · 선택 {_selectedRows.Count} · 미배치 {UnplacedCount}";
    #endregion

    #region - Load · sync -
    private void Load(IEnumerable<WiringSensorSeed>? sensors)
    {
        var seeds = (sensors ?? Enumerable.Empty<WiringSensorSeed>())
            .Select(s => (s.Id, s.Channel, s.Facts, s.Placement, s.Issue, s.Groups));
        _board.Load(seeds, Controller.TypeController);
        SyncAll();
        StatusText = _board.Rows.Count == 0
            ? "이 제어기에 센서가 없습니다 — [센서 여러 개 만들기] 로 시작하세요."
            : $"센서 {_board.Rows.Count}대를 불러왔습니다 — {ShapeText}.";
    }

    /// <summary>화면 전체를 보드에 맞춘다 — 묶어서 바꾼 뒤 한 번만 부른다.</summary>
    private void SyncAll()
    {
        SyncRows();
        SyncLines();
        SyncPalette();
        RefreshIssues();
        RefreshCommands();
    }

    private void SyncRows()
    {
        // 있는 항목은 그대로 두고 더하고 뺀다 — Clear() 뒤 Add() 는 바인딩된 선택을 통째로 날린다.
        foreach (var row in _board.Rows)
        {
            // 저장으로 서버 Id 를 받은 줄은 보드가 새 객체로 갈아 끼운다(키는 같다) — 그때는 화면 항목도 새로 만든다.
            if (_rowsByKey.TryGetValue(row.Key, out var existing) && ReferenceEquals(existing.Row, row)) continue;

            var item = new SensorRowViewModel(row, r => _board.PlacementOf(r.Key), OnRowEdited, r => PlacementTextOf(r.Key));
            _rowsByKey[row.Key] = item;

            var at = existing is null ? -1 : Rows.IndexOf(existing);
            if (at >= 0) Rows[at] = item;
        }

        var live = new HashSet<int>(_board.Rows.Select(r => r.Key));
        foreach (var key in _rowsByKey.Keys.Where(k => !live.Contains(k)).ToList()) _rowsByKey.Remove(key);

        for (var i = Rows.Count - 1; i >= 0; i--)
            if (!live.Contains(Rows[i].Key)) Rows.RemoveAt(i);

        var ordered = _board.Rows.Select(r => _rowsByKey[r.Key]).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (i < Rows.Count && ReferenceEquals(Rows[i], ordered[i])) continue;
            var existing = Rows.IndexOf(ordered[i]);
            if (existing >= 0) Rows.Move(existing, i);
            else Rows.Insert(i, ordered[i]);
        }

        foreach (var item in Rows) item.Refresh();
        NotifyOfPropertyChange(nameof(SensorCount));
        NotifyOfPropertyChange(nameof(IsNoSensors));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }

    /// <summary>
    /// 목록 보기 — 선마다 센서 칸 + <b>끝에 붙이기 빈 칸 하나</b>. 링 · 한 줄은 선 1 하나(2차 목록은 비운다), 양쪽 가지는 둘.
    /// </summary>
    private void SyncLines()
    {
        Sync(Line1, WiringSpec.LINE_PRIMARY);
        Sync(Line2, WiringSpec.LINE_SECONDARY);
        NotifyOfPropertyChange(nameof(UnplacedCount));

        void Sync(ObservableCollection<WiringSlotViewModel> slots, int line)
        {
            var count = line <= _board.LineCount ? _board.CountOn(line) + 1 : 0;
            while (slots.Count > count) slots.RemoveAt(slots.Count - 1);
            while (slots.Count < count) slots.Add(new WiringSlotViewModel(line, slots.Count));

            for (var i = 0; i < count; i++)
            {
                var row = _board.RowAt(line, i);
                slots[i].Row = row;
                slots[i].Order = _board.OrderAt(line, i);
                slots[i].LineName = LineNameOf(line);
                slots[i].PortText = row is not null && _board.NumberOf(row.Key) is { OppositeOrder: { } b } n ? $"A{n.Order} · B{b}" : string.Empty;
                slots[i].IsSuggested = row is not null && _board.IsSuggested(row.Key);
            }
        }
    }

    /// <summary>표의 "결선" 칸 글자.</summary>
    private string? PlacementTextOf(int key)
    {
        if (_board.NumberOf(key) is not { } n) return null;
        var text = _board.Shape switch
        {
            WiringShape.Ring => $"A{n.Order} · B{n.OppositeOrder}",
            WiringShape.TwoBranch => $"{LineNameOf(n.Line)} {n.Order}번",
            _ => $"{n.Order}번",
        };
        return _board.IsSuggested(key) ? $"제안 · {text}" : text;
    }

    private string LineNameOf(int line) => _board.Shape switch
    {
        WiringShape.TwoBranch => line == WiringSpec.LINE_PRIMARY ? "왼쪽 가지" : "오른쪽 가지",
        WiringShape.Ring => "체인",
        _ => "한 줄",
    };

    private void SyncPalette()
    {
        var unplaced = _board.Unplaced.Select(r => _rowsByKey[r.Key]).ToList();

        for (var i = Palette.Count - 1; i >= 0; i--)
            if (!unplaced.Contains(Palette[i])) Palette.RemoveAt(i);

        for (var i = 0; i < unplaced.Count; i++)
        {
            if (i < Palette.Count && ReferenceEquals(Palette[i], unplaced[i])) continue;
            var existing = Palette.IndexOf(unplaced[i]);
            if (existing >= 0) Palette.Move(existing, i);
            else Palette.Insert(i, unplaced[i]);
        }

        NotifyOfPropertyChange(nameof(IsPaletteEmpty));
        NotifyOfPropertyChange(nameof(PaletteEmptyText));
        NotifyOfPropertyChange(nameof(UnplacedCount));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }

    private void RefreshIssues()
    {
        var issues = WiringValidation.Evaluate(_board);

        for (var i = Issues.Count - 1; i >= 0; i--) Issues.RemoveAt(i);
        foreach (var issue in issues) Issues.Add(issue);

        NotifyOfPropertyChange(nameof(HasIssues));
        NotifyOfPropertyChange(nameof(BannerTitle));
        NotifyOfPropertyChange(nameof(BannerLevel));
        NotifyOfPropertyChange(nameof(IsWiringDone));
        NotifyOfPropertyChange(nameof(LoopText));
        NotifyOfPropertyChange(nameof(FaultText));
        NotifyOfPropertyChange(nameof(HasLegacyNotice));
        NotifyOfPropertyChange(nameof(LegacyNoticeText));
        NotifyOfPropertyChange(nameof(HasSuggestion));
        NotifyOfPropertyChange(nameof(SuggestionText));
    }

    /// <summary>단계 띠 — 어느 단계에 있고 어디까지 끝났는지(W1).</summary>
    private void RefreshSteps()
    {
        NotifyOfPropertyChange(nameof(StepSensorsGlyph));
        NotifyOfPropertyChange(nameof(StepWiringGlyph));
        NotifyOfPropertyChange(nameof(StepConfirmGlyph));
        NotifyOfPropertyChange(nameof(IsConfirmActive));
        NotifyOfPropertyChange(nameof(IsSensorStepDone));
        NotifyOfPropertyChange(nameof(IsWiringStepDone));
    }

    private void RefreshCommands()
    {
        RefreshSteps();
        foreach (var item in Rows) item.Refresh();
        NotifyOfPropertyChange(nameof(DraftText));
        NotifyOfPropertyChange(nameof(CanUndo));
        NotifyOfPropertyChange(nameof(CanSave));
        NotifyOfPropertyChange(nameof(SaveBlockedReason));
        NotifyOfPropertyChange(nameof(ChangePreview));
        NotifyOfPropertyChange(nameof(SaveNoteText));
        NotifyOfPropertyChange(nameof(HasChanges));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }

    private void OnRowEdited(SensorRowViewModel row)
    {
        SyncLines();
        SyncPalette();
        RefreshIssues();
        RefreshCommands();
    }
    #endregion

    #region - Banner · text -
    public bool HasIssues => Issues.Count > 0;

    public WiringIssueLevel BannerLevel => Issues.Count == 0
        ? WiringIssueLevel.Info
        : Issues.Max(i => i.Level);

    public string BannerTitle => Issues.Count == 0 && !_board.HasSuggestion
        ? $"이상 없습니다 — 센서 {SensorCount}대가 모두 결선에 붙었고 순번도 겹치지 않습니다."
        : "저장하기 전에 확인하세요";

    #region - Shape (FR-16) -
    /// <summary>결선 모양 — 링 · 양쪽 가지 · 한 줄.</summary>
    public WiringShape Shape => _board.Shape;
    public bool IsRing => _board.Shape == WiringShape.Ring;
    public bool IsTwoBranch => _board.Shape == WiringShape.TwoBranch;

    /// <summary>두 번째 목록(오른쪽 가지)을 보이는가 — 양쪽 가지만.</summary>
    public bool ShowSecondLine => _board.LineCount > 1;

    /// <summary>한 줄 설명(상태 문장 · 머리).</summary>
    public string ShapeText => _board.Shape switch
    {
        WiringShape.Ring => "링 결선(Sensor A → … → Sensor B)",
        WiringShape.TwoBranch => "양쪽 가지 결선(잠정)",
        _ => "한 줄 결선",
    };

    /// <summary>첫 목록 제목.</summary>
    public string Line1Title => _board.Shape switch
    {
        WiringShape.Ring => "링 — Sensor A → … → Sensor B",
        WiringShape.TwoBranch => "왼쪽 가지 ◀ 제어기",
        _ => "한 줄 — 제어기 ─▶",
    };

    /// <summary>첫 목록 설명.</summary>
    public string Line1Hint => _board.Shape switch
    {
        WiringShape.Ring => "왼쪽이 Sensor A 쪽 끝(A1)입니다 · 칩의 A·B 는 두 포트에서 센 번호 · 양 끝은 센서 없는 리턴케이블로 함체에 돌아옵니다 · Alt+← → 한 칸 · Delete 로 뺍니다",
        WiringShape.TwoBranch => "제어기 옆이 1번 · 바깥으로 갈수록 커집니다 · Alt+← → 한 칸 · Alt+↑ ↓ 다른 가지로 · Delete 로 뺍니다(가지 규칙은 확인 중)",
        _ => "제어기 쪽 끝이 1번입니다 · Alt+← → 한 칸 · Delete 로 뺍니다",
    };

    public string Line2Title => "제어기 ▶ 오른쪽 가지";
    public string Line2Hint => "제어기 옆이 1번 · 바깥으로 갈수록 커집니다";

    /// <summary>제어기 상자의 두 포트 글자.</summary>
    public string PortAText => IsRing ? "Sensor A" : IsTwoBranch ? "왼쪽" : "선";
    public string PortBText => IsRing ? "Sensor B" : IsTwoBranch ? "오른쪽" : string.Empty;
    public bool HasPortB => PortBText.Length > 0;
    #endregion

    #region - Load notices (FR-02 · FR-03) -
    /// <summary>옛 두 선 배치를 한 줄로 바꿨고 아직 저장하지 않았다.</summary>
    public bool HasLegacyNotice => _board.ConvertedFromLegacy;

    public const string LEGACY_NOTICE = WiringChainLoad.LEGACY_NOTICE;

    public string LegacyNoticeText => HasLegacyNotice ? LEGACY_NOTICE : string.Empty;

    /// <summary>번호순 제안이 걸려 있다 — [이대로 적용] 전에는 저장 대기가 아니다.</summary>
    public bool HasSuggestion => _board.HasSuggestion;

    public string SuggestionText => HasSuggestion
        ? $"저장된 배치가 없는 센서 {_board.SuggestedCount}대를 번호순으로 제안했습니다"
        : string.Empty;

    /// <summary>[이대로 적용] — 제안을 저장 대기로(되돌리기 한 걸음).</summary>
    public void AcceptSuggestion()
    {
        if (!_board.HasSuggestion || IsBusy) return;
        var count = _board.SuggestedCount;
        _board.PushUndo();
        _board.AcceptSuggestions();
        SyncAll();
        StatusText = $"번호순 제안 {count}대를 적용했습니다 — [저장하기]를 눌러야 저장됩니다.";
    }
    #endregion

    /// <summary>글로 확인(WS L706-708).</summary>
    public string LoopText => WiringValidation.LoopText(_board);

    /// <summary>고장 구간 예시(WS L709-712).</summary>
    public string FaultText => WiringValidation.FaultHint(_board);

    /// <summary>
    /// 저장 방식 안내(WS L424). 저장 위치 · 요청 횟수 같은 구현 설명은 운영자에게 보이지 않는다(U-18 D-9 9.1) —
    /// 몇 대가 저장되는지만 말한다. 바뀐 줄이 없으면 빈 글자라 뷰가 절을 접는다.
    /// </summary>
    public string SaveNoteText
    {
        get
        {
            var count = _board.Diff().ToSend.Count;
            return count == 0 ? string.Empty : $"센서별로 저장합니다(센서 {count}대).";
        }
    }

    /// <summary>저장 전 변경 미리보기(WS L450).</summary>
    public string ChangePreview
    {
        get
        {
            var diff = _board.Diff();
            if (diff.IsEmpty) return "바뀐 줄이 없습니다.";

            var lines = new List<string>();
            if (diff.Created.Count > 0) lines.Add($"만들 줄 {diff.Created.Count}: {Join(diff.Created)}");
            if (diff.FactChanged.Count > 0) lines.Add($"값이 바뀐 줄 {diff.FactChanged.Count}: {Join(diff.FactChanged)}");
            if (diff.WiringChanged.Count > 0) lines.Add($"결선이 바뀐 줄 {diff.WiringChanged.Count}: {string.Join(", ", diff.WiringChanged.Select(r => $"{r.Display}({Describe(r.BaselinePlacement)}→{Describe(_board.PlacementOf(r.Key))})"))}");
            lines.Add($"저장할 센서 {diff.ToSend.Count}대");
            return string.Join(Environment.NewLine, lines);

            static string Join(IReadOnlyList<WiringSensorRow> rows)
                => string.Join(", ", rows.Take(8).Select(r => r.Display)) + (rows.Count > 8 ? $" 외 {rows.Count - 8}" : string.Empty);

            static string Describe(WiringPlacement? placement) => placement?.Text ?? "미배치";
        }
    }

    public bool HasChanges => _board.IsDirty;
    #endregion

    #region - Table commands -
    /// <summary>표에서 고른 줄 — 뷰가 알려 준다.</summary>
    public void OnSelectionChanged(IEnumerable<SensorRowViewModel>? rows)
    {
        _selectedRows = rows?.Where(r => r is not null).ToList() ?? (IReadOnlyList<SensorRowViewModel>)Array.Empty<SensorRowViewModel>();
        ResetEdit();
        _touchedGroups.Clear();
        RefreshGroupChecks();
        NotifyOfPropertyChange(nameof(HasSelection));
        NotifyOfPropertyChange(nameof(HasNoSelection));
        NotifyOfPropertyChange(nameof(SelectionCount));
        NotifyOfPropertyChange(nameof(IsMultiSelect));
        NotifyOfPropertyChange(nameof(SelectionTitle));
        NotifyOfPropertyChange(nameof(SelectionKind));
        NotifyOfPropertyChange(nameof(ListStatusText));
        RefreshHints();
    }

    public bool HasSelection => _selectedRows.Count > 0;
    public bool HasNoSelection => _selectedRows.Count == 0;
    public int SelectionCount => _selectedRows.Count;
    public bool IsMultiSelect => _selectedRows.Count > 1;
    public string SelectionKind => _selectedRows.Count > 1 ? $"센서 {_selectedRows.Count}줄" : "센서";
    public string SelectionTitle => _selectedRows.Count == 0 ? "줄을 고르세요"
        : _selectedRows.Count > 1 ? "한꺼번에 채우기"
        : _selectedRows[0].Display;

    /// <summary>+ 한 줄(WS L349, L656-658).</summary>
    public void AddOneRow()
    {
        _board.PushUndo();
        var next = _board.Rows.Count == 0 ? 1101 : _board.Rows.Max(r => r.Facts.Number) + 1;
        var facts = new SensorFacts(next, $"센서 {next}", SensorTypes.FirstOrDefault() ?? string.Empty, LastZone());
        _board.AddRow(facts);
        SyncAll();
        StatusText = "한 줄을 추가했습니다 — [저장하기]를 눌러야 저장됩니다.";
    }

    /// <summary>센서 여러 개 만들기(WS L348, L649-654).</summary>
    public async Task MakeSensorsAsync()
    {
        if (IsBusy) return;

        var existing = _board.Rows.Select(r => r.Facts.Number).ToList();
        var suggested = existing.Count == 0 ? 1101 : existing.Max() + 1;
        var result = await _dialogs.AskMakeSensorsAsync(SensorTypes, SensorTypes.FirstOrDefault() ?? string.Empty, LastZone(), existing, suggested);
        if (result is null) return;

        var facts = result.SkipConflicts
            ? SensorBulkCreate.ExpandSkippingConflicts(result.Spec, existing)
            : SensorBulkCreate.Expand(result.Spec, existing);

        if (facts.Count == 0)
        {
            StatusText = "만들 줄이 없습니다. 번호가 모두 이미 있습니다.";
            return;
        }

        _board.PushUndo();
        foreach (var f in facts) _board.AddRow(f);
        SyncAll();
        StatusText = $"{facts.Count}줄을 추가했습니다 — [저장하기]를 눌러야 저장됩니다.";
    }

    /// <summary>엑셀에서 붙여넣기(WS L350, L659-664).</summary>
    public async Task PasteAsync()
    {
        if (IsBusy) return;

        var text = _dialogs.ReadClipboardText();
        var numbers = _board.Rows.Select(r => r.Facts.Number).ToList();
        var defaultType = SensorTypes.FirstOrDefault() ?? string.Empty;
        _dialogs.RememberPasteContext(numbers, defaultType, LastZone());
        var report = TsvPaste.Parse(text, numbers, defaultType, LastZone());

        if (!await _dialogs.ShowPasteReportAsync(report))
        {
            StatusText = report.FatalError ?? "붙여넣기를 취소했습니다.";
            return;
        }

        if (!report.HasRows) return;

        _board.PushUndo();
        foreach (var row in report.Accepted) _board.AddRow(row.Facts);
        SyncAll();
        StatusText = $"붙여넣은 {report.Accepted.Count}줄을 추가했습니다 — [저장하기]를 눌러야 저장됩니다" + (report.Rejected.Count > 0 ? $" · {report.Rejected.Count}줄은 건너뛰었습니다." : ".");
    }
    #endregion

    #region - Bulk edit (WS L595-646) -
    public string EditNumber { get => _editNumber ?? string.Empty; set { _editNumber = value ?? string.Empty; AfterEdit(nameof(EditNumber)); } }
    public string EditName { get => _editName ?? string.Empty; set { _editName = value ?? string.Empty; AfterEdit(nameof(EditName)); } }
    public string EditType { get => _editType ?? string.Empty; set { _editType = value ?? string.Empty; AfterEdit(nameof(EditType)); NotifyOfPropertyChange(nameof(EditTypeDisplay)); } }
    public string EditZone { get => _editZone ?? string.Empty; set { _editZone = value ?? string.Empty; AfterEdit(nameof(EditZone)); } }

    /// <summary>
    /// "한꺼번에 채우기" 종류 콤보가 실제로 그리는 칸 — "한국어 (코드)". 서버로 나가는 값은
    /// <see cref="EditType"/>(<see cref="CurrentEdit"/> 가 읽는 값) 그대로다 — 병기 표시를 그대로 저장하지
    /// 않도록 set 에서 코드만 추린다(<see cref="SensorRowViewModel.TypeDisplayText"/> 와 같은 계약).
    /// </summary>
    public string EditTypeDisplay
    {
        get => DeviceEnumDisplay.SensorTypeBilingual(EditType);
        set => EditType = DeviceEnumDisplay.ExtractSensorTypeCode(value);
    }

    /// <summary>손대지 않은 칸에 보이는 글자 — 값이 줄마다 다르면 "— 여러 값 —"(WS L597).</summary>
    public string NumberHint => Hint(SensorTableEdit.CommonNumberText(SelectedFacts()));
    public string NameHint => Hint(SensorTableEdit.CommonText(SelectedFacts(), f => f.Name));
    public string TypeHint => Hint(BilingualOrNull(SensorTableEdit.CommonText(SelectedFacts(), f => f.TypeText)));
    public string ZoneHint => Hint(SensorTableEdit.CommonText(SelectedFacts(), f => f.Zone));

    public bool IsNumberTouched => _editNumber is not null;
    public bool IsNameTouched => _editName is not null;
    public bool IsTypeTouched => _editType is not null;
    public bool IsZoneTouched => _editZone is not null;

    public SensorBulkEdit CurrentEdit => new(_editNumber, _editName, _editType, _editZone);

    public bool HasEdit => !CurrentEdit.IsEmpty || _touchedGroups.Count > 0;

    public string EditPreview
    {
        get
        {
            var fields = CurrentEdit.IsEmpty ? string.Empty : SensorTableEdit.PreviewSentence(CurrentEdit, _selectedRows.Count);
            var groups = SensorGroupEdit.PreviewSentence(_touchedGroups, NameOfGroup);
            if (groups.Length == 0) return fields.Length == 0 ? "고친 칸이 없습니다." : fields;
            var head = fields.Length == 0 ? $"{_selectedRows.Count}줄에 적용: " : fields + " · ";
            return head + "그룹 " + groups;
        }
    }

    private string NameOfGroup(int id) => AvailableGroups.FirstOrDefault(g => g.Id == id)?.Name ?? $"그룹 {id}";
    public string? EditAdvice => SensorTableEdit.Advice(CurrentEdit, _selectedRows.Count);
    public bool HasEditAdvice => !string.IsNullOrEmpty(EditAdvice);
    public string ApplyEditText => _selectedRows.Count > 1 ? $"{_selectedRows.Count}줄에 적용" : "적용";
    public bool CanApplyEdit => !IsBusy && HasSelection && HasEdit
        && (CurrentEdit.IsEmpty || SensorTableEdit.Validate(CurrentEdit, _selectedRows.Count) is null);
    public string? EditError => CurrentEdit.IsEmpty ? null : SensorTableEdit.Validate(CurrentEdit, _selectedRows.Count);
    public bool HasEditError => !string.IsNullOrEmpty(EditError);

    /// <summary>그룹 칸을 누른다 — 섞인 칸은 "전부 넣기"로 간다(WS L628). 적용 전까지는 Draft 표시일 뿐이다.</summary>
    public void ToggleGroup(WiringGroupCheckViewModel? group)
    {
        if (group is null || !HasSelection || IsBusy) return;

        var current = _touchedGroups.TryGetValue(group.Id, out var pending)
            ? (pending ? GroupCheck.All : GroupCheck.None)
            : SensorGroupEdit.StateOf(_selectedRows.Select(r => r.Row), group.Id);

        _touchedGroups[group.Id] = SensorGroupEdit.NextValue(current);
        RefreshGroupChecks();
        RefreshHints();
    }

    public void ApplyEdit()
    {
        if (!CanApplyEdit) return;

        var edit = CurrentEdit;
        var groups = new Dictionary<int, bool>(_touchedGroups);
        _board.PushUndo();
        foreach (var item in _selectedRows) item.Row.Facts = SensorTableEdit.Apply(item.Row.Facts, edit);
        SensorGroupEdit.Apply(_selectedRows.Select(r => r.Row), groups);

        var count = _selectedRows.Count;
        ResetEdit();
        SyncAll();
        StatusText = groups.Count == 0
            ? $"{count}줄에 적용했습니다 — 손대지 않은 칸은 줄마다 원래 값을 그대로 두었습니다."
            : $"{count}줄에 적용했습니다 — 체크를 바꾼 그룹만 더하고 뺐습니다(나머지 그룹은 그대로).";
    }

    public void CancelEdit()
    {
        ResetEdit();
        StatusText = "고치던 칸을 비웠습니다.";
    }

    /// <summary>연속 번호 채우기(WS L611, L631-632).</summary>
    public async Task FillSequentialAsync()
    {
        if (!HasSelection || IsBusy) return;

        var start = _selectedRows.OrderBy(r => Rows.IndexOf(r)).First().Row.Facts.Number;
        var text = await _dialogs.AskTextAsync("연속 번호 채우기", "시작 번호", start.ToString(CultureInfo.InvariantCulture));
        if (text is null) return;
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
        {
            StatusText = "시작 번호는 1 이상의 숫자로 적어 주세요.";
            return;
        }

        // 고른 차례가 아니라 <b>화면에 보이는 차례</b>로 채운다 — Ctrl 클릭 순서로 번호가 뒤섞이면 안 된다(C11).
        var ordered = _selectedRows.OrderBy(r => Rows.IndexOf(r)).ToList();
        _board.PushUndo();
        var filled = SensorTableEdit.FillSequential(ordered.Select(r => r.Row.Facts), parsed);
        for (var i = 0; i < ordered.Count; i++) ordered[i].Row.Facts = filled[i];

        SyncAll();
        StatusText = $"연속 번호 {parsed} 부터 {_selectedRows.Count}줄을 채웠습니다.";
    }

    /// <summary>첫 줄 값으로 통일(WS L612, L633-634).</summary>
    public void UnifyWithFirst()
    {
        if (!HasSelection || IsBusy) return;

        var orderedRows = _selectedRows.OrderBy(r => Rows.IndexOf(r)).ToList();
        _board.PushUndo();
        var unified = SensorTableEdit.UnifyWithFirst(orderedRows.Select(r => r.Row.Facts));
        for (var i = 0; i < orderedRows.Count; i++) orderedRows[i].Row.Facts = unified[i];

        SyncAll();
        StatusText = $"첫 줄 값(종류 · 구역)으로 {_selectedRows.Count}줄을 통일했습니다.";
    }

    /// <summary>이름 규칙 만들기(WS L613, L635-636).</summary>
    public async Task ApplyNameRuleAsync()
    {
        if (!HasSelection || IsBusy) return;

        var rule = await _dialogs.AskTextAsync("이름 규칙 만들기", "{번호} 자리에 그 줄의 번호가 들어갑니다", "북측 {번호}구간 펜스");
        if (string.IsNullOrWhiteSpace(rule)) return;

        _board.PushUndo();
        var named = SensorTableEdit.ApplyNameRule(_selectedRows.Select(r => r.Row.Facts), rule);
        for (var i = 0; i < _selectedRows.Count; i++) _selectedRows[i].Row.Facts = named[i];

        SyncAll();
        StatusText = $"이름 규칙을 {_selectedRows.Count}줄에 적용했습니다.";
    }

    private void RefreshGroupChecks()
    {
        foreach (var check in GroupChecks)
        {
            var touched = _touchedGroups.TryGetValue(check.Id, out var pending);
            check.Update(
                touched ? (pending ? GroupCheck.All : GroupCheck.None) : SensorGroupEdit.StateOf(_selectedRows.Select(r => r.Row), check.Id),
                touched);
        }
    }

    private IEnumerable<SensorFacts> SelectedFacts() => _selectedRows.Select(r => r.Row.Facts);

    private static string Hint(string? common) => common ?? SensorTableEdit.MULTI_VALUE_TEXT;

    /// <summary>종류 힌트 전용 — 공통값이 있으면 "한국어 (코드)"로, 줄마다 다르면(<c>null</c>) 그대로 <see cref="Hint"/> 에 맡긴다.</summary>
    private static string? BilingualOrNull(string? code) => code is null ? null : DeviceEnumDisplay.SensorTypeBilingual(code);

    private void ResetEdit()
    {
        _editNumber = _editName = _editType = _editZone = null;
        _touchedGroups.Clear();
        RefreshGroupChecks();
        NotifyOfPropertyChange(nameof(EditNumber));
        NotifyOfPropertyChange(nameof(EditName));
        NotifyOfPropertyChange(nameof(EditType));
        NotifyOfPropertyChange(nameof(EditTypeDisplay));
        NotifyOfPropertyChange(nameof(EditZone));
        RefreshHints();
    }

    private void AfterEdit(string property)
    {
        NotifyOfPropertyChange(property);
        RefreshHints();
    }

    private void RefreshHints()
    {
        NotifyOfPropertyChange(nameof(NumberHint));
        NotifyOfPropertyChange(nameof(NameHint));
        NotifyOfPropertyChange(nameof(TypeHint));
        NotifyOfPropertyChange(nameof(ZoneHint));
        NotifyOfPropertyChange(nameof(IsNumberTouched));
        NotifyOfPropertyChange(nameof(IsNameTouched));
        NotifyOfPropertyChange(nameof(IsTypeTouched));
        NotifyOfPropertyChange(nameof(IsZoneTouched));
        NotifyOfPropertyChange(nameof(HasEdit));
        NotifyOfPropertyChange(nameof(EditPreview));
        NotifyOfPropertyChange(nameof(EditAdvice));
        NotifyOfPropertyChange(nameof(HasEditAdvice));
        NotifyOfPropertyChange(nameof(ApplyEditText));
        NotifyOfPropertyChange(nameof(CanApplyEdit));
        NotifyOfPropertyChange(nameof(EditError));
        NotifyOfPropertyChange(nameof(HasEditError));
    }

    private string LastZone() => _board.Rows.LastOrDefault()?.Facts.Zone ?? string.Empty;
    #endregion

    #region - Wiring commands -
    /// <summary>
    /// 번호 순으로 배치(FR-03) — 링 · 한 줄은 전체를 장비번호 → id 순으로 한 줄에, 양쪽 가지는 가지 안에서만.
    /// </summary>
    public void AutoLayout()
    {
        if (_board.Rows.Count == 0)
        {
            StatusText = "배치할 센서가 없습니다.";
            return;
        }

        _board.PushUndo();
        _board.AutoLayoutByNumber();
        SyncAll();
        StatusText = IsTwoBranch
            ? "가지마다 번호 순으로 다시 줄 세웠습니다 — 어느 가지에 둘지는 끌어 놓아 정하세요."
            : "번호 순으로 한 줄에 배치했습니다(번호가 같으면 id 순).";
    }

    /// <summary>결선에서 뺀다(칸의 ✕ · Delete · 빼는 곳 드롭).</summary>
    public void Unplace(WiringSlotViewModel? slot)
    {
        if (slot?.Row is null) return;
        _board.PushUndo();
        var name = slot.Row.Display;
        if (!_board.Unplace(slot.Row.Key))
        {
            _board.Undo();
            return;
        }
        SyncAll();
        StatusText = $"{name} 을(를) 결선에서 뺐습니다 — 뒤 센서는 한 칸씩 당겨집니다.";
    }

    /// <summary>키보드 폴백 — 고른 칸의 센서를 빼기(Delete).</summary>
    public void UnplaceSelected()
    {
        var slot = SelectedSlots().FirstOrDefault(s => s.IsFilled);
        if (slot is null) return;
        Unplace(slot);
    }

    /// <summary>키보드 폴백 — 팔레트에서 Enter: 결선 끝에 붙인다.</summary>
    public void PlaceFromPalette(SensorRowViewModel? row)
        => PlaceManyFromPalette(row is null ? Array.Empty<SensorRowViewModel>() : new[] { row });

    /// <summary>여러 줄을 고르고 Enter — 고른 순서대로 끝에 붙는다. <b>되돌리기는 한 걸음</b>(C11).</summary>
    public void PlaceManyFromPalette(IEnumerable<SensorRowViewModel>? rows)
    {
        var list = rows?.Where(r => r is not null).ToList() ?? new List<SensorRowViewModel>();
        if (list.Count == 0 || IsBusy) return;

        _board.PushUndo();
        var placed = _board.Append(list.Select(r => r.Key));
        if (placed == 0)
        {
            _board.Undo();
            StatusText = "붙일 센서가 없습니다.";
            return;
        }

        SyncAll();
        StatusText = placed == 1
            ? $"{list[0].Display} → {PlacementTextOf(list[0].Key)} 에 붙였습니다"
            : $"{placed}대를 결선 끝에 차례로 붙였습니다";
    }

    /// <summary>키보드 폴백 — 다른 가지로 옮긴다(Alt+↑ · Alt+↓ · C6). <b>양쪽 가지에서만</b> 뜻이 있다.</summary>
    public void MoveSelectedToOtherLine()
    {
        var slot = SelectedSlots().FirstOrDefault(s => s.IsFilled);
        if (slot?.Row is null || IsBusy) return;

        if (!IsTwoBranch)
        {
            StatusText = "이 결선에는 다른 가지가 없습니다 — Alt+← → 로 순서를 바꿉니다.";
            return;
        }

        var key = slot.Row.Key;
        _board.PushUndo();
        if (!_board.MoveToOtherLine(key))
        {
            _board.Undo();
            return;
        }
        SyncAll();
        if (_board.LocationOf(key) is { } at) SelectSlot(at.Line, at.Index);
        StatusText = $"{_board.Find(key)?.Display} → {PlacementTextOf(key)}";
    }

    /// <summary>키보드 폴백 — 고른 칸의 센서를 한 칸 옮긴다(Alt+← · Alt+→). 옆 센서와 자리를 바꾼다.</summary>
    public void MoveSelected(int direction)
    {
        var slot = SelectedSlots().FirstOrDefault(s => s.IsFilled);
        if (slot?.Row is null || direction == 0) return;

        var key = slot.Row.Key;
        _board.PushUndo();
        if (!_board.MoveBy(key, Math.Sign(direction)))
        {
            _board.Undo();
            StatusText = "더 옮길 자리가 없습니다 — 끝입니다.";
            return;
        }

        SyncAll();
        if (_board.LocationOf(key) is { } at) SelectSlot(at.Line, at.Index);
        StatusText = $"{_board.Find(key)?.Display} → {PlacementTextOf(key)}";
    }

    public void MoveSelectedBack() => MoveSelected(-1);
    public void MoveSelectedForward() => MoveSelected(1);

    /// <summary>되돌리기 — 표 값과 결선을 한 단계 되돌린다(WS L181, L388).</summary>
    public void Undo()
    {
        if (!_board.Undo())
        {
            StatusText = "되돌릴 것이 없습니다.";
            return;
        }

        ResetEdit();
        SyncAll();
        StatusText = "되돌렸습니다.";
    }

    public bool CanUndo => !IsBusy && _board.CanUndo;

    private IEnumerable<WiringSlotViewModel> SelectedSlots() => Line1.Concat(Line2).Where(s => s.IsSelected);

    private void SelectSlot(int line, int index)
    {
        foreach (var slot in Line1.Concat(Line2)) slot.IsSelected = slot.Line == line && slot.Index == index;
    }
    #endregion

    #region - Drag (IDragDropHandler) -
    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (IsBusy || payload is null || target is null) return false;

        return target.ZoneKey switch
        {
            // 어느 칸에 놓아도 된다 — 그 자리에 끼워 넣고 뒤를 민다(체인에는 "찬 칸" 이 없다).
            SlotZoneKey => target.ZoneData is WiringSlotViewModel slot
                           && slot.Line <= _board.LineCount
                           && payload.Items.Count > 0
                           && payload.Items.All(i => i is SensorRowViewModel || i is WiringSlotViewModel { IsFilled: true }),
            BinZoneKey => payload.Items.Count > 0 && payload.Items.All(i => i is WiringSlotViewModel { IsFilled: true }),
            _ => false,
        };
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (!CanDrop(payload, target)) return;

        if (target.ZoneKey == BinZoneKey)
        {
            var removed = payload.Items.OfType<WiringSlotViewModel>().Where(s => s.Row is not null).Select(s => s.Row!).ToList();
            if (removed.Count == 0) return;

            _board.PushUndo();
            if (_board.UnplaceMany(removed.Select(r => r.Key)) == 0)
            {
                _board.Undo();
                return;
            }
            SyncAll();
            StatusText = removed.Count == 1
                ? $"{removed[0].Display} 을(를) 결선에서 뺐습니다 — 뒤 센서는 당겨집니다."
                : $"{removed.Count}대를 결선에서 뺐습니다 — 뒤 센서는 당겨집니다.";
            return;
        }

        if (target.ZoneData is not WiringSlotViewModel slot) return;

        var keys = payload.Items
            .Select(i => i switch
            {
                SensorRowViewModel row => row.Key,
                WiringSlotViewModel from => from.Row?.Key ?? 0,
                _ => 0,
            })
            .Where(k => k != 0)
            .ToList();
        if (keys.Count == 0) return;

        _board.PushUndo();
        var placed = _board.PlaceMany(keys, slot.Line, slot.Index);
        if (placed == 0)
        {
            _board.Undo();
            StatusText = "제자리입니다 — 바뀐 것이 없습니다.";
            return;
        }

        SyncAll();
        StatusText = placed == 1
            ? $"{_board.Find(keys[0])?.Display} → {PlacementTextOf(keys[0])} 에 놓았습니다 · 뒤는 한 칸씩 밀립니다"
            : $"{placed}대를 {LineNameOf(slot.Line)} {slot.Index + 1}번 자리부터 놓았습니다 · 뒤는 밀립니다";
    }
    #endregion

    #region - Save -
    public bool CanSave => !IsBusy && _apply is not null && _board.IsDirty && !WiringValidation.BlocksSave(Issues);

    public string? SaveBlockedReason => IsBusy ? "저장 중입니다."
        : _apply is null ? "현재 서버에서는 결선을 저장할 수 없습니다."
        : !_board.IsDirty ? "바뀐 줄이 없습니다."
        : WiringValidation.BlocksSave(Issues) ? Issues.First(i => i.Level == WiringIssueLevel.Critical).Message
        : null;

    /// <summary>[저장하기] — 확인 문장을 보이고, 예라면 바뀐 줄만 보낸다(WS L450, L772-776).</summary>
    public async Task SaveAsync()
    {
        if (!CanSave || _apply is null) return;

        var unplaced = _board.Unplaced.Count;
        var suggested = _board.SuggestedCount;
        var message = ChangePreview + Environment.NewLine + Environment.NewLine +
            (unplaced > 0
                ? $"센서 {unplaced}대는 아직 결선에 없습니다 — 그 센서의 결선은 저장하지 않습니다(표 값은 저장됩니다)." + Environment.NewLine + Environment.NewLine
                : string.Empty) +
            (suggested > 0
                ? $"번호순 제안 {suggested}대는 [이대로 적용] 전이라 저장하지 않습니다." + Environment.NewLine + Environment.NewLine
                : string.Empty) +
            "저장하기 전에 각 센서가 그사이 바뀌지 않았는지 확인합니다. 저장할까요?";

        if (!await _dialogs.ConfirmAsync("결선 저장", message)) return;

        IsBusy = true;
        SaveResults.Clear();
        NotifyOfPropertyChange(nameof(HasSaveResults));

        try
        {
            var progress = new Progress<WiringProgress>(p => ProgressText = p.Total == 0 ? string.Empty : $"{p.Done}/{p.Total} · {p.Current}");
            var result = await _apply.ApplyAsync(Controller.Id, _board, progress, CancellationToken.None);

            StatusText = result.Message;

            foreach (var row in result.Rows.Where(r => !r.Ok)) SaveResults.Add(row);
            NotifyOfPropertyChange(nameof(HasSaveResults));

            // 성공한 줄만 새 기준으로 — 실패한 줄은 Draft 로 남아 다시 보낼 수 있다.
            foreach (var row in result.Rows.Where(r => r.Ok && r.IsCreate && r.NewId is { } id && id > 0))
                _board.Promote(row.Key, row.NewId!.Value);

            // 표·결선은 PATCH/POST 가 된 줄만, 그룹은 서버가 맞춰 준 그룹 호출만 새 기준으로 — 따로 나가므로 따로 옮긴다.
            //   (종전: 그룹만 바꾼 저장은 기준선이 안 옮겨져 창이 계속 더러웠고, 행 PATCH 성공 + 그룹 실패는 그룹 변경을 조용히 먹었다.)
            if (result.OkKeys.Count > 0) _board.MarkBaseline(result.OkKeys, includeGroups: false);
            _board.MarkGroupsSaved(result.Groups.Where(g => g.Ok && g.DeviceIds is { Count: > 0 })
                                                .Select(g => (g.GroupId, g.Add, g.DeviceIds!)));
            if (result.IsSuccess && !_board.IsDirty) _closeWithoutAsking = true;   // 전부 저장됐다 — 닫을 때 묻지 않는다

            // 서버에 한 줄이라도 쓰였으면 기억한다 — 창은 OS ✕ 로만 닫혀 대화 결과가 늘 비므로(false),
            // 입구(WiringLauncher)는 이 값으로 "저장했다" 를 판정해 콘솔 목록을 다시 읽고 안내를 띄운다.
            if (result.OkKeys.Count > 0 || result.Groups.Any(g => g.Ok)) HasSaved = true;

            SyncAll();
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    /// <summary>
    /// 이 창에서 서버에 무언가 저장했는가(센서 표 · 결선 · 그룹 중 하나라도). 일부만 성공해도 true —
    /// 콘솔은 저장된 만큼 다시 읽어야 한다.
    /// </summary>
    public bool HasSaved { get; private set; }

    /// <summary>미저장 변경이 있으면 닫기 전에 묻는다.</summary>
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_board.IsDirty || _closeWithoutAsking) return true;
        return await _dialogs.ConfirmAsync("셋업 창 닫기", $"미저장 변경 {_board.UnsavedChangeCount}건이 있습니다. 버리고 닫을까요?");
    }

    public async Task CloseAsync()
    {
        await TryCloseAsync(!_board.IsDirty);
    }
    #endregion

    /// <summary>테스트 · 미리보기에서 보드를 들여다본다.</summary>
    internal WiringBoard Board => _board;
}
