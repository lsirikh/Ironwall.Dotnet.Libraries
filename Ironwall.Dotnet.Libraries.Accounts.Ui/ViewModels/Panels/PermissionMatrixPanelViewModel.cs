using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
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
   Purpose      : 권한 그룹 관리 — 그룹 CRUD(생성/이름수정/삭제) + 매트릭스(모듈×동작) 편집 + 구성원(계정) 배정
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : OQ-PG-01 확장(Option A→B, PRD GOP_Permission_Group_Management):
                  임의 권한그룹을 생성/삭제/이름수정하고, 계정을 그룹에 상시 배정(user.group_id)한다.
                  예약 5등급(ADMIN/MAINTAINER/OPERATOR/VIEWER/GUEST)은 삭제·개명 금지(매트릭스 편집만).
                  전 기능 ADMIN(콘솔 탭 CanSeePermission=IsAdmin + 서버 require_admin 최종방어).
                  3화면: 목록(List) / 권한매트릭스(Matrix) / 구성원(Members). 한시부여는 별도 '권한 부여' 탭.
****************************************************************************/
public class PermissionMatrixPanelViewModel : BasePanelViewModel, IHandle<CallDeleteGroupMessageModel>
{
    private readonly IAccountApiService _api;
    private List<UserGroupDto> _raw = new();

    // 예약 등급(역할명 그룹): 한글 라벨 + 정렬 우대 + 삭제/개명 금지
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
    /// <summary>그룹 목록(요약). DataGrid ItemsSource(목록 화면).</summary>
    public ObservableCollection<PermissionGroupRowViewModel> Groups { get; } = new();
    /// <summary>선택 그룹 상세 — 8모듈×4동작. DataGrid ItemsSource(매트릭스 화면).</summary>
    public ObservableCollection<ModulePermRowViewModel> Modules { get; } = new();
    /// <summary>구성원 화면 — 선택 그룹 소속 계정.</summary>
    public ObservableCollection<AuthUserDto> Members { get; } = new();
    /// <summary>구성원 화면 — 추가 후보(미소속 계정).</summary>
    public ObservableCollection<AuthUserDto> AddableAccounts { get; } = new();

