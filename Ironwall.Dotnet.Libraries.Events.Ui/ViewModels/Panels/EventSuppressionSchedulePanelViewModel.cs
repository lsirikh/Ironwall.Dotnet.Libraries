using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;   // SimpleCommand
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 이벤트 억제(정비 창) 스케줄 관리 패널 — 생성/목록/무한스크롤/취소(soft-cancel).
                  대상=장비 복수 / 그룹 복수 / 전체(혼합 없음). GrantManagement 선례 패턴.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventSuppressionSchedulePanelViewModel : BasePanelViewModel, IHandle<CallCancelSuppressionMessageModel>
{
    #region - Ctors -
    public EventSuppressionSchedulePanelViewModel(
        IEventAggregator eventAggregator,
        ILogService log,
        IEventSuppressionApiService api,
        DeviceProvider deviceProvider,
        DeviceGroupProvider groupProvider)
        : base(eventAggregator, log)
    {
        _api = api;
        DeviceProvider = deviceProvider;
        DeviceGroupProvider = groupProvider;

        _windowStart = DateTime.Now;
        _windowEnd = DateTime.Now.AddHours(1);

        SelectedDevices.CollectionChanged += OnTargetSelectionChanged;
        SelectedGroups.CollectionChanged += OnTargetSelectionChanged;

        LoadMoreCommand = new SimpleCommand(async () => await LoadNextPageAsync(_cancellationTokenSource?.Token ?? CancellationToken.None));
    }
    #endregion

    #region - Providers / Collections -
    /// <summary>장비 선택 소스(ComboBox ItemsSource).</summary>
    public DeviceProvider DeviceProvider { get; }
    /// <summary>그룹 선택 소스(ComboBox ItemsSource).</summary>
    public DeviceGroupProvider DeviceGroupProvider { get; }

    /// <summary>선택된 대상 장비(칩, 복수).</summary>
    public ObservableCollection<IBaseDeviceModel> SelectedDevices { get; } = new();
    /// <summary>선택된 대상 그룹(칩, 복수).</summary>
    public ObservableCollection<IDeviceGroupModel> SelectedGroups { get; } = new();
    /// <summary>억제 스케줄 목록(DataGrid ItemsSource).</summary>
    public ObservableCollection<EventSuppressionScheduleItemViewModel> Schedules { get; } = new();
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        var perm = ResolvePermissionService();
        if (perm != null) perm.PermissionsChanged += OnPermissionsChanged;
        await LoadAllAsync(_cancellationTokenSource?.Token ?? cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        var perm = ResolvePermissionService();
        if (perm != null) perm.PermissionsChanged -= OnPermissionsChanged;
        await base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - 권한 게이팅 (events 도메인) — DetectionEventPanel 패턴 -
    private IPermissionService? _permissionService;
    private bool _permissionResolved;
    private IPermissionService? ResolvePermissionService()
    {
        if (_permissionResolved) return _permissionService;
        try { _permissionService = IoC.Get<IPermissionService>(); _permissionResolved = _permissionService != null; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(EventSuppressionSchedulePanelViewModel)}] PermissionService 미해석(전체허용 폴백): {ex.Message}");
            _permissionService = null;
        }
        return _permissionService;
    }
    private bool CanViewEvents() => ResolvePermissionService()?.CanView("events") ?? true;
    private bool CanEditEvents() => ResolvePermissionService()?.CanEdit("events") ?? true;
    private bool CanDelEvents()  => ResolvePermissionService()?.CanDelete("events") ?? true;

    /// <summary>생성/저장 버튼 활성 — events:edit + 폼 유효.</summary>
    public bool CanCreate =>
        CanEditEvents()
        && !string.IsNullOrWhiteSpace(Name)
        && WindowEnd > WindowStart
        && TargetType switch
        {
            "device" => SelectedDevices.Count > 0,
            "group" => SelectedGroups.Count > 0,
            _ => true,   // all
        };

    /// <summary>취소(삭제) 권한 — 행 취소 버튼 게이팅 보조.</summary>
    public bool CanDelete => CanDelEvents();

    private void OnPermissionsChanged()
    {
        Execute.OnUIThread(() =>
        {
            NotifyOfPropertyChange(nameof(CanCreate));
            NotifyOfPropertyChange(nameof(CanDelete));
        });
    }
    #endregion

    #region - Binding Methods -
    /// <summary>헤더 X 버튼(ModernPanelCloseButton, x:Name="Close") — 패널 닫기(ReportConsole 패턴).</summary>
    public Task Close() => TryCloseAsync();

    public async Task OnClickReloadButton()
        => await LoadAllAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>대상 유형 전환(장비/그룹/전체 배타).</summary>
    public void SetTargetType(string type)
    {
        if (_targetType == type) return;
        _targetType = type;
        NotifyOfPropertyChange(nameof(TargetType));
        NotifyOfPropertyChange(nameof(IsDeviceMode));
        NotifyOfPropertyChange(nameof(IsGroupMode));
        NotifyOfPropertyChange(nameof(IsAllMode));
        NotifyOfPropertyChange(nameof(CanCreate));
    }

    /// <summary>칩 제거 — 장비.</summary>
    public void RemoveDevice(IBaseDeviceModel device) { if (device != null) SelectedDevices.Remove(device); }
    /// <summary>칩 제거 — 그룹.</summary>
    public void RemoveGroup(IDeviceGroupModel group) { if (group != null) SelectedGroups.Remove(group); }

    /// <summary>억제 창 생성 — 클라 1차 검증 후 POST(대상 배열 + KST ISO8601). 성공 시 폼 리셋 + 재조회.</summary>
    public async Task ClickCreate()
    {
        if (!CanEditEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = "이벤트 편집 권한(events:edit)이 없습니다." });
            return;
        }
        if (!CanCreate)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "억제 창 생성", Explain = "작업명·대상(≥1)·시간창(종료>시작)을 확인하세요." });
            return;
        }
        try
        {
            var dto = new EventSuppressionScheduleRequestDto
            {
                Name = Name!.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                TargetType = TargetType,
                TargetDeviceIds = TargetType == "device" ? SelectedDevices.Select(d => d.Id).ToList() : new(),
                TargetGroupIds = TargetType == "group" ? SelectedGroups.Select(g => g.Id).ToList() : new(),
                TargetSide = TargetSide,
                EventScope = EventScope,
                WindowStart = KoreaTimeHelper.ToServerIso8601(WindowStart),
                WindowEnd = KoreaTimeHelper.ToServerIso8601(WindowEnd),
            };
            var res = await _api.CreateSuppressionScheduleAsync(dto);
            if (res.Success)
            {
                ResetForm();
                await LoadAllAsync();
            }
            else
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "억제 창 생성", Explain = $"생성 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[Suppression] 생성 실패: {ex.Message}"); }
    }

    /// <summary>행 취소 클릭 → Confirm(즉시 삭제 않음). Yes 시 CallCancelSuppressionMessageModel 발행.</summary>
    public async Task OnClickCancel(EventSuppressionScheduleItemViewModel item)
    {
        if (item is null) return;
        if (!CanDelEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = "이벤트 삭제 권한(events:delete)이 없습니다." });
            return;
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "억제 창 취소",
            Explain = $"'{item.Name}' 억제 창(#{item.Id})을 취소하시겠습니까?\n취소해도 이력은 보존(revoked_at)됩니다.",
            MessageModel = new CallCancelSuppressionMessageModel { ScheduleId = item.Id }
        });
    }

    /// <summary>Confirm→Yes 후 실제 DELETE(soft-cancel) + 목록 갱신.</summary>
    public async Task HandleAsync(CallCancelSuppressionMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _api.CancelSuppressionScheduleAsync(message.ScheduleId);
            if (res.Success) await LoadAllAsync();
            else await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "억제 창 취소", Explain = $"취소 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[Suppression] 취소 실패: {ex.Message}"); }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
        }
    }
    #endregion

    #region - Processes -
    private void OnTargetSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyOfPropertyChange(nameof(CanCreate));

    private void ResetForm()
    {
        Name = string.Empty;
        Description = null;
        SelectedDevices.Clear();
        SelectedGroups.Clear();
        WindowStart = DateTime.Now;
        WindowEnd = DateTime.Now.AddHours(1);
        NotifyOfPropertyChange(nameof(CanCreate));
    }

    private async Task LoadAllAsync(CancellationToken ct = default)
    {
        _currentPage = 0; _totalPages = 1; _totalCount = 0;
        try
        {
            var res = await _api.GetSuppressionSchedulesAsync(
                page: 1, limit: PAGE_SIZE, status: FilterStatus, targetType: FilterTargetType, token: ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;
            if (res.Success && res.Data is not null)
            {
                _totalCount = res.Pagination?.Total ?? res.Total ?? res.Data.Count;
                _currentPage = res.Pagination?.Page ?? 1;
                _totalPages = _totalCount > 0 ? (int)Math.Ceiling(_totalCount / (double)PAGE_SIZE) : 1;
                Execute.OnUIThread(() =>
                {
                    Schedules.Clear();
                    foreach (var d in res.Data) Schedules.Add(new EventSuppressionScheduleItemViewModel(d, DeviceProvider, DeviceGroupProvider));
                    NotifyOfPropertyChange(nameof(LoadedCountText));
                    NotifyOfPropertyChange(nameof(HasMorePages));
                });
            }
            else if (!res.Success)
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "억제 스케줄", Explain = $"목록 불러오기 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[Suppression] 전체 로드 실패: {ex.Message}"); }
    }

    private async Task LoadNextPageAsync(CancellationToken ct = default)
    {
        if (_isLoadingMore || !HasMorePages || ct.IsCancellationRequested) return;
        _isLoadingMore = true; IsLoadingMore = true;
        try
        {
            var res = await _api.GetSuppressionSchedulesAsync(
                page: _currentPage + 1, limit: PAGE_SIZE, status: FilterStatus, targetType: FilterTargetType, token: ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested || !res.Success || res.Data is null) return;
            _totalCount = res.Pagination?.Total ?? res.Total ?? _totalCount;
            _currentPage = res.Pagination?.Page ?? (_currentPage + 1);
            _totalPages = _totalCount > 0 ? (int)Math.Ceiling(_totalCount / (double)PAGE_SIZE) : _totalPages;
            Execute.OnUIThread(() =>
            {
                foreach (var d in res.Data) Schedules.Add(new EventSuppressionScheduleItemViewModel(d, DeviceProvider, DeviceGroupProvider));
                NotifyOfPropertyChange(nameof(LoadedCountText));
                NotifyOfPropertyChange(nameof(HasMorePages));
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[Suppression] 다음 페이지 실패: {ex.Message}"); }
        finally { _isLoadingMore = false; IsLoadingMore = false; }
    }
    #endregion

    #region - Properties (Form) -
    private string? _name = string.Empty;
    public string? Name { get => _name; set { _name = value; NotifyOfPropertyChange(nameof(Name)); NotifyOfPropertyChange(nameof(CanCreate)); } }

    private string? _description;
    public string? Description { get => _description; set { _description = value; NotifyOfPropertyChange(nameof(Description)); } }

    private string _targetType = "device";
    /// <summary>대상 모드(배타): device / group / all.</summary>
    public string TargetType { get => _targetType; set => SetTargetType(value); }
    public bool IsDeviceMode { get => _targetType == "device"; set { if (value) SetTargetType("device"); } }
    public bool IsGroupMode  { get => _targetType == "group";  set { if (value) SetTargetType("group"); } }
    public bool IsAllMode    { get => _targetType == "all";    set { if (value) SetTargetType("all"); } }

    private IBaseDeviceModel? _deviceToAdd;
    /// <summary>장비 추가 ComboBox — 선택 시 칩에 추가하고 비운다.</summary>
    public IBaseDeviceModel? DeviceToAdd
    {
        get => _deviceToAdd;
        set
        {
            _deviceToAdd = value;
            if (value != null && !SelectedDevices.Any(d => d.Id == value.Id)) SelectedDevices.Add(value);
            _deviceToAdd = null;
            NotifyOfPropertyChange(nameof(DeviceToAdd));
        }
    }

    private IDeviceGroupModel? _groupToAdd;
    /// <summary>그룹 추가 ComboBox — 선택 시 칩에 추가하고 비운다.</summary>
    public IDeviceGroupModel? GroupToAdd
    {
        get => _groupToAdd;
        set
        {
            _groupToAdd = value;
            if (value != null && !SelectedGroups.Any(g => g.Id == value.Id)) SelectedGroups.Add(value);
            _groupToAdd = null;
            NotifyOfPropertyChange(nameof(GroupToAdd));
        }
    }

    private string _targetSide = "both";
    /// <summary>감지/감시 필터(group·all): detection / surveillance / both.</summary>
    public string TargetSide { get => _targetSide; set { _targetSide = value; NotifyOfPropertyChange(nameof(TargetSide)); } }

    private string _eventScope = "all";
    /// <summary>억제 범위: connection / detection / malfunction / all.</summary>
    public string EventScope { get => _eventScope; set { _eventScope = value; NotifyOfPropertyChange(nameof(EventScope)); } }

    private DateTime _windowStart;
    public DateTime WindowStart { get => _windowStart; set { _windowStart = value; NotifyOfPropertyChange(nameof(WindowStart)); NotifyOfPropertyChange(nameof(CanCreate)); } }

    private DateTime _windowEnd;
    public DateTime WindowEnd { get => _windowEnd; set { _windowEnd = value; NotifyOfPropertyChange(nameof(WindowEnd)); NotifyOfPropertyChange(nameof(CanCreate)); } }
    #endregion

    #region - Properties (Filter / List) -
    private string? _filterStatus;
    /// <summary>상태 필터(null=전체). 변경 시 재조회.</summary>
    public string? FilterStatus { get => _filterStatus; set { _filterStatus = string.IsNullOrEmpty(value) ? null : value; NotifyOfPropertyChange(nameof(FilterStatus)); _ = LoadAllAsync(); } }

    private string? _filterTargetType;
    /// <summary>대상유형 필터(null=전체). 변경 시 재조회.</summary>
    public string? FilterTargetType { get => _filterTargetType; set { _filterTargetType = string.IsNullOrEmpty(value) ? null : value; NotifyOfPropertyChange(nameof(FilterTargetType)); _ = LoadAllAsync(); } }

    private bool _isLoadingMore;
    public bool IsLoadingMore { get => _isLoadingMore; set { _isLoadingMore = value; NotifyOfPropertyChange(nameof(IsLoadingMore)); } }

    public string LoadedCountText => $"{Schedules.Count} / {_totalCount}건";
    public bool HasMorePages => _currentPage < _totalPages;
    public ICommand LoadMoreCommand { get; }
    #endregion

    #region - Attributes -
    private readonly IEventSuppressionApiService _api;
    private const int PAGE_SIZE = 100;
    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    #endregion
}

/// <summary>억제 창 취소 확인 트리거 — Confirm 팝업 '확인' 시 발행 → HandleAsync가 실제 DELETE(soft-cancel).</summary>
public class CallCancelSuppressionMessageModel : IMessageModel
{
    public int ScheduleId { get; set; }
}
