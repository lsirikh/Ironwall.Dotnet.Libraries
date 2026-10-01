using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Libraries.Base.Services;
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
/// <param name="SavedShape">저장된 결선 모양(<c>spec.wiring</c> 의 <c>"v": 2</c> 표지). 옛 값이면 <c>null</c>(F-2b H3).</param>
/// <param name="ConnectionType">접속 축 종류(<c>connection.type</c> — <c>RS485</c> · <c>IP_DIRECT</c> · <c>IP_CONVERTER</c> …) — IP 센서 · 노드 주소를 가른다(fence-wiring-editor FR-15).</param>
/// <param name="IpAddress">IP 센서의 주소(제어기 뒤 내부망일 수 있다).</param>
/// <param name="LinkHealth">매니저가 보고한 <c>NETWORK_INTERFACE</c> 부품 health(OK · DEGRADED · FAULT) — 센서 신호등(FR-14).</param>
public sealed record WiringSensorSeed(int Id, int? Channel, SensorFacts Facts, WiringPlacement? Placement = null, string? Issue = null,
                                     IReadOnlyList<int>? Groups = null, WiringShape? SavedShape = null,
                                     string? ConnectionType = null, string? IpAddress = null, string? LinkHealth = null);

/// <summary>
/// 결선 창의 펜스 편집 재료(fence-wiring-editor FR-01 · FR-11 · FR-14) — 로컬에 저장된 구성 · 로컬 저장소 · 제어기 ping.
/// 모두 없어도 창은 열린다(없으면 제안 구성 · 로컬 저장 칸 숨김 · 신호등 모름).
/// </summary>
/// <param name="Key">저장소 열쇠(서버 · 제어기). 없으면 (<c>unknown</c>, 제어기 id).</param>
/// <param name="Load">불러오기 결과 — 읽기 실패(손상 · DB 예외 · 시간 초과)면 저장은 덮어쓰기 확인 뒤에만. 없으면 <paramref name="Document"/> 로 판단.</param>
public sealed record WiringFenceContext(
    Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutDocument? Document = null,
    Ironwall.Dotnet.Monitoring.Models.Fences.IFenceLayoutStore? Store = null,
    Signals.IPingProbe? Ping = null,
    Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutKey? Key = null,
    Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutLoadResult? Load = null);

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
public sealed partial class WiringViewModel : Screen, IDragDropHandler
{
    /// <summary>칸 드롭존.</summary>
    public const string SlotZoneKey = "wiring-slot";
    /// <summary>빼는 곳 드롭존.</summary>
    public const string BinZoneKey = "wiring-bin";

    private readonly WiringBoard _board = new();
    private readonly WiringApplyService? _apply;
    private readonly IWiringDialogs _dialogs;
    private readonly ILogService? _log;
    private readonly Dictionary<int, SensorRowViewModel> _rowsByKey = new();

    private WiringStep _step = WiringStep.Sensors;
    private string _statusText = string.Empty;
    private string _progressText = string.Empty;
    private bool _isBusy;
    private IReadOnlyList<SensorRowViewModel> _selectedRows = Array.Empty<SensorRowViewModel>();

    private readonly Dictionary<int, bool> _touchedGroups = new();
    private string? _editNumber;
    private string? _editName;
    private string? _editType;
    private string? _editZone;