    private PermissionGroupRowViewModel? _selectedGroup;
    public PermissionGroupRowViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            _selectedGroup = value;
            NotifyOfPropertyChange(() => SelectedGroup);
            NotifyOfPropertyChange(() => CanModifySelectedGroup);
            NotifyOfPropertyChange(() => HasSelectedGroup);
        }
    }

    /// <summary>선택된 그룹이 있음 — 구성원 관리 버튼 활성(예약 등급 포함).</summary>
    public bool HasSelectedGroup => _selectedGroup is not null;

    // ── 화면 모드 ──
    private enum PanelMode { List, Matrix, Members }
    private PanelMode _mode = PanelMode.List;
    public bool IsListView => _mode == PanelMode.List;
    public bool IsDetailView => _mode == PanelMode.Matrix;
    public bool IsMembersView => _mode == PanelMode.Members;
    private void SetMode(PanelMode m)
    {
        _mode = m;
        NotifyOfPropertyChange(() => IsListView);
        NotifyOfPropertyChange(() => IsDetailView);
        NotifyOfPropertyChange(() => IsMembersView);
        NotifyOfPropertyChange(() => CanSave);
    }

    /// <summary>선택 그룹이 예약 등급이 아님 → 삭제/이름수정 가능(구성원 관리는 예약 그룹도 허용).</summary>
    public bool CanModifySelectedGroup => _selectedGroup is { IsReserved: false };

    // ── 매트릭스 상세 ──
    private string _detailGroupName = string.Empty;
    public string DetailGroupName { get => _detailGroupName; set { _detailGroupName = value; NotifyOfPropertyChange(() => DetailGroupName); } }
    private int _detailGroupId;
    private List<int>? _detailDeviceGroups;

    private bool _isSaving;
    public bool IsSaving { get => _isSaving; set { _isSaving = value; NotifyOfPropertyChange(() => IsSaving); NotifyOfPropertyChange(() => CanSave); } }
    /// <summary>[저장] 활성 — 매트릭스 화면 + 저장 중 아님.</summary>
    public bool CanSave => IsDetailView && !_isSaving;

    // ── 그룹 생성/이름수정 폼 ──
    private bool _isGroupFormOpen;
    public bool IsGroupFormOpen { get => _isGroupFormOpen; set { _isGroupFormOpen = value; NotifyOfPropertyChange(() => IsGroupFormOpen); } }
    private int _formGroupId;   // 0 = 신규, >0 = 수정
    public string FormTitle => _formGroupId > 0 ? "그룹 이름/설명 수정" : "새 권한 그룹";
    private string _formName = string.Empty;
    public string FormName { get => _formName; set { _formName = value; NotifyOfPropertyChange(() => FormName); NotifyOfPropertyChange(() => CanSaveGroupForm); } }
    private string _formDescription = string.Empty;
    public string FormDescription { get => _formDescription; set { _formDescription = value; NotifyOfPropertyChange(() => FormDescription); } }
    public bool CanSaveGroupForm => !string.IsNullOrWhiteSpace(_formName);

    // ── 구성원 화면 ──
    private int _membersGroupId;
    private string _membersGroupName = string.Empty;
    public string MembersGroupName { get => _membersGroupName; set { _membersGroupName = value; NotifyOfPropertyChange(() => MembersGroupName); } }
    private AuthUserDto? _selectedAddAccount;
    public AuthUserDto? SelectedAddAccount { get => _selectedAddAccount; set { _selectedAddAccount = value; NotifyOfPropertyChange(() => SelectedAddAccount); NotifyOfPropertyChange(() => CanAddMember); } }
    public bool CanAddMember => _selectedAddAccount is not null;
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await ReloadAsync(cancellationToken);
    }
    #endregion

    #region - Binding: 목록/CRUD -
    public async Task OnClickReloadButton() => await ReloadAsync(CancellationToken.None);

    /// <summary>그룹 더블클릭 → 그 그룹의 전체 권한(모듈×동작) 매트릭스 화면으로 진입.</summary>
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
        SetMode(PanelMode.Matrix);
    }

    public void ClickBackToList() { IsGroupFormOpen = false; SetMode(PanelMode.List); }

    /// <summary>새 그룹 폼 열기.</summary>
    public void OnClickNewGroup()
    {
        _formGroupId = 0;
        FormName = string.Empty;
        FormDescription = string.Empty;
        NotifyOfPropertyChange(() => FormTitle);
        IsGroupFormOpen = true;
    }

    /// <summary>선택 그룹 이름/설명 수정 폼 열기 (예약 등급 제외).</summary>
    public void OnClickRenameGroup()
    {
        var row = SelectedGroup;
        if (row is null || row.IsReserved) return;
        var g = _raw.FirstOrDefault(x => x.Id == row.GroupId);
        _formGroupId = row.GroupId;
        FormName = g?.Name ?? row.GroupName;
        FormDescription = g?.Description ?? string.Empty;
        NotifyOfPropertyChange(() => FormTitle);
        IsGroupFormOpen = true;
    }

    public void ClickCancelGroupForm() => IsGroupFormOpen = false;

    /// <summary>그룹 폼 저장 — _formGroupId=0이면 생성(POST), >0이면 메타 수정(PUT).</summary>
    public async Task ClickSaveGroupForm()
    {
        if (!CanSaveGroupForm) return;
        try
        {
            if (_formGroupId > 0)
            {
                var res = await _api.UpdateUserGroupAsync(_formGroupId, new UserGroupUpdateDto { Name = FormName.Trim(), Description = FormDescription });
                if (!res.Success) { await Info($"수정 실패: {res.Error?.Message ?? res.Message}"); return; }
            }
            else
            {
                var res = await _api.CreateUserGroupAsync(new UserGroupCreateDto { Name = FormName.Trim(), Description = FormDescription });
                if (!res.Success) { await Info($"생성 실패: {res.Error?.Message ?? res.Message}"); return; }
            }
            IsGroupFormOpen = false;
            await ReloadAsync(CancellationToken.None);
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 그룹 저장 실패: {ex.Message}"); }
    }

    /// <summary>선택 그룹 삭제 → Confirm(소속 해제 경고). Yes 시 HandleAsync 가 실제 DELETE.</summary>
    public async Task OnClickDeleteGroup()
    {
        var row = SelectedGroup;
        if (row is null || row.IsReserved) return;
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "권한 그룹 삭제",
            Explain = $"'{row.GroupName}' 그룹을 삭제하시겠습니까?\n소속 계정 {row.UserCount}명의 그룹 배정이 해제됩니다(해당 계정 권한 초기화).",
            MessageModel = new CallDeleteGroupMessageModel { GroupId = row.GroupId, GroupName = row.GroupName }
        });
    }

    public async Task HandleAsync(CallDeleteGroupMessageModel message, CancellationToken cancellationToken)
    {
        if (message is null || message.GroupId <= 0) return;
        try
        {
            var res = await _api.DeleteUserGroupAsync(message.GroupId);
            if (res.Success) await ReloadAsync(CancellationToken.None);
            else await Info($"삭제 실패: {res.Error?.Message ?? res.Message}");
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 그룹 삭제 실패: {ex.Message}"); }
    }
    #endregion

    #region - Binding: 매트릭스 저장 -
    /// <summary>현재 매트릭스 그룹의 권한(모듈×동작)을 서버에 저장(ADMIN). POST /user-groups/{id}/permissions.</summary>
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
                await Info($"'{DetailGroupName}' 그룹의 권한을 저장했습니다.");
                await ReloadAsync(CancellationToken.None);
            }
            else await Info($"저장 실패: {res.Error?.Message ?? res.Message}");
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 권한 저장 실패: {ex.Message}"); }
        finally { IsSaving = false; }
    }
    #endregion

    #region - Binding: 구성원 -
    /// <summary>선택 그룹의 구성원 관리 화면으로 진입.</summary>
    public async Task OnClickManageMembers()
    {
        var row = SelectedGroup;
        if (row is null) return;
        _membersGroupId = row.GroupId;
        MembersGroupName = row.GroupName;
        SetMode(PanelMode.Members);
        await ReloadMembersAsync();
    }

    /// <summary>선택 계정을 이 그룹에 배정(user.group_id = 그룹).</summary>
    public async Task ClickAddMember()
    {
        var acc = SelectedAddAccount;
        if (acc is null || _membersGroupId <= 0) return;
        try
        {
            var res = await _api.AssignUserGroupAsync(acc.Id, _membersGroupId);
            if (res.Success) await ReloadMembersAsync();
            else await Info($"추가 실패: {res.Error?.Message ?? res.Message}");
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 구성원 추가 실패: {ex.Message}"); }
    }

    /// <summary>구성원 해제(user.group_id = null).
    /// ⚠ 서버 update_user(users.py: `if group_id is not None`)가 null을 무시할 수 있음 → 반환 사용자 group_id로 실제 반영 확인,
    /// 미반영 시 무증상 실패 대신 안내(서버 수정 대기, PRD V-03).</summary>
    public async Task OnClickRemoveMember(AuthUserDto member)
    {
        if (member is null) return;
        try
        {
            var res = await _api.AssignUserGroupAsync(member.Id, null);
            if (!res.Success) { await Info($"해제 실패: {res.Error?.Message ?? res.Message}"); return; }
            if (res.Data?.GroupId == _membersGroupId)
            {
                await Info("서버가 그룹 해제(group_id=null)를 반영하지 않습니다. 서버 수정이 필요합니다(구성원 해제 보류).");
                return;
            }
            await ReloadMembersAsync();
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 구성원 해제 실패: {ex.Message}"); }
    }

    public async Task OnClickBackFromMembers() { SetMode(PanelMode.List); await ReloadAsync(CancellationToken.None); }
    #endregion

    #region - Processes -
    private async Task ReloadAsync(CancellationToken ct)
    {
        try
        {
            IsGroupFormOpen = false;
            SetMode(PanelMode.List);
            var res = await _api.GetUserGroupsAsync(ct);
            _raw = (res.Success && res.Data is not null) ? res.Data : new List<UserGroupDto>();

            // ⚠ 서버 /users limit 상한=100(le=100). 그룹별 사용자 수 = group_id 소속 계정 수(상시 배정).
            var usersRes = await _api.GetUsersAsync(1, 100, ct);
            var users = (usersRes.Success && usersRes.Data is not null) ? usersRes.Data : new List<AuthUserDto>();
            var countByGroupId = users.Where(u => u.GroupId.HasValue)
                                      .GroupBy(u => u.GroupId!.Value)
                                      .ToDictionary(x => x.Key, x => x.Count());

            Groups.Clear();
            // 예약 등급 우선(랭크 desc) → 임의 그룹(이름 asc)
            var ordered = _raw
                .Select(g => new { g, reserved = _levels.ContainsKey(g.Name) })
                .OrderByDescending(x => x.reserved ? _levels[x.g.Name].rank : 0)
                .ThenBy(x => x.reserved ? string.Empty : x.g.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var item in ordered)
            {
                var g = item.g;
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
                    GroupName = item.reserved ? _levels[g.Name].label : g.Name,
                    IsReserved = item.reserved,
                    UserCount = countByGroupId.TryGetValue(g.Id, out var uc) ? uc : 0,
                    Active = g.IsActive ? "사용" : "미사용",
                    ViewCount = v,
                    EditCount = e,
                    DeleteCount = d,
                    ControlCount = c,
                });
            }
            if (!res.Success)
                await Info($"불러오기 실패: {res.Error?.Message ?? res.Message}");
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 로드 실패: {ex.Message}"); }
    }

    private async Task ReloadMembersAsync()
    {
        try
        {
            Members.Clear();
            var res = await _api.GetUserGroupUsersAsync(_membersGroupId);
            var members = (res.Success && res.Data is not null) ? res.Data : new List<AuthUserDto>();
            foreach (var u in members) Members.Add(u);
            if (!res.Success) await Info($"구성원 불러오기 실패: {res.Error?.Message ?? res.Message}");

            // 추가 후보 = 전체 계정 − 현재 구성원
            AddableAccounts.Clear();
            var allRes = await _api.GetUsersAsync(1, 100);
            if (allRes.Success && allRes.Data is not null)
            {
                var memberIds = new HashSet<int>(members.Select(m => m.Id));
                foreach (var u in allRes.Data.Where(u => !memberIds.Contains(u.Id))) AddableAccounts.Add(u);
            }
            SelectedAddAccount = null;
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 구성원 로드 실패: {ex.Message}"); }
    }

    private Task Info(string msg) =>
        _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "권한 그룹", Explain = msg });
    #endregion
}

/// <summary>권한 그룹 삭제 확인 트리거 — Confirm 다이얼로그 Yes 시 발행되어 HandleAsync 가 실제 DELETE.</summary>
public class CallDeleteGroupMessageModel : IMessageModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
}
