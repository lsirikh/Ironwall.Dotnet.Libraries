using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
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
    #region - Overrides -
    /// <summary>
    /// 호스트(ConductorControlViewModel)가 다이얼로그를 띄울 때. <see cref="BeginEdit"/> 를 거치지 않고 누가
    /// <see cref="ViewModel"/> 에 직접 채워 넣었더라도 여기서 편집 전 기준을 잡는다(다른 계정이면 새로 잡는다).
    /// </summary>
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_baseline is null || _baseline.Id != ViewModel.Model.Id) CaptureBaseline();
        return base.OnActivateAsync(cancellationToken);
    }
    #endregion
    #region - Binding Methods -
    /// <summary>
    /// 편집할 계정을 싣고 <b>편집 전 기준</b>을 잡는다 — [확인] 은 이 기준과 달라진 칸만 서버에 보낸다.
    /// 계정 관리 패널(행 더블클릭)이 다이얼로그를 열기 직전에 부른다.
    /// </summary>
    public void BeginEdit(IAccountModel account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ViewModel.Insert(account);
        CaptureBaseline();
    }

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
            Filter = "이미지 파일 (JPG/PNG/WebP/GIF)|*.jpg;*.jpeg;*.png;*.webp;*.gif|모든 파일|*.*",   // 서버 허용 형식과 정렬(bmp/tiff 제외)
            Title = "이미지 선택",
            RestoreDirectory = true
        };
        if (dlg.ShowDialog() != true) return;
        await UploadPictureAsync(dlg.FileName);
    }

    /// <summary>
    /// 고른 파일을 대상 계정 사진으로 올린다 — 파일 고르기(<see cref="ClickAddPicture"/>)와 분리해
    /// 헤드리스 시험 · 라이브 왕복 하네스가 창을 띄우지 않고 같은 경로를 탄다.
    /// </summary>
    public async Task UploadPictureAsync(string filePath)
    {
        // 이미지 검증만 수행(로컬 복사 없이) — 실패 업로드마다 로컬 orphan 이 쌓이던 문제 제거. 원본 경로를 그대로 서버 전송.
        if (!ProfileImageHelper.IsValid(filePath, out var error))
        {
            _log?.Warning(error);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "이미지", Explain = error });
            return;
        }

        var oldImage = ViewModel.Image;   // 실패 시 표시 원복용
        try
        {
            // ⚠ 반드시 대상 계정 {id} 로 업로드(=본인 /me 금지). 성공=서버 photo_url(절대 URL).
            var url = await _gateway.UploadPhotoAsync(ViewModel.Model.Id, filePath, CancellationToken.None);
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
        // ⚠ Confirm 팝업은 self-close 안 함(시블링 핸들러 CallDeleteAccount/Unlock 동형) → 진입 시 반드시 청산.
        //   기존엔 누락되어 '확인'을 눌러도 팝업이 남아 '삭제가 안 되는 것처럼' 보였다(소프트락).
        await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        string explain;
        try
        {
            var ok = await _gateway.DeletePhotoAsync(ViewModel.Model.Id, cancellationToken);
            if (ok)
            {
                ViewModel.Image = null;   // 서버 photo_url=null → default 아바타
                await _eventAggregator!.PublishOnCurrentThreadAsync(new RefreshAccountsMessageModel(), cancellationToken);
                explain = "프로필 사진을 삭제했습니다.";
            }
            else
                explain = "사진 삭제가 반영되지 않았습니다. (권한 또는 서버 오류)";
        }
        catch (Exception ex)
        {
            _log?.Error($"[EditorDialog] 사진 삭제 실패: {ex.Message}");
            explain = "사진 삭제 중 오류가 발생했습니다. 다시 시도해 주세요.";
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사진 삭제", Explain = explain }, cancellationToken);
    }

    public async Task HandleAsync(CallResetPasswordAdminProcessMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

            // 관리자 강제 초기화(현재 비밀번호 검증 없음). 초기 비밀번호는 설정에서 주입(하드코딩 제거).
            var updated = await _gateway.ResetAccountPasswordAsync(ViewModel.Model, _session.AdminResetPassword, cancellationToken);
            if (updated != null) { ViewModel.Insert(updated); CaptureBaseline(); }

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
            // 바뀐 칸만 보낸다(편집 전 기준과 견줌). 전체 모델을 실으면 ① 바꾸지 않은 role 이 딸려 가 users:edit 만 가진
            // 비-ADMIN 편집자는 부서 하나 고쳐도 403("Only ADMIN role can change role or group assignment") 이었고
            // ② 상태(is_active)는 아예 실리지 않아 "미사용" 이 서버에 반영되지 않았다(라이브 실측 2026-09-26).
            // 비운 칸은 서버가 null 로 비운다. DB 모드 게이트웨이는 기본구현이 행 전체 저장이라 결과가 같다.
            var changed = _baseline is null ? null : AccountDtoMapper.ChangedFields(_baseline, ViewModel.Model);
            if (changed is { Count: 0 })
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "계정 편집", Explain = NothingChangedText }, cancellationToken);
                await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel(), cancellationToken);
                return;
            }

            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

            var ret = changed is null
                ? await _gateway.UpdateAccountAsync(ViewModel.Model, cancellationToken)   // 기준 없음(열기 경로 밖) — 종전 전체 저장
                : await _gateway.UpdateAccountFieldsAsync(ViewModel.Model, changed, cancellationToken);
            if (ret == null)   // 저장 실패(null)인데 "완료"+닫힘으로 오인시키던 버그 — 실패 노출, 다이얼로그 유지
            {
                _log?.Warning("계정 정보 변경 실패 — 서버가 저장하지 못함(null)");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "계정 편집", Explain = "계정 정보 변경이 반영되지 않았습니다. 다시 시도해 주세요." }, cancellationToken);
                return;
            }
            ViewModel.Insert(ret);
            CaptureBaseline();

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
    #region - Processes -
    /// <summary>지금 <see cref="ViewModel"/> 값을 편집 전 기준으로 복사해 둔다(행 모델과 분리된 사본).</summary>
    private void CaptureBaseline()
    {
        var copy = new AccountModel();
        copy.Update(ViewModel.Model);
        _baseline = copy;
    }
    #endregion
    #region - Properties -
    /// <summary>[확인] 을 눌렀지만 바뀐 칸이 없을 때의 안내.</summary>
    public const string NothingChangedText = "변경된 내용이 없습니다.";

    /// <summary>관리자 초기화 기본 비밀번호 (설정 주입, 하드코딩 제거).</summary>
    public string ResetPassword => _session.AdminResetPassword;
    public AccountViewModel ViewModel { get; }
    #endregion
    #region - Attributes -
    private readonly IUserDirectoryGateway _gateway;
    private readonly ISessionConfigService _session;
    private readonly IProfileImageService _profileImage;
    private readonly IProfileGateway _profileGateway;
    /// <summary>편집 전 기준(<see cref="BeginEdit"/> · 활성화 · 저장 성공 때 갱신). null = 아직 못 잡음.</summary>
    private AccountModel? _baseline;
    #endregion
}
