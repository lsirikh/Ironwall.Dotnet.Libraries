using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
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
public sealed record WiringControllerInfo(int Id, int Number, string Name, string Address)
{
    public string Subject => string.IsNullOrWhiteSpace(Address) ? Name : $"{Name} · {Address}";
}

/// <summary>서버에서 받은 센서 한 대 — 창은 모델 타입을 모른다(헤드리스 테스트).</summary>
public sealed record WiringSensorSeed(int Id, int? Channel, SensorFacts Facts, WiringPlacement? Placement = null, string? Issue = null);

/// <summary>
/// 장비 셋업 · 결선맵 — 제어기 <b>한 대</b>의 센서 표와 1차/2차 선(WS 전체 · PRD N-04).
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

    private string? _editNumber;
    private string? _editName;
    private string? _editType;
    private string? _editZone;

    private WiringViewModel(WiringControllerInfo controller, IReadOnlyList<string> sensorTypes, WiringApplyService? apply, IWiringDialogs dialogs)
    {
        Controller = controller ?? throw new ArgumentNullException(nameof(controller));
        SensorTypes = sensorTypes ?? Array.Empty<string>();
        _apply = apply;
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        Rows = new ObservableCollection<SensorRowViewModel>();
        Palette = new ObservableCollection<SensorRowViewModel>();
        Line1 = new ObservableCollection<WiringSlotViewModel>();
        Line2 = new ObservableCollection<WiringSlotViewModel>();
        Issues = new ObservableCollection<WiringIssue>();
        SaveResults = new ObservableCollection<WiringRowResult>();
        DisplayName = "장비 셋업 · 결선맵";
    }

    /// <summary>제어기 한 대와 그 센서로 창을 연다.</summary>
    public static WiringViewModel ForController(WiringControllerInfo controller,
                                                IEnumerable<WiringSensorSeed> sensors,
                                                IReadOnlyList<string> sensorTypes,
                                                WiringApplyService? apply,
                                                IWiringDialogs dialogs)
    {
        var vm = new WiringViewModel(controller, sensorTypes, apply, dialogs);
        vm.Load(sensors);
        return vm;
    }

    #region - Head -
    public WiringControllerInfo Controller { get; }
    public IReadOnlyList<string> SensorTypes { get; }

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
        }
    }

    public bool IsSensorStep => _step == WiringStep.Sensors;
    public bool IsWiringStep => _step == WiringStep.Wiring;

    public void GoSensors() => Step = WiringStep.Sensors;
    public void GoWiring() => Step = WiringStep.Wiring;

    /// <summary>단계 띠의 "결선" 이 끝났다고 볼 수 있는가 — 치명 문제가 없고 미배치가 없다.</summary>
    public bool IsWiringDone => Issues.All(i => i.Level != WiringIssueLevel.Critical) && _board.Unplaced.Count == 0 && _board.Rows.Count > 0;

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

    public bool HasSaveResults => SaveResults.Count > 0;

    public int SensorCount => _board.Rows.Count;
    public int UnplacedCount => _board.Unplaced.Count;
    public bool IsPaletteEmpty => Palette.Count == 0;
    public string PaletteEmptyText => _board.Rows.Count == 0 ? "센서가 없습니다 — [센서 여러 개 만들기] 로 먼저 만드세요." : "전부 선에 붙였습니다 ✓";
    public string ListStatusText => $"센서 {SensorCount} · 선택 {_selectedRows.Count} · 미배치 {UnplacedCount}";
    #endregion

    #region - Load · sync -
    private void Load(IEnumerable<WiringSensorSeed>? sensors)
    {
        var seeds = (sensors ?? Enumerable.Empty<WiringSensorSeed>())
            .Select(s => (s.Id, s.Channel, s.Facts, s.Placement, s.Issue));
        _board.Load(seeds);
        SyncAll();
        StatusText = _board.Rows.Count == 0
            ? "이 제어기에 센서가 없습니다 — [센서 여러 개 만들기] 로 시작하세요."
            : $"센서 {_board.Rows.Count}대를 불러왔습니다.";
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

            var item = new SensorRowViewModel(row, r => _board.PlacementOf(r.Key), OnRowEdited);
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
        NotifyOfPropertyChange(nameof(ListStatusText));
    }

    private void SyncLines()
    {
        Sync(Line1, WiringSpec.LINE_PRIMARY);
        Sync(Line2, WiringSpec.LINE_SECONDARY);
        NotifyOfPropertyChange(nameof(UnplacedCount));

        void Sync(ObservableCollection<WiringSlotViewModel> slots, int line)
        {
            var count = _board.SlotCount(line);
            while (slots.Count > count) slots.RemoveAt(slots.Count - 1);
            while (slots.Count < count) slots.Add(new WiringSlotViewModel(line, slots.Count));

            for (var i = 0; i < count; i++)
            {
                slots[i].Row = _board.RowAt(line, i);
                slots[i].Order = _board.OrderAt(line, i);
            }
        }
    }

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
    }

    private void RefreshCommands()
    {
        foreach (var item in Rows) item.Refresh();
        NotifyOfPropertyChange(nameof(DraftText));
        NotifyOfPropertyChange(nameof(CanUndo));
        NotifyOfPropertyChange(nameof(CanSave));
        NotifyOfPropertyChange(nameof(SaveBlockedReason));
        NotifyOfPropertyChange(nameof(ChangePreview));
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

    public string BannerTitle => Issues.Count == 0
        ? $"이상 없습니다 — 센서 {SensorCount}대가 모두 선에 붙었고 순번도 겹치지 않습니다."
        : "저장하기 전에 확인하세요";

    /// <summary>글로 확인(WS L706-708).</summary>
    public string LoopText => WiringValidation.LoopText(_board);

    /// <summary>고장 구간 예시(WS L709-712).</summary>
    public string FaultText => WiringValidation.FaultHint(_board);

    /// <summary>저장 방식 안내(WS L424).</summary>
    public string SaveNoteText =>
        "회선·순번을 담는 서버 필드가 아직 없어 장비의 벤더 확장 칸(hardware_spec.spec.wiring)에 함께 저장합니다. 센서 한 대당 1회 호출이라 [저장하기] 를 누를 때 한 번에 보냅니다.";

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
            lines.Add($"보낼 호출 {diff.ToSend.Count}회(센서 한 대당 1회)");
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
        NotifyOfPropertyChange(nameof(HasSelection));
        NotifyOfPropertyChange(nameof(SelectionCount));
        NotifyOfPropertyChange(nameof(IsMultiSelect));
        NotifyOfPropertyChange(nameof(SelectionTitle));
        NotifyOfPropertyChange(nameof(SelectionKind));
        NotifyOfPropertyChange(nameof(ListStatusText));
        RefreshHints();
    }

    public bool HasSelection => _selectedRows.Count > 0;
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
        StatusText = "한 줄을 Draft 로 더했습니다.";
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
            StatusText = "만들 줄이 없습니다 — 번호가 모두 겹칩니다.";
            return;
        }

        _board.PushUndo();
        foreach (var f in facts) _board.AddRow(f);
        SyncAll();
        StatusText = $"{facts.Count}줄을 Draft 로 만들었습니다 — [저장하기] 를 누르면 서버에 보냅니다.";
    }

    /// <summary>엑셀에서 붙여넣기(WS L350, L659-664).</summary>
    public async Task PasteAsync()
    {
        if (IsBusy) return;

        var text = _dialogs.ReadClipboardText();
        var report = TsvPaste.Parse(text, _board.Rows.Select(r => r.Facts.Number).ToList(),
                                    SensorTypes.FirstOrDefault() ?? string.Empty, LastZone());

        if (!await _dialogs.ShowPasteReportAsync(report))
        {
            StatusText = report.FatalError ?? "붙여넣기를 취소했습니다.";
            return;
        }

        if (!report.HasRows) return;

        _board.PushUndo();
        foreach (var row in report.Accepted) _board.AddRow(row.Facts);
        SyncAll();
        StatusText = $"붙여넣기 {report.Accepted.Count}줄을 Draft 로 만들었습니다" + (report.Rejected.Count > 0 ? $" · {report.Rejected.Count}줄은 건너뛰었습니다." : ".");
    }
    #endregion

    #region - Bulk edit (WS L595-646) -
    public string EditNumber { get => _editNumber ?? string.Empty; set { _editNumber = value ?? string.Empty; AfterEdit(nameof(EditNumber)); } }
    public string EditName { get => _editName ?? string.Empty; set { _editName = value ?? string.Empty; AfterEdit(nameof(EditName)); } }
    public string EditType { get => _editType ?? string.Empty; set { _editType = value ?? string.Empty; AfterEdit(nameof(EditType)); } }
    public string EditZone { get => _editZone ?? string.Empty; set { _editZone = value ?? string.Empty; AfterEdit(nameof(EditZone)); } }

    /// <summary>손대지 않은 칸에 보이는 글자 — 값이 줄마다 다르면 "— 여러 값 —"(WS L597).</summary>
    public string NumberHint => Hint(SensorTableEdit.CommonNumberText(SelectedFacts()));
    public string NameHint => Hint(SensorTableEdit.CommonText(SelectedFacts(), f => f.Name));
    public string TypeHint => Hint(SensorTableEdit.CommonText(SelectedFacts(), f => f.TypeText));
    public string ZoneHint => Hint(SensorTableEdit.CommonText(SelectedFacts(), f => f.Zone));

    public bool IsNumberTouched => _editNumber is not null;
    public bool IsNameTouched => _editName is not null;
    public bool IsTypeTouched => _editType is not null;
    public bool IsZoneTouched => _editZone is not null;

    public SensorBulkEdit CurrentEdit => new(_editNumber, _editName, _editType, _editZone);

    public bool HasEdit => !CurrentEdit.IsEmpty;
    public string EditPreview => SensorTableEdit.PreviewSentence(CurrentEdit, _selectedRows.Count);
    public string? EditAdvice => SensorTableEdit.Advice(CurrentEdit, _selectedRows.Count);
    public bool HasEditAdvice => !string.IsNullOrEmpty(EditAdvice);
    public string ApplyEditText => _selectedRows.Count > 1 ? $"{_selectedRows.Count}줄에 적용" : "적용";
    public bool CanApplyEdit => !IsBusy && HasSelection && HasEdit && SensorTableEdit.Validate(CurrentEdit, _selectedRows.Count) is null;
    public string? EditError => HasEdit ? SensorTableEdit.Validate(CurrentEdit, _selectedRows.Count) : null;
    public bool HasEditError => !string.IsNullOrEmpty(EditError);

    public void ApplyEdit()
    {
        if (!CanApplyEdit) return;

        var edit = CurrentEdit;
        _board.PushUndo();
        foreach (var item in _selectedRows) item.Row.Facts = SensorTableEdit.Apply(item.Row.Facts, edit);

        var count = _selectedRows.Count;
        ResetEdit();
        SyncAll();
        StatusText = $"{count}줄에 적용했습니다 — 손대지 않은 칸은 줄마다 원래 값을 그대로 두었습니다.";
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

        var start = _selectedRows[0].Row.Facts.Number;
        var text = await _dialogs.AskTextAsync("연속 번호 채우기", "시작 번호", start.ToString(CultureInfo.InvariantCulture));
        if (text is null) return;
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
        {
            StatusText = "시작 번호는 1 이상의 숫자로 적어 주세요.";
            return;
        }

        _board.PushUndo();
        var filled = SensorTableEdit.FillSequential(_selectedRows.Select(r => r.Row.Facts), parsed);
        for (var i = 0; i < _selectedRows.Count; i++) _selectedRows[i].Row.Facts = filled[i];

        SyncAll();
        StatusText = $"연속 번호 {parsed} 부터 {_selectedRows.Count}줄을 채웠습니다.";
    }

    /// <summary>첫 줄 값으로 통일(WS L612, L633-634).</summary>
    public void UnifyWithFirst()
    {
        if (!HasSelection || IsBusy) return;

        _board.PushUndo();
        var unified = SensorTableEdit.UnifyWithFirst(_selectedRows.Select(r => r.Row.Facts));
        for (var i = 0; i < _selectedRows.Count; i++) _selectedRows[i].Row.Facts = unified[i];

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

    private IEnumerable<SensorFacts> SelectedFacts() => _selectedRows.Select(r => r.Row.Facts);

    private static string Hint(string? common) => common ?? SensorTableEdit.MULTI_VALUE_TEXT;

    private void ResetEdit()
    {
        _editNumber = _editName = _editType = _editZone = null;
        NotifyOfPropertyChange(nameof(EditNumber));
        NotifyOfPropertyChange(nameof(EditName));
        NotifyOfPropertyChange(nameof(EditType));
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
    /// <summary>칸을 하나 늘린다(WS L411).</summary>
    public void AddSlot(int line)
    {
        _board.PushUndo();
        if (!_board.AddSlot(line))
        {
            StatusText = $"한 선에 칸은 {WiringBoard.MAX_SLOTS}개까지입니다.";
            return;
        }
        SyncAll();
        StatusText = $"{line}차 선의 칸을 늘렸습니다.";
    }

    public void AddSlotPrimary() => AddSlot(WiringSpec.LINE_PRIMARY);
    public void AddSlotSecondary() => AddSlot(WiringSpec.LINE_SECONDARY);

    /// <summary>번호 순으로 자동 배치(WS L387, L757-763).</summary>
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
        StatusText = "번호 순으로 자동 배치했습니다 — 앞 절반은 1차, 뒤 절반은 2차입니다.";
    }

    /// <summary>선에서 뺀다(칸의 ✕ · Delete · 빼는 곳 드롭).</summary>
    public void Unplace(WiringSlotViewModel? slot)
    {
        if (slot?.Row is null) return;
        _board.PushUndo();
        _board.Unplace(slot.Row.Key);
        var name = slot.Row.Display;
        SyncAll();
        StatusText = $"{name} 을(를) 선에서 뺐습니다 — 뒤 순번이 당겨졌습니다.";
    }

    /// <summary>키보드 폴백 — 고른 칸의 센서를 빼기(Delete).</summary>
    public void UnplaceSelected()
    {
        var slot = SelectedSlots().FirstOrDefault(s => s.IsFilled);
        if (slot is null) return;
        Unplace(slot);
    }

    /// <summary>키보드 폴백 — 팔레트에서 Enter: 첫 빈 칸에 붙인다.</summary>
    public void PlaceFromPalette(SensorRowViewModel? row)
    {
        if (row is null) return;

        var target = FirstEmptySlot();
        if (target is null)
        {
            StatusText = "빈 칸이 없습니다 — [＋ 칸] 으로 칸을 먼저 늘리세요.";
            return;
        }

        _board.PushUndo();
        _board.Place(row.Key, target.Value.Line, target.Value.Index);
        SyncAll();
        StatusText = $"{row.Display} → {_board.PlacementOf(row.Key)?.Text} 에 놓았습니다 · 순번 자동";
    }

    /// <summary>키보드 폴백 — 고른 칸의 센서를 한 칸 옮긴다(Alt+← · Alt+→).</summary>
    public void MoveSelected(int direction)
    {
        var slot = SelectedSlots().FirstOrDefault(s => s.IsFilled);
        if (slot?.Row is null || direction == 0) return;

        var target = NextSlot(slot.Line, slot.Index, Math.Sign(direction));
        if (target is null)
        {
            StatusText = "옮길 빈 칸이 없습니다.";
            return;
        }

        _board.PushUndo();
        var key = slot.Row.Key;
        _board.Place(key, target.Value.Line, target.Value.Index);
        SyncAll();
        SelectSlot(target.Value.Line, target.Value.Index);
        StatusText = $"{_board.Find(key)?.Display} → {_board.PlacementOf(key)?.Text}";
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

    private (int Line, int Index)? FirstEmptySlot()
    {
        foreach (var line in new[] { WiringSpec.LINE_PRIMARY, WiringSpec.LINE_SECONDARY })
            for (var i = 0; i < _board.SlotCount(line); i++)
                if (_board.RowAt(line, i) is null) return (line, i);
        return null;
    }

    /// <summary>그 방향의 첫 빈 칸 — 선 끝에 닿으면 다른 선으로 넘어가지 않는다(순서가 뒤집히지 않게).</summary>
    private (int Line, int Index)? NextSlot(int line, int index, int direction)
    {
        for (var i = index + direction; i >= 0 && i < _board.SlotCount(line); i += direction)
            if (_board.RowAt(line, i) is null) return (line, i);
        return null;
    }
    #endregion

    #region - Drag (IDragDropHandler) -
    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (IsBusy || payload is null || target is null) return false;

        return target.ZoneKey switch
        {
            SlotZoneKey => target.ZoneData is WiringSlotViewModel slot
                           && (slot.IsEmpty || payload.Items.Contains(slot))
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
            foreach (var row in removed) _board.Unplace(row.Key);
            SyncAll();
            StatusText = removed.Count == 1
                ? $"{removed[0].Display} 을(를) 선에서 뺐습니다 — 뒤 순번이 당겨졌습니다."
                : $"{removed.Count}대를 선에서 뺐습니다 — 뒤 순번이 당겨졌습니다.";
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

        // 여러 대를 끌면 놓은 칸부터 차례로 빈 칸을 채운다 — 찬 칸은 건너뛴다.
        var placed = 0;
        var index = slot.Index;
        foreach (var key in keys)
        {
            while (index < _board.SlotCount(slot.Line) && _board.RowAt(slot.Line, index) is not null && _board.RowAt(slot.Line, index)!.Key != key) index++;
            if (index >= _board.SlotCount(slot.Line)) break;
            if (_board.Place(key, slot.Line, index)) placed++;
            index++;
        }

        SyncAll();
        StatusText = placed == 0 ? "빈 칸이 아니어서 놓지 못했습니다."
            : placed == 1 ? $"{_board.Find(keys[0])?.Display} → {_board.PlacementOf(keys[0])?.Text} 에 놓았습니다 · 순번 자동"
            : $"{placed}대를 {slot.Line}차 선에 놓았습니다 · 순번 자동";

        if (placed < keys.Count) StatusText += $" ({keys.Count - placed}대는 빈 칸이 모자라 그대로 두었습니다)";
    }
    #endregion

    #region - Save -
    public bool CanSave => !IsBusy && _apply is not null && _board.IsDirty && !WiringValidation.BlocksSave(Issues);

    public string? SaveBlockedReason => IsBusy ? "저장 중입니다."
        : _apply is null ? "이 서버 판본에서는 저장할 수 없습니다."
        : !_board.IsDirty ? "바뀐 줄이 없습니다."
        : WiringValidation.BlocksSave(Issues) ? Issues.First(i => i.Level == WiringIssueLevel.Critical).Message
        : null;

    /// <summary>[저장하기] — 확인 문장을 보이고, 예라면 바뀐 줄만 보낸다(WS L450, L772-776).</summary>
    public async Task SaveAsync()
    {
        if (!CanSave || _apply is null) return;

        var unplaced = _board.Unplaced.Count;
        var message = ChangePreview + Environment.NewLine + Environment.NewLine +
            (unplaced > 0
                ? $"센서 {unplaced}대는 아직 선에 없습니다 — 그 센서의 결선은 저장하지 않습니다(표 값은 저장됩니다)." + Environment.NewLine + Environment.NewLine
                : string.Empty) +
            "보내기 직전에 각 센서를 다시 받아 그 사이 바뀌지 않았는지 확인합니다.";

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

            if (result.OkKeys.Count > 0) _board.MarkBaseline(result.OkKeys);

            SyncAll();
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

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
