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
   Purpose      : 권한 등급(역할) 설정 — 등급 목록(요약) + 더블클릭 상세(모듈×동작 매트릭스)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : ★ OQ-PG-01 = Option A 확정: 권한 = 역할(등급) 단위. 권한그룹 == 권한레벨 == 같은 개념.
                  5개 등급(ADMIN/MAINTAINER/OPERATOR/VIEWER/GUEST)만 노출(임의 팀그룹 제외), 등급 높은 순.
                  계정은 "구분(등급)" 하나로 권한 결정(서버 로그인이 역할명 등급 그룹의 매트릭스 사용).
                  (1)등급당 1행 요약, (2)더블클릭 → 8모듈×4동작 체크박스 상세, 체크→[저장]→POST /user-groups/{id}/permissions(ADMIN).
****************************************************************************/
public class PermissionMatrixPanelViewModel : BasePanelViewModel
{
    private readonly IAccountApiService _api;
    private List<UserGroupDto> _raw = new();

    // Option A(OQ-PG-01): 권한 = 역할(등급) 단위. 5개 역할명 등급 그룹만 노출 + 등급순 정렬 + 한글 라벨.
    private static readonly Dictionary<string, (int rank, string label)> _levels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] = (5, "관리자"),
            ["MAINTAINER"] = (4, "유지보수자"),
            ["OPERATOR"] = (3, "운영자"),
            ["VIEWER"] = (2, "조회자"),
            ["GUEST"] = (1, "게스트"),
        };

    public PermissionMatrixPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api)
        : base(eventAggregator, log) => _api = api;

    #region - Properties -
    /// <summary>등급 목록(요약). DataGrid ItemsSource(목록 화면).</summary>
    public ObservableCollection<PermissionGroupRowViewModel> Groups { get; } = new();
    /// <summary>선택 등급 상세 — 8모듈×4동작. DataGrid ItemsSource(상세 화면).</summary>
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

    // 상세 화면에서 편집 중인 등급 식별/보존(저장 시 사용)
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

    /// <summary>등급 더블클릭 → 그 등급의 전체 권한(모듈×동작) 상세 화면으로 진입.</summary>
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

    /// <summary>현재 상세 등급의 권한(모듈×동작)을 서버에 저장(ADMIN). POST /user-groups/{id}/permissions.
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
                { Title = "권한 등급", Explain = $"'{DetailGroupName}' 등급의 권한을 저장했습니다." });
                await ReloadAsync(CancellationToken.None);   // 목록 갱신(요약 카운트 반영, 상세→목록 복귀)
            }
            else
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 등급", Explain = $"저장 실패: {res.Error?.Message ?? res.Message}" });
            }
        }
        catch (Exception ex) { _log?.Error($"[Permission] 권한 저장 실패: {ex.Message}"); }
        finally { IsSaving = false; }
    }
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

            // 등급별 사용자 수 = 그 역할(role)을 가진 계정 수 (Option A: 등급=역할, group_id 아님).
            // 서버 GET /user-groups 목록은 user_count 미포함 + 사용자는 group_id가 아닌 role로 등급 소속.
            // ⚠ 서버 /users limit 상한=100(le=100) — 1000 지정 시 422. 계정 수가 100 초과면 페이징 필요(현재 GOP 규모 <100).
            var usersRes = await _api.GetUsersAsync(1, 100, ct);
            var countByRole = (usersRes.Success && usersRes.Data is not null)
                ? usersRes.Data.GroupBy(u => (u.Role ?? string.Empty).ToUpperInvariant())
                               .ToDictionary(x => x.Key, x => x.Count())
                : new Dictionary<string, int>();

            // Option A: 5개 등급(역할명) 그룹만 노출, 등급 높은 순(ADMIN 위 → GUEST 아래). 임의 팀그룹은 제외.
            foreach (var g in _raw.Where(x => _levels.ContainsKey(x.Name))
                                  .OrderByDescending(x => _levels[x.Name].rank))
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
                    GroupName = _levels[g.Name].label,   // 한글 등급 라벨
                    UserCount = countByRole.TryGetValue(g.Name.ToUpperInvariant(), out var uc) ? uc : 0,   // 역할(role)별 실제 계정 수
                    Active = g.IsActive ? "사용" : "미사용",
                    ViewCount = v,
                    EditCount = e,
                    DeleteCount = d,
                    ControlCount = c,
                });
            }
            if (!res.Success)
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 등급", Explain = $"불러오기 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[Permission] 로드 실패: {ex.Message}"); }
    }
    #endregion
}
