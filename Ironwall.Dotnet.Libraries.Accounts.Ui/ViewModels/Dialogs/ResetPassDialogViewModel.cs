using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : 본인 비밀번호 변경 다이얼로그 — 라이브러리 이관(B) + gateway 주입 + 결함 수정
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountDbService → IProfileGateway(검증+변경+실패 롤백 흡수),
                  비밀번호 선적용 버그 제거, async void → async Task, 최소 8자 검증 추가.
****************************************************************************/
public class ResetPassDialogViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ResetPassDialogViewModel(IEventAggregator eventAggregator
                                    , ILogService log
                                    , LoginViewModel viewModel
                                    , IProfileGateway gateway)
                                    : base(eventAggregator, log)
    {
        ViewModel = viewModel;
        _gateway = gateway;
    }
    #endregion
    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        InputPassword = null;
        NewPassword = null;
        NewPasswordConfirm = null;
        return base.OnActivateAsync(cancellationToken);
    }
    #endregion
    #region - Binding Methods -
    public bool CanClickOk =>
           !string.IsNullOrEmpty(InputPassword)
           && !string.IsNullOrEmpty(NewPassword)
           && !string.IsNullOrEmpty(NewPasswordConfirm)
           && NewPassword!.Equals(NewPasswordConfirm)
           && NewPassword!.Length >= 8;          // 최소 8자 (비밀번호 정책)

    public async Task ClickOk()
    {
        var ct = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            // 게이트웨이가 현재 비밀번호 검증 + 변경 + 실패 시 롤백 (Db: 로컬, Api(GOP-00): 서버)
            var updated = await _gateway.ChangePasswordAsync(ViewModel.Model, InputPassword!, NewPassword!, ct);
            if (updated != null)
            {
                ViewModel.Insert(updated);
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "비밀번호가 변경되었습니다." });
                await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
            }
            else
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "현재 비밀번호가 틀렸습니다." });
                InputPassword = null;
            }
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "비밀번호 변경을 실패하였습니다." });
        }
    }

    public async Task ClickCancel()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
    #endregion
    #region - Properties -
    public string? InputPassword
    {
        get => _password;
        set { _password = value; NotifyOfPropertyChange(() => InputPassword); NotifyOfPropertyChange(nameof(CanClickOk)); }
    }

    public string? NewPassword
    {
        get => _newPassword;
        set { _newPassword = value; NotifyOfPropertyChange(() => NewPassword); NotifyOfPropertyChange(() => NewPasswordConfirm); NotifyOfPropertyChange(nameof(CanClickOk)); }
    }

    public string? NewPasswordConfirm
    {
        get => _newPasswordConfirm;
        set { _newPasswordConfirm = value; NotifyOfPropertyChange(() => NewPasswordConfirm); NotifyOfPropertyChange(nameof(CanClickOk)); }
    }

    public LoginViewModel ViewModel { get; }
    #endregion
    #region - Attributes -
    private readonly IProfileGateway _gateway;
    private string? _password;
    private string? _newPassword;
    private string? _newPasswordConfirm;
    #endregion
}
