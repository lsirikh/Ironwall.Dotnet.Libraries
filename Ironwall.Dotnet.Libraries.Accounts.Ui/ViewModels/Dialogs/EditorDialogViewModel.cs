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
                                , IProfileImageService profileImage
                                , IProfileGateway profileGateway)
                                : base(eventAggregator, log)
    {
        ViewModel = accountViewModel;
        _gateway = gateway;
        _session = session;
        _profileImage = profileImage;
        _profileGateway = profileGateway;
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
        // ⚠ [데이터 무결성 사고 차단] EditorDialog 는 '관리자가 타 계정을 편집'하는 다이얼로그다.
        //   기존엔 _profileGateway.UploadPhotoAsync(파일)이 서버 POST /users/me/photo(=토큰 소유자=로그인 관리자 '본인')를
        //   호출해, m_manager 편집 중 사진을 올리면 '관리자 자신(admin)'의 사진이 오염됐다(admin↔m_manager 동일 사진 사고).
        //   서버에 관리자용 POST /users/{id}/photo 가 없어 '대상 계정' 업로드가 불가하다(REQ_Server_Admin_Photo_Upload).
        //   → 서버 엔드포인트 배포 전까지 EditorDialog 사진 변경은 차단한다. 본인 사진은 마이페이지(POST /me/photo)에서.
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
        {
            Title = "사진 변경 안내",
            Explain = "다른 계정의 사진 변경은 현재 서버에서 지원되지 않습니다.\n" +
                      "본인 사진은 '마이페이지'에서 변경하세요. (관리자용 타 계정 업로드 API 준비 중)"
        });
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
    private readonly IProfileGateway _profileGateway;
    #endregion
}
