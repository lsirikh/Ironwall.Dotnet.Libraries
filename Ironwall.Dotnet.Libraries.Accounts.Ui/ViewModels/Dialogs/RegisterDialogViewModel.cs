using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Microsoft.Win32;
using System;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : 회원가입 다이얼로그 — 라이브러리 이관(B) + gateway 주입 + 결함 수정
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountDbService→IUserDirectoryGateway, 파일IO→IProfileImageService(검증 포함),
                  H-4 중복확인 debounce(취소토큰+최신값만 반영), async void→Task, ct 전달,
                  SetupModel(dead) 제거, Task.Delay 제거, 최소 8자 검증.
****************************************************************************/
public class RegisterDialogViewModel : BasePanelViewModel
{
    #region - Ctors -
    public RegisterDialogViewModel(IEventAggregator eventAggregator
                                , ILogService log
                                , RegisterViewModel registerViewModel
                                , AccountProvider accountProvider
                                , IUserDirectoryGateway gateway
                                , IProfileImageService profileImage)
                                : base(eventAggregator, log)
    {
        ViewModel = registerViewModel;
        AccountProvider = accountProvider;
        _gateway = gateway;
        _profileImage = profileImage;
    }
    #endregion
    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Username = string.Empty;
        PasswordConfirm = string.Empty;
        RegisterPass = string.Empty;
        Name = string.Empty;
        Phone = string.Empty;
        _duplicate = false;

        ViewModel.Clear();
        ViewModel.Level = EnumLevelType.USER;
        ViewModel.Used = EnumUsedType.USED;

        return base.OnActivateAsync(cancellationToken);
    }
    #endregion
    #region - Processes -
    // H-4: 중복확인 debounce — 이전 검사 취소, 최신 입력만 반영
    private async Task CheckDuplicateAsync(string? id)
    {
        _dupCts?.Cancel();
        var cts = _dupCts = new CancellationTokenSource();
        var token = cts.Token;

        if (string.IsNullOrWhiteSpace(id))
        {
            _duplicate = false;
            NotifyOfPropertyChange(nameof(CanClickOk));
            return;
        }

        bool taken;
        try { taken = await _gateway.IsUsernameTakenAsync(id, token); }
        catch (OperationCanceledException) { return; }

        if (token.IsCancellationRequested) return;   // 더 최신 입력이 들어옴 → 무시

        _duplicate = taken;
        NotifyOfPropertyChange(nameof(CanClickOk));

        string msg = taken ? $"{id} 아이디가 존재합니다." : "사용 가능한 아이디입니다.";
        if (taken) Username = string.Empty;
        _log?.Info(msg);
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "아이디 확인", Explain = msg });
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
            // 파일 검증(확장자/크기) + 복사를 서비스로 위임 (M-1)
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

    public async Task ClickCancel()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());

    public async Task ClickOk()
    {
        var ct = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel());

            // 첫 등록자는 ADMIN, 그 외 USER
            if (AccountProvider.Count() == 0)
                ViewModel.Level = EnumLevelType.ADMIN;

            var created = await _gateway.CreateAccountAsync(ViewModel.Model, ct);
            if (created == null) throw new Exception("계정 등록에 실패했습니다.");
            AccountProvider.Add(created);

            await _eventAggregator!.PublishOnCurrentThreadAsync(new RefreshAccountsMessageModel());
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사용자 등록", Explain = "계정 등록을 성공하였습니다." });
            await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사용자 등록", Explain = "계정 등록이 실패하였습니다." });
        }
    }
    #endregion
    #region - Properties -
    public string? Username
    {
        get => _username;
        set
        {
            if (Set(ref _username, value?.ToLowerInvariant()))
            {
                ViewModel.Username = _username ?? string.Empty;   // 모델 동기화
                _ = CheckDuplicateAsync(_username);               // 내부 debounce로 H-4 해소
            }
        }
    }

    public string Name
    {
        get => _name;
        set { if (Set(ref _name, value)) { ViewModel.Name = _name; NotifyOfPropertyChange(nameof(CanClickOk)); } }
    }

    public string RegisterPass
    {
        get => _pass;
        set
        {
            _pass = value;
            ViewModel.Password = _pass;
            NotifyOfPropertyChange(nameof(RegisterPass));
            NotifyOfPropertyChange(nameof(PasswordConfirm));
            NotifyOfPropertyChange(nameof(CanClickOk));
        }
    }

    public string PasswordConfirm
    {
        get => _passConfirm;
        set
        {
            _passConfirm = value;
            NotifyOfPropertyChange(nameof(RegisterPass));
            NotifyOfPropertyChange(nameof(PasswordConfirm));
            NotifyOfPropertyChange(nameof(CanClickOk));
        }
    }

    public string? Phone
    {
        get => _phone;
        set { if (Set(ref _phone, value)) { ViewModel.Phone = _phone; } }
    }

    public bool CanClickOk =>
        !string.IsNullOrWhiteSpace(Username) &&
        !_duplicate &&
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(RegisterPass) &&
        RegisterPass.Length >= 8 &&
        RegisterPass == PasswordConfirm;

    public RegisterViewModel ViewModel { get; }
    public AccountProvider AccountProvider { get; }
    #endregion
    #region - Attributes -
    private readonly IUserDirectoryGateway _gateway;
    private readonly IProfileImageService _profileImage;
    private CancellationTokenSource? _dupCts;
    private string? _username;
    private string _name = "";
    private string _pass = "";
    private string _passConfirm = "";
    private string? _phone;
    private bool _duplicate;
    #endregion
}
