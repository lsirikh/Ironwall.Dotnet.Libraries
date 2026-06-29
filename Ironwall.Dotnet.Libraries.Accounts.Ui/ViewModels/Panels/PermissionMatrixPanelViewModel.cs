using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 권한 설정 — 그룹 목록(요약) + 더블클릭 상세(모듈×동작 매트릭스)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 사용자 피드백 — 플랫 (그룹×모듈) 리스트 폐기. (1)그룹당 1행 요약(조회N·편집N…),
                  (2)더블클릭 → 그 그룹 전체 권한 페이지(8모듈×4동작 체크박스, 현재상태 표시).
                  마스터-디테일은 IsListView/IsDetailView 토글(서브 Conductor 없이 Visibility 스왑).
                  IAccountApiService 직접 주입(GOP 모드).
                  ★ IMPL-06: 체크 → [저장] → POST /user-groups/{id}/permissions(ADMIN) 로 편집저장(서버 v5.0).
****************************************************************************/
public class PermissionMatrixPanelViewModel : BasePanelViewModel
{
    private readonly IAccountApiService _api;
    private List<UserGroupDto> _raw = new();

    public PermissionMatrixPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api)
        : base(eventAggregator, log) => _api = api;

    #region - Properties -
    /// <summary>그룹 목록(요약). DataGrid ItemsSource(목록 화면).</summary>
    public ObservableCollection<PermissionGroupRowViewModel> Groups { get; } = new();
    /// <summary>선택 그룹 상세 — 8모듈×4동작. DataGrid ItemsSource(상세 화면).</summary>
    public ObservableCollection<ModulePermRowViewModel> Modules { get; } = new();

    private PermissionGroupRowViewModel? _selectedGroup;
    public PermissionGroupRowViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set { _selectedGroup = value; NotifyOfPropertyChange(() => SelectedGroup); }
    }

    private bool _isDetailView;
    public bool IsDetailView
    {
        get => _isDetailView;
        set
        {
            _isDetailView = value;
            NotifyOfPropertyChange(() => IsDetailView);
            NotifyOfPropertyChange(() => IsListView);
            NotifyOfPropertyChange(() => CanSave);
        }
    }
    public bool IsListView => !_isDetailView;

    private string _detailGroupName = string.Empty;
    public string DetailGroupName
    {
        get => _detailGroupName;
        set { _detailGroupName = value; NotifyOfPropertyChange(() => DetailGroupName); }
    }

    // 상세 화면에서 편집 중인 그룹 식별/보존(저장 시 사용)
    private int _detailGroupId;
    private List<int>? _detailDeviceGroups;

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        set { _isSaving = value; NotifyOfPropertyChange(() => IsSaving); NotifyOfPropertyChange(() => CanSave); }
    }
    /// <summary>[저장] 활성 조건 — 상세 화면 + 저장 중 아님.</summary>
    public bool CanSave => IsDetailView && !_isSaving;
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await ReloadAsync(cancellationToken);
    }
    #endregion
    #region - Binding Methods -
    public async Task OnClickReloadButton() => await ReloadAsync(CancellationToken.None);

    /// <summary>그룹 더블클릭 → 그 그룹의 전체 권한(모듈×동작) 상세 화면으로 진입.</summary>
    public void OnClickGroupDetail()
    {
        var row = SelectedGroup;
        if (row is null) return;

        var g = _raw.FirstOrDefault(x => x.Id == row.GroupId);
        var mods = g?.Permissions?.Modules ?? new Dictionary<string, ModulePermissionDto>();
        _detailGroupId = row.GroupId;
        _detailDeviceGroups = g?.Permissions?.DeviceGroups;

        Modules.Clear();
        foreach (var m in PermissionCatalog.Modules)
        {
            var key = PermissionCatalog.ServerKey(m);
            mods.TryGetValue(key, out var mp);
            Modules.Add(new ModulePermRowViewModel
            {
                ModuleKey = key,
                ModuleDisplay = PermissionCatalog.DisplayName(m),
                View = mp?.View ?? false,
                Edit = mp?.Edit ?? false,
                Delete = mp?.Delete ?? false,
                Control = mp?.Control ?? false,
                ViewEnabled = PermissionCatalog.IsVerbAllowed(m, EnumPermissionVerb.View),
                EditEnabled = PermissionCatalog.IsVerbAllowed(m, EnumPermissionVerb.Edit),
                DeleteEnabled = PermissionCatalog.IsVerbAllowed(m, EnumPermissionVerb.Delete),
                ControlEnabled = PermissionCatalog.IsVerbAllowed(m, EnumPermissionVerb.Control),
            });
        }
        DetailGroupName = row.GroupName;
        IsDetailView = true;
    }

    public void ClickBackToList() => IsDetailView = false;

    /// <summary>현재 상세 그룹의 권한(모듈×동작)을 서버에 저장(ADMIN). POST /user-groups/{id}/permissions.
    /// 성공 시 목록을 재조회(요약 카운트 반영)하며 목록 화면으로 복귀한다. 실패 시 사유 안내.</summary>
    public async Task OnClickSave()
    {
        if (_detailGroupId <= 0) return;
        try
        {
            IsSaving = true;
            var dto = new PermissionsDto
            {
                DeviceGroups = _detailDeviceGroups,
                Modules = Modules.ToDictionary(
                    m => m.ModuleKey,
                    m => new ModulePermissionDto { View = m.View, Edit = m.Edit, Delete = m.Delete, Control = m.Control }),
            };

            var res = await _api.UpdateGroupPermissionsAsync(_detailGroupId, dto);
            if (res.Success)
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 설정", Explain = $"'{DetailGroupName}' 그룹의 권한을 저장했습니다." });
                await ReloadAsync(CancellationToken.None);   // 목록 갱신(요약 카운트 반영, 상세→목록 복귀)
            }
            else
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 설정", Explain = $"저장 실패: {res.Error?.Message ?? res.Message}" });
            }
        }
        catch (Exception ex) { _log?.Error($"[Permission] 권한 저장 실패: {ex.Message}"); }
        finally { IsSaving = false; }
    }

    /// <summary>그룹 추가 — 서버 v5.0 권한관리 API 후 활성화(현재 GOP 서버가 그룹 CRUD 미지원).</summary>
    public async Task OnClickAddGroup()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
        { Title = "권한 그룹", Explain = "그룹 추가는 서버 v5.0 권한관리 API 후 활성화됩니다." });

    public async Task OnClickRemoveGroup()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
        { Title = "권한 그룹", Explain = "그룹 삭제는 서버 v5.0 권한관리 API 후 활성화됩니다." });
    #endregion
    #region - Processes -
    private async Task ReloadAsync(CancellationToken ct)
    {
        try
        {
            var res = await _api.GetUserGroupsAsync(ct);
            IsDetailView = false;
            Groups.Clear();
            _raw = (res.Success && res.Data is not null) ? res.Data : new List<UserGroupDto>();

            // 그룹 이름순(ㄱㄴㄷ/ABC, 문화권 정렬) 고정 — 서버 GET 이 ORDER BY 없어 편집(UPDATE) 후 순서가 비결정적으로 바뀌는 문제 해결.
            foreach (var g in _raw.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                int v = 0, e = 0, d = 0, c = 0;
                if (g.Permissions?.Modules is { } mods)
                    foreach (var kv in mods.Values)
                    {
                        if (kv.View) v++;
                        if (kv.Edit) e++;
                        if (kv.Delete) d++;
                        if (kv.Control) c++;
                    }
                Groups.Add(new PermissionGroupRowViewModel
                {
                    GroupId = g.Id,
                    GroupName = g.Name,
                    UserCount = g.UserCount ?? 0,
                    Active = g.IsActive ? "사용" : "미사용",
                    ViewCount = v,
                    EditCount = e,
                    DeleteCount = d,
                    ControlCount = c,
                });
            }
            if (!res.Success)
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 설정", Explain = $"불러오기 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[Permission] 로드 실패: {ex.Message}"); }
    }
    #endregion
}
