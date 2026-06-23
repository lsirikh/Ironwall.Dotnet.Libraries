using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : 관리자 계정 편집/비밀번호 초기화 다이얼로그 — 이관(B) + gateway 주입
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountDbService→IUserDirectoryGateway, 하드코딩 "12345678" →
                  ISessionConfigService.AdminResetPassword(외부화), async void→Task, Task.Delay 제거.
****************************************************************************/
public class EditorDialogViewModel : BasePanelViewModel
                                    , IHandle<CallEditAccountAdminProcessMessageModel>
                                    , IHandle<CallResetPasswordAdminProcessMessageModel>
{
    #region - Ctors -
    public EditorDialogViewModel(IEventAggregator eventAggregator
                                , ILogService log
                                , AccountViewModel accountViewModel
                                , IUserDirectoryGateway gateway
                                , ISessionConfigService session)
                                : base(eventAggregator, log)
    {
        ViewModel = accountViewModel;
        _gateway = gateway;
        _session = session;
    }
    #endregion
    #region - Binding Methods -
    public async Task ClickOk()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "사용자 정보를 변경하시겠습니까?",
            MessageModel = new CallEditAccountAdminProcessMessageModel()
        });

    public async Task ClickResetPassword()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = $"비밀번호를 초기화({ResetPassword})하시겠습니까?",
            MessageModel = new CallResetPasswordAdminProcessMessageModel()
        });

    public async Task ClickCancel()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CallResetPasswordAdminProcessMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

            // 관리자 강제 초기화(현재 비밀번호 검증 없음). 초기 비밀번호는 설정에서 주입(하드코딩 제거).
            var updated = await _gateway.ResetAccountPasswordAsync(ViewModel.Model, _session.AdminResetPassword, cancellationToken);
            if (updated != null) ViewModel.Insert(updated);

            await _eventAggregator!.PublishOnCurrentThreadAsync(new RefreshAccountsMessageModel(), cancellationToken);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "사용자 비밀번호가 변경되었습니다." }, cancellationToken);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel(), cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "사용자 정보 변경이 실패하였습니다." }, cancellationToken);
        }
    }

    public async Task HandleAsync(CallEditAccountAdminProcessMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

            var ret = await _gateway.UpdateAccountAsync(ViewModel.Model, cancellationToken);
            if (ret != null) ViewModel.Insert(ret);

            await _eventAggregator!.PublishOnCurrentThreadAsync(new RefreshAccountsMessageModel(), cancellationToken);
            _log?.Info("사용자 정보 변경작업 성공");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "사용자 정보 변경이 정상적으로 완료되었습니다." }, cancellationToken);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel(), cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Info($"변경작업 실패 : {ex.Message}");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "사용자 정보 변경이 실패하였습니다." }, cancellationToken);
        }
    }
    #endregion
    #region - Properties -
    /// <summary>관리자 초기화 기본 비밀번호 (설정 주입, 하드코딩 제거).</summary>
    public string ResetPassword => _session.AdminResetPassword;
    public AccountViewModel ViewModel { get; }
    #endregion
    #region - Attributes -
    private readonly IUserDirectoryGateway _gateway;
    private readonly ISessionConfigService _session;
    #endregion
}
