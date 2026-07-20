using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;
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
                                    , IHandle<CallDeletePhotoAdminProcessMessageModel>
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

    /// <summary>관리자: 대상 계정(ViewModel.Model) 프로필 사진 업로드. 서버 POST /users/{id}/photo 로 **대상 {id}** 를 향한다 —
    ///   본인 /me/photo 재사용으로 로그인 관리자 사진이 오염되던 사고(2026-07-13, 6842db5 차단) 재발 원천 차단. — Admin_Photo_Upload FR-04
    ///   ⚠ 업로드는 즉시 서버 커밋 — 다이얼로그 '취소'를 눌러도 사진 변경은 이미 반영됨(다른 필드와 비대칭).</summary>
    public async Task ClickAddPicture()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.bmp;*.jpg;*.jpeg;*.gif;*.png;*.webp|All files|*.*",
            Title = "이미지 선택",
            RestoreDirectory = true
        };
        if (dlg.ShowDialog() != true) return;

        // 이미지 검증만 수행(로컬 복사 없이) — 실패 업로드마다 로컬 orphan 이 쌓이던 문제 제거. 원본 경로를 그대로 서버 전송.
        if (!ProfileImageHelper.IsValid(dlg.FileName, out var error))
        {
            _log?.Warning(error);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "이미지", Explain = error });
            return;
        }

        var oldImage = ViewModel.Image;   // 실패 시 표시 원복용
        try
        {
            // ⚠ 반드시 대상 계정 {id} 로 업로드(=본인 /me 금지). 성공=서버 photo_url(절대 URL).
            var url = await _gateway.UploadPhotoAsync(ViewModel.Model.Id, dlg.FileName, CancellationToken.None);
            if (!string.IsNullOrEmpty(url))
            {
                ViewModel.Image = url;
                await _eventAggregator!.PublishOnCurrentThreadAsync(new RefreshAccountsMessageModel());
            }
            else
            {
                // API 실패(403/네트워크) 또는 DB 모드(미지원) — 사진 미반영, 표시 원복.
                ViewModel.Image = oldImage;
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사진 변경", Explain = "사진 변경이 반영되지 않았습니다. (권한 또는 서버 오류)" });
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[EditorDialog] 사진 업로드 실패: {ex.Message}");
            ViewModel.Image = oldImage;
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사진 변경", Explain = "사진 업로드에 실패했습니다. 다시 시도해 주세요." });
        }
    }

    /// <summary>관리자: 대상 계정 프로필 사진 삭제 — 서버 영구삭제·되돌릴 수 없어 확인 팝업 후 실행(HandleAsync). — Admin_Photo_Upload FR-05</summary>
    public async Task ClickDeletePicture()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "이 계정의 프로필 사진을 삭제하시겠습니까?\n(서버에서 영구 삭제되며 되돌릴 수 없습니다.)",
            MessageModel = new CallDeletePhotoAdminProcessMessageModel()
        });
    #endregion
    #region - IHanldes -
    /// <summary>사진 삭제 확인('확인') → 대상 계정 {id} 사진 서버 삭제 → default 아바타 복귀. — Admin_Photo_Upload FR-05</summary>
    public async Task HandleAsync(CallDeletePhotoAdminProcessMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            var ok = await _gateway.DeletePhotoAsync(ViewModel.Model.Id, cancellationToken);
            if (ok)
            {
                ViewModel.Image = null;   // 서버 photo_url=null → default 아바타
                await _eventAggregator!.PublishOnCurrentThreadAsync(new RefreshAccountsMessageModel(), cancellationToken);
            }
            else
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사진 삭제", Explain = "사진 삭제가 반영되지 않았습니다. (권한 또는 서버 오류)" }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[EditorDialog] 사진 삭제 실패: {ex.Message}");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사진 삭제", Explain = "사진 삭제에 실패했습니다. 다시 시도해 주세요." }, cancellationToken);
        }
    }

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
