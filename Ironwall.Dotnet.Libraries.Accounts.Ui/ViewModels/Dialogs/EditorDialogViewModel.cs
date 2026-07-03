using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Microsoft.Win32;
using System;
using System.IO;

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
                                , ISessionConfigService session
                                , IProfileImageService profileImage)
                                : base(eventAggregator, log)
    {
        ViewModel = accountViewModel;
        _gateway = gateway;
        _session = session;
        _profileImage = profileImage;
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

    /// <summary>사진 추가 — 파일 선택 후 IProfileImageService로 저장. 등록(Register)·마이페이지엔 있으나 변경(Editor) 다이얼로그엔
    ///   누락돼 사진 버튼이 무반응이던 버그 수정(사용자 실측). Register 패턴과 동일.</summary>
    public async Task ClickAddPicture()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.bmp;*.jpg;*.gif;*.png;*.tiff|All files|*.*",
            Title = "이미지 선택",
            RestoreDirectory = true
        };
        if (dlg.ShowDialog() != true) return;

        var ct = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            var key = $"{DateTime.Now:yyyyMMddHHmmssfff}";
            var saved = await _profileImage.SaveAsync(dlg.FileName, key, ct);
            ViewModel.Image = Path.GetFileName(saved);
        }
        catch (ArgumentException ex)
        {
            _log?.Warning(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "이미지", Explain = ex.Message });
        }
    }
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
            if (ret == null)   // 저장 실패(null)인데 "완료"+닫힘으로 오인시키던 버그 — 실패 노출, 다이얼로그 유지
            {
                _log?.Warning("계정 정보 변경 실패 — 서버가 저장하지 못함(null)");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "계정 편집", Explain = "계정 정보 변경이 반영되지 않았습니다. 다시 시도해 주세요." }, cancellationToken);
                return;
            }
            ViewModel.Insert(ret);

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
    private readonly IProfileImageService _profileImage;
    #endregion
}