    private WiringViewModel(WiringControllerInfo controller, IReadOnlyList<string> sensorTypes, IReadOnlyList<WiringGroupInfo> groups,
                            WiringApplyService? apply, IWiringDialogs dialogs, WiringFenceContext? fence, ILogService? log)
    {
        _log = log;
        Controller = controller ?? throw new ArgumentNullException(nameof(controller));
        SensorTypes = sensorTypes ?? Array.Empty<string>();
        AvailableGroups = groups ?? Array.Empty<WiringGroupInfo>();
        _apply = apply;
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _fenceStore = fence?.Store;
        _fenceDocument = fence?.Document ?? fence?.Load?.Document;
        _ping = fence?.Ping;
        _fenceKey = fence?.Key ?? new Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutKey(
            Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutRows.UNKNOWN_SERVER, controller.Id);
        _fenceReadFailed = fence?.Load?.IsReadFailure == true;
        _fenceRevision = fence?.Load?.RowRevision ?? 0;

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
                                                IReadOnlyList<WiringGroupInfo>? groups = null,
                                                WiringFenceContext? fence = null,
                                                ILogService? log = null)
    {
        var vm = new WiringViewModel(controller, sensorTypes, groups ?? Array.Empty<WiringGroupInfo>(), apply, dialogs, fence, log);
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
                                && !_board.HasPendingProposals;

    public string DraftText => HasLocalChanges ? $"바뀐 줄 {_board.UnsavedChangeCount} · 펜스 구성 바뀜" : $"바뀐 줄 {_board.UnsavedChangeCount}";

    /// <summary>
    /// 상태 줄. 마지막 동작이 번호를 위치 순서대로 다시 매겼으면(Draft) 끝에 "번호 n대 바뀜" 을 붙인다 — 번호가 조용히 바뀌지 않게.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            var text = value ?? string.Empty;
            var renumbered = _board.TakeRenumbered();
            if (renumbered > 0) text = $"{text.TrimEnd('.', ' ')} · 번호 {renumbered}대 바뀜";
            _statusText = text;
            NotifyOfPropertyChange();
        }
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
        var list = (sensors ?? Enumerable.Empty<WiringSensorSeed>()).ToList();
        var shapes = list.Where(s => s.Id > 0 && s.SavedShape is not null)
                         .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().SavedShape!.Value);
        _board.Load(list.Select(s => (s.Id, s.Channel, s.Facts, s.Placement, s.Issue, s.Groups)), Controller.TypeController, shapes);
        LoadLinks(list);
        LoadFence(_fenceDocument);
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
        RaiseFence();
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
        NotifyOfPropertyChange(nameof(ChainSummaryText));

        void Sync(ObservableCollection<WiringSlotViewModel> slots, int line)
        {
            var count = line <= _board.LineCount ? _board.CountOn(line) + 1 : 0;
            while (slots.Count > count) slots.RemoveAt(slots.Count - 1);
            while (slots.Count < count) slots.Add(new WiringSlotViewModel(line, slots.Count));

            var duplicates = _board.Rows.GroupBy(r => r.Facts.Number).Where(g => g.Count() > 1).SelectMany(g => g).Select(r => r.Key).ToHashSet();
            var gap = _board.Chain.ControllerGap;
            var placed = count - 1;
            WiringSensorRow? previous = null;
            for (var i = 0; i < count; i++)
            {
                var row = _board.RowAt(line, i);
                var slot = slots[i];
                slot.Row = row;
                slot.Order = _board.OrderAt(line, i);
                slot.LineName = LineNameOf(line);
                // "Ch1 · Ch2 번호" 칸 — 두 포트에서 센 자리만("1 · 50"). A · B 는 머리 칸 툴팁의 별칭.
                slot.PortText = row is not null && _board.NumberOf(row.Key) is { OppositeOrder: { } b } n ? $"{n.Order} · {b}" : string.Empty;
                slot.IsSuggested = row is not null && _board.IsProposed(row.Key);

                // 표 보기 열(표 보기 정리) — 종류 · 방향 · 간격 · 상태 · 함체 자리 구분 띠
                var type = WiringTopology.ParseSensorType(row?.Facts.TypeText);
                slot.TypeText = row is null ? string.Empty : TypeShortText(type, row.Facts.TypeText);
                slot.TypeIcon = TypeIconName(type);
                slot.FacingText = row is null ? string.Empty : row.SupportsFacing ? (row.Facing == WiringFacing.Back ? "뒤" : "앞") : "—";
                slot.GapText = row is null ? string.Empty
                    : previous is null ? "—"
                    : $"{_board.Spacing.GapBetween(WiringTopology.ParseSensorType(previous.Facts.TypeText), type):0.#}m";
                slot.IsDraft = row is not null && (row.IsNew || row.FactsChanged || !WiringSpec.SameWiring(_board.PlacementOf(row.Key), row.BaselinePlacement));
                slot.IsDuplicateNumber = row is not null && duplicates.Contains(row.Key);
                slot.IsIpAddress = row is not null && IsIpSensor(row.Key);
                slot.AddressCellText = row is null ? string.Empty : AddressTextOf(row.Key);
                slot.EnclosureBefore = IsRing && placed > 0 && i == Math.Clamp(gap, 0, placed);
                slot.EnclosureLabel = slot.EnclosureBefore
                    ? $"▲ A 쪽 {(gap > 0 ? $"1~{gap}" : "없음")}   ·   함체(제어기) 자리   ·   B 쪽 {(gap < placed ? $"{gap + 1}~{placed}" : "없음")} ▼"
                    : string.Empty;
                if (row is not null) previous = row;
            }
        }

        static string TypeShortText(EnumDeviceType type, string raw) => type switch
        {
            _ when WiringTopology.IsSmartSensor(type) => "스마트 복합",
            EnumDeviceType.Multi => "복합",
            EnumDeviceType.Fence => "펜스",
            EnumDeviceType.Underground => "지진동",
            _ => string.IsNullOrWhiteSpace(raw) ? "—" : raw,
        };

        static string TypeIconName(EnumDeviceType type) => type switch
        {
            _ when WiringTopology.IsSmartSensor(type) => "Radar",
            EnumDeviceType.Multi => "MotionSensor",
            EnumDeviceType.Fence => "Fence",
            EnumDeviceType.Underground => "Waveform",
            _ => "Radar",
        };
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
        return _board.IsProposed(key) ? $"제안 · {text}" : text;
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
        NotifyOfPropertyChange(nameof(HasAppliedNotice));
        NotifyOfPropertyChange(nameof(AppliedNoticeText));
        NotifyOfPropertyChange(nameof(HasTopologyNotice));
        NotifyOfPropertyChange(nameof(TopologyNoticeText));
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
        NotifyOfPropertyChange(nameof(HasLocalChanges));
        NotifyOfPropertyChange(nameof(CanApplyPanelEdit));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }

    private void OnRowEdited(SensorRowViewModel row)
    {
        SyncLines();
        SyncPalette();
        RefreshIssues();
        RefreshCommands();
        RaiseFence();       // 이름 · 번호 · 종류가 펜스 칩 · 모양에도 보인다
    }
    #endregion

    #region - Banner · text -
    public bool HasIssues => Issues.Count > 0;

    public WiringIssueLevel BannerLevel => Issues.Count == 0
        ? WiringIssueLevel.Info
        : Issues.Max(i => i.Level);

    public string BannerTitle => Issues.Count == 0 && !_board.HasPendingProposals
        ? $"이상 없습니다 — 센서 {SensorCount}대가 모두 결선에 붙었고 순번도 겹치지 않습니다."
        : "저장하기 전에 확인하세요";

    #region - Shape (FR-16) -
    /// <summary>결선 모양 — v0.4 부터 모든 제어기가 링(옛 가지 · 한 줄 저장값은 불러올 때 링으로 읽는다).</summary>
    public WiringShape Shape => _board.Shape;
    public bool IsRing => _board.Shape == WiringShape.Ring;

    /// <summary>"고장 구간과의 관계" 설명 — 모든 제어기가 링(v0.4 §1-C)이라 늘 A/B 양 끝 설명.</summary>
    public string FaultRelationText
        => $"링에서 1차 번호는 {WiringValidation.PORT_1} 쪽 끝에서, 2차 번호는 {WiringValidation.PORT_2} 쪽 끝에서 센 자리입니다(같은 체인을 양 끝에서 셉니다). "
           + "결선을 저장해 두면 장애 화면이 \"1차 4~5\" 같은 구간을 센서 이름으로 보여 줄 수 있습니다.";

    /// <summary>두 번째 목록을 보이는가 — 링은 선이 하나라 늘 거짓(옛 양쪽 가지 목록은 v0.4 에서 뺐다).</summary>
    public bool ShowSecondLine => false;

    /// <summary>결선 설명(상태 문장 · 머리).</summary>
    public string ShapeText => $"링 결선({WiringValidation.PORT_1} → … → {WiringValidation.PORT_2})";

    /// <summary>목록 제목.</summary>
    public string Line1Title => $"링 — {WiringValidation.PORT_1} → … → {WiringValidation.PORT_2}";

    /// <summary>표 보기 머리 — 제어기 종류(모든 제어기가 링 · v0.4).</summary>
    public string ControllerKindText => _board.Topology.ControllerKind switch
    {
        WiringControllerKind.Smart => "스마트 제어기 · 링(1U 도킹 · 스마트 + VBUS 제어기)",
        WiringControllerKind.Pids => "펜스 경계 제어기 · 링",
        WiringControllerKind.Io => "IO 제어기 · 링",
        _ => "제어기 · 링(종류 모름)",
    };

    /// <summary>표 보기 머리 — 체인 대수 · 길이 · 기준(한도 표 · 섞이면 O-11).</summary>
    public string ChainSummaryText
    {
        get
        {
            var reference = _board.Limits.ReferenceLength(_board.Family) is { } m ? $"기준 {m:0}m"
                : _board.IsMixedFamily ? $"기준 — {WiringLimitTable.MIXED_SHORT}" : "기준 —";
            return $"체인 {_board.Chain.Count}대 · 길이 약 {_board.ChainLengthMetres:0}m / {reference} · 함체 자리 {EnclosureGapText}";
        }
    }

    /// <summary>목록 설명.</summary>
    public string Line1Hint => $"위가 {WiringValidation.PORT_1} 쪽 끝(A1)입니다 · A·B 번호는 두 포트(Ch1 · Ch2)에서 센 자리 · Ch1 → 아래 줄 → 먼 끝에서 꺾여 위 줄(또는 리턴선) → Ch2 · 순서를 바꾸면 펜스 위 자리도 따라갑니다 · Alt+← → 한 칸 · Delete 로 뺍니다";
    #endregion

    #region - Load notices (FR-02 · FR-03 · F-2b) -
    /// <summary>옛 두 선 배치를 한 줄로 바꿨고 아직 저장하지 않았다.</summary>
    public bool HasLegacyNotice => _board.ConvertedFromLegacy || _board.JoinedFromBranches;

    public const string LEGACY_NOTICE = WiringChainLoad.LEGACY_NOTICE;

    /// <summary>옛 가지 배치(<c>"shape": "branch"</c>)를 링으로 이어 붙였을 때(v0.4 · 가지 폐기).</summary>
    public const string JOINED_NOTICE = WiringBoard.JOINED_NOTICE;

    /// <summary>제품군이 섞인 제어기의 번호순 제안 안내(O-12) — 번호 대역이 종류별로 달라 번호순이 실제 순서와 다를 수 있다.</summary>
    public const string MIXED_ORDER_NOTICE = "종류가 섞여 있어 번호순이 실제 순서와 다를 수 있습니다 — 확인하세요";

    /// <summary>체인을 고치다가 제안을 함께 적용했을 때의 알림(H2).</summary>
    public const string APPLIED_NOTICE = "제안 · 변환 배치를 함께 적용했습니다 — Ctrl+Z 로 취소";

    public string LegacyNoticeText => _board.JoinedFromBranches ? JOINED_NOTICE : HasLegacyNotice ? LEGACY_NOTICE : string.Empty;

    /// <summary>
    /// 적용하지 않은 불러오기 제안이 걸려 있다(번호순 제안 · 옛 배치 변환 · 빈 자리 당김) — [이대로 적용] 전에는 저장 대상이 아니다.
    /// </summary>
    public bool HasSuggestion => _board.HasPendingProposals;

    /// <summary>제안 배너 문장 — 갈래마다 한 조각.</summary>
    public string SuggestionText
    {
        get
        {
            if (!_board.HasPendingProposals) return string.Empty;
            var parts = new List<string>();
            var suggested = _board.ProposalCount(WiringProposalKind.Suggested);
            var converted = _board.ProposalCount(WiringProposalKind.Converted);
            var compacted = _board.ProposalCount(WiringProposalKind.Compacted);
            var joined = _board.ProposalCount(WiringProposalKind.JoinedBranches);
            if (suggested > 0) parts.Add($"저장된 배치가 없는 센서 {suggested}대를 번호순으로 제안했습니다");
            if (joined > 0) parts.Add($"옛 가지 배치 {joined}대를 링 한 줄로 이어 보였습니다(왼쪽 가지 바깥 → 제어기 → 오른쪽 가지 바깥)");
            if (converted > 0) parts.Add($"옛 두 선 배치 {converted}대를 한 줄로 바꿔 보였습니다");
            if (compacted > 0) parts.Add($"빈 자리를 당겨 붙인 센서 {compacted}대");
            // 제품군이 섞인 제어기(스마트 1번~ · 펜스 101번~)는 번호순이 공간 순서가 아닐 수 있다(O-12) — 체인을 고쳐도 저절로 적용하지 않는다.
            var mixed = suggested > 0 && _board.IsMixedFamily ? " — " + MIXED_ORDER_NOTICE : string.Empty;
            if (parts.Count == 1 && suggested > 0) return parts[0] + mixed;
            return string.Join(" · ", parts) + mixed + " — 적용 전에는 저장 대상이 아닙니다";
        }
    }

    /// <summary>체인을 고치다가 제안을 함께 적용했다(저장 전까지).</summary>
    public bool HasAppliedNotice => _board.ProposalsAppliedByEdit;

    public string AppliedNoticeText => HasAppliedNotice ? APPLIED_NOTICE : string.Empty;

    /// <summary>
    /// 결선 모양을 어떻게 정했는가 — 저장된 모양과 추정이 다르면 그 알림, 제어기 종류를 몰라 센서로 추정했으면 그 알림(H3).
    /// </summary>
    public string TopologyNoticeText
        => _board.ShapeNotice
           ?? (_board.StoredShape is null && _board.Topology.IsInferred && _board.Rows.Count > 0
               ? $"제어기 종류를 몰라 센서로 추정했습니다: {WiringBoard.ShapeName(_board.Shape)}"
               : string.Empty);

    public bool HasTopologyNotice => TopologyNoticeText.Length > 0;

    /// <summary>[이대로 적용] — 걸린 제안을 전부 저장 대기로(되돌리기 한 걸음).</summary>
    public void AcceptSuggestion()
    {
        if (!_board.HasPendingProposals || IsBusy) return;
        _board.PushUndo();
        _board.AcceptProposals();
        SyncAll();
        StatusText = "제안을 적용했습니다 — [저장하기]를 눌러야 저장됩니다.";
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
            if (diff.WiringChanged.Count > 0)
            {
                // 불러오기 제안에서 온 결선은 갈래마다 따로 — 사람이 옮긴 것과 자동으로 바뀐 것을 섞어 보이지 않는다(M1).
                // 방향만 바뀐 줄(FR-20)은 자리 갈래에 넣지 않고 "방향 바뀜" 으로 따로 센다.
                var moved = diff.WiringChanged.Where(r => !WiringSpec.SamePlacement(_board.PlacementOf(r.Key), r.BaselinePlacement)).ToList();
                var byKind = moved.GroupBy(r => _board.Proposals.TryGetValue(r.Key, out var kind) ? (WiringProposalKind?)kind : null)
                                  .ToDictionary(g => g.Key ?? (WiringProposalKind)(-1), g => g.ToList());
                if (byKind.TryGetValue(WiringProposalKind.Converted, out var converted))
                    lines.Add($"자동 변환 {converted.Count}건(옛 두 선 → 한 줄) — 옛 2차 선 센서를 {WiringValidation.PORT_2} 쪽 끝부터 이어 붙인 자리입니다: {Places(converted)}");
                if (byKind.TryGetValue(WiringProposalKind.JoinedBranches, out var joinedRows))
                    lines.Add($"옛 가지 → 링 이어 붙임 {joinedRows.Count}건 — 왼쪽 가지를 뒤집어(바깥 → 제어기 옆) 오른쪽 가지 앞에 붙인 자리입니다: {Places(joinedRows)}");
                if (byKind.TryGetValue(WiringProposalKind.Compacted, out var compacted))
                    lines.Add($"빈 자리 당겨 붙임 {compacted.Count}건 — 저장된 순번의 빈 자리를 메워 뒤 센서의 순번이 앞당겨집니다: {Places(compacted)}");
                if (byKind.TryGetValue(WiringProposalKind.Suggested, out var suggested))
                    lines.Add($"번호순 제안 {suggested.Count}건 — 저장된 자리가 없던 센서를 장비번호 순으로 붙인 자리입니다: {Places(suggested)}");
                if (byKind.TryGetValue((WiringProposalKind)(-1), out var edited))
                    lines.Add($"결선이 바뀐 줄 {edited.Count}: {Places(edited)}");
                var turned = diff.WiringChanged.Where(r => _board.FacingChanged(r.Key)).ToList();
                if (turned.Count > 0)
                    lines.Add($"방향 바뀜 {turned.Count}건 — 자리는 그대로 두고 보는 쪽만 바꿉니다: "
                              + string.Join(", ", turned.Take(8).Select(r => $"{r.Display}({FacingName(r.BaselinePlacement!.Facing)}→{FacingName(r.Facing)})"))
                              + (turned.Count > 8 ? $" 외 {turned.Count - 8}" : string.Empty));
                if (_board.Shape == WiringShape.Ring && moved.Any(r => r.BaselinePlacement is not null))
                    lines.Add("링 위치가 바뀌면 이미 기록된 장애 고장 구간 번호가 가리키는 센서가 달라집니다.");
            }
            lines.Add($"저장할 센서 {diff.ToSend.Count}대");
            return string.Join(Environment.NewLine, lines);

            static string Join(IReadOnlyList<WiringSensorRow> rows)
                => string.Join(", ", rows.Take(8).Select(r => r.Display)) + (rows.Count > 8 ? $" 외 {rows.Count - 8}" : string.Empty);

            static string Describe(WiringPlacement? placement) => placement?.Text ?? "미배치";

            string Places(IReadOnlyList<WiringSensorRow> rows)
                => string.Join(", ", rows.Take(8).Select(r => $"{r.Display}({Describe(r.BaselinePlacement)}→{Describe(_board.PlacementOf(r.Key))})"))
                   + (rows.Count > 8 ? $" 외 {rows.Count - 8}" : string.Empty);
        }
    }

    /// <summary>저장할 것이 있는가 — 서버(바뀐 줄) 또는 로컬(펜스 구성 · 저장소가 있을 때만).</summary>
    public bool HasChanges => _board.IsDirty || HasLocalChanges;
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
        // 종류가 섞였으면(스마트 1번~ · 펜스 101번~) 번호순이 공간 순서가 아닐 수 있다(O-12) — 사람이 고른 동작이라 적용하되 알린다.
        StatusText = _board.IsMixedFamily
            ? $"번호 순으로 한 줄에 배치했습니다(번호가 같으면 id 순) — {MIXED_ORDER_NOTICE}."
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
        StatusText = WiringValidation.Particles($"{name}을(를) 결선에서 뺐습니다 — 뒤 센서는 한 칸씩 당겨집니다.");
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
            ? $"{list[0].Display} → {PlacementTextOf(list[0].Key)}에 붙였습니다"
            : $"{placed}대를 결선 끝에 차례로 붙였습니다";
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

        if (target.ZoneKey == FenceZoneKey) return CanDropOnFence(payload);

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

        if (target.ZoneKey == FenceZoneKey)
        {
            DropOnFence(payload, target);
            return;
        }

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
                ? WiringValidation.Particles($"{removed[0].Display}을(를) 결선에서 뺐습니다 — 뒤 센서는 당겨집니다.")
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
    /// <summary>서버에 보낼 것이 있고 보낼 수 있는가(번호 · 순서 · 방향 · 표 값 · 그룹).</summary>
    private bool CanSaveServer => _apply is not null && _board.IsDirty;

    public bool CanSave => !IsBusy && (CanSaveServer || HasLocalChanges) && !WiringValidation.BlocksSave(Issues);

    public string? SaveBlockedReason => IsBusy ? "저장 중입니다."
        : _apply is null && !HasLocalChanges ? "현재 서버에서는 결선을 저장할 수 없습니다."
        : !_board.IsDirty && !HasLocalChanges ? "바뀐 줄이 없습니다."
        : WiringValidation.BlocksSave(Issues) ? Issues.First(i => i.Level == WiringIssueLevel.Critical).Message
        : null;

    /// <summary>
    /// [저장하기](fence-wiring-editor FR-11 · [구성 저장]) — 확인 문장(번호가 바뀌면 <b>바뀌는 번호 표 + 경고</b>)을 보이고, 예라면
    /// ① 서버: 바뀐 줄만(<c>number_device</c> · <c>spec.wiring</c> · 표 값 · 그룹) ② 로컬: 펜스 구성(망 · 설치 위치 · 번호 대역 · 간격).
    /// 기준은 <b>저장이 된 뒤에만</b> 옮긴다 — 부분 실패면 된 것만 옮기고 나머지는 Draft 로 남는다.
    /// </summary>
    public async Task SaveAsync()
    {
        if (!CanSave) return;
        var server = CanSaveServer;
        var local = _fenceStore is not null && _board.FenceLayout.IsActive
                    && (HasLocalChanges || _board.FenceLayout.IsProposed || _fenceReadFailed);

        var unplaced = _board.Unplaced.Count;
        var suggested = _board.HasPendingProposals ? _board.Proposals.Count : 0;
        var message = (server ? ChangePreview + Environment.NewLine + Environment.NewLine : string.Empty) +
            (server && unplaced > 0
                ? $"센서 {unplaced}대는 아직 결선에 없습니다 — 그 센서의 결선은 저장하지 않습니다(표 값은 저장됩니다)." + Environment.NewLine + Environment.NewLine
                : string.Empty) +
            (server && suggested > 0
                ? $"적용하지 않은 제안(번호순 · 옛 배치 변환 · 빈 자리 당김) {suggested}대의 자리는 저장하지 않습니다 — [이대로 적용] 을 먼저 누르세요." + Environment.NewLine + Environment.NewLine
                : string.Empty) +
            (local
                ? "펜스 구성(망 · 설치 위치 · 번호 대역 · 간격)을 이 PC 에 저장합니다 — 다른 GIS 에서는 보이지 않습니다."
                  + (server ? " 서버 저장이 하나라도 실패하면 펜스 구성은 이번에 저장하지 않습니다(서버 순서와 어긋나지 않게)." : string.Empty)
                  + (_fenceReadFailed ? " 이 PC 의 펜스 구성을 읽지 못해 덮어쓸지 따로 묻습니다." : string.Empty)
                  + Environment.NewLine + Environment.NewLine
                : string.Empty) +
            (server ? "저장하기 전에 각 센서가 그사이 바뀌지 않았는지 확인합니다. 저장할까요?" : "저장할까요?");

        var numberChanges = server ? _board.NumberChanges() : Array.Empty<Ironwall.Dotnet.Monitoring.Models.Fences.NumberChange>();
        var confirmed = numberChanges.Count > 0
            ? await _dialogs.ConfirmNumberChangesAsync("구성 저장", numberChanges, NUMBER_WARNING, message)
            : await _dialogs.ConfirmAsync("결선 저장", message);
        if (!confirmed) return;

        // 읽지 못한 로컬 구성(본문 손상 · 읽기 실패) — 판을 모르는 채로 저장하면 기존 행과 부딪혀 "다른 GIS 충돌" 로 오보되거나 영영 저장하지 못한다.
        // 사람이 확인하면 덮어쓴다(읽지 못한 본문은 저장소가 보관 칸에 남긴다).
        var overwrite = false;
        string? localSkipped = null;
        if (local && _fenceReadFailed)
        {
            overwrite = await _dialogs.ConfirmAsync("펜스 구성 덮어쓰기", FENCE_OVERWRITE_QUESTION);
            if (!overwrite)
            {
                local = false;
                localSkipped = "펜스 구성은 저장하지 않았습니다(덮어쓰기 취소)";
            }
        }
        if (!server && !local)
        {
            if (localSkipped is not null) StatusText = localSkipped + ".";
            return;
        }

        IsBusy = true;
        SaveResults.Clear();
        NotifyOfPropertyChange(nameof(HasSaveResults));

        try
        {
            var serverOk = true;
            if (server && _apply is not null)
            {
                var progress = new Progress<WiringProgress>(p => ProgressText = p.Total == 0 ? string.Empty : $"{p.Done}/{p.Total} · {p.Current}");
                var result = await _apply.ApplyAsync(Controller.Id, _board, progress, CancellationToken.None);
                serverOk = result.IsSuccess;

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

                // 서버에 한 줄이라도 쓰였으면 기억한다 — 창은 OS ✕ 로만 닫혀 대화 결과가 늘 비므로(false),
                // 입구(WiringLauncher)는 이 값으로 "저장했다" 를 판정해 콘솔 목록을 다시 읽고 안내를 띄운다.
                if (result.OkKeys.Count > 0 || result.Groups.Any(g => g.Ok)) HasSaved = true;
            }

            // 로컬 — 서버 뒤에(새 센서가 서버 id 를 받은 뒤라야 자리를 id 로 실을 수 있다).
            // 서버 저장이 하나라도 실패하면 로컬의 센서 자리도 이번에는 싣지 않는다 — 서버에 안 들어간 순서 · 번호로 펜스 위 자리를 굳히면
            // 다음에 열 때 로컬 자리와 서버 순서가 어긋난다. 서버에 보낼 것이 없던 저장(망만 고침)은 그대로 로컬에 저장한다.
            if (local && server && !serverOk)
            {
                local = false;
                localSkipped = "펜스 구성은 저장하지 않았습니다(서버 저장 일부 실패 — 서버 순서와 어긋나지 않게 남겨 둠 · 다시 저장하면 함께 저장)";
            }
            if (local)
            {
                var saved = await SaveFenceLayoutAsync(overwrite ? FenceLayoutSaveMode.Overwrite : FenceLayoutSaveMode.Normal);
                if (saved is not null)
                    StatusText = server ? $"{StatusText} · {saved.Message}" : saved.Message;
            }
            else if (localSkipped is not null)
            {
                StatusText = server ? $"{StatusText} · {localSkipped}." : localSkipped + ".";
            }

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

    /// <summary>미저장 변경(서버 · 로컬 펜스 구성)이 있으면 닫기 전에 묻는다.</summary>
    /// <remarks>저장한 뒤라도 그 뒤에 고친 것이 있으면 묻는다 — "저장했다" 는 끈적한 표지가 아니라 지금 남은 변경으로 판단한다.</remarks>
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_board.IsDirty && !HasLocalChanges) return true;
        var count = _board.UnsavedChangeCount + (HasLocalChanges ? 1 : 0);
        return await _dialogs.ConfirmAsync("셋업 창 닫기", $"미저장 변경 {count}건이 있습니다. 버리고 닫을까요?");
    }

    public async Task CloseAsync()
    {
        await TryCloseAsync(!_board.IsDirty && !HasLocalChanges);
    }
    #endregion

    /// <summary>테스트 · 미리보기에서 보드를 들여다본다.</summary>
    internal WiringBoard Board => _board;
}
