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
                  IAccountApiService 직접 주입(GOP 모드). ⚠ 조회 전용 — 편집저장/그룹 CRUD는 서버 v5.0 후속.
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
        set { _isDetailView = value; NotifyOfPropertyChange(() => IsDetailView); NotifyOfPropertyChange(() => IsListView); }
    }
    public bool IsListView => !_isDetailView;

    private string _detailGroupName = string.Empty;
    public string DetailGroupName
    {
        get => _detailGroupName;
        set { _detailGroupName = value; NotifyOfPropertyChange(() => DetailGroupName); }
    }
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

            foreach (var g in _raw)
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
