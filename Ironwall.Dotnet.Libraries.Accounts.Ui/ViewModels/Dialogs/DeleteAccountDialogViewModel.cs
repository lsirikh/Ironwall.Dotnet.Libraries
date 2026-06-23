using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : 계정 삭제 다이얼로그 — 라이브러리 이관(B) + gateway 주입 + 결함 수정
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountDbService → IUserDirectoryGateway, PasswordHelper 검증을 gateway가 흡수,
                  2중 루프 버그 → AccountDeletionPolicy, async void → async Task, ct 전달,
                  인위적 Task.Delay 제거.
****************************************************************************/
public class DeleteAccountDialogViewModel : BasePanelViewModel
{
    #region - Ctors -
    public DeleteAccountDialogViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , LoginViewModel loginViewModel
                                        , AccountProvider accountProvider
                                        , IUserDirectoryGateway gateway)
                                        : base(eventAggregator, log)
    {
        ViewModel = loginViewModel;
        _gateway = gateway;
        _accountProvider = accountProvider;
    }
    #endregion
    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        InputPassword = null;
        return base.OnActivateAsync(cancellationToken);
    }
    #endregion
    #region - Binding Methods -
    public async Task ClickOk()
    {
        var ct = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel());

            // 관리자 삭제 가능 여부(정책) — 인메모리 provider 기준
            if (ViewModel.Level == EnumLevelType.ADMIN
                && !AccountDeletionPolicy.CanDeleteAdmin(_accountProvider.ToList()))
                throw new Exception("마지막 관리자 계정은 삭제할 수 없습니다.");

            // 게이트웨이가 비밀번호 검증 후 삭제 (Db: 로컬, Api(GOP-00): 서버)
            var ok = await _gateway.RemoveAccountAsync(ViewModel.Model, InputPassword!, ct);
            if (!ok) throw new Exception("비밀번호가 일치하지 않거나 삭제에 실패했습니다.");

            var selected = _accountProvider.Where(entity => entity.Id == ViewModel.Model.Id).FirstOrDefault();
            if (selected != null) _accountProvider.Remove(selected);

            ViewModel.Logout();
            await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseAllWindowsMessageModel());
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = ex.Message });
        }
    }

    public async Task ClickCancel()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
    #endregion
    #region - Processes -
    public bool CanClickOk => !string.IsNullOrEmpty(InputPassword);
    #endregion
    #region - Properties -
    public string? InputPassword
    {
        get => _password;
        set
        {
            _password = value;
            NotifyOfPropertyChange(() => InputPassword);
            NotifyOfPropertyChange(nameof(CanClickOk));
        }
    }

    public LoginViewModel ViewModel { get; }
    #endregion
    #region - Attributes -
    private readonly IUserDirectoryGateway _gateway;
    private readonly AccountProvider _accountProvider;
    private string? _password;
    #endregion
}
