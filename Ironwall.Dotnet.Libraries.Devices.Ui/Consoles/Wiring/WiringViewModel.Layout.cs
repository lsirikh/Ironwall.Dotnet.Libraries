using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>속성 칸이 무엇을 보여 주나 — 마지막으로 고른 쪽(fence-wiring-editor SB B "속성 칸은 마지막으로 고른 쪽").</summary>
public enum FenceSelectionKind
{
    None = 0,
    Sensors = 1,
    Panels = 2,
    Controller = 3,
}

/// <summary>센서 접속(fence-wiring-editor FR-15) — IP 센서 · RS485 노드 주소 · 부품 health(신호등).</summary>
public sealed record WiringSensorLink(string? ConnectionType, string? IpAddress, string? Health)
{
    /// <summary>센서마다 IP 가 있는 접속인가(<c>IP_*</c>) — 스마트복합센서2 는 제어기 뒤 내부망.</summary>
    public bool IsIp => ConnectionType?.Trim().StartsWith("IP", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>오른쪽 클릭 메뉴 한 줄(FR-08) — 캔버스가 <c>MenuItem</c> 으로 그린다. <see cref="Run"/> 이 없으면 구분선.</summary>
public sealed record FenceMenuEntry(string Text, string AutomationId, Func<Task>? Run, bool IsEnabled = true, string? Gesture = null)
{
    public bool IsSeparator => Run is null;

    public static FenceMenuEntry Separator(string id) => new(string.Empty, id, null, false);
}

/// <summary>메뉴를 연 곳.</summary>
public enum FenceMenuTargetKind
{
    Empty = 0,
    Sensor = 1,
    Panel = 2,
}

/// <summary>설치 방식을 퍼뜨릴 범위(FR-08).</summary>
public enum FenceApplyScope
{
    /// <summary>이 제어기 모든 센서.</summary>
    All = 0,
    /// <summary>같은 종류 센서.</summary>
    SameType = 1,
    /// <summary>선택한 센서.</summary>
    Selected = 2,
}

/// <summary>번호 대역 직접 설정의 한 줄(갈래 · 시작 · 끝).</summary>
public sealed class WiringBandRowViewModel : PropertyChangedBase
{
    private string _start;
    private string _end;

    public WiringBandRowViewModel(FenceSensorCategory category, NumberBand? band)
    {
        Category = category;
        _start = band?.Start.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _end = band?.End.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public FenceSensorCategory Category { get; }
    public string CategoryText => $"{NumberingMath.CategoryText(Category)}센서";
    public string AutomationKey => Category.ToString();

    public string StartText { get => _start; set { _start = value ?? string.Empty; NotifyOfPropertyChange(); } }
    public string EndText { get => _end; set { _end = value ?? string.Empty; NotifyOfPropertyChange(); } }

    /// <summary>둘 다 비었으면 대역 없음(<c>null</c>) · 읽을 수 없으면 <paramref name="error"/>.</summary>
    internal NumberBand? Parse(out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(_start) && string.IsNullOrWhiteSpace(_end)) return null;
        if (!int.TryParse(_start.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(_end.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            error = $"{CategoryText} 대역은 숫자 두 개(시작 · 끝)로 적어 주세요.";
            return null;
        }
        if (start < 1 || end > NumberingMath.MAX_NUMBER || end < start)
        {
            error = $"{CategoryText} 대역은 1~{NumberingMath.MAX_NUMBER} 안에서 시작 ≤ 끝이어야 합니다.";
            return null;
        }
        return new NumberBand(Category, start, end);
    }
}

/// <summary>
/// 결선 펜스 편집기(fence-wiring-editor) — 펜스 구성(망 · 설치 자리 · 번호 대역) · 선택 하나로 · 망 속성 · 설치 위치 · 오른쪽 클릭 메뉴 · 로컬 저장.
/// </summary>
/// <remarks>
/// <para>판정은 보드(<see cref="WiringBoard"/> · <see cref="WiringFenceLayout"/>)와 순수 함수(<see cref="FenceLayoutMath"/> · <see cref="NumberingMath"/>)에 있다 —
/// 여기서는 고른 것을 모아 보드에 넘기고 한 걸음씩 되돌리기를 찍는다.</para>
/// </remarks>
public sealed partial class WiringViewModel
{
    public const string FENCE_PROPOSED_NOTICE = "저장된 펜스 구성이 없어 지금 배치에서 망을 제안했습니다 — [저장하기] 를 누르면 이 PC 에 저장됩니다";
    public const string FENCE_REORDERED_NOTICE = "펜스 위 자리를 서버 결선 순서에 맞췄습니다 — 확인 후 저장하세요";
    public const string FENCE_RESEATED_NOTICE = "펜스 위 자리가 없던 센서를 이웃 사이에 놓았습니다 — 확인 후 저장하세요";
    public const string NUMBER_WARNING = "현장 센서의 번호 설정과 같아야 합니다";
    public const string FENCE_READ_FAILED_NOTICE = "이 PC 의 펜스 구성을 읽지 못했습니다 — 지금 보이는 것은 제안 구성입니다. [저장하기] 때 덮어쓸지 묻습니다(읽지 못한 본문은 보관)";
    public const string FENCE_OVERWRITE_QUESTION = "이 PC 의 펜스 구성을 읽지 못했습니다(본문 손상 또는 로컬 DB 읽기 실패).\n"
                                                 + "지금 화면의 펜스 구성으로 덮어쓸까요? 읽지 못한 이전 본문은 지우지 않고 보관합니다.\n"
                                                 + "[취소] 를 누르면 서버 저장만 하고 펜스 구성은 저장하지 않습니다.";
    public const string NO_BANDS_NOTICE = "번호 대역 미설정 — 위치대로 번호를 매기려면";

    /// <summary>고른 것마다 값이 다른 칸(망 속성 · 높이 · 거리).</summary>
    private const string MULTI_VALUE = "여러 값";

    private readonly IFenceLayoutStore? _fenceStore;
    private readonly FenceLayoutDocument? _fenceDocument;
    private readonly IPingProbe? _ping;
    private readonly FenceLayoutKey _fenceKey;
    private bool _fenceReadFailed;
    private string? _editPanelNumber;
    private readonly Dictionary<int, WiringSensorLink> _links = new();
    private readonly List<int> _panelSelection = new();
    private int _fenceRevision;
    private string? _fenceNotice;
    private FenceSelectionKind _selectionKind;
    private FencePanelSpec? _copiedPanel;
    private bool _syncingTableSelection;

    // 망 속성 칸의 고친 값(적용 전) — null 이면 손대지 않음
    private EnumFenceStyle? _editStyle;
    private string? _editColor;
    private bool _editColorTouched;
    private string? _editHeight;
    private string? _editSpan;
    private string? _editOffset;

    #region - Load -
    private void LoadLinks(IEnumerable<WiringSensorSeed> seeds)
    {
        _links.Clear();
        foreach (var seed in seeds.Where(s => s.Id > 0))
            _links[seed.Id] = new WiringSensorLink(seed.ConnectionType, seed.IpAddress, seed.LinkHealth);
    }

    /// <summary>
    /// 펜스 구성을 싣는다(FR-01) — 로컬에 저장된 것이 있으면 그것을 <b>서버 체인 순서</b>에 맞춰(서버가 순서의 정본), 없으면 지금 배치에서 <b>제안</b>한다
    /// (자동 저장 안 함 · 바뀐 줄 아님). 번호는 매기지 않는다 — 불러오기만으로 바뀐 줄이 생기지 않게.
    /// </summary>
    private void LoadFence(FenceLayoutDocument? document)
    {
        if (document is not null) _fenceRevision = document.Revision;      // 읽기 실패면 저장소가 본 행 판(모르면 0)을 그대로 둔다
        _fenceNotice = null;
        var chain = _board.Chain.Keys;

        if (document is { Panels.Count: > 0 })
        {
            if (document.FenceSpacingM is { } spacing) _board.SetFenceSpacing(spacing);
            var known = document.Mounts
                .Where(p => chain.Contains(p.Key) && _board.Find(p.Key) is { Id: > 0 })
                .ToDictionary(p => p.Key, p => p.Value);
            var baseline = WiringFenceLayout.Create(document.Panels, known, document.Bands, controllerEnd: document.ControllerEnd, vbusGap: document.VbusGap);
            var storedOrder = FenceLayoutMath.ChainOrder(baseline.Mounts.Select(p => (p.Key, p.Value)), document.ControllerEnd, chain);
            var (mounts, panels) = FenceLayoutMath.Reconcile(storedOrder, chain, baseline.Mounts, baseline.Panels, _board.CategoryOf, document.ControllerEnd);
            var layout = WiringFenceLayout.Create(panels, mounts, document.Bands, controllerEnd: document.ControllerEnd, vbusGap: document.VbusGap);
            if (!storedOrder.SequenceEqual(chain.Where(baseline.Mounts.ContainsKey))) _fenceNotice = FENCE_REORDERED_NOTICE;
            else if (!layout.SameContent(baseline)) _fenceNotice = FENCE_RESEATED_NOTICE;
            _board.LoadFenceLayout(layout, baseline);
            return;
        }

        var slots = FenceSlotLayout.Build(_board.Chain, k => WiringTopology.ParseSensorType(_board.Find(k)?.Facts.TypeText),
                                          new FenceLayoutOptions { PixelsPerMetre = 1, Spacing = _board.Spacing });
        var info = slots.Slots.Select(s => (s.Key, _board.CategoryOf(s.Key), s.Metres)).ToList();
        var (proposedPanels, proposedMounts) = FenceLayoutMath.Propose(info, _board.Spacing.FenceMetres);
        _board.LoadFenceLayout(WiringFenceLayout.Create(proposedPanels, proposedMounts, null, isProposed: true));
    }
    #endregion

    #region - Layout facts -
    /// <summary>지금 펜스 구성(캔버스 · 개념도가 그린다).</summary>
    public WiringFenceLayout FenceLayout => _board.FenceLayout;

    /// <summary>로컬 저장소가 있는가 — 없으면 로컬 저장 칸(망 · 설치 위치 · 대역을 이 PC 에 저장)을 숨긴다.</summary>
    public bool HasFenceStore => _fenceStore is not null;

    /// <summary>로컬에 저장할 것이 있는가(저장소가 있을 때만 센다).</summary>
    public bool HasLocalChanges => _fenceStore is not null && _board.IsFenceDirty;

    /// <summary>불러올 때 알릴 것(제안 · 서버 순서에 맞춤).</summary>
    public string FenceNoticeText
        => _fenceReadFailed && HasFenceStore ? FENCE_READ_FAILED_NOTICE
         : _fenceNotice ?? (_board.FenceLayout.IsProposed
            ? (HasFenceStore ? FENCE_PROPOSED_NOTICE : "펜스 구성은 이 창에서만 쓰입니다 — 이 PC 에 저장할 곳(로컬 DB)이 없습니다")
            : string.Empty);

    public bool HasFenceNotice => FenceNoticeText.Length > 0;

    /// <summary>그 센서의 접속(없으면 모름).</summary>
    public WiringSensorLink LinkOf(int key) => _board.Find(key) is { Id: > 0 } row && _links.TryGetValue(row.Id, out var link) ? link : new WiringSensorLink(null, null, null);

    /// <summary>IP 센서인가(FR-15).</summary>
    public bool IsIpSensor(int key) => LinkOf(key).IsIp;

    /// <summary>주소 칸 글자 — IP 센서는 IP(없으면 "내부망"), 그 밖은 노드 주소(<c>connection.channel</c>).</summary>
    public string AddressTextOf(int key)
    {
        if (_board.Find(key) is not { } row) return string.Empty;
        var link = LinkOf(key);
        if (link.IsIp) return string.IsNullOrWhiteSpace(link.IpAddress) ? "내부망" : link.IpAddress!.Trim();
        return row.Channel is { } channel ? channel.ToString(CultureInfo.InvariantCulture) : "—";
    }

    /// <summary>주소 칸 이름 — "IP 주소" · "노드 주소".</summary>
    public string AddressLabelOf(int key) => IsIpSensor(key) ? "IP 주소" : "노드 주소";

    /// <summary>센서 신호등(FR-14) — 매니저가 보고한 <c>NETWORK_INTERFACE</c> 부품 health. 보고가 없으면 모름.</summary>
    public SignalLevel SensorSignal(int key) => SignalMath.FromHealth(LinkOf(key).Health);

    /// <summary>"망 12칸 · 길이 72m / 기준 200m".</summary>
    public string PanelTotalText
    {
        get
        {
            var layout = _board.FenceLayout;
            var reference = _board.Limits.ReferenceLength(_board.Family) is { } m ? $"기준 {m:0}m" : "기준 —";
            return $"망 {layout.Panels.Count}칸 · 길이 {layout.Geometry.LengthM:0.#}m / {reference}";
        }
    }
    #endregion

    #region - Selection (FR-04 · FR-05 · FR-13) -
    /// <summary>속성 칸이 보여 주는 쪽 — 마지막으로 고른 것.</summary>
    public FenceSelectionKind FencePaneKind => _selectionKind;

    /// <summary>고른 망(고른 차례).</summary>
    public IReadOnlyList<int> FenceSelectedPanels => _panelSelection;

    public bool IsPanelSelected(int index) => _panelSelection.Contains(index);

    public bool HasPanelSelection => _selectionKind == FenceSelectionKind.Panels && _panelSelection.Count > 0;

    /// <summary>센서든 망이든 무언가 골라져 있는가.</summary>
    public bool HasAnySelection => _fenceSelection.Count > 0 || _panelSelection.Count > 0 || _isControllerSelected;

    /// <summary>망 하나만 고른다(클릭).</summary>
    public void FenceSelectPanel(int? index)
    {
        _panelSelection.Clear();
        if (index is { } i && i >= 0 && i < _board.FenceLayout.Panels.Count) _panelSelection.Add(i);
        _isControllerSelected = false;
        _selectionKind = _panelSelection.Count > 0 ? FenceSelectionKind.Panels : KindAfterClear();
        OnPanelSelectionChanged();
    }

    /// <summary>Ctrl+클릭 · Ctrl+Space — 망 하나를 더하거나 뺀다.</summary>
    public void FenceTogglePanel(int index)
    {
        if (index < 0 || index >= _board.FenceLayout.Panels.Count) return;
        if (!_panelSelection.Remove(index)) _panelSelection.Add(index);
        _isControllerSelected = false;
        _selectionKind = _panelSelection.Count > 0 ? FenceSelectionKind.Panels : KindAfterClear();
        OnPanelSelectionChanged();
    }

    /// <summary>망 선택 사각형(Shift+끌기) · 망 모두 선택 — <paramref name="additive"/>(Ctrl)면 더한다.</summary>
    public void FenceSelectPanels(IEnumerable<int> indexes, bool additive = false)
    {
        var valid = (indexes ?? Enumerable.Empty<int>()).Where(i => i >= 0 && i < _board.FenceLayout.Panels.Count).ToList();
        var merged = FenceRubberBand.Merge(_panelSelection.ToList(), valid, additive);
        _panelSelection.Clear();
        _panelSelection.AddRange(merged);
        _isControllerSelected = false;
        _selectionKind = _panelSelection.Count > 0 ? FenceSelectionKind.Panels : KindAfterClear();
        OnPanelSelectionChanged();
        if (_panelSelection.Count > 0) StatusText = $"망 {_panelSelection.Count}칸 선택 — 오른쪽 속성 칸에서 고칩니다";
    }

    /// <summary>센서 선택 사각형 · 표 · 개념도 · 모두 선택 — <paramref name="additive"/>(Ctrl)면 더한다.</summary>
    public void FenceSelectSensors(IEnumerable<int> keys, bool additive = false)
    {
        var valid = (keys ?? Enumerable.Empty<int>()).Where(k => _board.Find(k) is not null).ToList();
        var merged = FenceRubberBand.Merge(_fenceSelection.ToList(), valid, additive);
        _fenceSelection.Clear();
        _fenceSelection.AddRange(merged);
        _fenceSelectedKey = _fenceSelection.Count > 0 ? _fenceSelection[^1] : null;
        _isControllerSelected = false;
        _selectionKind = _fenceSelection.Count > 0 ? FenceSelectionKind.Sensors : KindAfterClear();
        RaiseSelection();
        if (_fenceSelection.Count > 1) StatusText = $"센서 {_fenceSelection.Count}대 선택 — 함께 끌거나 오른쪽 클릭으로 설치 방식을 적용합니다";
    }

    /// <summary>Ctrl+A — 체인 센서 모두.</summary>
    public void FenceSelectAllSensors() => FenceSelectSensors(_board.Chain.Keys);

    /// <summary>망 모두.</summary>
    public void FenceSelectAllPanels() => FenceSelectPanels(Enumerable.Range(0, _board.FenceLayout.Panels.Count));

    /// <summary>Shift+←/→ — 기준 센서에서 <paramref name="toKey"/> 까지 체인 범위를 고른다.</summary>
    public void FenceExtendSensorSelection(int toKey)
    {
        var chain = _board.Chain.Keys.ToList();
        var anchor = _fenceSelection.Count > 0 ? _fenceSelection[0] : toKey;
        int a = chain.IndexOf(anchor), b = chain.IndexOf(toKey);
        if (a < 0 || b < 0) { FenceSelect(toKey); return; }
        var range = chain.Skip(Math.Min(a, b)).Take(Math.Abs(a - b) + 1).ToList();
        if (b < a) range.Reverse();
        range.Remove(anchor);
        range.Insert(0, anchor);                         // 기준은 늘 첫 자리(다음 Shift+화살표의 기준)
        FenceSelectSensors(range);
    }

    /// <summary>Shift+←/→ — 기준 망에서 <paramref name="toIndex"/> 까지 망 범위를 고른다.</summary>
    public void FenceExtendPanelSelection(int toIndex)
    {
        var anchor = _panelSelection.Count > 0 ? _panelSelection[0] : toIndex;
        var range = Enumerable.Range(Math.Min(anchor, toIndex), Math.Abs(anchor - toIndex) + 1).ToList();
        range.Remove(anchor);
        range.Insert(0, anchor);
        FenceSelectPanels(range);
    }

    /// <summary>선택을 모두 푼다(빈 곳 클릭 · Esc). 풀 것이 있었으면 <c>true</c>.</summary>
    public bool FenceClearSelection()
    {
        var had = HasAnySelection;
        _fenceSelection.Clear();
        _fenceSelectedKey = null;
        _panelSelection.Clear();
        _isControllerSelected = false;
        _selectionKind = FenceSelectionKind.None;
        ResetPanelEdit();
        RaiseSelection();
        OnPanelSelectionChanged();
        return had;
    }

    /// <summary>표 보기에서 고른 줄 → 같은 선택(FR-13).</summary>
    public void SelectFromTable(IEnumerable<int> keys)
    {
        if (_syncingTableSelection) return;
        var list = (keys ?? Enumerable.Empty<int>()).ToList();
        if (list.SequenceEqual(_fenceSelection)) return;
        FenceSelectSensors(list);
    }

    private FenceSelectionKind KindAfterClear()
        => _fenceSelection.Count > 0 ? FenceSelectionKind.Sensors : _panelSelection.Count > 0 ? FenceSelectionKind.Panels : FenceSelectionKind.None;

    /// <summary>센서 선택이 바뀌면 표 보기의 고른 칸도 같게(FR-13).</summary>
    private void SyncTableSelection()
    {
        _syncingTableSelection = true;
        try
        {
            foreach (var slot in Line1.Concat(Line2))
            {
                var selected = slot.Row is not null && _fenceSelection.Contains(slot.Row.Key);
                if (slot.IsSelected != selected) slot.IsSelected = selected;
            }
        }
        finally { _syncingTableSelection = false; }
    }

    private void OnPanelSelectionChanged()
    {
        ResetPanelEdit();
        RaisePanelPane();
        NotifyOfPropertyChange(nameof(FencePaneKind));
        NotifyOfPropertyChange(nameof(HasFenceSelection));
        NotifyOfPropertyChange(nameof(HasNoFenceSelection));
        NotifyOfPropertyChange(nameof(HasAnySelection));
        FenceChanged?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    #region - Panel pane (FR-03) -
    /// <summary>"선택한 망 3칸".</summary>
    public string PanelSelectionTitle => $"선택한 망 {_panelSelection.Count}칸";

    private IReadOnlyList<FencePanelSpec> SelectedPanelSpecs
        => _panelSelection.Where(i => i >= 0 && i < _board.FenceLayout.Panels.Count).Select(i => _board.FenceLayout.Panels[i]).ToList();

    private static T? Common<T>(IReadOnlyList<FencePanelSpec> specs, Func<FencePanelSpec, T> pick)
        => specs.Count > 0 && specs.Select(pick).Distinct().Count() == 1 ? pick(specs[0]) : default;

    /// <summary>판 종류 — 고친 값, 없으면 고른 망이 모두 같을 때 그 값(여러 값이면 <c>null</c>).</summary>
    public EnumFenceStyle? PanelStyleValue
        => _editStyle ?? (SelectedPanelSpecs is { Count: > 0 } s && s.Select(p => p.Style).Distinct().Count() == 1 ? s[0].Style : null);

    public bool IsPanelStyleChainLink => PanelStyleValue == EnumFenceStyle.ChainLink;
    public bool IsPanelStyleRazor => PanelStyleValue == EnumFenceStyle.ChainLinkRazor;
    public bool IsPanelStyleBrick => PanelStyleValue == EnumFenceStyle.Brick;
    public bool IsPanelStyleConcrete => PanelStyleValue == EnumFenceStyle.Concrete;
    public bool IsPanelStyleDesign => PanelStyleValue == EnumFenceStyle.DesignFence;

    /// <summary>판 종류 줄 아래 글 — "철조망" · "여러 값".</summary>
    public string PanelStyleText => PanelStyleValue is { } style ? FencePanelSpec.StyleText(style) : MULTI_VALUE;

    /// <summary>색 — "기본색" · "#4E565E" · "여러 값".</summary>
    public string PanelColorText
    {
        get
        {
            if (_editColorTouched) return _editColor ?? "기본색";
            var specs = SelectedPanelSpecs;
            if (specs.Count == 0) return string.Empty;
            return specs.Select(p => p.Color).Distinct().Count() == 1 ? specs[0].Color ?? "기본색" : MULTI_VALUE;
        }
    }

    /// <summary>높이 칸(m) — 고친 글자, 없으면 공통 값, 여러 값이면 빈 칸(힌트가 "여러 값").</summary>
    public string PanelHeightText
    {
        get => _editHeight ?? (Common(SelectedPanelSpecs, p => (double?)p.HeightM) is { } h ? h.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty);
        set { _editHeight = value ?? string.Empty; RaisePanelPane(); }
    }

    public string PanelHeightHint => Common(SelectedPanelSpecs, p => (double?)p.HeightM) is null && SelectedPanelSpecs.Count > 1 ? MULTI_VALUE : "0.5~6.0";

    /// <summary>망 간 거리 칸(m).</summary>
    public string PanelSpanText
    {
        get => _editSpan ?? (Common(SelectedPanelSpecs, p => (double?)p.SpanM) is { } s ? s.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty);
        set { _editSpan = value ?? string.Empty; RaisePanelPane(); }
    }

    public string PanelSpanHint => Common(SelectedPanelSpecs, p => (double?)p.SpanM) is null && SelectedPanelSpecs.Count > 1 ? MULTI_VALUE : "직접 입력 (0.5~20m)";

    private double? PanelSpanValue => double.TryParse(PanelSpanText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    public bool IsPanelSpan1 => PanelSpanValue is 1.0;
    public bool IsPanelSpan2 => PanelSpanValue is 2.0;
    public bool IsPanelSpan3 => PanelSpanValue is 3.0;
    public bool IsPanelSpan4 => PanelSpanValue is 4.0;
    public bool IsPanelSpan5 => PanelSpanValue is 5.0;
    public bool IsPanelSpan6 => PanelSpanValue is 6.0;

    /// <summary>고친 칸이 있는가.</summary>
    public bool HasPanelEdit => _editStyle is not null || _editColorTouched || _editHeight is not null || _editSpan is not null;

    /// <summary>고친 값의 까닭(범위 밖 · 숫자 아님). 없으면 <c>null</c>.</summary>
    public string? PanelEditError
    {
        get
        {
            if (_editHeight is { } h && (!double.TryParse(h, NumberStyles.Float, CultureInfo.InvariantCulture, out var hv) || !FencePanelSpec.IsValidHeight(hv)))
                return $"높이는 {FencePanelSpec.MIN_HEIGHT_M}~{FencePanelSpec.MAX_HEIGHT_M}m 로 적어 주세요.";
            if (_editSpan is { } s && (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var sv) || !FencePanelSpec.IsValidSpan(sv)))
                return $"망 간 거리는 {FencePanelSpec.MIN_SPAN_M}~{FencePanelSpec.MAX_SPAN_M}m 로 적어 주세요.";
            return null;
        }
    }

    public bool HasPanelEditError => PanelEditError is not null;

    public bool CanApplyPanelEdit => !IsBusy && HasPanelSelection && HasPanelEdit && !HasPanelEditError;

    /// <summary>"선택한 망에 적용 (3칸)".</summary>
    public string ApplyPanelEditText => $"선택한 망에 적용 ({_panelSelection.Count}칸)";

    public void ChoosePanelStyle(EnumFenceStyle style) { _editStyle = style; RaisePanelPane(); }

    /// <summary>색 견본 · 직접 입력 — <c>null</c> 이면 기본색.</summary>
    public void ChoosePanelColor(string? color)
    {
        _editColorTouched = true;
        _editColor = FencePanelSpec.NormalizeColor(color);
        RaisePanelPane();
    }

    /// <summary>[직접…] — 색 글자(#RRGGBB)를 묻는다.</summary>
    public async Task AskPanelColorAsync()
    {
        var text = await _dialogs.AskTextAsync("망 색 직접 입력", "색(#RRGGBB) — 비우면 기본색", _editColor ?? SelectedPanelSpecs.FirstOrDefault()?.Color ?? string.Empty);
        if (text is null) return;
        if (!string.IsNullOrWhiteSpace(text) && FencePanelSpec.NormalizeColor(text) is null)
        {
            StatusText = "색은 #RRGGBB 로 적어 주세요(예: #4E565E).";
            return;
        }
        ChoosePanelColor(text);
    }

    public void ChoosePanelSpan(double metres) { _editSpan = metres.ToString("0.##", CultureInfo.InvariantCulture); RaisePanelPane(); }

    public void CancelPanelEdit() { ResetPanelEdit(); RaisePanelPane(); }

    /// <summary>[선택한 망에 적용] — 고친 칸만 고른 망 전부에(FR-03). 되돌리기 한 걸음.</summary>
    public bool ApplyPanelEdit()
    {
        if (!CanApplyPanelEdit) return false;
        var height = _editHeight is { } h ? double.Parse(h, NumberStyles.Float, CultureInfo.InvariantCulture) : (double?)null;
        var span = _editSpan is { } s ? double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture) : (double?)null;
        var targets = _panelSelection.ToHashSet();
        var count = targets.Count;
        int raised = 0, lowered = 0;
        var ok = EditFence(layout => FollowRazor(layout, layout.WithPanels(layout.Panels.Select((p, i) =>
        {
            if (!targets.Contains(i)) return p;
            var next = p;
            if (_editStyle is { } style) next = next with { Style = style };
            if (_editColorTouched) next = next with { Color = _editColor };
            if (height is { } hv) next = next with { HeightM = hv };
            if (span is { } sv) next = next with { SpanM = sv };
            return next;
        })), out raised, out lowered));
        ResetPanelEdit();
        RaisePanelPane();
        StatusText = ok ? $"망 {count}칸에 적용했습니다{RazorFollowText(raised, lowered)} — Ctrl+Z 로 되돌립니다" : "바뀐 것이 없습니다.";
        return ok;
    }

    private void ResetPanelEdit()
    {
        _editStyle = null;
        _editColor = null;
        _editColorTouched = false;
        _editHeight = null;
        _editSpan = null;
    }

    private void RaisePanelPane()
    {
        foreach (var name in new[]
        {
            nameof(HasPanelSelection), nameof(PanelSelectionTitle), nameof(FenceSelectedPanels), nameof(PanelStyleValue),
            nameof(IsPanelStyleChainLink), nameof(IsPanelStyleRazor), nameof(IsPanelStyleBrick), nameof(IsPanelStyleConcrete), nameof(IsPanelStyleDesign),
            nameof(PanelStyleText), nameof(PanelColorText), nameof(PanelHeightText), nameof(PanelHeightHint), nameof(PanelSpanText), nameof(PanelSpanHint),
            nameof(IsPanelSpan1), nameof(IsPanelSpan2), nameof(IsPanelSpan3), nameof(IsPanelSpan4), nameof(IsPanelSpan5), nameof(IsPanelSpan6),
            nameof(HasPanelEdit), nameof(PanelEditError), nameof(HasPanelEditError), nameof(CanApplyPanelEdit), nameof(ApplyPanelEditText),
            nameof(PanelTotalText),
        }) NotifyOfPropertyChange(name);
    }
    #endregion

    #region - Sensor mount pane (FR-07) -
    /// <summary>자리를 고칠 센서 — 고른 것 중 체인에 있는 것(고른 차례).</summary>
    private IReadOnlyList<int> MountTargets()
        => (_fenceSelection.Count > 0 ? _fenceSelection.ToList() : _fenceSelectedKey is { } k ? new List<int> { k } : new List<int>())
            .Where(k => _board.FenceLayout.MountOf(k) is not null).ToList();

    /// <summary>설치 위치 칸을 보일까 — 펜스 구성이 켜져 있고 체인 센서를 골랐을 때.</summary>
    public bool HasMountRow => _board.FenceLayout.IsActive && _selectionKind == FenceSelectionKind.Sensors && MountTargets().Count > 0;

    /// <summary>첫 센서가 담 위에 있는가 — 자리 단추가 "담 위 · 담 앞면" 으로 바뀐다.</summary>
    public bool IsMountOnWall => MountTargets().FirstOrDefault() is var k && _board.FenceLayout.MountOf(k) is { } m && SensorMountSpec.IsWall(m.Spot);

    public bool IsMountOnFence => !IsMountOnWall;

    /// <summary>고른 센서의 자리가 모두 같으면 그것.</summary>
    public FenceMountSpot? SelectedSpot
        => MountTargets().Select(k => _board.FenceLayout.MountOf(k)!.Spot).Distinct().ToList() is { Count: 1 } one ? one[0] : null;

    public bool IsSpotPostTop => SelectedSpot == FenceMountSpot.PostTop;
    public bool IsSpotPostMiddle => SelectedSpot == FenceMountSpot.PostMiddle;
    public bool IsSpotPanelCenter => SelectedSpot == FenceMountSpot.PanelCenter;
    public bool IsSpotWallTop => SelectedSpot == FenceMountSpot.WallTop;
    public bool IsSpotWallFace => SelectedSpot == FenceMountSpot.WallFace;
    public bool IsSpotPanelBottom => SelectedSpot == FenceMountSpot.PanelBottom;
    public bool IsSpotRazorCoil => SelectedSpot == FenceMountSpot.RazorCoil;

    /// <summary>첫 센서가 윤형 망(기둥이면 양옆 중 하나) 위에 있는가 — [윤형 코일] 단추를 보인다.</summary>
    public bool IsMountOnRazor => MountTargets().FirstOrDefault() is var k && _board.FenceLayout.MountOf(k) is { } m
                                  && FenceLayoutMath.HeightStops(m, _board.FenceLayout.Panels).Contains(FenceMountSpot.RazorCoil);

    /// <summary>"망 3 · 기둥 위" · "기둥 4 · 기둥 위" · "센서 3대".</summary>
    public string MountPlaceText
    {
        get
        {
            var targets = MountTargets();
            if (targets.Count != 1 || _board.FenceLayout.MountOf(targets[0]) is not { } m) return targets.Count > 1 ? $"센서 {targets.Count}대" : string.Empty;
            return $"{(m.IsPostSpot ? "기둥" : "망")} {m.Panel + 1} · {SensorMountSpec.SpotText(m.Spot)}";
        }
    }

    /// <summary>높이 조정 칸(m) — 고친 글자, 없으면 공통 값.</summary>
    public string MountOffsetText
    {
        get => _editOffset ?? (MountTargets().Select(k => _board.FenceLayout.MountOf(k)!.HeightOffsetM).Distinct().ToList() is { Count: 1 } one
                ? one[0].ToString("0.##", CultureInfo.InvariantCulture) : string.Empty);
        set { _editOffset = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>[기둥 위] · [기둥 중간] · [망 가운데] · [망 아래] · [윤형 코일] · [담 위] · [담 앞면] — 고른 센서 모두. 되돌리기 한 걸음(윤형 코일은 위 줄).</summary>
    public bool ChooseMountSpot(FenceMountSpot spot)
    {
        var targets = MountTargets();
        if (targets.Count == 0 || IsBusy) return false;
        var ok = EditFence(layout =>
        {
            var mounts = new Dictionary<int, SensorMountSpec>(layout.Mounts);
            foreach (var key in targets)
            {
                var current = mounts[key];
                var (next, _) = FenceLayoutMath.ApplyMountStyle(mounts, new[] { key }, spot, current.HeightOffsetM, layout.Panels);
                mounts[key] = next[key];
            }
            return layout.WithMounts(mounts);
        });
        StatusText = ok ? $"설치 위치 — {(targets.Count > 1 ? $"{targets.Count}대" : _board.Find(targets[0])?.Display)}: {SensorMountSpec.SpotText(spot)} · Ctrl+Z 로 되돌립니다"
                        : "바뀐 것이 없습니다.";
        return ok;
    }

    /// <summary>높이 조정 적용(Enter · 칸을 떠날 때).</summary>
    public bool ApplyMountOffset()
    {
        var text = _editOffset;
        _editOffset = null;
        if (text is null) return false;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var offset)
            || offset < SensorMountSpec.MIN_OFFSET_M || offset > SensorMountSpec.MAX_OFFSET_M)
        {
            StatusText = $"높이 조정은 {SensorMountSpec.MIN_OFFSET_M}~+{SensorMountSpec.MAX_OFFSET_M}m 로 적어 주세요.";
            NotifyOfPropertyChange(nameof(MountOffsetText));
            return false;
        }
        var targets = MountTargets();
        var ok = EditFence(layout => layout.WithMounts(layout.Mounts.ToDictionary(p => p.Key,
            p => targets.Contains(p.Key) ? p.Value with { HeightOffsetM = SensorMountSpec.ClampOffset(offset) } : p.Value)));
        StatusText = ok ? $"높이 조정 {offset:+0.##;-0.##;0}m — {targets.Count}대 · Ctrl+Z 로 되돌립니다" : "바뀐 것이 없습니다.";
        return ok;
    }

    /// <summary>
    /// "망 이동" 번호 칸(1부터) — 고친 글자, 없으면 고른 센서의 망(기둥) 번호가 모두 같을 때 그 값. 기둥 센서는 기둥 번호, 망 · 담 센서는 망 번호다.
    /// </summary>
    public string MountPanelText
    {
        get => _editPanelNumber ?? (MountTargets().Select(k => _board.FenceLayout.MountOf(k)!.Panel).Distinct().ToList() is { Count: 1 } one
                ? (one[0] + 1).ToString(CultureInfo.InvariantCulture) : string.Empty);
        set { _editPanelNumber = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>"망 이동" 칸 이름 — 기둥 센서도 같은 말(검토: 시나리오마다 "기둥 이동" · "망 이동" 으로 갈렸다).</summary>
    public string MountPanelLabel => "망 이동";

    /// <summary>[◀] — 고른 센서를 앞(Ch1(A) 쪽) 망(기둥)으로 한 칸(Ctrl+← · Alt+Shift+←). 빈 망도 건너뛰지 않는다.</summary>
    public bool FenceMoveSelectedToPreviousPanel() => FenceMoveSelectedByPanels(-1);

    /// <summary>[▶] — 고른 센서를 뒤(Ch2(B) 쪽) 망(기둥)으로 한 칸(Ctrl+→ · Alt+Shift+→).</summary>
    public bool FenceMoveSelectedToNextPanel() => FenceMoveSelectedByPanels(1);

    /// <summary>
    /// 고른 센서를 망(기둥) <paramref name="delta"/> 칸 옮긴다 — 끌어 옮기기(FR-05)의 키보드 · 단추 대신. 함께 고른 센서는 같은 칸 수만큼(서로 간격 유지),
    /// 빈 망도 한 칸으로 센다(건너뛰지 않는다). 옮긴 자리대로 체인 · 번호가 다시 선다. 되돌리기 한 걸음.
    /// </summary>
    public bool FenceMoveSelectedByPanels(int delta)
    {
        var targets = MountTargets();
        if (targets.Count == 0 || delta == 0) return false;
        var grabbed = _fenceSelectedKey is { } k && targets.Contains(k) ? k : targets[0];
        return MoveSensorsByPanels(targets, grabbed, delta);
    }

    /// <summary>번호 칸 적용(Enter · 칸을 떠날 때) — 기준 센서를 그 망(기둥) 번호로, 함께 고른 센서는 같은 칸 수만큼.</summary>
    public bool ApplyMountPanel()
    {
        var text = _editPanelNumber;
        _editPanelNumber = null;
        NotifyOfPropertyChange(nameof(MountPanelText));
        if (text is null) return false;
        var targets = MountTargets();
        if (targets.Count == 0) return false;
        var grabbed = _fenceSelectedKey is { } k && targets.Contains(k) ? k : targets[0];
        var mount = _board.FenceLayout.MountOf(grabbed)!;
        var max = mount.IsPostSpot ? _board.FenceLayout.Panels.Count + 1 : _board.FenceLayout.Panels.Count;
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < 1 || number > max)
        {
            StatusText = $"{(mount.IsPostSpot ? "기둥" : "망")} 번호는 1~{max} 로 적어 주세요.";
            return false;
        }
        return MoveSensorsByPanels(targets, grabbed, number - 1 - mount.Panel);
    }

    private void RaiseMountPane()
    {
        foreach (var name in new[]
        {
            nameof(HasMountRow), nameof(IsMountOnWall), nameof(IsMountOnFence), nameof(SelectedSpot), nameof(IsSpotPostTop), nameof(IsSpotPostMiddle),
            nameof(IsSpotPanelCenter), nameof(IsSpotWallTop), nameof(IsSpotWallFace), nameof(IsSpotPanelBottom), nameof(IsSpotRazorCoil), nameof(IsMountOnRazor),
            nameof(MountPlaceText), nameof(MountOffsetText),
            nameof(MountPanelText), nameof(MountPanelLabel),
        }) NotifyOfPropertyChange(name);
        RaiseLanePane();
    }
    #endregion

    #region - Fence moves (FR-05) -
    /// <summary>
    /// 센서를 끌어 다른 망(기둥)으로 옮긴다 — 잡은 센서가 <paramref name="targetMetres"/>(m) 아래 칸으로 가고, 함께 끈 센서는 같은 칸 수만큼 옮긴다
    /// (서로 간격을 지킨다). 옮긴 자리대로 체인 · 번호가 다시 선다. 되돌리기 한 걸음.
    /// </summary>
    public bool FenceMoveSensors(IReadOnlyList<int> keys, int grabbedKey, double targetMetres)
    {
        if (IsBusy || keys is null || keys.Count == 0) return false;
        var layout = _board.FenceLayout;
        if (!layout.IsActive || layout.MountOf(grabbedKey) is not { } grabbed) return false;
        var delta = FenceLayoutMath.IndexAt(grabbed, layout.Geometry, targetMetres) - grabbed.Panel;
        return MoveSensorsByPanels(keys, grabbedKey, delta);
    }

    /// <summary>센서를 망(기둥) <paramref name="delta"/> 칸 옮기는 한 길 — 끌기 · 단추 · 키보드 · 번호 칸이 함께 쓴다.</summary>
    private bool MoveSensorsByPanels(IReadOnlyList<int> keys, int grabbedKey, int delta)
    {
        if (IsBusy || keys is null || keys.Count == 0) return false;
        var layout = _board.FenceLayout;
        if (!layout.IsActive || layout.MountOf(grabbedKey) is null) return false;
        if (delta == 0)
        {
            StatusText = "제자리 — 바뀐 것이 없습니다.";
            return false;
        }
        var moving = keys.Where(k => layout.MountOf(k) is not null).ToList();
        // 이미 센서가 있는 자리에 놓으면 — 오른쪽으로 옮겼으면 그 센서들 뒤, 왼쪽이면 앞(끈 방향으로 한 칸 더 간 것으로 읽는다).
        var rest = _board.Chain.Keys.Where(k => !moving.Contains(k)).ToList();
        var tie = delta > 0 ? rest.Concat(moving).ToList() : moving.Concat(rest).ToList();
        var ok = EditFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => moving.Contains(p.Key) ? FenceLayoutMath.MoveBy(p.Value, delta, l.Panels) : p.Value)), tie);
        if (ok) StatusText = $"옮김 — {(moving.Count > 1 ? $"{moving.Count}대" : _board.Find(grabbedKey)?.Display)}: {MountTextOf(grabbedKey)} · Ctrl+Z 로 되돌립니다";
        else StatusText = "더 옮길 자리가 없습니다 — 끝입니다.";
        return ok;
    }

    /// <summary>
    /// 끄는 동안 목표 알약(삽입 막대) — 보드를 바꾸지 않고 흉내만 낸다. "위치 3 · 기둥 3 · 기둥 위"(대역이 있으면 "· 번호 3" 도).
    /// </summary>
    public string FenceMoveLabel(IReadOnlyList<int> keys, int grabbedKey, double targetMetres)
    {
        var layout = _board.FenceLayout;
        if (layout.MountOf(grabbedKey) is not { } grabbed) return string.Empty;
        var delta = FenceLayoutMath.IndexAt(grabbed, layout.Geometry, targetMetres) - grabbed.Panel;
        var moving = (keys ?? Array.Empty<int>()).Where(k => layout.MountOf(k) is not null).ToList();
        if (!moving.Contains(grabbedKey)) moving.Add(grabbedKey);
        var mounts = layout.Mounts.ToDictionary(p => p.Key, p => moving.Contains(p.Key) ? FenceLayoutMath.MoveBy(p.Value, delta, layout.Panels) : p.Value);
        var rest = _board.Chain.Keys.Where(k => !moving.Contains(k)).ToList();
        var tie = delta > 0 ? rest.Concat(moving).ToList() : moving.Concat(rest).ToList();
        var order = FenceLayoutMath.ChainOrder(mounts.Select(p => (p.Key, p.Value)), layout.ControllerEnd, tie).ToList();
        var target = mounts[grabbedKey];
        var place = $"{(target.IsPostSpot ? "기둥" : "망")} {target.Panel + 1} · {SensorMountSpec.SpotText(target.Spot)}";
        var numbering = FenceLayoutMath.NumberingOrder(order, k => mounts.TryGetValue(k, out var m) ? m.Lane : FenceLane.Lower, _board.NumberingDirection);
        var number = layout.Bands is { } bands
            && NumberingMath.Assign(numbering.Select(k => (k, _board.CategoryOf(k))), bands).TryGetValue(grabbedKey, out var n) ? $" · 번호 {n}" : string.Empty;
        return $"위치 {order.IndexOf(grabbedKey) + 1} · {place}{number}";
    }

    /// <summary>끄는 동안 목표 칸의 가로 위치(m) — 기둥이면 기둥 x, 망이면 망 가운데.</summary>
    public double FenceMoveTargetMetres(int grabbedKey, double targetMetres)
    {
        var layout = _board.FenceLayout;
        if (layout.MountOf(grabbedKey) is not { } grabbed) return targetMetres;
        var index = FenceLayoutMath.IndexAt(grabbed, layout.Geometry, targetMetres);
        return FenceLayoutMath.PointOf(grabbed with { Panel = index }, layout.Geometry).XM;
    }

    /// <summary>팔레트 센서를 펜스 위 x(m) 에 놓는다 — 그 센서 갈래의 기본 자리(기둥 위 · 망 가운데) 중 가장 가까운 칸.</summary>
    public bool FenceDropAt(IReadOnlyList<int> keys, double metres)
    {
        if (IsBusy || keys is null || keys.Count == 0 || !_board.FenceLayout.IsActive) return false;
        var geometry = _board.FenceLayout.Geometry;
        var ok = EditFence(l =>
        {
            var mounts = new Dictionary<int, SensorMountSpec>(l.Mounts);
            foreach (var key in keys.Where(k => _board.Find(k) is not null))
            {
                var category = _board.CategoryOf(key);
                var seat = new SensorMountSpec(0, FenceLayoutMath.DefaultSpotFor(category));
                // 펜스센서는 윤형과 같이 — 윤형 망에 놓으면 위 줄 · 윤형 코일(사용자 결정)
                mounts[key] = FenceLayoutMath.DefaultMount(FenceLayoutMath.Normalize(seat with { Panel = FenceLayoutMath.IndexAt(seat, geometry, metres) }, l.Panels),
                                                           category, l.Panels);
            }
            return l.WithMounts(mounts);
        });
        if (ok) StatusText = $"붙임 — {(keys.Count > 1 ? $"{keys.Count}대" : _board.Find(keys[0])?.Display)}: {MountTextOf(keys[0])} · Ctrl+Z 로 되돌립니다";
        return ok;
    }

    private string MountTextOf(int key)
        => _board.FenceLayout.MountOf(key) is { } m ? MountText(m) : "미배치";

    /// <summary>"망 3 · 윤형 코일" · "기둥 4 · 기둥 위".</summary>
    private static string MountText(SensorMountSpec m) => $"{(m.IsPostSpot ? "기둥" : "망")} {m.Panel + 1} · {SensorMountSpec.SpotText(m.Spot)}";

    /// <summary>펜스 구성 편집 한 걸음 — 되돌리기를 찍고 보드에 맡긴다. 바뀐 것이 없으면 되돌리기 장면도 걷는다.</summary>
    private bool EditFence(Func<WiringFenceLayout, WiringFenceLayout> edit, IReadOnlyList<int>? tieOrder = null)
    {
        if (IsBusy || !_board.FenceLayout.IsActive) return false;
        _board.PushUndo();
        if (!_board.ApplyFenceEdit(edit, tieOrder))
        {
            _board.Undo();
            return false;
        }
        SyncAll();
        return true;
    }
    #endregion

    #region - Context menu (FR-08) -
    /// <summary>메뉴를 연 곳의 항목 — 캔버스 · 개념도가 그린다(헤드리스 시험 대상).</summary>
    public IReadOnlyList<FenceMenuEntry> FenceMenu(FenceMenuTargetKind kind, int target)
    {
        const string P = "Devices.Wiring.Fence.Menu.";
        var entries = new List<FenceMenuEntry>();
        switch (kind)
        {
            case FenceMenuTargetKind.Sensor when _board.Find(target) is { } row:
            {
                var type = WiringTopology.ParseSensorType(row.Facts.TypeText);
                var same = _board.Chain.Keys.Count(k => WiringTopology.ParseSensorType(_board.Find(k)?.Facts.TypeText) == type);
                var selected = SelectionFor(target).Where(_board.Chain.Contains).ToList();
                var placed = _board.FenceLayout.MountOf(target) is not null;
                var typeName = Helpers.DeviceEnumDisplay.SensorTypeBilingual(row.Facts.TypeText);
                entries.Add(new FenceMenuEntry("이 설치 방식을 이 제어기 모든 센서에 적용", P + "ApplyAll", () => ApplyMountStyleAsync(FenceApplyScope.All, target), placed));
                entries.Add(new FenceMenuEntry($"같은 종류 센서에만 적용 ({typeName} {same}대)", P + "ApplySameType", () => ApplyMountStyleAsync(FenceApplyScope.SameType, target), placed));
                entries.Add(new FenceMenuEntry($"선택한 센서에 적용 ({selected.Count}대)", P + "ApplySelected", () => ApplyMountStyleAsync(FenceApplyScope.Selected, target), placed && selected.Count > 0));
                entries.Add(FenceMenuEntry.Separator(P + "Sep1"));
                entries.Add(new FenceMenuEntry("방향만 모두 앞으로", P + "FacingFront", () => SetFacingAllAsync(WiringFacing.Front)));
                entries.Add(new FenceMenuEntry("방향만 모두 뒤로", P + "FacingBack", () => SetFacingAllAsync(WiringFacing.Back)));
                // 줄(FR-18) — 고른 센서 모두(이 센서가 선택 밖이면 이 센서만)
                var laneKeys = SelectionFor(target).Where(k => _board.FenceLayout.MountOf(k) is not null).ToList();
                entries.Add(new FenceMenuEntry("위 줄로", P + "LaneUpper", () => { FenceSetLane(laneKeys, FenceLane.Upper); return Task.CompletedTask; },
                                               laneKeys.Any(k => _board.FenceLayout.LaneOf(k) != FenceLane.Upper), "Alt+↑"));
                entries.Add(new FenceMenuEntry("아래 줄로", P + "LaneLower", () => { FenceSetLane(laneKeys, FenceLane.Lower); return Task.CompletedTask; },
                                               laneKeys.Any(k => _board.FenceLayout.LaneOf(k) != FenceLane.Lower), "Alt+↓"));
                if (PanelIndexOf(target) is { } panel)
                {
                    entries.Add(new FenceMenuEntry("망 속성 복사", P + "CopyPanel", () => { CopyPanel(panel); return Task.CompletedTask; }));
                    entries.Add(new FenceMenuEntry($"선택한 망에 붙여넣기 ({_panelSelection.Count}칸)", P + "PastePanel", PastePanelAsync,
                                                   _copiedPanel is not null && _panelSelection.Count > 0));
                }
                entries.Add(FenceMenuEntry.Separator(P + "Sep2"));
                entries.Add(new FenceMenuEntry("결선에서 빼기", P + "Unplace", () => { FenceUnplace(FenceDragKeys(target)); return Task.CompletedTask; },
                                               _board.Chain.Contains(target), "Delete"));
                break;
            }
            case FenceMenuTargetKind.Panel when target >= 0 && target < _board.FenceLayout.Panels.Count:
                entries.Add(new FenceMenuEntry("망 속성 복사", P + "CopyPanel", () => { CopyPanel(target); return Task.CompletedTask; }));
                entries.Add(new FenceMenuEntry($"선택한 망에 붙여넣기 ({_panelSelection.Count}칸)", P + "PastePanel", PastePanelAsync,
                                               _copiedPanel is not null && _panelSelection.Count > 0));
                entries.Add(new FenceMenuEntry("이 망 속성을 모든 망에", P + "PanelToAll", () => ApplyPanelToAllAsync(target)));
                break;
            default:
                entries.Add(new FenceMenuEntry("센서 모두 선택", P + "SelectAllSensors", () => { FenceSelectAllSensors(); return Task.CompletedTask; }, _board.Chain.Count > 0, "Ctrl+A"));
                entries.Add(new FenceMenuEntry("망 모두 선택", P + "SelectAllPanels", () => { FenceSelectAllPanels(); return Task.CompletedTask; }, _board.FenceLayout.Panels.Count > 0));
                break;
        }
        return entries.Select(e => e.Run is { } run ? e with { Run = Guard(e.Text, run) } : e).ToList();
    }

    /// <summary>
    /// 메뉴 동작을 감싼다 — 실패하면 상태 줄에 알리고(사람이 본다) 앱 로그에 한 줄. 메뉴를 그리는 쪽(캔버스 · 개념도)은 예외를 보지 않는다.
    /// </summary>
    private Func<Task> Guard(string text, Func<Task> run) => async () =>
    {
        try { await run(); }
        catch (Exception ex) { ReportFenceMenuFailure(text, ex); }
    };

    /// <summary>메뉴 동작 실패를 알린다 — 상태 줄(사람) + 앱 로그 한 줄.</summary>
    public void ReportFenceMenuFailure(string text, Exception ex)
    {
        _log?.Warning($"[Wiring] 펜스 메뉴 '{text}' 실패: {ex?.Message}");
        StatusText = $"'{text}' 을(를) 하지 못했습니다 — {ex?.Message}";
    }

    /// <summary>그 센서가 선택 안이면 선택 전부, 아니면 그 센서만.</summary>
    private IReadOnlyList<int> SelectionFor(int key) => _fenceSelection.Contains(key) ? _fenceSelection.ToList() : new List<int> { key };

    /// <summary>센서가 달린 망(기둥이면 그 기둥 오른쪽 망 · 끝 기둥이면 끝 망).</summary>
    private int? PanelIndexOf(int key)
    {
        var layout = _board.FenceLayout;
        if (layout.MountOf(key) is not { } m || layout.Panels.Count == 0) return null;
        return Math.Clamp(m.Panel, 0, layout.Panels.Count - 1);
    }

    /// <summary>
    /// 기준 센서의 설치 방식(자리 종류 · 높이 조정 · 보는 쪽)을 퍼뜨린다(FR-08) — 적용 전 "13대 중 10대가 바뀝니다" 확인, 되돌리기 한 번으로 통째 취소.
    /// </summary>
    public async Task<bool> ApplyMountStyleAsync(FenceApplyScope scope, int referenceKey)
    {
        var layout = _board.FenceLayout;
        if (IsBusy || layout.MountOf(referenceKey) is not { } reference || _board.Find(referenceKey) is not { } refRow) return false;
        var type = WiringTopology.ParseSensorType(refRow.Facts.TypeText);
        var targets = scope switch
        {
            FenceApplyScope.SameType => _board.Chain.Keys.Where(k => WiringTopology.ParseSensorType(_board.Find(k)?.Facts.TypeText) == type).ToList(),
            FenceApplyScope.Selected => SelectionFor(referenceKey).Where(_board.Chain.Contains).ToList(),
            _ => _board.Chain.Keys.ToList(),
        };
        var (mounts, changedMounts) = FenceLayoutMath.ApplyMountStyle(layout.Mounts, targets, reference.Spot, reference.HeightOffsetM, layout.Panels);
        var facingTargets = refRow.SupportsFacing ? targets.Where(k => _board.SupportsFacing(k) && _board.FacingOf(k) != refRow.Facing).ToList() : new List<int>();
        var changed = changedMounts.Union(facingTargets).Count();
        if (changed == 0)
        {
            StatusText = $"바뀌는 센서가 없습니다 — {targets.Count}대 모두 이미 같은 설치 방식입니다.";
            return false;
        }
        var what = $"{SensorMountSpec.SpotText(reference.Spot)}{(reference.HeightOffsetM != 0 ? $" · 높이 {reference.HeightOffsetM:+0.##;-0.##}m" : string.Empty)}"
                   + (refRow.SupportsFacing ? $" · {FacingName(refRow.Facing)}" : string.Empty);
        // 자리 종류가 바뀌면 같은 망 안의 앞뒤(기둥 위 → 망 가운데)가 바뀌어 순서 · 번호가 따라 바뀔 수 있다 — 적용 전에 함께 알린다.
        var renumber = NumberChangesIf(mounts);
        if (!await _dialogs.ConfirmAsync("설치 방식 적용",
                $"{targets.Count}대 중 설치 방식 {changed}대 · 번호 {renumber}대 바뀜 — {what}.{Environment.NewLine}"
                + (renumber > 0 ? $"번호가 바뀌면 {NUMBER_WARNING}.{Environment.NewLine}" : string.Empty)
                + "되돌리기 한 번으로 통째 취소됩니다. 적용할까요?"))
            return false;

        _board.PushUndo();
        _board.SetFacing(facingTargets, refRow.Facing);
        _board.ApplyFenceEdit(l => l.WithMounts(mounts));
        SyncAll();
        StatusText = $"설치 방식을 {changed}대에 적용했습니다 — Ctrl+Z 로 한 번에 되돌립니다";
        return true;
    }

    /// <summary>
    /// 자리를 <paramref name="mounts"/> 로 바꾸면 다시 매겨질 번호 수(대역이 없거나 순서가 그대로면 0) — 보드를 바꾸지 않는다.
    /// 번호는 체인 순서가 바뀔 때만 다시 매기므로(<see cref="WiringBoard.ApplyFenceEdit"/>) 같은 규칙으로 센다.
    /// </summary>
    private int NumberChangesIf(IReadOnlyDictionary<int, SensorMountSpec> mounts)
    {
        if (_board.FenceLayout.Bands is not { } bands) return 0;
        var order = FenceLayoutMath.ChainOrder(mounts.Where(p => _board.Find(p.Key) is not null).Select(p => (p.Key, p.Value)),
                                               _board.FenceLayout.ControllerEnd, _board.Chain.Keys);
        var numbering = FenceLayoutMath.NumberingOrder(order, k => mounts.TryGetValue(k, out var m) ? m.Lane : FenceLane.Lower, _board.NumberingDirection);
        if (order.SequenceEqual(_board.Chain.Keys) && numbering.SequenceEqual(_board.NumberingOrder())) return 0;
        return NumberingMath.Assign(numbering.Select(k => (k, _board.CategoryOf(k))), bands)
                            .Count(p => _board.Find(p.Key) is { } row && row.Facts.Number != p.Value);
    }

    /// <summary>방향만 모두 앞 · 뒤(FR-08) — 방향이 있는 센서만. 확인 뒤 되돌리기 한 걸음.</summary>
    public async Task<bool> SetFacingAllAsync(WiringFacing facing)
    {
        if (IsBusy) return false;
        var capable = _board.Chain.Keys.Where(_board.SupportsFacing).ToList();
        var changed = capable.Count(k => _board.FacingOf(k) != facing);
        if (changed == 0)
        {
            StatusText = capable.Count == 0 ? "방향이 있는 센서(스마트 · 복합)가 없습니다." : $"바뀌는 센서가 없습니다 — 이미 모두 {FacingName(facing)}";
            return false;
        }
        if (!await _dialogs.ConfirmAsync("방향 적용", $"{capable.Count}대 중 {changed}대가 바뀝니다 — 모두 {FacingName(facing)}.{Environment.NewLine}되돌리기 한 번으로 통째 취소됩니다. 적용할까요?"))
            return false;
        return FenceSetFacing(capable, facing);
    }

    /// <summary>망 속성 복사.</summary>
    public void CopyPanel(int index)
    {
        if (index < 0 || index >= _board.FenceLayout.Panels.Count) return;
        _copiedPanel = _board.FenceLayout.Panels[index];
        StatusText = $"망 {index + 1} 속성을 복사했습니다 — {Describe(_copiedPanel)} · 망을 골라 [선택한 망에 붙여넣기]";
    }

    /// <summary>선택한 망에 붙여넣기 — 확인 뒤 되돌리기 한 걸음.</summary>
    public Task<bool> PastePanelAsync() => _copiedPanel is { } copied ? ApplyPanelTemplateAsync(copied, _panelSelection.ToList(), "선택한 망에 붙여넣기") : Task.FromResult(false);

    /// <summary>이 망 속성을 모든 망에 — 확인 뒤 되돌리기 한 걸음.</summary>
    public Task<bool> ApplyPanelToAllAsync(int index)
        => index >= 0 && index < _board.FenceLayout.Panels.Count
            ? ApplyPanelTemplateAsync(_board.FenceLayout.Panels[index], Enumerable.Range(0, _board.FenceLayout.Panels.Count).ToList(), "모든 망에 적용")
            : Task.FromResult(false);

    private async Task<bool> ApplyPanelTemplateAsync(FencePanelSpec template, IReadOnlyList<int> targets, string title)
    {
        if (IsBusy || targets.Count == 0) return false;
        var panels = _board.FenceLayout.Panels;
        var changed = targets.Count(i => i >= 0 && i < panels.Count && panels[i] != template);
        if (changed == 0)
        {
            StatusText = "바뀌는 망이 없습니다 — 이미 같은 속성입니다.";
            return false;
        }
        if (!await _dialogs.ConfirmAsync(title, $"{targets.Count}칸 중 {changed}칸이 바뀝니다 — {Describe(template)}.{Environment.NewLine}되돌리기 한 번으로 통째 취소됩니다. 적용할까요?"))
            return false;
        var set = targets.ToHashSet();
        int raised = 0, lowered = 0;
        var ok = EditFence(l => FollowRazor(l, l.WithPanels(l.Panels.Select((p, i) => set.Contains(i) ? template : p)), out raised, out lowered));
        StatusText = ok ? $"망 {changed}칸에 적용했습니다{RazorFollowText(raised, lowered)} — Ctrl+Z 로 한 번에 되돌립니다" : "바뀐 것이 없습니다.";
        return ok;
    }

    private static string Describe(FencePanelSpec p)
        => $"{FencePanelSpec.StyleText(p.Style)} · 높이 {p.HeightM:0.##}m · 거리 {p.SpanM:0.##}m{(p.Color is { } c ? $" · {c}" : string.Empty)}";
    #endregion

    #region - Number bands (FR-10) -
    /// <summary>지금 대역 — "중요시설 4차 · 스마트 1~99 · 펜스 101~199" · 없으면 그 말.</summary>
    public string BandText => _board.FenceLayout.Bands is { } bands ? bands.Text : "정하지 않음 — 번호를 그대로 둡니다(대역을 고르면 위치 순서대로 매깁니다)";

    public bool HasBands => _board.FenceLayout.Bands is not null;

    /// <summary>
    /// 번호 대역을 고르지 않았다는 알림을 보일까 — 펜스 구성이 켜져 있고 결선에 센서가 있는데 대역이 없을 때. 대역이 없으면 번호를 저절로 매기지 않아
    /// 위치 순서와 번호가 어긋나 보일 수 있다(101 · 102 · 104 · 103 …) — 고르는 곳으로 바로 간다.
    /// </summary>
    public bool HasNoBandsNotice => _board.FenceLayout.IsActive && _board.FenceLayout.Bands is null && _board.Chain.Count > 0;

    /// <summary>"번호 대역 미설정 — 위치대로 번호를 매기려면" (+ 번호가 위치 순서와 다르면 그 말).</summary>
    public string NoBandsNoticeText
    {
        get
        {
            if (!HasNoBandsNotice) return string.Empty;
            var numbers = _board.Chain.Keys.Select(k => _board.Find(k)?.Facts.Number ?? 0).ToList();
            var ordered = numbers.Zip(numbers.Skip(1), (a, b) => a < b).All(x => x);
            return ordered ? NO_BANDS_NOTICE : $"{NO_BANDS_NOTICE} (지금 번호는 위치 순서와 다릅니다)";
        }
    }

    /// <summary>[대역 고르기] — 제어기를 골라 오른쪽 칸에 번호 대역(프리셋 2차 · 3차 · 4차 · 직접 설정)을 연다.</summary>
    public void ShowBandPicker()
    {
        FenceSelectController();
        StatusText = "번호 대역 — 오른쪽 칸에서 2차 · 3차 · 4차 중 고르거나 직접 설정하세요(고르면 위치 순서대로 번호를 매깁니다).";
    }

    /// <summary>직접 설정 줄 — 이 제어기에 있는 갈래만.</summary>
    public ObservableCollection<WiringBandRowViewModel> BandRows { get; } = new();

    public string? BandError { get; private set; }

    /// <summary>[2차] · [3차] · [4차] — 고르면 바로 위치 순서대로 번호를 다시 매긴다(Draft · 되돌리기 한 걸음).</summary>
    public bool ChooseBandPreset(string preset)
    {
        var bands = NumberBandSet.Presets.FirstOrDefault(p => p.Preset == preset);
        return bands is not null && SetBands(bands);
    }

    /// <summary>대역 끄기 — 번호는 지금 값 그대로 남는다.</summary>
    public bool ClearBands() => SetBands(null);

    /// <summary>직접 설정 적용.</summary>
    public bool ApplyCustomBands()
    {
        var bands = new List<NumberBand>();
        foreach (var row in BandRows)
        {
            var band = row.Parse(out var error);
            if (error is not null) { BandError = error; NotifyOfPropertyChange(nameof(BandError)); NotifyOfPropertyChange(nameof(HasBandError)); return false; }
            if (band is not null) bands.Add(band);
        }
        BandError = null;
        NotifyOfPropertyChange(nameof(BandError));
        NotifyOfPropertyChange(nameof(HasBandError));
        return SetBands(bands.Count == 0 ? null : new NumberBandSet(NumberBandSet.PRESET_CUSTOM, bands.OrderBy(b => b.Start).ToList()));
    }

    public bool HasBandError => BandError is not null;

    private bool SetBands(NumberBandSet? bands)
    {
        if (IsBusy || !_board.FenceLayout.IsActive) return false;
        _board.PushUndo();
        // 대역을 처음 고를 때(아직 모두 아래 줄) 윤형 망의 펜스센서를 위 줄 · 윤형 코일로 — 같은 되돌리기 한 걸음(FR-18 기본값 · "4차일 때만" 을 대신한다).
        var raised = bands is not null && !_board.FenceLayout.HasUpperSensors ? ApplyDefaultLanes() : 0;
        var lanesSet = raised > 0;
        if (!_board.SetNumberBands(bands) && !lanesSet)
        {
            _board.Undo();
            StatusText = "바뀐 것이 없습니다.";
            return false;
        }
        var renumbered = _board.TakeRenumbered();
        SyncAll();
        RefreshBandRows();
        var pending = _board.Rows.Count(r => r.Facts.Number != r.Baseline.Number);
        StatusText = bands is null
            ? "번호 대역을 껐습니다 — 번호는 지금 값 그대로입니다."
            : $"번호 대역 {bands.PresetText} — 위치 순서대로 번호를 매겼습니다 · 번호 {renumbered}대 바뀜(저장 대기 {pending}대)"
              + (lanesSet ? $" · 윤형 망의 펜스센서 {raised}대를 위 줄 · 윤형 코일로 옮겼습니다(Ctrl+Z 로 함께 취소)" : string.Empty) + $". {NUMBER_WARNING}.";
        return true;
    }

    /// <summary>
    /// 기본 줄을 매긴다(FR-18 · 윤형 설치) — 윤형 망 가운데 · 아래에 있는 아래 줄 펜스센서를 위 줄 · 윤형 코일로. 옮긴 센서 수. 되돌리기는 부르는 쪽.
    /// </summary>
    private int ApplyDefaultLanes()
    {
        var layout = _board.FenceLayout;
        var upper = layout.Mounts
            .Where(p => p.Value.Lane != FenceLane.Upper && !p.Value.IsPostSpot
                        && FenceLayoutMath.DefaultLane(_board.CategoryOf(p.Key), FenceLayoutMath.IsRazorPanel(layout.Panels, p.Value.Panel)) == FenceLane.Upper)
            .Select(p => p.Key).ToList();
        return upper.Count > 0 && _board.SetLanes(upper, FenceLane.Upper) ? upper.Count : 0;
    }

    /// <summary>
    /// 망 모양이 바뀐 편집에 윤형 자리를 따라 붙인다 — 윤형이 된 망의 펜스센서(아래 줄 · 망 가운데 · 높이 조정 0 = 손대지 않은 기본 자리)는 위 줄 · 윤형 코일로,
    /// 윤형이 아니게 된 망의 코일 센서는 아래 줄 · 망 가운데로. 손으로 고친 자리(다른 자리 · 높이 조정)는 건드리지 않는다.
    /// </summary>
    private WiringFenceLayout FollowRazor(WiringFenceLayout before, WiringFenceLayout next, out int raised, out int lowered)
    {
        raised = 0;
        lowered = 0;
        var mounts = new Dictionary<int, SensorMountSpec>(next.Mounts);
        foreach (var (key, mount) in before.Mounts)
        {
            if (!mounts.TryGetValue(key, out var now) || mount.IsPostSpot) continue;
            var wasRazor = FenceLayoutMath.IsRazorPanel(before.Panels, mount.Panel);
            var isRazor = FenceLayoutMath.IsRazorPanel(next.Panels, mount.Panel);
            if (!wasRazor && isRazor && mount is { Spot: FenceMountSpot.PanelCenter, Lane: FenceLane.Lower, HeightOffsetM: 0 }
                && _board.CategoryOf(key) == FenceSensorCategory.Fence)
            {
                mounts[key] = FenceLayoutMath.DefaultMount(now, FenceSensorCategory.Fence, next.Panels);
                raised++;
            }
            else if (wasRazor && !isRazor && mount.Spot == FenceMountSpot.RazorCoil)
            {
                mounts[key] = now with { Spot = FenceMountSpot.PanelCenter, Lane = FenceLane.Lower };
                lowered++;
            }
        }
        return raised + lowered > 0 ? next.WithMounts(mounts) : next;
    }

    /// <summary>윤형 따라 붙이기 결과를 상태 줄 꼬리로.</summary>
    private static string RazorFollowText(int raised, int lowered)
        => (raised > 0 ? $" · 펜스센서 {raised}대를 윤형 코일(위 줄)로" : string.Empty)
           + (lowered > 0 ? $" · 윤형이 빠진 망의 코일 센서 {lowered}대를 망 가운데(아래 줄)로" : string.Empty);

    private void RefreshBandRows()
    {
        var categories = _board.Rows.Select(r => _board.CategoryOf(r.Key)).Where(c => c != FenceSensorCategory.Other).Distinct().OrderBy(c => c).ToList();
        foreach (var band in _board.FenceLayout.Bands?.Bands ?? Array.Empty<NumberBand>())
            if (!categories.Contains(band.Category)) categories.Add(band.Category);
        BandRows.Clear();
        foreach (var category in categories) BandRows.Add(new WiringBandRowViewModel(category, _board.FenceLayout.Bands?.BandOf(category)));
    }
    #endregion

    #region - Local save (FR-11) -
    /// <summary>
    /// 로컬 저장 — 망 · 설치 자리(서버 id 가 있는 센서만) · 대역 · 간격. 저장소가 없으면 아무것도 하지 않는다. 저장이 <b>된 뒤에만</b> 기준을 옮긴다.
    /// </summary>
    private async Task<FenceLayoutSaveResult?> SaveFenceLayoutAsync(FenceLayoutSaveMode mode = FenceLayoutSaveMode.Normal)
    {
        if (_fenceStore is null || !_board.FenceLayout.IsActive) return null;
        var layout = _board.FenceLayout;
        var document = new FenceLayoutDocument
        {
            ControllerId = Controller.Id,
            Panels = layout.Panels.ToList(),
            Mounts = layout.Mounts
                .Where(p => _board.Find(p.Key) is { Id: > 0 })
                .ToDictionary(p => _board.Find(p.Key)!.Id, p => p.Value with { FacesBack = _board.FacingOf(p.Key) == WiringFacing.Back }),
            Bands = layout.Bands,
            FenceSpacingM = _board.Spacing.FenceMetres,
            ControllerEnd = layout.ControllerEnd,
            VbusGap = layout.VbusGap,
            Revision = _fenceRevision,
        };
        FenceLayoutSaveResult result;
        try { result = await _fenceStore.SaveAsync(_fenceKey, document, mode); }
        catch (Exception ex)
        {
            result = new FenceLayoutSaveResult(FenceLayoutSaveStatus.Failed, _fenceRevision, FenceLayoutRows.FAILED);
            _log?.Warning($"[Wiring] {_fenceKey} 펜스 구성 저장 예외: {ex.Message}");
        }
        if (result.IsSaved)
        {
            _fenceRevision = result.Revision;
            _fenceReadFailed = false;
            _board.MarkFenceSaved();
            _fenceNotice = null;
        }
        return result;
    }
    #endregion

    /// <summary>속성 칸 · 캔버스를 모두 다시 알린다(선택 · 펜스 구성 · 대역).</summary>
    private void RaiseLayoutPane()
    {
        RaisePanelPane();
        RaiseMountPane();
        RaiseLanePane();
        foreach (var name in new[]
        {
            nameof(FenceLayout), nameof(HasLocalChanges), nameof(FenceNoticeText), nameof(HasFenceNotice), nameof(BandText), nameof(HasBands),
            nameof(HasNoBandsNotice), nameof(NoBandsNoticeText),
            nameof(FencePaneKind), nameof(HasAnySelection),
        }) NotifyOfPropertyChange(name);
    }
}
