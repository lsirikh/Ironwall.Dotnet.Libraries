using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 세션 모니터(읽기전용) — GOP /api/user-sessions 조회
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountApiService 직접 주입(GOP 모드 전용). 강제 로그아웃(쓰기)은 서버/배선 후속 — 현재 조회만.
                  AuditLog/PermissionMatrix와 동일 패턴(BasePanelViewModel + ObservableCollection<DTO>).
****************************************************************************/
public class UserSessionPanelViewModel : BasePanelViewModel
{
    private readonly IAccountApiService _api;

    public UserSessionPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api)
        : base(eventAggregator, log) => _api = api;

    /// <summary>접속 세션 목록. DataGrid ItemsSource.</summary>
    public ObservableCollection<UserSessionDto> Items { get; } = new();

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await ReloadAsync(cancellationToken);
    }

    public async Task OnClickReloadButton() => await ReloadAsync(CancellationToken.None);

    /// <summary>세션 강제 로그아웃(ADMIN) — DELETE /user-sessions/{id} 후 목록 갱신. 비활성 세션은 무시.</summary>
    public async Task OnClickForceLogout(UserSessionDto session)
    {
        if (session is null || !session.IsActive) return;
        try
        {
            var res = await _api.ForceLogoutSessionAsync(session.Id);
            if (res.Success)
                await ReloadAsync(CancellationToken.None);
            else
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 관리", Explain = $"강제 로그아웃 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[UserSession] 강제로그아웃 실패: {ex.Message}"); }
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        try
        {
            var res = await _api.GetUserSessionsAsync(ct);
            Items.Clear();
            if (res.Success && res.Data is not null)
                foreach (var d in res.Data) Items.Add(d);
            else
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "세션 관리", Explain = $"불러오기 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[UserSession] 로드 실패: {ex.Message}"); }
    }
}
