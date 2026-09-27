using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Units;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Door;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Utils.Converters;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : PIDS 심볼 상세 보기 창 VM — PRD symbol-detail-and-door-control FR-15/18/19/20
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>정보 탭 한 줄(라벨-값). <paramref name="Tone"/>=상태색 힌트(스타일 트리거가 읽는다).</summary>
/// <param name="Label">항목 이름.</param>
/// <param name="Value">표시 값. 값이 없으면 "—".</param>
/// <param name="Tone">null·"ok"·"warn"·"fault" 중 하나.</param>
public sealed record SymbolDetailField(string Label, string Value, string? Tone = null);

/// <summary>"소속 부대" 한 줄의 상태(FR-46).</summary>
/// <param name="IsVisible">줄을 보이는가 — 부대 사전이 없거나 서버가 8.0 미만이면 숨긴다.</param>
/// <param name="Text">줄 문구("소속 부대 7중대 · 2대대 › 1연대 › 제○○사단").</param>
/// <param name="UnitId">장비의 소속 부대 id(없으면 <c>null</c>).</param>
/// <param name="CanOpen">[관계도에서 보기]를 누를 수 있는가 — 편제에서 찾은 부대만.</param>
public sealed record SymbolDetailUnitLineState(bool IsVisible, string Text, int? UnitId, bool CanOpen)
{
    public static SymbolDetailUnitLineState Hidden { get; } = new(false, string.Empty, null, false);
}

/// <summary>"소속 부대" 줄의 순수 판정(FR-46) — 상세 창 · 마커 우클릭 메뉴가 같은 문구를 쓴다.</summary>
public static class SymbolDetailUnitLine
{
    public const string Prefix = "소속 부대";
    public const string NoUnitText = "소속 부대 정보 없음";
    public const string LoadingText = "소속 부대 불러오는 중…";

    /// <param name="unitId">장비 모델의 <c>UnitId</c>(심볼의 <b>연결 장비 객체</b>에서 읽는다 — 장비 미연결이면 <c>null</c>).</param>
    /// <param name="directory">부대 사전(<c>null</c> = DI 미등록).</param>
    /// <param name="loadAttempted">사전 적재를 한 번 기다렸는가 — 그 뒤에도 못 찾으면 "불러오는 중" 대신 "편제에서 찾지 못함".</param>
    public static SymbolDetailUnitLineState Evaluate(int? unitId, IUnitDirectory? directory, bool loadAttempted)
    {
        if (directory is null || !directory.IsAvailable) return SymbolDetailUnitLineState.Hidden;
        if (unitId is not int id || id <= 0) return new(true, NoUnitText, null, false);

        var described = directory.Describe(id);
        if (!string.IsNullOrWhiteSpace(described)) return new(true, $"{Prefix} {described}", id, true);

        return loadAttempted
            ? new(true, $"{Prefix} #{id} — 편제에서 찾지 못했습니다", id, false)   // 이름을 지어내지 않는다
            : new(true, LoadingText, id, false);
    }
}

/// <summary>탭 하나 — 헤더·활성 여부·내용.</summary>
public sealed class SymbolDetailTabModel : PropertyChangedBase
{
    public SymbolDetailTabModel(SymbolDetailTab tab, string header, bool isEnabled, string? disabledReason)
    {
        Tab = tab; Header = header; IsEnabled = isEnabled; DisabledReason = disabledReason;
    }

    public SymbolDetailTab Tab { get; }
    public string Header { get; }
    public bool IsEnabled { get; }
    /// <summary>비활성 사유 — 빈 탭을 눌러보게 두지 않는다(FR-28).</summary>
    public string? DisabledReason { get; }
    public ObservableCollection<SymbolDetailField> Fields { get; } = new();
    /// <summary>계측 앵커 — `GMaps.SymbolDetail.Tab.Basic` 형태.</summary>
    public string AutomationId => $"GMaps.SymbolDetail.Tab.{Tab}";

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; NotifyOfPropertyChange(nameof(IsSelected)); } }
}

/// <summary>액션 바 버튼 하나 — 기존 컨텍스트 메뉴 항목과 1:1.</summary>
public sealed class SymbolDetailActionModel : PropertyChangedBase
{
    public SymbolDetailActionModel(SymbolDetailAction action, string header, PackIconKind icon, bool isPrimary, ICommand command)
    {
        Action = action; Header = header; Icon = icon; IsPrimary = isPrimary; Command = command;
    }

    public SymbolDetailAction Action { get; }
    public string Header { get; }
    public PackIconKind Icon { get; }
    public bool IsPrimary { get; }
    public ICommand Command { get; }
    public string AutomationId => $"GMaps.SymbolDetail.Action.{Action}";

