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
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "이미지", Explain = ex.Message });
        }
    }

    public void ClickClearPicture() => ViewModel.Image = null;

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
            explain = ex.Message;
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
    #endregion
    #region - Properties -
    public LoginViewModel ViewModel { get; }
    #endregion
    #region - Attributes -
    private readonly IProfileGateway _gateway;
    private readonly IProfileImageService _profileImage;
    #endregion
}
