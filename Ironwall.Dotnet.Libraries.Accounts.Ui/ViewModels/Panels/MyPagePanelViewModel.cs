using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Microsoft.Win32;
using System;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 마이페이지 패널 — 라이브러리 이관(B) + IProfileGateway + 사진 위임
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountDbService → IProfileGateway, 사진 하드코딩 경로 → IProfileImageService(검증),
                  async void→Task, ct 전달, Task.Delay 제거.
****************************************************************************/
public class MyPagePanelViewModel : BasePanelViewModel
                                  , IHandle<CallResetProcessMessageModel>
                                  , IHandle<CallEditProcessMessageModel>
                                  , IHandle<CallDeletePhotoProcessMessageModel>
{
    #region - Ctors -
    public MyPagePanelViewModel(IEventAggregator eventAggregator
                                , ILogService log
                                , LoginViewModel loginViewModel
                                , IProfileGateway gateway
                                , IProfileImageService profileImage)
                                : base(eventAggregator, log)
    {
        ViewModel = loginViewModel;
        _gateway = gateway;
        _profileImage = profileImage;
    }
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        if (ViewModel.Model == null) return;
        var vm = await _gateway.GetProfileAsync(ViewModel.Model.Id, cancellationToken);
        if (vm == null) return;
        ViewModel.Insert(vm);
    }
    #endregion
    #region - Binding Methods -
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
            var saved = await _profileImage.SaveAsync(dlg.FileName, key, ct);   // 검증(확장자/크기)+로컬 복사(즉시표시/DB모드)
            ViewModel.Image = Path.GetFileName(saved);

            // API 모드: 원본을 서버에 업로드 → photo_url(절대 URL) 받으면 그걸로 영속·표시(W1 해결). DB 모드는 null이라 로컬 파일명 유지.
            var url = await _gateway.UploadPhotoAsync(dlg.FileName, ct);
            if (!string.IsNullOrEmpty(url)) ViewModel.Image = url;
        }
        catch (ArgumentException ex)
        {
            _log?.Warning(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사진", Explain = Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers.ProfileImageHelper.RejectedText });
        }
    }

    /// <summary>사진 제거 클릭 → 확인 팝업(파괴적). '확인' 시 HandleAsync가 서버 삭제. (기존 UI-only=서버 미삭제·부활 버그 수정) — MyPage_SelfPhoto_Delete_Fix</summary>
    public async Task ClickClearPicture()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "사진 삭제",
            Explain = "프로필 사진을 삭제하시겠습니까?\n(서버에서 삭제되며 기본 아바타로 복귀합니다.)",
            MessageModel = new CallDeletePhotoProcessMessageModel()
        });

    public async Task ClickPasswordReset()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenResetPasswordDialogMessageModel());

    public async Task ClickResetAccount()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel { Explain = "사용자 정보를 초기화하시겠습니까?", MessageModel = new CallResetProcessMessageModel() });

    public async Task ClickApplyAccount()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel { Explain = "사용자 정보를 변경하시겠습니까?", MessageModel = new CallEditProcessMessageModel() });

    public async Task ClickDeleteAccount()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenDeleteAccountDialogMessageModel());

    public async Task ClickCancel()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());
    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CallResetProcessMessageModel message, CancellationToken cancellationToken)
    {
        // (MC-MP-1/INV-4) Confirm(ClickResetAccount)은 ClickOk이 self-close 안 함 → 진입 청산 + Progress는 finally 청산.
        await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        string explain;
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
            var fetchAcc = await _gateway.GetProfileAsync(ViewModel.Model.Id, cancellationToken);
            if (fetchAcc == null) throw new Exception("변경 전 정보를 불러오는 과정에서 문제가 발생하였습니다.");
            ViewModel.Insert(fetchAcc);
            _log?.Info("초기화 성공");
            explain = "변경 전 정보를 정상적으로 불러왔습니다.";
        }
        catch (OperationCanceledException)   // (INV-9) 취소/타임아웃 분리
        {
            _log?.Warning("초기화 취소");
            explain = "초기화가 취소되었습니다.";
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            explain = "내 정보를 다시 불러오지 못했습니다. 잠시 뒤 다시 시도하세요.";
        }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());   // Progress 항상 청산
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = explain });
    }

    public async Task HandleAsync(CallEditProcessMessageModel message, CancellationToken cancellationToken)
    {
        // (MC-MP-1/INV-4) Confirm(ClickApplyAccount)은 ClickOk이 self-close 안 함 → 진입 청산 + Progress는 finally 청산.
        await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        string explain;
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
            var requestedEmployeeNumber = ViewModel.Model.EmployeeNumber;   // 서버 에코와 견줘 "저장 안 된 사원번호" 를 알린다
            var ret = await _gateway.UpdateProfileAsync(ViewModel.Model, cancellationToken);
            if (ret == null)   // 서버 저장 실패(null)인데 "완료"로 오인 표시하던 버그 — 실패 노출 후 종료
            {
                _log?.Warning("사용자 정보 변경 실패 — 서버가 저장하지 못함(null)");
                explain = "사용자 정보 변경이 반영되지 않았습니다. 다시 시도해 주세요.";
            }
            else
            {
                ViewModel.Insert(ret);   // 서버 에코(저장된 실제 값)로 화면 갱신
                _log?.Info("사용자 정보 변경작업 성공");
                explain = "사용자 정보 변경이 정상적으로 완료되었습니다.";
                // 서버 본인 수정(PUT /users/me)은 사원번호를 받지 않는다 — 고친 값이 에코에 없으면 "완료" 만 말하지 않는다
                // (라이브 실측 2026-09-26: 칸을 고쳐도 서버 값 그대로인데 "정상적으로 완료").
                if (!string.Equals(NullIfBlank(requestedEmployeeNumber), NullIfBlank(ret.EmployeeNumber), StringComparison.Ordinal))
                    explain += EmployeeNumberNotSavedNote;
            }
        }
        catch (OperationCanceledException)   // (INV-9) 취소/타임아웃 분리
        {
            _log?.Warning("사용자 정보 변경 취소");
            explain = "변경이 취소되었습니다.";
        }
        catch (Exception ex)
        {
            _log?.Info($"변경작업 실패 : {ex.Message}");
            explain = "사용자 정보 변경이 실패하였습니다.";
        }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());   // Progress 항상 청산
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "내 정보", Explain = explain });
    }

    /// <summary>사진 삭제 확인('확인') → 본인 /me/photo 서버 삭제 → default 아바타. Confirm self-close 안 함 → 진입 시 ClosePopup 청산. — MyPage_SelfPhoto_Delete_Fix</summary>
    public async Task HandleAsync(CallDeletePhotoProcessMessageModel message, CancellationToken cancellationToken)
    {
        await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        string explain;
        try
        {
            var ok = await _gateway.DeletePhotoAsync(cancellationToken);
            if (ok)
            {
                ViewModel.Image = null;   // 서버 photo_url=null → default 아바타
                explain = "프로필 사진을 삭제했습니다.";
            }
            else
                explain = "사진 삭제가 반영되지 않았습니다. 다시 시도해 주세요.";
        }
        catch (Exception ex)
        {
            _log?.Error($"[MyPage] 사진 삭제 실패: {ex.Message}");
            explain = "사진 삭제 중 오류가 발생했습니다. 다시 시도해 주세요.";
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사진 삭제", Explain = explain }, cancellationToken);
    }
    #endregion
    #region - Properties -
    /// <summary>사원번호를 고쳤지만 서버가 반영하지 않았을 때 완료 안내에 덧붙이는 한 줄.</summary>
    public const string EmployeeNumberNotSavedNote = "\n(사원번호는 본인이 변경할 수 없어 저장되지 않았습니다 — 관리자에게 요청하세요.)";

    /// <summary>
    /// 사원번호 칸이 읽기 전용인가 — 서버 모드는 true(본인 수정 본문에 없음, 관리자 경로 전용). DB 모드는 편집 가능.
    /// </summary>
    public bool IsEmployeeNumberReadOnly => !_gateway.CanSelfEditEmployeeNumber;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public LoginViewModel ViewModel { get; }
    #endregion
    #region - Attributes -
    private readonly IProfileGateway _gateway;
    private readonly IProfileImageService _profileImage;
    #endregion
}