    private bool _isEnabled;
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; NotifyOfPropertyChange(nameof(IsEnabled)); } }

    private string? _disabledReason;
    /// <summary>비활성 사유 — 항상 툴팁으로 노출한다(<c>ToolTipService.ShowOnDisabled</c>).</summary>
    public string? DisabledReason { get => _disabledReason; set { _disabledReason = value; NotifyOfPropertyChange(nameof(DisabledReason)); } }
}

/// <summary>
/// 심볼 상세 보기 창의 DataContext. 좌측 스테이지(3D/2D 프리뷰) + 우측 5탭 + 하단 액션 바.
///
/// <para><b>판정은 하지 않는다</b> — 어떤 탭·버튼이 보이고 눌리는지는 전부
/// <see cref="SymbolDetailRules"/>(순수 함수, 헤드리스 테스트 32건)가 정하고 이 VM 은 그 결과를 그린다.
/// 실제 동작은 <see cref="ActionRequested"/> 로 <c>MapViewModel</c> 에 위임한다 —
/// 컨텍스트 메뉴와 <b>같은 메서드</b>를 부르게 해서 "메뉴는 되는데 버튼은 안 되는" 상태를 원천 차단한다(FR-21).</para>
/// </summary>
public sealed class SymbolDetailViewModel : PropertyChangedBase, IDisposable
{
    /// <param name="unitDirectory">부대 이름 · 경로 사전(선택 — 없으면 "소속 부대" 줄을 숨긴다).</param>
    /// <param name="events">[관계도에서 보기]를 보낼 곳(선택 — 없으면 단추가 눌리지 않는다).</param>
    public SymbolDetailViewModel(IUnitDirectory? unitDirectory = null, IEventAggregator? events = null)
    {
        _unitDirectory = unitDirectory;
        _events = events;
        OpenUnitMapCommand = new RelayCommand(_ => OpenUnitMap(), _ => UnitLine.CanOpen && _events != null);
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
        ResetViewCommand = new RelayCommand(_ => ResetViewRequested?.Invoke());
        ToggleAutoRotateCommand = new RelayCommand(_ => IsAutoRotating = !IsAutoRotating);
        SelectTabCommand = new RelayCommand(p => { if (p is SymbolDetailTabModel tab) SelectTab(tab); });
        _actionCommand = new RelayCommand(p => { if (p is SymbolDetailActionModel item) ActionRequested?.Invoke(item.Action); });
    }

    #region - Events (MapViewModel 배선) -

    public event System.Action? CloseRequested;
    /// <summary>액션 바 버튼 클릭 — 실제 동작은 MapViewModel 이 수행(단일 출처).</summary>
    public event System.Action<SymbolDetailAction>? ActionRequested;
    /// <summary>`⟲ 정면` — 컨트롤이 프리뷰 카메라를 되돌린다.</summary>
    public event System.Action? ResetViewRequested;
    /// <summary>마이크 버튼을 <b>누른 순간</b>. push-to-talk 라 토글이 아니다(FR-22).</summary>
    public event System.Action? MicPressed;
    /// <summary>마이크 버튼에서 <b>손을 뗀 순간</b>(캡처 상실 포함) — 반드시 짝이 맞아야 방송이 멈춘다.</summary>
    public event System.Action? MicReleased;

    #endregion

    #region - Commands -

    public RelayCommand CloseCommand { get; }
    public RelayCommand ResetViewCommand { get; }
    public RelayCommand ToggleAutoRotateCommand { get; }
    public RelayCommand SelectTabCommand { get; }
    /// <summary>[관계도에서 보기] — 부대 콘솔을 관계도 레일로 열고 그 부대를 고른다(<c>OpenUnitConsoleRequest</c>).</summary>
    public RelayCommand OpenUnitMapCommand { get; }
    private readonly RelayCommand _actionCommand;

    #endregion

    #region - Fields -

    private readonly IUnitDirectory? _unitDirectory;
    private readonly IEventAggregator? _events;

    private IPidsEditableMarker? _marker;
    private INotifyPropertyChanged? _markerNotifier;
    private SymbolDetailContext _context;

    #endregion

    #region - Presentation properties -

    private string _headerText = string.Empty;
    /// <summary>제목 표시줄 — "상세 보기 — 감지센서 · 외곽-07".</summary>
    public string HeaderText { get => _headerText; private set { _headerText = value; NotifyOfPropertyChange(nameof(HeaderText)); } }

    private string _symbolName = string.Empty;
    public string SymbolName { get => _symbolName; private set { _symbolName = value; NotifyOfPropertyChange(nameof(SymbolName)); } }

    private string _kindText = string.Empty;
    /// <summary>스테이지 아래 설명 — "3D 심볼 · 45°씩 자동 회전" / "2D 심볼 · 회전 없음".</summary>
    public string KindText { get => _kindText; private set { _kindText = value; NotifyOfPropertyChange(nameof(KindText)); } }

