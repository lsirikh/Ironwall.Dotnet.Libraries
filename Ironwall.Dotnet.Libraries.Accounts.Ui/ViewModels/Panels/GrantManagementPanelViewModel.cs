using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 권한그룹 한시부여(Grant Scheduling) 관리 — ADMIN (T4/FR-GS-06)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 사용자 선택 → 부여 목록 조회 + 부여(그룹·유효기간)/회수(Confirm). 서버 grants API 소비.
                  GET/POST /users/{id}/grants · DELETE /grants/{id}. IAccountApiService 직접 주입(GOP 모드).
****************************************************************************/
public class GrantManagementPanelViewModel : BasePanelViewModel, IHandle<CallRevokeGrantMessageModel>
{
    private readonly IAccountApiService _api;

    public GrantManagementPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api)
        : base(eventAggregator, log)
    {
        _api = api;
        _validFrom = DateTime.Now;
    }

    /// <summary>부여 대상 계정 목록(선택).</summary>
    public ObservableCollection<AuthUserDto> Accounts { get; } = new();
    /// <summary>부여할 권한그룹 목록(선택).</summary>
    public ObservableCollection<UserGroupDto> Groups { get; } = new();
    /// <summary>선택 계정의 부여 목록. DataGrid ItemsSource.</summary>
    public ObservableCollection<GrantDto> Grants { get; } = new();

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await LoadAccountsAndGroupsAsync(cancellationToken);
    }
    #endregion

    #region - Binding Methods -
    public async Task OnClickReloadButton() => await ReloadGrantsAsync();

    /// <summary>부여 실행 — 클라 1차 경계검증(until>from) 후 POST. 서버 422가 최종.</summary>
    public async Task ClickCreateGrant()
    {
        var acc = SelectedAccount; var grp = SelectedGroup;
        if (acc is null || grp is null) return;
        if (ValidUntil.HasValue && ValidUntil.Value <= ValidFrom)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 부여", Explain = "종료 일시는 시작 일시보다 뒤여야 합니다." });
            return;
        }
        try
        {
            var dto = new GrantCreateDto { GroupId = grp.Id, ValidFrom = ValidFrom, ValidUntil = ValidUntil };
            var res = await _api.CreateGrantAsync(acc.Id, dto);
            if (res.Success)
            {
                await ReloadGrantsAsync();
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 부여", Explain = $"'{acc.LoginId}'에게 '{grp.Name}' 부여 완료." });
            }
            else
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 부여", Explain = $"부여 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 부여 실패: {ex.Message}"); }
    }

    /// <summary>회수 클릭 → Confirm(즉시 삭제 않음). Yes 시 CallRevokeGrantMessageModel 발행.</summary>
    public async Task OnClickRevoke(GrantDto grant)
    {
        if (grant is null) return;
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "권한 회수",
            Explain = $"'{grant.GroupName ?? grant.GroupId.ToString()}' 부여(#{grant.Id})를 회수하시겠습니까?",
            MessageModel = new CallRevokeGrantMessageModel { Grant = grant }
        });
    }

    /// <summary>Confirm→Yes 확인 후 실제 DELETE /grants/{id} + 목록 갱신.</summary>
    public async Task HandleAsync(CallRevokeGrantMessageModel message, CancellationToken cancellationToken)
    {
        var grant = message.Grant;
        if (grant is null) return;
        try
        {
            var res = await _api.DeleteGrantAsync(grant.Id);
            if (res.Success) await ReloadGrantsAsync();
            else await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 회수", Explain = $"회수 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 회수 실패: {ex.Message}"); }
    }
    #endregion

    #region - Processes -
    private async Task LoadAccountsAndGroupsAsync(CancellationToken ct)
    {
        try
        {
            var usersRes = await _api.GetUsersAsync(1, 200, ct);
            Accounts.Clear();
            if (usersRes.Success && usersRes.Data is not null)
                foreach (var u in usersRes.Data) Accounts.Add(u);

            var groupsRes = await _api.GetUserGroupsAsync(ct);
            Groups.Clear();
            if (groupsRes.Success && groupsRes.Data is not null)
                foreach (var g in groupsRes.Data) Groups.Add(g);
        }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 계정/그룹 로드 실패: {ex.Message}"); }
    }

    private async Task ReloadGrantsAsync()
    {
        Grants.Clear();
        var acc = SelectedAccount;
        if (acc is null) return;
        try
        {
            var res = await _api.GetUserGrantsAsync(acc.Id);
            if (res.Success && res.Data is not null)
                foreach (var gr in res.Data) Grants.Add(gr);
            else if (!res.Success)
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 부여", Explain = $"부여 목록 불러오기 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[GrantMgmt] 부여 목록 로드 실패: {ex.Message}"); }
    }
    #endregion

    #region - Properties -
    private AuthUserDto? _selectedAccount;
    /// <summary>선택 계정 — 변경 시 해당 사용자 부여 목록 재조회.</summary>
    public AuthUserDto? SelectedAccount
    {
        get => _selectedAccount;
        set { _selectedAccount = value; NotifyOfPropertyChange(() => SelectedAccount); NotifyOfPropertyChange(nameof(CanCreateGrant)); _ = ReloadGrantsAsync(); }
    }

    private UserGroupDto? _selectedGroup;
    public UserGroupDto? SelectedGroup
    {
        get => _selectedGroup;
        set { _selectedGroup = value; NotifyOfPropertyChange(() => SelectedGroup); NotifyOfPropertyChange(nameof(CanCreateGrant)); }
    }

    private DateTime _validFrom;
    /// <summary>유효 시작(기본=현재). </summary>
    public DateTime ValidFrom { get => _validFrom; set { _validFrom = value; NotifyOfPropertyChange(() => ValidFrom); } }

    private DateTime? _validUntil;
    /// <summary>유효 종료(null=상시).</summary>
    public DateTime? ValidUntil { get => _validUntil; set { _validUntil = value; NotifyOfPropertyChange(() => ValidUntil); } }

    /// <summary>부여 버튼 활성 — 계정·그룹 선택 시.</summary>
    public bool CanCreateGrant => SelectedAccount is not null && SelectedGroup is not null;
    #endregion
}

/// <summary>권한그룹 부여 회수 확인 트리거 — Confirm 다이얼로그 Yes 시 발행되어 HandleAsync가 실제 DELETE. (T4)</summary>
public class CallRevokeGrantMessageModel : IMessageModel
{
    public GrantDto Grant { get; set; } = default!;
}
