using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
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
        _pendingPhotoPath = null;

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
        await SetPictureAsync(dlg.FileName);
    }

    /// <summary>
    /// 고른 파일을 등록할 사진으로 잡는다 — 파일 고르기(<see cref="ClickAddPicture"/>)와 분리해
    /// 헤드리스 시험 · 라이브 왕복 하네스가 창을 띄우지 않고 같은 경로를 탄다.
    /// </summary>
    public async Task SetPictureAsync(string filePath)
    {
        var ct = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            // 파일 검증(확장자/크기) + 복사를 서비스로 위임 (M-1)
            var key = $"{DateTime.Now:yyyyMMddHHmmssfff}";
            var saved = await _profileImage.SaveAsync(filePath, key, ct);
            ViewModel.Image = Path.GetFileName(saved);   // 미리보기(로컬 Profile 폴더) · DB 모드 저장값
            _pendingPhotoPath = saved;                    // 서버 모드: 계정이 생긴 뒤 이 파일을 올린다(ClickOk)
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

            // 고른 사진 — 서버 POST /users 는 파일을 받지 않고(photo_url 은 URL 만) 로컬 파일 이름은 실리지 않는다.
            // 종전에는 여기서 끝나 고른 사진이 조용히 버려졌다(라이브 실측 2026-09-24: 생성 계정 photo_url=default.png, 사진 호출 0건).
            // 계정이 생긴 뒤 관리자 사진 경로(POST /users/{id}/photo)로 올린다. DB 모드는 파일 이름을 그대로 저장하므로
            // (돌아온 모델이 이미 그 이름을 들고 있다) 올리지 않는다.
            var photoNote = await UploadPendingPhotoAsync(created, ct);
            AccountProvider.Add(created);

            await _eventAggregator!.PublishOnCurrentThreadAsync(new RefreshAccountsMessageModel());
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사용자 등록", Explain = "계정 등록을 성공하였습니다." + photoNote });
            await _eventAggregator!.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사용자 등록", Explain = "계정 등록이 실패하였습니다." });
        }
    }

    /// <summary>계정은 만들었지만 사진 업로드가 실패했을 때 완료 안내에 덧붙이는 한 줄.</summary>
    public const string PhotoNotAppliedNote = "\n(사진은 반영되지 않았습니다 — 계정 편집에서 다시 올려 주세요.)";

    /// <summary>등록할 사진이 있고 서버에 아직 없으면 올린다. 돌려주는 글은 완료 안내에 덧붙일 한 줄(성공이면 빈 글).</summary>
    private async Task<string> UploadPendingPhotoAsync(IAccountModel created, CancellationToken ct)
    {
        var path = _pendingPhotoPath;
        if (string.IsNullOrEmpty(path) || created.Id <= 0) return string.Empty;
        if (string.Equals(created.Image, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)) return string.Empty;   // DB 모드

        try
        {
            var url = await _gateway.UploadPhotoAsync(created.Id, path, ct);
            if (!string.IsNullOrEmpty(url))
            {
                created.Image = url;
                _pendingPhotoPath = null;
                return string.Empty;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.Error($"[RegisterDialog] 사진 업로드 실패: {ex.Message}");
        }
        return PhotoNotAppliedNote;
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
    private string? _pendingPhotoPath;
    private string? _username;
    private string _name = "";
    private string _pass = "";
    private string _passConfirm = "";
    private string? _phone;
    private bool _duplicate;
    #endregion
}