    private bool _is3D;
    /// <summary>3D 하우징을 가진 타입인가. false 면 2D 아이콘을 고정 표시한다(FR-16).</summary>
    public bool Is3D { get => _is3D; private set { _is3D = value; NotifyOfPropertyChange(nameof(Is3D)); NotifyOfPropertyChange(nameof(Is2D)); } }
    public bool Is2D => !_is3D;

    private string? _modelKey;
    public string? ModelKey { get => _modelKey; private set { _modelKey = value; NotifyOfPropertyChange(nameof(ModelKey)); } }

    private EnumDeviceType _deviceType;
    /// <summary>2D 아이콘 매핑(<see cref="HousingFallbackIconConverter"/>)의 입력.</summary>
    public EnumDeviceType DeviceType { get => _deviceType; private set { _deviceType = value; NotifyOfPropertyChange(nameof(DeviceType)); } }

    private double _doorOpen;
    /// <summary>프리뷰 문 열림 0/1 — 통문·함체. 마커 <c>DoorState</c> 를 그대로 따라간다.</summary>
    public double DoorOpen { get => _doorOpen; private set { _doorOpen = value; NotifyOfPropertyChange(nameof(DoorOpen)); } }

    private bool _isAutoRotating = true;
    public bool IsAutoRotating
    {
        get => _isAutoRotating;
        set { _isAutoRotating = value; NotifyOfPropertyChange(nameof(IsAutoRotating)); NotifyOfPropertyChange(nameof(AutoRotateToggleText)); }
    }
    public string AutoRotateToggleText => _isAutoRotating ? "회전 멈춤" : "회전 시작";

    public ObservableCollection<SymbolDetailTabModel> Tabs { get; } = new();
    public ObservableCollection<SymbolDetailActionModel> Actions { get; } = new();

    private SymbolDetailTabModel? _selectedTab;
    public SymbolDetailTabModel? SelectedTab
    {
        get => _selectedTab;
        private set { _selectedTab = value; NotifyOfPropertyChange(nameof(SelectedTab)); }
    }

    private bool _isBroadcasting;
    /// <summary>이 스피커가 지금 송출 중인가 — <c>BROADCAST_STATUS</c> 수신 또는 마이크 누름(로컬 폴백).</summary>
    public bool IsBroadcasting
    {
        get => _isBroadcasting;
        set { _isBroadcasting = value; NotifyOfPropertyChange(nameof(IsBroadcasting)); NotifyOfPropertyChange(nameof(BroadcastStateText)); RebuildBroadcastTab(); }
    }

    public string BroadcastStateText => _isBroadcasting ? "송출 중" : "대기";

    private bool _isMicHeld;
    /// <summary>마이크 버튼을 누르고 있는 중.</summary>
    public bool IsMicHeld
    {
        get => _isMicHeld;
        private set { _isMicHeld = value; NotifyOfPropertyChange(nameof(IsMicHeld)); }
    }

    private string _noDeviceReason = string.Empty;
    /// <summary>
    /// 장비 정보를 못 보여주는 <b>사유</b> — 두 경우를 구분한다(2026-09-08 실기 발견).
    /// <para>① 애초에 장비를 연결하지 않은 심볼 ② 연결 Id 는 있는데 그 장비를 장비 목록에서 못 찾은 경우.
    /// ②를 ①과 같이 뭉뚱그리면 "왜 안 보이지"를 사용자가 추적할 수 없다.</para>
    /// </summary>
    public string NoDeviceReason
    {
        get => _noDeviceReason;
        private set { _noDeviceReason = value; NotifyOfPropertyChange(nameof(NoDeviceReason)); }
    }

    private bool _hasDevice;
    /// <summary>장비 연결 여부 — 미연결 안내 문구 표시에 쓴다.</summary>
    public bool HasDevice { get => _hasDevice; private set { _hasDevice = value; NotifyOfPropertyChange(nameof(HasDevice)); } }

