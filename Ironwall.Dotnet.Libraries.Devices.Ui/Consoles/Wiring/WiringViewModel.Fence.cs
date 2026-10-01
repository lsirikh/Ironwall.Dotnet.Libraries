using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 펜스 형상 뷰(wiring-fence-view F-3 · FR-04 ~ FR-11 · FR-13 · FR-15 · FR-17 ~ FR-19)가 보는 뷰모델 면.
/// </summary>
/// <remarks>
/// 캔버스(<see cref="FenceCanvas"/>)는 판단하지 않는다 — 누름 · 끌기 · 키를 뜻 없이 올리고, 체인 편집 · 선택 · 속성 칸은 여기서 한다.
/// 편집은 전부 보드(<see cref="WiringBoard"/>)의 같은 길이라 표 보기와 펜스 보기가 늘 같은 체인을 본다.
/// </remarks>
public sealed partial class WiringViewModel
{
    /// <summary>펜스 캔버스 드롭존(팔레트 → 펜스).</summary>
    public const string FenceZoneKey = "wiring-fence";

    private bool _isFenceView = true;
    private bool _isFlatChosen;
    private bool _isSoftwareRendering;
    private bool _showRange;
    private int? _fenceSelectedKey;
    private readonly List<int> _fenceSelection = new();
    // 포커스가 바꾼 선택(포커스 = 선택) — 그 직전 선택을 기억해 Ctrl+Space 가 "더하기"로 읽게 한다(헤디드 r23 SC-FEN-026)
    private (int Key, int[] Before)? _focusReplaced;
    private bool _isControllerSelected;
    private int? _hoverKey;

    /// <summary>체인 · 선택 · 보기 방식이 바뀌었다 — 캔버스가 다시 그린다.</summary>
    public event EventHandler? FenceChanged;

