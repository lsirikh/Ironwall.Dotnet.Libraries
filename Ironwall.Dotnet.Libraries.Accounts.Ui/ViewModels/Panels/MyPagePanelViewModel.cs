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
            var saved = await _profileImage.SaveAsync(dlg.FileName, key, ct);   // 검증(확장자/크기)+복사
            ViewModel.Image = Path.GetFileName(saved);
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
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
        try
        {
            var fetchAcc = await _gateway.GetProfileAsync(ViewModel.Model.Id, cancellationToken);
            if (fetchAcc == null) throw new Exception("변경 전 정보를 불러오는 과정에서 문제가 발생하였습니다.");
            ViewModel.Insert(fetchAcc);
            _log?.Info("초기화 성공");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "변경 전 정보를 정상적으로 불러왔습니다." }, cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = ex.Message }, cancellationToken);
        }
    }

    public async Task HandleAsync(CallEditProcessMessageModel message, CancellationToken cancellationToken)
    {
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
        try
        {
            var ret = await _gateway.UpdateProfileAsync(ViewModel.Model, cancellationToken);
            if (ret != null) ViewModel.Insert(ret);
            _log?.Info("사용자 정보 변경작업 성공");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "사용자 정보 변경이 정상적으로 완료되었습니다." }, cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Info($"변경작업 실패 : {ex.Message}");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Explain = "사용자 정보 변경이 실패하였습니다." }, cancellationToken);
        }
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