    private SymbolDetailUnitLineState _unitLine = SymbolDetailUnitLineState.Hidden;
    /// <summary>"소속 부대" 줄(FR-46). 바뀌면 문구 · 표시 · 단추가 함께 바뀐다.</summary>
    public SymbolDetailUnitLineState UnitLine
    {
        get => _unitLine;
        private set
        {
            _unitLine = value;
            NotifyOfPropertyChange(nameof(UnitLine));
            NotifyOfPropertyChange(nameof(IsUnitLineVisible));
            NotifyOfPropertyChange(nameof(UnitLineText));
            OpenUnitMapCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsUnitLineVisible => _unitLine.IsVisible;
    public string UnitLineText => _unitLine.Text;

    /// <summary>가장 최근 사전 적재 대기(시험용).</summary>
    internal Task PendingUnitLoad { get; private set; } = Task.CompletedTask;

    #endregion

    #region - Load -

    /// <summary>
    /// 창 내용을 채운다. 장비 정보는 <paramref name="marker"/>.LinkedDevice 에서 읽고,
    /// 권한·웹서버 같은 바깥 사정은 <paramref name="context"/> 로 받는다(VM 은 서비스를 모른다).
    /// </summary>
    /// <param name="marker">대상 PIDS 심볼.</param>
    /// <param name="context">탭·액션 판정 입력.</param>
    /// <param name="layerName">심볼이 속한 레이어 이름(없으면 null).</param>
    /// <param name="groupNames">장비그룹 표시 문자열.</param>
    public void Load(IPidsEditableMarker marker, in SymbolDetailContext context,
        string? layerName = null, string? groupNames = null)
    {
        Unsubscribe();
        _marker = marker ?? throw new ArgumentNullException(nameof(marker));
        _context = context;

        var device = marker.LinkedDevice;
        DeviceType = marker.DeviceType;
        HasDevice = context.HasDevice;

        string typeName = UiKoreanMap.To(marker.DeviceType);
        SymbolName = string.IsNullOrWhiteSpace(marker.Title) ? typeName : marker.Title;
        HeaderText = $"상세 보기 — {typeName} · {SymbolName}";

        // 3D/2D 판정은 지도와 같은 조건이어야 한다 — 지도는 3D 인데 상세 창은 2D 면 다른 장비처럼 보인다.
        ModelKey = Symbol3DFeature.IsEnabled ? Symbols3D.HousingModels.DeviceKey(marker.DeviceType) : null;
        Is3D = !string.IsNullOrEmpty(ModelKey);
        KindText = Is3D ? "3D 심볼 · 45°씩 자동 회전\n드래그로 자유 회전 · 놓으면 재개" : "2D 심볼 · 회전 없음";
        DoorOpen = marker.DoorState == EnumDoorState.Open ? 1 : 0;

        NoDeviceReason = context.HasDevice ? string.Empty
            : marker.LinkedDeviceId > 0
                ? $"연결된 장비(#{marker.LinkedDeviceId}) 정보를 찾지 못했습니다 — 장비 목록에 없거나 아직 불러오지 못했습니다."
                : "이 심볼은 장비와 연결되지 않아 심볼 탭만 볼 수 있습니다.";

        RefreshUnitLine(device?.UnitId);
        BuildTabs(marker, device, layerName, groupNames);
        BuildActions(marker.DeviceType);
        RefreshActionStates();
        Subscribe(marker);
    }

    private int? _unitLineUnitId;
    private bool _unitLoadAttempted;
    private bool _unitDirectoryHooked;

    /// <summary>"소속 부대" 줄을 다시 계산한다 — 사전에 이름이 없으면 한 번 읽게 하고, 읽은 뒤 다시 계산한다. 호출 스레드: UI.</summary>
    private void RefreshUnitLine(int? unitId)
    {
        _unitLineUnitId = unitId;
        _unitLoadAttempted = false;
        HookUnitDirectory();
        UnitLine = SymbolDetailUnitLine.Evaluate(unitId, _unitDirectory, _unitLoadAttempted);

        if (_unitDirectory is { IsAvailable: true } directory && unitId is > 0 && !UnitLine.CanOpen)
            PendingUnitLoad = LoadUnitDirectoryAsync(directory, unitId);
    }

    private async Task LoadUnitDirectoryAsync(IUnitDirectory directory, int? unitId)
    {
        try { await directory.EnsureLoadedAsync(); }
        catch (Exception) { /* 사전이 스스로 로그를 남긴다 — 여기서는 "찾지 못함" 으로 떨어진다 */ }

        if (_unitLineUnitId != unitId || _marker is null) return;   // 그 사이 다른 심볼을 열었다
        _unitLoadAttempted = true;
        UnitLine = SymbolDetailUnitLine.Evaluate(unitId, _unitDirectory, _unitLoadAttempted);
    }

    /// <summary>사전이 편제를 다시 읽었다(SYNC_UNIT) — 문구만 다시 계산한다. 사전은 UI 스레드로 옮겨 발화한다.</summary>
    private void OnUnitDirectoryChanged(object? sender, EventArgs e)
    {
        if (_marker is null) return;
        UnitLine = SymbolDetailUnitLine.Evaluate(_unitLineUnitId, _unitDirectory, loadAttempted: true);
    }

    private void HookUnitDirectory()
    {
        if (_unitDirectory is null || _unitDirectoryHooked) return;
        _unitDirectory.Changed += OnUnitDirectoryChanged;
        _unitDirectoryHooked = true;
    }

    private void UnhookUnitDirectory()
    {
        if (_unitDirectory is null || !_unitDirectoryHooked) return;
        _unitDirectory.Changed -= OnUnitDirectoryChanged;
        _unitDirectoryHooked = false;
    }

    /// <summary>[관계도에서 보기] — 부대 콘솔(런처)이 받아 창을 열거나 활성화하고 관계도 레일에서 그 부대를 고른다.</summary>
    private void OpenUnitMap()
    {
        if (_events is null || !UnitLine.CanOpen || UnitLine.UnitId is not int unitId) return;
        _ = PublishOpenUnitConsoleAsync(new OpenUnitConsoleRequest(unitId, OpenMap: true));
    }

    private async Task PublishOpenUnitConsoleAsync(OpenUnitConsoleRequest request)
    {
        try { await _events!.PublishOnCurrentThreadAsync(request); }
        catch (Exception) { /* 받는 쪽(런처)의 실패는 받는 쪽이 알린다 */ }
    }

    /// <summary>권한·문 상태가 바뀌었을 때 액션 활성만 다시 계산한다(창을 다시 열 필요 없이).</summary>
    public void UpdateContext(in SymbolDetailContext context)
    {
        _context = context;
        RefreshActionStates();
    }

    /// <summary>마이크 버튼을 눌렀다 — 규칙이 막고 있으면 발화하지 않는다(비활성 버튼의 우회 차단).</summary>
    public void BeginMic()
    {
        if (IsMicHeld) return;
        var ctx = _context;
        if (!SymbolDetailRules.Evaluate(SymbolDetailAction.MicPtt, in ctx).IsEnabled) return;
        IsMicHeld = true;
        MicPressed?.Invoke();
    }

    /// <summary>손을 뗐다(또는 캡처를 잃었다) — <b>누른 적이 있으면 반드시</b> 중지를 보낸다.</summary>
    public void EndMic()
    {
        if (!IsMicHeld) return;
        IsMicHeld = false;
        MicReleased?.Invoke();
    }

    /// <summary>송출 상태가 바뀌면 방송 탭 내용을 다시 만든다(탭을 다시 열 필요 없이).</summary>
    private void RebuildBroadcastTab()
    {
        if (_marker is null) return;
        var tab = Tabs.FirstOrDefault(t => t.Tab == SymbolDetailTab.Broadcast);
        if (tab is null) return;
        tab.Fields.Clear();
        foreach (var field in FieldsOf(SymbolDetailTab.Broadcast, _marker, _marker.LinkedDevice, null, null))
            tab.Fields.Add(field);
    }

    private void BuildTabs(IPidsEditableMarker marker, IBaseDeviceModel? device, string? layerName, string? groupNames)
    {
        Tabs.Clear();
        foreach (var tab in SymbolDetailRules.VisibleTabs(marker.DeviceType))
        {
            bool enabled = SymbolDetailRules.IsTabEnabled(tab, _context.HasDevice);
            var model = new SymbolDetailTabModel(tab, HeaderOf(tab), enabled, enabled ? null : "장비 미연결");
            foreach (var field in FieldsOf(tab, marker, device, layerName, groupNames))
                model.Fields.Add(field);
            Tabs.Add(model);
        }

        var initial = SymbolDetailRules.DefaultTab(_context.HasDevice);
        SelectTab(Tabs.FirstOrDefault(t => t.Tab == initial) ?? Tabs.FirstOrDefault(t => t.IsEnabled) ?? Tabs.FirstOrDefault()!);
    }

    private void SelectTab(SymbolDetailTabModel? tab)
    {
        if (tab is null || !tab.IsEnabled) return;      // 비활성 탭은 선택되지 않는다(FR-19)
        foreach (var item in Tabs) item.IsSelected = ReferenceEquals(item, tab);
        SelectedTab = tab;
    }

    private static string HeaderOf(SymbolDetailTab tab) => tab switch
    {
        SymbolDetailTab.Basic => "기본",
        SymbolDetailTab.Comm => "통신",
        SymbolDetailTab.Broadcast => "방송",
        SymbolDetailTab.Status => "상태",
        SymbolDetailTab.Symbol => "심볼",
        _ => tab.ToString(),
    };

    /// <summary>
    /// 탭 내용 — <b>서버가 실제로 주는 필드</b>(<c>geolocation{location,latitude,longitude,altitude,heading}</c> 등)를
    /// 장비 타입별 모델에서 그대로 읽는다. 없는 값은 지어내지 않고 행 자체를 빼거나 "—" 로 둔다.
    /// </summary>
    private IEnumerable<SymbolDetailField> FieldsOf(SymbolDetailTab tab, IPidsEditableMarker marker,
        IBaseDeviceModel? device, string? layerName, string? groupNames)
    {
        switch (tab)
        {
            case SymbolDetailTab.Basic:
                // 항목은 앱 '센서 설정 패널' 컬럼과 같은 것만 둔다(사용자 결정 2026-09-09):
                // 번호 · 장비번호 · 장비명 · 유형 · 제어기 · 위치 · 상태 · 활성화.
                // 여기에 요청받은 GPS 좌표(서버 geolocation)를 더한다 — 그것이 이 탭의 존재 이유다.
                yield return new("번호", Text(device?.Id));
                yield return new("장비번호", Text(device?.DeviceNumber));
                yield return new("장비명", Text(device?.DeviceName));
                yield return new("유형", UiKoreanMap.To(marker.DeviceType));
                if (device is ISensorDeviceModel sensor)
                    yield return new("제어기", Text(sensor.Controller?.DeviceName));
                yield return new("위치", Text(device?.Location));

                // ── 등록 좌표(서버 geolocation{latitude,longitude,altitude,heading}) ──
                //  장비가 "어디에 있다고 서버에 등록돼 있는지"다. 심볼을 지도에 찍어둔 위치와는 별개이며,
                //  둘이 벌어져 있으면 '현재위치 적용'을 안 한 심볼이다 — 그래서 차이를 숫자로 보여준다.
                if (device is not null && GeoFormat.HasPosition(device.Latitude, device.Longitude))
                {
                    yield return new("위도", GeoFormat.Latitude(device.Latitude, device.Longitude)!);
                    yield return new("경도", GeoFormat.Longitude(device.Latitude, device.Longitude)!);
                    if (GeoFormat.Altitude(device.Altitude) is { } alt) yield return new("고도", alt);
                    if (GeoFormat.Heading(device.Heading) is { } head) yield return new("방위", head);

                    var gap = GeoFormat.DistanceMeters(device.Latitude, device.Longitude, marker.Position.Lat, marker.Position.Lng);
                    if (gap is { } meters)
                        yield return new("심볼과 차이", GeoFormat.Distance(meters),
                            meters >= GeoFormat.SymbolMismatchWarnMeters ? "warn" : null);
                }
                else if (device is not null)
                {
                    // (0,0) 은 좌표가 아니라 미등록이다 — 기니 만 한복판으로 그리면 안 된다.
                    yield return new("등록 좌표", "미등록", "warn");
                }

                if (device is not null)
                    yield return new("상태", $"{UiKoreanMap.To(device.Status)} · {(device.IsEnable ? "활성화" : "비활성")}", ToneOf(device.Status));
                else
                    yield return new("상태", Dash);
                break;

            case SymbolDetailTab.Comm:
                switch (device)
                {
                    case ICameraDeviceModel camera:
                        yield return new("IP 주소", Text(camera.IpAddress));
                        yield return new("포트", Text(camera.IpPort));
                        yield return new("모드", UiKoreanMap.To(camera.Mode));
                        yield return new("카테고리", UiKoreanMap.To(camera.Category));
                        yield return new("녹화", camera.IsRecord == true ? "사용" : "미사용");
                        // 하드웨어 제원(ONVIF 조회 결과) — 있으면 보여준다. 현장에서 기종을 확인할 때 쓴다.
                        if (camera.HardwareSpec is { } spec)
                        {
                            yield return new("제조사", Text(spec.Manufacturer));
                            yield return new("모델", Text(spec.Model));
                            yield return new("펌웨어", Text(spec.Firmware));
                            yield return new("MAC", Text(spec.MacAddress));
                            if (spec.MaxDetectionRange is { } range && range > 0)
                                yield return new("최대 탐지거리", $"{range:0.#} m");
                        }
                        break;
                    case IControllerDeviceModel controller:
                        yield return new("IP 주소", Text(controller.IpAddress));
                        yield return new("포트", Text(controller.Port));
                        yield return new("연결 장비", Text(controller.Devices?.Count));
                        break;
                    case ILampDeviceModel lamp:
                        yield return new("IP 주소", Text(lamp.IpAddress));
                        yield return new("포트", Text(lamp.IpPort));
                        yield return new("설명", Text(lamp.Description));
                        break;
                    case ISpeakerDeviceModel speaker:
                        yield return new("스피커 종류", UiKoreanMap.To(speaker.SpeakerType));
                        yield return new("방송 서버", Text(speaker.Server?.Name));
                        yield return new("서버 주소", speaker.Server is { } srv && !string.IsNullOrWhiteSpace(srv.IpAddress)
                            ? $"{srv.IpAddress}:{srv.Port}" : Dash);
                        yield return new("설명", Text(speaker.Description));
                        break;
                    default:
                        yield return new("통신 정보", Dash);
                        break;
                }
                break;

            case SymbolDetailTab.Broadcast:
                yield return new("송출 상태", IsBroadcasting ? "송출 중" : "대기", IsBroadcasting ? "warn" : null);
                yield return new("대상", $"{Text(device?.DeviceName ?? marker.Title)} (이 심볼 1대)");
                // 마이크는 방송서버에 물려 있다 — GIS 는 대상 스피커만 지정한다(사용자 확정 2026-09-08).
                yield return new("마이크", "방송서버 설치 마이크");
                yield return new("입력 장치", "GIS 미관여");
                break;

            case SymbolDetailTab.Status:
                if (device is not null)
                    yield return new("현재 상태", UiKoreanMap.To(device.Status), ToneOf(device.Status));
                if (SymbolDetailRules.HasDoor(marker.DeviceType))
                    yield return new("문 상태", DoorText(_context.DoorState), _context.DoorState == DoorUiState.Open ? "warn" : null);

                switch (device)
                {
                    case IEnclosureDeviceModel enclosure:
                        yield return new("도어", Text(enclosure.DoorStatus));
                        yield return new("히터", enclosure.HeaterEnabled ? "동작" : "정지");
                        yield return new("팬", enclosure.FanEnabled ? "동작" : "정지");
                        // 임계 설정 — 온도/습도/전류/전압/진동 경보가 언제 뜨는지의 근거값.
                        if (enclosure.ThresholdConfig is { } th)
                        {
                            if (th.TempHigh is { } hi || th.TempLow is { } lo)
                                yield return new("온도 임계", $"{Num(th.TempLow)} ~ {Num(th.TempHigh)} ℃");
                            if (th.HumidityHigh is { } hum) yield return new("습도 상한", $"{hum:0.#} %");
                            if (th.CurrentHigh is { } cur) yield return new("전류 상한", $"{cur:0.##} A");
                            if (th.VoltageLow is { } vol) yield return new("전압 하한", $"{vol:0.#} V");
                            if (th.VibrationHigh is { } vib) yield return new("진동 상한", vib.ToString(CultureInfo.InvariantCulture));
                        }
                        break;

                    case IGateDeviceModel gate:
                        yield return new("게이트 상태", Text(gate.GateStatus));
                        // 결선 방식 — 개폐 명령을 실제로 수행하는 주체(제어기 접점 / 함체 접점 / IP 컨버터)를 가르는 값.
                        // 문이 안 열릴 때 어디를 봐야 하는지가 여기 있다.
                        foreach (var row in LinkInfoRows(gate.LinkInfoJson)) yield return row;
                        break;
                }
                yield return new("심볼 이벤트", EventStatusText(marker.EventStatus));
                break;

            case SymbolDetailTab.Symbol:
                // 이 탭만은 장비가 없어도 값이 채워진다 — 심볼 자체 정보라서(FR-19).
                yield return new("심볼 제목", Text(marker.Title));
                yield return new("위도", GeoFormat.Latitude(marker.Position.Lat, marker.Position.Lng) ?? "미등록");
                yield return new("경도", GeoFormat.Longitude(marker.Position.Lat, marker.Position.Lng) ?? "미등록");
                yield return new("회전", $"{marker.Bearing:F0}°");
                yield return new("크기", $"{marker.Width:F0} × {marker.Height:F0}");
                yield return new("최소표시줌", marker.Zoom.ToString("F0", CultureInfo.InvariantCulture));
                yield return new("레이어", Text(layerName));
                yield return new("잠금", marker.IsLocked ? "잠김" : "해제");
                yield return new("라벨 표시", marker.ShowTitle ? "표시" : "숨김");
                break;
        }
    }

    /// <summary>통문 <c>link_info</c>(JSONB 원본) → 표시 행. 형식이 달라도 죽지 않게 관대하게 읽는다.</summary>
    private static IEnumerable<SymbolDetailField> LinkInfoRows(string? linkInfoJson)
    {
        if (string.IsNullOrWhiteSpace(linkInfoJson)) yield break;
        Newtonsoft.Json.Linq.JObject? obj = null;
        try { obj = Newtonsoft.Json.Linq.JObject.Parse(linkInfoJson!); } catch { }
        if (obj is null) yield break;

        var type = obj.Value<string>("type");
        if (!string.IsNullOrWhiteSpace(type)) yield return new("결선 방식", type!);
        var channel = obj["channel"];
        if (channel is not null && channel.Type != Newtonsoft.Json.Linq.JTokenType.Null)
            yield return new("접점 채널", channel.ToString());
        var parent = obj["parent_hint"];
        if (parent is not null && parent.Type != Newtonsoft.Json.Linq.JTokenType.Null)
            yield return new("연결 장비", $"#{parent}");
    }

    private static string Num(double? value)
        => value is { } v && double.IsFinite(v) ? v.ToString("0.#", CultureInfo.InvariantCulture) : "—";

    private void BuildActions(EnumDeviceType deviceType)
    {
        Actions.Clear();
        bool first = true;
        foreach (var action in SymbolDetailRules.VisibleActions(deviceType))
        {
            var (header, icon) = Describe(action, deviceType);
            Actions.Add(new SymbolDetailActionModel(action, header, icon, isPrimary: first, _actionCommand));
            first = false;
        }
    }

    /// <summary>비활성 여부·사유를 규칙에서 다시 읽어 반영한다.</summary>
    private void RefreshActionStates()
    {
        var ctx = _context;
        foreach (var item in Actions)
        {
            var state = SymbolDetailRules.Evaluate(item.Action, in ctx);
            item.IsEnabled = state.IsEnabled;
            item.DisabledReason = state.DisabledReason;
        }
    }

    private static (string Header, PackIconKind Icon) Describe(SymbolDetailAction action, EnumDeviceType deviceType)
    {
        string name = UiKoreanMap.To(deviceType);
        return action switch
        {
            SymbolDetailAction.DevicePage => ($"{name}페이지", PackIconKind.ViewList),
            SymbolDetailAction.DeviceDetail => ($"{name}상세", PackIconKind.InformationOutline),
            SymbolDetailAction.DeviceEdit => ($"{name}수정", PackIconKind.Pencil),
            SymbolDetailAction.DetectionHistory => ("탐지 이력", PackIconKind.ChartLine),
            SymbolDetailAction.ControllerHome => ("제어기 홈페이지", PackIconKind.Web),
            SymbolDetailAction.CameraHome => ("카메라 홈페이지", PackIconKind.Web),
            SymbolDetailAction.AimLocation => ("특정 위치 확인", PackIconKind.CrosshairsGps),
            SymbolDetailAction.SoundPlay => ("음원 실행", PackIconKind.Play),
            SymbolDetailAction.Tts => ("TTS 실행", PackIconKind.Bullhorn),
            SymbolDetailAction.BroadcastStop => ("방송 정지", PackIconKind.Stop),
            SymbolDetailAction.MicPtt => ("누르고 말하기", PackIconKind.Microphone),
            SymbolDetailAction.DoorOpen => ("문 열기", PackIconKind.DoorOpen),
            SymbolDetailAction.DoorClose => ("문 닫기", PackIconKind.DoorClosed),
            SymbolDetailAction.ShowOnMap => ("지도에서 보기", PackIconKind.MapMarker),
            _ => (action.ToString(), PackIconKind.HelpCircle),
        };
    }

    private static string DoorText(DoorUiState state) => state switch
    {
        DoorUiState.Open => "열림",
        DoorUiState.Closed => "닫힘",
        DoorUiState.Pending => "명령 대기 중",
        _ => "상태 미수신",
    };

    private static string EventStatusText(EnumEventStatus status) => status switch
    {
        EnumEventStatus.Normal => "정상",
        EnumEventStatus.Connection => "통신 이상",
        EnumEventStatus.Detecting => "탐지 중",
        EnumEventStatus.Fault => "장애",
        EnumEventStatus.Blackout => "무통신",
        _ => status.ToString(),
    };

    private static string? ToneOf(EnumDeviceStatus status) => status switch
    {
        EnumDeviceStatus.ACTIVATED => "ok",
        EnumDeviceStatus.ERROR => "fault",
        _ => "warn",
    };

    private const string Dash = "—";
    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? Dash : value!;
    private static string Text(int? value) => value is null or 0 ? Dash : value.Value.ToString(CultureInfo.InvariantCulture);

    #endregion

    #region - Live sync (마커 변경 추종) -

    private void Subscribe(IPidsEditableMarker marker)
    {
        if (marker is not INotifyPropertyChanged notifier) return;
        _markerNotifier = notifier;
        notifier.PropertyChanged += OnMarkerPropertyChanged;
    }

    private void Unsubscribe()
    {
        if (_markerNotifier is null) return;
        _markerNotifier.PropertyChanged -= OnMarkerPropertyChanged;
        _markerNotifier = null;
    }

    /// <summary>문이 실제로 움직이면 상세 창 프리뷰도 같이 움직인다 — 지도만 갱신되면 창이 거짓말을 한다.</summary>
    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_marker is null) return;
        if (e.PropertyName == nameof(IPidsEditableMarker.DoorState))
            DoorOpen = _marker.DoorState == EnumDoorState.Open ? 1 : 0;
    }

    #endregion

    /// <summary>
    /// 창을 닫을 때 마커 구독을 놓는다(NFR-01) — 남으면 닫힌 창이 마커를 붙잡는다.
    /// <para>이 VM 은 창을 다시 열 때 <see cref="Load"/> 로 재사용되므로, 여기서 커맨드·이벤트 배선은 건드리지 않는다.</para>
    /// </summary>
    public void Unload()
    {
        EndMic();               // 마이크를 누른 채로 창이 닫히면 중지가 안 나간다
        Unsubscribe();
        UnhookUnitDirectory();  // 닫힌 창이 사전(싱글턴)에 붙잡히지 않게
        UnitLine = SymbolDetailUnitLineState.Hidden;
        _marker = null;
        IsBroadcasting = false;
    }

    public void Dispose() => Unload();
}