    #region - View switch -
    /// <summary>[펜스 보기](기본) / [표 보기].</summary>
    public bool IsFenceView
    {
        get => _isFenceView;
        set
        {
            if (_isFenceView == value) return;
            _isFenceView = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsTableView));
        }
    }

    public bool IsTableView
    {
        get => !_isFenceView;
        set => IsFenceView = !value;
    }

    public void ShowFenceView() => IsFenceView = true;
    public void ShowTableView() => IsFenceView = false;
    #endregion

    #region - Tilt · flat (FR-10) -
    /// <summary>사람이 [평면 보기]를 골랐는가.</summary>
    public bool IsFlatChosen
    {
        get => _isFlatChosen;
        set { if (_isFlatChosen == value) return; _isFlatChosen = value; NotifyFlat(); }
    }

    /// <summary>
    /// 소프트웨어 렌더링(Tier 0 · 원격 데스크톱)인가 — 뷰가 알려 준다. <b>정보(로그)용일 뿐</b> 보기를 바꾸지 않는다(2026-09-30 결정):
    /// 입체는 Viewport3D 가 아니라 2D 비스듬 투영이라 Tier 0 에서도 비용이 같고(첫 그리기 33~36ms), 원격으로 쓰는 운영자도 입체를 봐야 한다.
    /// </summary>
    public bool IsSoftwareRendering
    {
        get => _isSoftwareRendering;
        set { if (_isSoftwareRendering == value) return; _isSoftwareRendering = value; NotifyOfPropertyChange(); }
    }

    /// <summary>지금 평면으로 그리는가 — 사람이 [평면 보기]를 골랐을 때만(기본은 입체 · 모든 렌더 tier).</summary>
    public bool IsFlat => _isFlatChosen;

    public bool IsTilt => !IsFlat;

    /// <summary>[입체 보기]를 누를 수 있는가 — 늘 누를 수 있다(원격 데스크톱 자동 평면 폐지).</summary>
    public bool CanChooseTilt => true;

    /// <summary>"평면으로 표시 중" 표지 글자 — 사람이 고른 평면일 때만.</summary>
    public string FlatNoteText => _isFlatChosen ? "평면 표시 중 — [입체 보기]로 되돌립니다" : string.Empty;

    public bool HasFlatNote => IsFlat;

    public void ChooseFlat() => IsFlatChosen = true;

    public void ChooseTilt() => IsFlatChosen = false;

    private void NotifyFlat()
    {
        NotifyOfPropertyChange(nameof(IsFlatChosen));
        NotifyOfPropertyChange(nameof(IsSoftwareRendering));
        NotifyOfPropertyChange(nameof(IsFlat));
        NotifyOfPropertyChange(nameof(IsTilt));
        NotifyOfPropertyChange(nameof(CanChooseTilt));
        NotifyOfPropertyChange(nameof(FlatNoteText));
        NotifyOfPropertyChange(nameof(HasFlatNote));
        RaiseFence();
    }
    #endregion

    #region - Range (FR-19) -
    public bool ShowRange
    {
        get => _showRange;
        set { if (_showRange == value) return; _showRange = value; NotifyOfPropertyChange(); RaiseFence(); }
    }

    /// <summary>탐지 반경이 있는 센서(복합 · 지진동)가 있는가 — 없으면 [탐지 범위]를 끈다.</summary>
    public bool HasRangeSensors => _board.Rows.Any(r => FenceWorld.RangeOf(FenceWorld.KindOf(WiringTopology.ParseSensorType(r.Facts.TypeText))) > 0);

    public void ToggleRange() => ShowRange = !ShowRange && HasRangeSensors;
    #endregion

    #region - Spacing (v0.4 §1-C) -
    /// <summary>지금 간격 표(펜스센서 현장 간격 포함) — 캔버스가 세계를 세울 때 쓴다.</summary>
    public WiringSpacingTable FenceSpacing => _board.Spacing;

    /// <summary>[펜스센서 간격 ▾] 고를 값 — 2 · 2.5 · 3 · 3.5 · 4 m.</summary>
    public IReadOnlyList<double> FenceSpacingChoices => WiringSpacingTable.FenceChoices;

    /// <summary>
    /// 펜스센서 현장 간격(m) — 바꾸면 그림이 다시 놓인다. 서버 바뀐 줄 · 되돌리기와는 무관하고 서버에 싣지 않는다(O-10) —
    /// 펜스 구성이 켜져 있으면 로컬 문서(<c>fence_spacing_m</c>)에 실리므로 <b>그것만으로 로컬 저장 대상</b>이다.
    /// </summary>
    public double FenceSpacingMetres
    {
        get => _board.Spacing.FenceMetres;
        set
        {
            if (!_board.SetFenceSpacing(value)) return;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(FenceSpacing));
            StatusText = HasLocalChanges
                ? $"펜스센서 간격 {FenceSpacingMetres:0.#}m 로 다시 놓았습니다 — [저장하기] 를 누르면 이 PC 의 펜스 구성에 저장됩니다."
                : $"펜스센서 간격 {FenceSpacingMetres:0.#}m 로 다시 놓았습니다 — 그림만 바뀌고 저장 대상은 아닙니다.";
            RefreshIssues();
            SyncLines();
            RefreshCommands();
            RaiseFence();
        }
    }

    /// <summary>펜스센서가 하나라도 있는가 — 없으면 간격 고르기를 끈다.</summary>
    public bool HasFenceSensors => _board.Rows.Any(r => WiringTopology.ParseSensorType(r.Facts.TypeText) == EnumDeviceType.Fence);
    #endregion

    #region - Scene facts -
    /// <summary>펜스 장면의 센서 사실(체인 · 팔레트 모두).</summary>
    public IReadOnlyDictionary<int, FenceSensor> FenceSensors()
    {
        var duplicates = _board.Rows
            .GroupBy(r => r.Facts.Number)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderBy(r => r.Id <= 0 ? 1 : 0).ThenBy(r => r.Id).ThenBy(r => Math.Abs(r.Key)).Skip(1))
            .Select(r => r.Key)
            .ToHashSet();

        var result = new Dictionary<int, FenceSensor>(_board.Rows.Count);
        foreach (var row in _board.Rows)
        {
            var n = _board.NumberOf(row.Key);
            result[row.Key] = new FenceSensor(
                row.Key, row.Id, row.Facts.Number, row.Display,
                WiringTopology.ParseSensorType(row.Facts.TypeText), row.Channel,
                n?.Line ?? 0, n?.Order ?? 0, n?.OppositeOrder,
                _board.IsProposed(row.Key),
                !WiringSpec.SameWiring(_board.PlacementOf(row.Key), row.BaselinePlacement),
                duplicates.Contains(row.Key),
                row.Facing,
                row.Yaw);
        }
        return result;
    }

    /// <summary>지금 체인(캔버스가 세계를 세운다).</summary>
    public WiringChain FenceChain => _board.Chain;

    /// <summary>끌어 놓으면 그 센서의 새 자리 글자(삽입 막대 알약) — 보드를 바꾸지 않고 체인으로만 흉내 낸다.</summary>
    public string FenceDropLabel(IReadOnlyList<int> keys, int line, int index)
    {
        if (keys.Count == 0) return string.Empty;
        var basis = _board.HasPendingProposals ? _board.Chain.AcceptSuggestions() : _board.Chain;
        var next = basis.PlaceInBranch(keys, line, index);
        return next.NumberOf(keys[0]) is { } n ? NumberText(n) : string.Empty;
    }

    private string NumberText(WiringChainNumber n) => _board.Shape switch
    {
        WiringShape.Ring => $"위치 {n.Order} · A{n.Order} · B{n.OppositeOrder}",
        WiringShape.TwoBranch => $"{(n.Line == WiringSpec.LINE_PRIMARY ? "왼쪽 가지 L" : "오른쪽 가지 R")}{n.Order}",
        _ => $"위치 {n.Order}",
    };
    #endregion

    #region - Selection (FR-07) -
    /// <summary>펜스에서 고른 센서 키(없으면 <c>null</c>).</summary>
    public int? FenceSelectedKey => _fenceSelectedKey;

    /// <summary>제어기(함체)를 골랐는가.</summary>
    public bool IsControllerSelected => _isControllerSelected;

    /// <summary>이름 알약을 띄울 센서 — 하나만 골랐으면 그것, 아니면 가리킨 것.</summary>
    public int? FenceNamedKey => _fenceSelection.Count <= 1 ? _fenceSelectedKey ?? _hoverKey : _hoverKey;

    /// <summary>고른 센서 전부(Ctrl 클릭 · FR-07) — 고른 차례.</summary>
    public IReadOnlyList<int> FenceSelectedKeys => _fenceSelection;

    public bool IsFenceSelected(int key) => _fenceSelection.Contains(key);

    /// <summary>여러 대를 골랐는가.</summary>
    public bool HasMultiSelection => _fenceSelection.Count > 1;

    public string MultiSelectionText => HasMultiSelection ? $"센서 {_fenceSelection.Count}대 선택" : string.Empty;

    /// <summary>
    /// 이 센서를 끌면 함께 갈 센서 — 여럿을 골랐고 그 안이면 고른 것 전부(<b>체인 순서</b> · 팔레트 것은 뒤), 아니면 그 센서 하나.
    /// 끼워 넣기는 <see cref="WiringChain.PlaceInBranch"/> 가 상대 순서를 지킨다.
    /// </summary>
    public IReadOnlyList<int> FenceDragKeys(int key)
    {
        if (_fenceSelection.Count <= 1 || !_fenceSelection.Contains(key)) return new[] { key };
        var chain = _board.Chain;
        return _fenceSelection.OrderBy(k => chain.IndexOf(k) is var i && i >= 0 ? i : int.MaxValue).ToList();
    }

    public void FenceSelect(int? key)
    {
        if (key is { } k && _board.Find(k) is null) key = null;
        _fenceSelectedKey = key;
        _fenceSelection.Clear();
        if (key is { } selectedKey) _fenceSelection.Add(selectedKey);
        _isControllerSelected = false;
        _selectionKind = key is null ? KindAfterClear() : FenceSelectionKind.Sensors;
        RaiseSelection();
        if (key is { } selected && _board.Find(selected) is { } row)
            StatusText = _board.NumberOf(selected) is { } n ? $"{row.Display} — {NumberText(n)}" : $"{row.Display} — 미배치";
    }

    /// <summary>Ctrl 클릭 — 고른 것에 넣거나 뺀다(FR-07).</summary>
    public void FenceToggleSelect(int key)
    {
        if (_board.Find(key) is null) return;
        _isControllerSelected = false;
        if (!_fenceSelection.Remove(key)) _fenceSelection.Add(key);
        _fenceSelectedKey = _fenceSelection.Count > 0 ? _fenceSelection[^1] : null;
        _selectionKind = _fenceSelection.Count > 0 ? FenceSelectionKind.Sensors : KindAfterClear();
        RaiseSelection();
        StatusText = _fenceSelection.Count > 1 ? $"센서 {_fenceSelection.Count}대 선택 — 함께 끌거나 [빼기]" : StatusText;
    }

    /// <summary>
    /// 포커스가 센서 칩에 왔다(포커스 = 선택) — 이미 고른 센서면 선택을 그대로 둔다(여러 대 선택 사이를 Tab 으로 다녀도 풀리지 않는다).
    /// 아니면 그 센서 하나로 바꾸되, 바뀌기 전 선택을 기억해 곧바로 누른 Ctrl+Space 가 그 선택에 <b>더하게</b> 한다.
    /// </summary>
    public void FenceFocusSelect(int key)
    {
        if (_fenceSelection.Contains(key) || _board.Find(key) is null) return;
        var before = _fenceSelection.ToArray();
        FenceSelect(key);
        _focusReplaced = before.Length > 0 ? (key, before) : null;
    }

    /// <summary>
    /// Ctrl+Space — 포커스 센서를 더하거나 뺀다. 포커스가 방금 선택을 이 센서 하나로 바꿨다면(<see cref="FenceFocusSelect"/>) 빼는 대신
    /// 바뀌기 전 선택 + 이 센서로 — "A 고르고 B 로 포커스 옮겨 Ctrl+Space" 가 두 대 선택이 된다(헤디드 r23: 선택이 비어 한 대만 끌렸다).
    /// </summary>
    public void FenceToggleFocused(IReadOnlyList<int> keys)
    {
        if (keys is not { Count: > 0 }) return;
        if (_focusReplaced is { } replaced && keys.Contains(replaced.Key) && _fenceSelection.Count == 1 && _fenceSelection[0] == replaced.Key)
        {
            FenceSelectSensors(replaced.Before.Concat(keys).Distinct().ToList());
            return;
        }
        foreach (var key in keys) FenceToggleSelect(key);
    }

    public void FenceSelectController()
    {
        _fenceSelectedKey = null;
        _fenceSelection.Clear();
        _isControllerSelected = true;
        _selectionKind = FenceSelectionKind.Controller;
        RefreshBandRows();
        RaiseSelection();
        StatusText = ShowCables && IsRing ? "함체 — 옆으로 끌거나 Alt+←/→ 로 옮깁니다(표시만)" : "제어기 — 번호 대역 · 통신 상태는 오른쪽 칸에서 봅니다";
    }

    public void FenceHover(int? key)
    {
        if (_hoverKey == key) return;
        _hoverKey = key;
        FenceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>하나만 골랐을 때의 그 센서 — 여럿이면 <c>null</c>(속성 칸은 "센서 N대 선택").</summary>
    private WiringSensorRow? SelectedFenceRow => _fenceSelection.Count <= 1 && _fenceSelectedKey is { } k ? _board.Find(k) : null;

    /// <summary>센서 · 제어기 칸을 보일까 — 마지막으로 고른 쪽이 센서 · 제어기일 때(망이면 망 속성 칸이 대신 선다).</summary>
    public bool HasFenceSelection => _selectionKind switch
    {
        FenceSelectionKind.Controller => _isControllerSelected,
        FenceSelectionKind.Sensors => SelectedFenceRow is not null || HasMultiSelection,
        _ => false,
    };
    public bool HasSensorSelection => _selectionKind == FenceSelectionKind.Sensors && SelectedFenceRow is not null;
    public bool HasNoFenceSelection => !HasFenceSelection && !HasPanelSelection;

    public string SelectedKindText => _isControllerSelected ? (IsRing && ShowCables ? "함체" : "제어기") : HasMultiSelection ? "여러 센서" : "선택한 센서";

    public string SelectedTitle => _isControllerSelected ? Controller.Name : HasMultiSelection ? MultiSelectionText : SelectedFenceRow?.Display ?? "없음";

    public string SelectedNumberText => SelectedFenceRow?.Facts.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    public bool IsSelectedDuplicateNumber => SelectedFenceRow is { } r && _board.Rows.Count(x => x.Facts.Number == r.Facts.Number) > 1;

    public string SelectedIdText => SelectedFenceRow is { } r ? (r.Id > 0 ? $"{r.Id}" : "새 줄(저장 때 만듦)") : string.Empty;

    public string SelectedTypeText => SelectedFenceRow is { } r ? DeviceEnumDisplay.SensorTypeBilingual(r.Facts.TypeText) : _isControllerSelected ? ControllerTypeText : string.Empty;

    private string ControllerTypeText => _board.Shape switch
    {
        WiringShape.Ring => "스마트 제어기 + VBUS 제어기(1U 도킹)",
        WiringShape.TwoBranch => "PIDS 제어기 · 24VDC · Ethernet",
        _ => "제어기 · 24VDC · Ethernet",
    };

    /// <summary>버스 주소 + "체인 순서와 다를 수 있음" 주석.</summary>
    public string SelectedChannelText => SelectedFenceRow is { } r ? (r.Channel is { } c ? $"{c}" : "—") : string.Empty;

    /// <summary>주소 칸 이름(FR-15) — IP 센서는 "IP 주소", 그 밖은 "노드 주소".</summary>
    public string SelectedAddressLabel => SelectedFenceRow is { } r ? AddressLabelOf(r.Key) : "노드 주소";

    /// <summary>주소 칸 값(FR-15) — IP(없으면 "내부망") · 노드 주소.</summary>
    public string SelectedAddressText => SelectedFenceRow is { } r ? AddressTextOf(r.Key) : string.Empty;

    public string SelectedChannelNote
    {
        get
        {
            if (SelectedFenceRow is not { } r) return string.Empty;
            if (IsIpSensor(r.Key)) return "센서마다 IP — 제어기 뒤 내부망이라 GIS 에서 직접 닿지 않습니다";
            if (_board.NumberOf(r.Key) is not { } n || r.Channel is not { } c) return "체인 순서와 다를 수 있음";
            return c == n.Position ? "체인 순서와 다를 수 있음 — 지금은 같음" : $"주소 {c} · 위치 {n.Position} — 체인 순서와 다를 수 있음(주소는 바꾸지 않음)";
        }
    }

    /// <summary>체인 위치 — 링 "3 / 13 · Sensor A 쪽이 1" · 가지 "L2 / 5 · 제어기 쪽이 1(확인 중 O-6)" · 한 줄 "4 / 10 · 제어기 쪽이 1".</summary>
    public string SelectedPositionText
    {
        get
        {
            if (SelectedFenceRow is not { } r || _board.NumberOf(r.Key) is not { } n) return "—";
            var count = _board.CountOn(n.Line);
            return _board.Shape switch
            {
                WiringShape.Ring => $"{n.Order} / {count} · {WiringValidation.PORT_1} 쪽이 1",
                WiringShape.TwoBranch => $"{(n.Line == WiringSpec.LINE_PRIMARY ? "L" : "R")}{n.Order} / {count} · 제어기 쪽이 1",
                _ => $"{n.Order} / {count} · 제어기 쪽이 1",
            } + (_board.IsProposed(r.Key) ? " · 제안" : string.Empty);
        }
    }

    /// <summary>링의 Ch1 · Ch2 번호(1차 · 2차 번호) — "1 · 50"(A · B 는 별칭으로만). 링이 아니면 빈 글자.</summary>
    public string SelectedPortText => SelectedFenceRow is { } r && _board.NumberOf(r.Key) is { OppositeOrder: { } b } n
        ? $"{n.Order} · {b} — {WiringValidation.PORT_1} 에서 {n.Order}번째 · {WiringValidation.PORT_2} 에서 {b}번째"
        : string.Empty;

    public bool HasSelectedPort => SelectedPortText.Length > 0;

    /// <summary>앞 센서와의 거리 — 두 종류 기본 간격 중 작은 값(FR-17).</summary>
    public string SelectedDistanceText
    {
        get
        {
            if (SelectedFenceRow is not { } r) return string.Empty;
            var keys = _board.Chain.Keys;
            var i = _board.Chain.IndexOf(r.Key);
            if (i <= 0) return string.Empty;
            var neighbour = _board.Shape == WiringShape.TwoBranch && i == _board.Chain.ControllerGap ? (int?)null : keys[i - 1];
            if (neighbour is not { } prev || _board.Find(prev) is not { } p) return string.Empty;
            var metres = _board.Spacing.GapBetween(WiringTopology.ParseSensorType(p.Facts.TypeText), WiringTopology.ParseSensorType(r.Facts.TypeText));
            return $"{metres:0.#}m · 두 종류 기본 간격 중 작은 값";
        }
    }

    public bool HasSelectedDistance => SelectedDistanceText.Length > 0;

    /// <summary>제품 사진(종류별 · 라이브러리 리소스) — 없으면 <c>null</c>(그림 자리에 "사진 준비 중").</summary>
    public Uri? SelectedPhoto
    {
        get
        {
            if (_isControllerSelected) return IsRing ? AssetUri("thumb-1u-dock.png") : null;
            if (SelectedFenceRow is not { } r) return null;
            return WiringTopology.IsSmartSensor(WiringTopology.ParseSensorType(r.Facts.TypeText)) ? AssetUri("thumb-smart-sensor2.png") : null;
        }
    }

    public bool HasSelectedPhoto => SelectedPhoto is not null;

    public string SelectedPhotoCaption => _isControllerSelected
        ? (IsRing ? "1U 도킹 — 스마트 제어기 + VBUS 제어기" : "제품 사진 준비 중")
        : SelectedFenceRow is { } r
            ? $"{DeviceEnumDisplay.SensorTypeBilingual(r.Facts.TypeText)} · 기본 간격 {_board.Spacing.SpacingOf(WiringTopology.ParseSensorType(r.Facts.TypeText)):0.#}m"
              + (FenceWorld.RangeOf(FenceWorld.KindOf(WiringTopology.ParseSensorType(r.Facts.TypeText))) is var range and > 0 ? $" · 탐지 반경 {range:0}m" : string.Empty)
              + (HasSelectedPhoto ? string.Empty : " · 제품 사진 준비 중")
            : string.Empty;

    internal static Uri AssetUri(string file)
        => new($"pack://application:,,,/Ironwall.Dotnet.Libraries.Devices.Ui;component/Consoles/Wiring/Fence/Assets/{file}", UriKind.Absolute);

    #region - Facing (FR-20) -
    /// <summary>방향 칸을 보일까 — 센서 하나 또는 여럿을 골랐을 때.</summary>
    public bool HasFacingRow => SelectedFenceRow is not null || HasMultiSelection;

    /// <summary>방향을 바꿀 센서 — 고른 것 중 방향이 있는 것(스마트 복합 · 복합)만, 고른 차례.</summary>
    private IReadOnlyList<int> FacingTargets()
    {
        var keys = HasMultiSelection ? _fenceSelection.ToList() : SelectedFenceRow is { } r ? new List<int> { r.Key } : new List<int>();
        return keys.Where(_board.SupportsFacing).ToList();
    }

    /// <summary>[앞(외부)] · [뒤(내부)]를 누를 수 있는가 — 펜스센서 · 지진동은 방향이 없다.</summary>
    public bool CanChooseFacing => !IsBusy && FacingTargets().Count > 0;

    /// <summary>고른 기둥 센서가 모두 앞을 보는가(분할 단추의 켜짐 표시).</summary>
    public bool IsSelectedFront => FacingTargets() is { Count: > 0 } t && t.All(k => _board.FacingOf(k) == WiringFacing.Front);

    public bool IsSelectedBack => FacingTargets() is { Count: > 0 } t && t.All(k => _board.FacingOf(k) == WiringFacing.Back);

    /// <summary>방향 칸 아래 한 줄 — 방향이 없는 종류면 까닭, 섞였으면 그 말.</summary>
    public string FacingNote
    {
        get
        {
            if (!HasFacingRow) return string.Empty;
            if (FacingTargets().Count == 0) return "펜스센서는 철망 가운데 · 지진동센서는 땅속 — 방향이 없습니다";
            var side = IsSelectedBack ? "펜스 내부에 답니다" : IsSelectedFront ? "펜스 외부에 답니다" : "설치 면이 섞여 있습니다";
            var yaw = SelectedYaw is { } y ? $"{WiringYawMath.LongText(y)}을 봅니다" : "보는 방향이 섞여 있습니다";
            return $"{side} · {yaw} — F 면 뒤집기 · R / Shift+R 돌리기";
        }
    }

    /// <summary>[앞(외부)] — 고른 기둥 센서를 모두 앞으로. 되돌리기 한 걸음.</summary>
    public bool FenceSetFacingFront() => FenceSetFacing(FacingTargets(), WiringFacing.Front);

    /// <summary>[뒤(내부)] — 고른 기둥 센서를 모두 뒤로. 되돌리기 한 걸음.</summary>
    public bool FenceSetFacingBack() => FenceSetFacing(FacingTargets(), WiringFacing.Back);

    /// <summary>
    /// 키 F — 그 센서(여럿을 골랐고 그 안이면 고른 것 전부)의 방향을 뒤집는다. 여럿이면 <b>첫 센서의 반대쪽으로 모두 맞춘다</b>
    /// (따로 뒤집으면 섞인 채로 남는다). 방향이 없는 종류는 건너뛴다.
    /// </summary>
    public bool FenceFlipFacing(int key)
    {
        var keys = (_fenceSelection.Count > 1 && _fenceSelection.Contains(key) ? _fenceSelection.ToList() : new List<int> { key })
                   .Where(_board.SupportsFacing).ToList();
        if (keys.Count == 0)
        {
            StatusText = "펜스센서 · 지진동센서는 방향이 없습니다(철망 가운데 · 땅속).";
            return false;
        }
        var first = keys.Contains(key) ? key : keys[0];
        var next = _board.FacingOf(first) == WiringFacing.Front ? WiringFacing.Back : WiringFacing.Front;
        return FenceSetFacing(keys, next);
    }

    /// <summary>표 보기의 키 F — 고른 칸(여럿이면 고른 칸 전부)의 방향을 첫 칸의 반대쪽으로 맞춘다.</summary>
    public bool FlipSelectedSlotFacing()
    {
        var keys = Line1.Concat(Line2).Where(s => s.IsSelected && s.Row is not null).Select(s => s.Row!.Key).ToList();
        if (keys.Count == 0) return false;
        var capable = keys.Where(_board.SupportsFacing).ToList();
        if (capable.Count == 0)
        {
            StatusText = "펜스센서 · 지진동센서는 방향이 없습니다(철망 가운데 · 땅속).";
            return false;
        }
        var next = _board.FacingOf(capable[0]) == WiringFacing.Front ? WiringFacing.Back : WiringFacing.Front;
        var selected = keys.ToHashSet();
        var ok = FenceSetFacing(capable, next);
        foreach (var slot in Line1.Concat(Line2)) slot.IsSelected = slot.Row is not null && selected.Contains(slot.Row.Key);   // 다시 그려도 고른 칸 유지
        return ok;
    }

    private bool FenceSetFacing(IReadOnlyList<int> keys, WiringFacing facing)
    {
        if (IsBusy || keys.Count == 0) return false;
        _board.PushUndo();
        if (_board.SetFacing(keys, facing) == 0)
        {
            _board.Undo();
            StatusText = $"바뀐 것이 없습니다 — 이미 {FacingName(facing)}";
            return false;
        }
        SyncAll();
        var who = keys.Count > 1 ? $"{keys.Count}대" : _board.Find(keys[0])?.Display;
        StatusText = $"방향 — {who}: {FacingName(facing)} · Ctrl+Z 로 되돌립니다";
        return true;
    }

    internal static string FacingName(WiringFacing facing) => facing == WiringFacing.Back ? "펜스 내부" : "펜스 외부";

    /// <summary>"펜스 외부 · 정방향(90°)".</summary>
    internal static string OrientationName(WiringFacing facing, WiringYaw yaw) => $"{FacingName(facing)} · {WiringYawMath.LongText(yaw)}";
    #endregion

    #region - Yaw (보는 방향 · 센서 방향 2026-10-01) -
    /// <summary>고른 방향 센서의 보는 방향이 모두 같으면 그것.</summary>
    public WiringYaw? SelectedYaw => FacingTargets().Select(_board.YawOf).Distinct().ToList() is { Count: 1 } one ? one[0] : null;

    public bool IsYawAway => SelectedYaw == WiringYaw.Away;
    public bool IsYawAlong => SelectedYaw == WiringYaw.Along;
    public bool IsYawToward => SelectedYaw == WiringYaw.Toward;
    public bool IsYawAgainst => SelectedYaw == WiringYaw.Against;

    /// <summary>방향 고르개 가운데 글자 — "정방향 90°" · 섞였으면 "여러 값".</summary>
    public string SelectedYawText => SelectedYaw is { } y ? $"{WiringYawMath.Text(y)} {(int)y}°" : FacingTargets().Count > 0 ? MULTI_VALUE : "—";

    /// <summary>[⟳ 90°] · [⟲ 90°] — 고른 방향 센서를 저마다 90° 돌린다. 되돌리기 한 걸음.</summary>
    public bool FenceRotateSelected(int steps) => FenceApplyYaw(FacingTargets(), steps, null);

    /// <summary>방향 고르개(정면 · 정방향 · 펜스 쪽 · 역방향) — 고른 방향 센서를 모두 그 방향으로. 되돌리기 한 걸음.</summary>
    public bool FenceSetYaw(WiringYaw yaw) => FenceApplyYaw(FacingTargets(), 0, yaw);

    /// <summary>
    /// 키 R(+90°) · Shift+R(−90°) — 그 센서(여럿을 골랐고 그 안이면 고른 것 전부)를 돌린다. 방향이 없는 종류는 건너뛴다.
    /// </summary>
    public bool FenceRotate(int key, int steps)
    {
        var keys = (_fenceSelection.Count > 1 && _fenceSelection.Contains(key) ? _fenceSelection.ToList() : new List<int> { key })
                   .Where(_board.SupportsFacing).ToList();
        if (keys.Count == 0)
        {
            StatusText = "펜스센서 · 지진동센서는 방향이 없습니다(철망 가운데 · 땅속).";
            return false;
        }
        return FenceApplyYaw(keys, steps, null);
    }

    /// <summary>메뉴 "외부로" · "내부로" — 고른 센서(이 센서가 선택 밖이면 이 센서만).</summary>
    public bool FenceSetSide(IReadOnlyList<int> keys, WiringFacing facing) => FenceSetFacing((keys ?? Array.Empty<int>()).Where(_board.SupportsFacing).ToList(), facing);

    private bool FenceApplyYaw(IReadOnlyList<int> keys, int steps, WiringYaw? to)
    {
        if (IsBusy || keys.Count == 0) return false;
        _board.PushUndo();
        var changed = to is { } yaw ? _board.SetYaw(keys, yaw) : _board.RotateYaw(keys, steps);
        if (changed == 0)
        {
            _board.Undo();
            StatusText = to is { } y ? $"바뀐 것이 없습니다 — 이미 {WiringYawMath.LongText(y)}" : "바뀐 것이 없습니다.";
            return false;
        }
        SyncAll();
        var who = keys.Count > 1 ? $"{keys.Count}대" : _board.Find(keys[0])?.Display;
        var what = to is { } target ? WiringYawMath.LongText(target)
                 : keys.Count == 1 ? $"{(steps > 0 ? "⟳" : "⟲")} {WiringYawMath.LongText(_board.YawOf(keys[0]))}"
                 : $"{(steps > 0 ? "⟳" : "⟲")} 90° 씩";
        StatusText = $"보는 방향 — {who}: {what} · Ctrl+Z 로 되돌립니다";
        return true;
    }
    #endregion

    public bool IsSelectedPlaced => SelectedFenceRow is { } r && _board.Chain.Contains(r.Key);
    public bool IsSelectedUnplaced => SelectedFenceRow is { } r && !_board.Chain.Contains(r.Key);

    public bool CanStepSelectedBack => SelectedFenceRow is { } r && _board.LocationOf(r.Key) is { Index: > 0 };
    public bool CanStepSelectedForward => SelectedFenceRow is { } r && _board.LocationOf(r.Key) is { } at && at.Index < _board.CountOn(at.Line) - 1;

    /// <summary>함체 위치 글자(링) — "#6 ~ #7 사이".</summary>
    public string EnclosureGapText
    {
        get
        {
            var n = _board.Chain.Count;
            var g = _board.Chain.ControllerGap;
            return g <= 0 ? "#1 왼쪽" : g >= n ? $"#{n} 오른쪽" : $"#{g} ~ #{g + 1} 사이";
        }
    }

    private void RaiseSelection()
    {
        _focusReplaced = null;
        foreach (var name in new[]
        {
            nameof(FenceSelectedKey), nameof(IsControllerSelected), nameof(HasFenceSelection), nameof(HasSensorSelection), nameof(HasNoFenceSelection),
            nameof(SelectedKindText), nameof(SelectedTitle), nameof(SelectedNumberText), nameof(IsSelectedDuplicateNumber), nameof(SelectedIdText),
            nameof(SelectedTypeText), nameof(SelectedChannelText), nameof(SelectedChannelNote), nameof(SelectedPositionText), nameof(SelectedPortText),
            nameof(HasSelectedPort), nameof(SelectedDistanceText), nameof(HasSelectedDistance), nameof(SelectedPhoto), nameof(HasSelectedPhoto),
            nameof(SelectedPhotoCaption), nameof(IsSelectedPlaced), nameof(IsSelectedUnplaced), nameof(CanStepSelectedBack),
            nameof(CanStepSelectedForward), nameof(EnclosureGapText), nameof(FenceCountsText), nameof(HasRangeSensors),
            nameof(FenceSelectedKeys), nameof(HasMultiSelection), nameof(MultiSelectionText),
            nameof(HasFacingRow), nameof(CanChooseFacing), nameof(IsSelectedFront), nameof(IsSelectedBack), nameof(FacingNote),
            nameof(SelectedYaw), nameof(IsYawAway), nameof(IsYawAlong), nameof(IsYawToward), nameof(IsYawAgainst), nameof(SelectedYawText),
            nameof(SelectedAddressLabel), nameof(SelectedAddressText), nameof(FencePaneKind), nameof(HasAnySelection), nameof(HasPanelSelection),
            nameof(ControllerSignal), nameof(ControllerSignalText), nameof(BandText), nameof(HasBands), nameof(ConceptTitle), nameof(HasIpSensors),
        }) NotifyOfPropertyChange(name);
        _editOffset = null;
        RaiseMountPane();
        SyncTableSelection();
        FenceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>체인이 바뀌면(보드 동기화 끝) — 선택이 사라졌으면 풀고 캔버스를 다시 그린다.</summary>
    private void RaiseFence()
    {
        _fenceSelection.RemoveAll(k => _board.Find(k) is null);
        if (_fenceSelectedKey is { } k && _board.Find(k) is null) _fenceSelectedKey = _fenceSelection.Count > 0 ? _fenceSelection[^1] : null;
        _panelSelection.RemoveAll(i => i < 0 || i >= _board.FenceLayout.Panels.Count);
        if (_selectionKind == FenceSelectionKind.Panels && _panelSelection.Count == 0) _selectionKind = KindAfterClear();
        if (_selectionKind == FenceSelectionKind.Sensors && _fenceSelection.Count == 0) _selectionKind = KindAfterClear();
        RaiseLayoutPane();
        RaiseSelection();
    }
    #endregion

    #region - Fence edits (FR-08 · FR-09 · FR-11) -
    /// <summary>펜스에서 끌어 놓기 — 선 · 자리(옮기기 전 기준). 되돌리기 한 걸음.</summary>
    public bool FencePlace(IReadOnlyList<int> keys, int line, int index)
    {
        if (keys is null || keys.Count == 0 || IsBusy) return false;
        var before = keys[0];
        var wasPlaced = _board.Chain.Contains(before);
        _board.PushUndo();
        if (_board.PlaceMany(keys, line, index) == 0)
        {
            _board.Undo();
            StatusText = "제자리 — 바뀐 것이 없습니다.";
            return false;
        }
        SyncAll();
        var label = _board.NumberOf(before) is { } n ? NumberText(n) : "미배치";
        StatusText = $"{(wasPlaced ? "옮김" : "붙임")} — {(keys.Count > 1 ? $"{keys.Count}대" : _board.Find(before)?.Display)}: {label} · Ctrl+Z 로 되돌립니다";
        return true;
    }

    /// <summary>펜스에서 빼기(빼는 곳 · 팔레트로 끌기 · Delete · [빼기]).</summary>
    public bool FenceUnplace(IReadOnlyList<int> keys)
    {
        if (keys is null || keys.Count == 0 || IsBusy) return false;
        _board.PushUndo();
        if (_board.UnplaceMany(keys) == 0)
        {
            _board.Undo();
            StatusText = "이미 미배치입니다.";
            return false;
        }
        SyncAll();
        StatusText = $"뺌 — {(keys.Count > 1 ? $"{keys.Count}대" : _board.Find(keys[0])?.Display)}: 미배치, 뒤 위치가 당겨졌습니다 · Ctrl+Z 로 되돌립니다";
        return true;
    }

    /// <summary>체인 끝에 붙이기(팔레트 Enter · [체인 끝에 붙이기]).</summary>
    public bool FenceAppend(int key)
    {
        if (IsBusy) return false;
        _board.PushUndo();
        if (_board.Append(new[] { key }) == 0)
        {
            _board.Undo();
            return false;
        }
        SyncAll();
        StatusText = $"붙임 — {_board.Find(key)?.Display}: {(_board.NumberOf(key) is { } n ? NumberText(n) : "미배치")}(끝)";
        return true;
    }

    /// <summary>[앞으로] · [뒤로] · Alt+←/→ — 같은 목록 안에서 한 칸.</summary>
    public bool FenceStep(int key, int direction)
    {
        if (IsBusy) return false;
        _board.PushUndo();
        if (!_board.MoveBy(key, direction))
        {
            _board.Undo();
            StatusText = direction < 0 ? "이미 맨 앞입니다." : "이미 맨 뒤입니다.";
            return false;
        }
        SyncAll();
        StatusText = $"옮김 — {_board.Find(key)?.Display}: {(_board.NumberOf(key) is { } n ? NumberText(n) : string.Empty)}";
        return true;
    }

    /// <summary>Alt+Home/End — 목록 맨 앞 · 맨 끝으로.</summary>
    public bool FenceToEnd(int key, bool end)
    {
        if (_board.LocationOf(key) is not { } at) return false;
        return FencePlace(new[] { key }, at.Line, end ? _board.CountOn(at.Line) : 0);
    }

    /// <summary>함체를 틈 <paramref name="gap"/> 으로(표시만 · FR-09). 되돌리기 한 걸음.</summary>
    public bool FenceMoveEnclosure(int gap)
    {
        if (!IsRing || IsBusy) return false;
        _board.PushUndo();
        if (!_board.MoveControllerGap(gap))
        {
            _board.Undo();
            StatusText = "함체 위치 그대로입니다.";
            return false;
        }
        SyncAll();
        StatusText = $"함체 이동 — {EnclosureGapText} · 리턴케이블 · VBUS 표지만 따라감(체인 순서 · A/B 번호 그대로)";
        return true;
    }

    public void StepSelectedBack() { if (_fenceSelectedKey is { } k) FenceStep(k, -1); }
    public void StepSelectedForward() { if (_fenceSelectedKey is { } k) FenceStep(k, 1); }
    public void UnplaceFenceSelected()
    {
        if (HasMultiSelection) FenceUnplace(FenceDragKeys(_fenceSelection[0]).Where(_board.Chain.Contains).ToList());
        else if (_fenceSelectedKey is { } k) FenceUnplace(new[] { k });
    }
    public void AppendFenceSelected() { if (_fenceSelectedKey is { } k) FenceAppend(k); }
    public void MoveEnclosureBack() => FenceMoveEnclosure(_board.Chain.ControllerGap - 1);
    public void MoveEnclosureForward() => FenceMoveEnclosure(_board.Chain.ControllerGap + 1);
    #endregion

    /// <summary>캔버스가 상태줄에 한 줄 알린다(취소 · 함체 후보 · 묶음 접힘).</summary>
    public void NotifyFenceStatus(string text) => StatusText = text;

    #region - Palette → fence (kernel drop zone) -
    private bool CanDropOnFence(DragPayload payload)
        => payload.Items.Count > 0 && payload.Items.All(i => i is SensorRowViewModel || i is WiringSlotViewModel { IsFilled: true });

    private void DropOnFence(DragPayload payload, DropTarget target)
    {
        if (target.ZoneData is not IFenceDropSurface surface) return;
        var keys = payload.Items.Select(i => i switch
        {
            SensorRowViewModel row => row.Key,
            WiringSlotViewModel slot => slot.Row?.Key ?? 0,
            _ => 0,
        }).Where(k => k != 0).ToList();

        // 펜스 구성이 켜져 있으면 포인터 아래 망 · 기둥에 단다(자리 순서가 곧 체인 순서 · FR-09).
        if (_board.FenceLayout.IsActive && surface.PointerMetres() is { } metres)
        {
            if (FenceDropAt(keys, metres) && keys.Count == 1) FenceSelect(keys[0]);
            return;
        }
        if (surface.PointerTarget() is not { } at) return;
        if (FencePlace(keys, at.Line, at.Index) && keys.Count == 1) FenceSelect(keys[0]);
    }
    #endregion

    #region - Footer counts (FR-01 · FR-18) -
    /// <summary>
    /// 아래 띠(v0.4 §1-C) — "센서 13 · 체인 12 · 미배치 1 · 체인 길이 약 66m / 기준 200m". 종류가 둘 이상이면 앞에 종류별 수.
    /// 섞였으면 기준은 "확인 중(O-11)" — 숫자를 지어내지 않는다.
    /// </summary>
    public string FenceCountsText
    {
        get
        {
            var all = _board.Rows.Count;
            var placed = _board.Chain.Count;
            var unplaced = _board.Unplaced.Count;
            var kinds = _board.Rows.GroupBy(r => FenceWorld.KindOf(WiringTopology.ParseSensorType(r.Facts.TypeText)))
                                   .ToDictionary(g => g.Key, g => g.Count());
            var parts = new List<string>();
            if (kinds.Count > 1)
                parts.AddRange(new[] { (FenceKind.Smart, "스마트"), (FenceKind.Multi, "복합"), (FenceKind.Fence, "펜스"), (FenceKind.Underground, "지진동") }
                    .Where(k => kinds.ContainsKey(k.Item1)).Select(k => $"{k.Item2} {kinds[k.Item1]}"));
            parts.Add($"센서 {all} · 체인 {placed} · 미배치 {unplaced}");
            var reference = _board.Limits.ReferenceLength(_board.Family) is { } m ? $"기준 {m:0}m"
                : _board.IsMixedFamily ? $"기준 — {WiringLimitTable.MIXED_SHORT}" : "기준 —";
            parts.Add($"체인 길이 약 {_board.ChainLengthMetres:0}m / {reference}");
            return string.Join(" · ", parts);
        }
    }
    #endregion
}

/// <summary>팔레트에서 끌어 온 것을 놓을 펜스 자리 — 캔버스가 포인터 아래 자리를 준다(커널 드롭존 → 뷰모델).</summary>
public interface IFenceDropSurface
{
    /// <summary>지금 포인터 아래 (선, 옮기기 전 자리). 캔버스 밖이면 <c>null</c>.</summary>
    (int Line, int Index)? PointerTarget();

    /// <summary>지금 포인터 아래 펜스 위 가로 위치(m, A 쪽 끝이 0) — 펜스 구성이 켜진 캔버스만. 모르면 <c>null</c>.</summary>
    double? PointerMetres() => null;
}
