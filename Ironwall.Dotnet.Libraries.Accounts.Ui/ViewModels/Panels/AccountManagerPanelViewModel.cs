using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 계정관리 패널(DataGrid) — 라이브러리 이관(B) + IUserDirectoryGateway
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountDbService → IUserDirectoryGateway, OnClickSaveButton NotImplemented 제거(no-op),
                  DataInitialize 취소토큰 미발사 버그(new TaskCanceledException) → ThrowIfCancellationRequested,
                  잘못된 Mysqlx.Cursor import 제거. OnClick* 핸들러는 base void override라 async void 유지(이벤트 핸들러).
****************************************************************************/
public class AccountManagerPanelViewModel : BaseDataGridPanelViewModel<AccountViewModel>
                                          , IHandle<CallDeleteAccountAdminProcessMessageModel>
                                          , IHandle<CallUnlockAccountMessageModel>
                                          , IHandle<RefreshAccountsMessageModel>
{
    #region - Ctors -
    public AccountManagerPanelViewModel(IEventAggregator eventAggregator
                                    , ILogService log
                                    , EditorDialogViewModel editorDialogViewModel
                                    , AccountProvider accountProvider
                                    , LoginViewModel loginViewModel
                                    , IUserDirectoryGateway gateway)
                                    : base(eventAggregator, log)
    {
        ViewModel = loginViewModel;
        _editorDialogViewModel = editorDialogViewModel;
        _accountProvider = accountProvider;
        _gateway = gateway;
    }

    /// <summary>권한 서비스 optional 해석(Events/GMaps 패턴) — 미등록/일시실패 시 재시도 허용(영구 null캐시 방지). FR-05.</summary>
    private IPermissionService? _permissionService;
    private bool _permissionResolved;
    private IPermissionService? ResolvePermissionService()
    {
        if (_permissionResolved) return _permissionService;
        try
        {
            _permissionService = IoC.Get<IPermissionService>();
            _permissionResolved = _permissionService != null;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[AccountManager] PermissionService 미해석(전체허용 폴백): {ex.Message}");
            _permissionService = null;
        }
        return _permissionService;
    }
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        // 최초 진입 시에도 서버에서 계정을 로드한다. 기존엔 _accountProvider(인메모리)에서 복사만 해
        // '갱신' 버튼을 눌러야만 fetch 돼 첫 화면이 비어 보였음.
        // (MC-AM-4/INV-9) 활성화 중 서버 예외가 Caliburn 활성화 파이프라인으로 전파되지 않도록 포획.
        try
        {
            var fetched = await _gateway.GetAllAccountsAsync(cancellationToken).ConfigureAwait(false);
            if (fetched != null)
            {
                _accountProvider.Clear();
                fetched.OrderByDescending(a => a.Role == EnumUserRole.ADMIN).ThenBy(a => a.Id).ToList().ForEach(acc => _accountProvider.Add(acc)); // ADMIN 최상단 + 생성순(Id=SERIAL=created_at순)
            }
            else   // W5: fetch 실패 시 빈 화면 + 무안내 방지
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "계정 관리", Explain = "계정 목록을 불러오지 못했습니다. 서버 연결을 확인하세요." });
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[AccountManager] 활성화 로드 실패: {ex.Message}");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "계정 관리", Explain = "계정 목록을 불러오지 못했습니다. 서버 연결을 확인하세요." });
        }
        await DataInitialize(cancellationToken).ConfigureAwait(false);
    }

    protected override Task SelectAll(bool isSelected)
    {
        return Task.Run((System.Action)(async () =>
        {
            try
            {
                foreach (var item in ViewModelProvider)
                {
                    if (item.Level != EnumLevelType.ADMIN)
                        item.IsSelected = isSelected;
                }
                await CheckSelectState();
            }
            catch (Exception ex) { _log?.Error(ex.Message); }
        }));
    }

    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                IsVisible = false;
                await Task.Delay(500, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var item in _accountProvider)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ViewModelProvider.Add(new AccountViewModel(_eventAggregator!, _log!, item));
                    }
                    NotifyOfPropertyChange(() => ViewModelProvider);
                });

                IsCheckedCheckBoxColumnHeader = false;
                await SelectAll(false);
                IsVisible = true;
            }
            catch (OperationCanceledException ex)   // TaskCanceledException 포함
            {
                _log?.Error($"Cancelled(DataInitialize) : {ex.Message}");
            }
        });
    }
    #endregion
    #region - Binding Methods -
    public override async void OnClickInsertButton(object sender, RoutedEventArgs e)
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenRegisterDialogMessageModel());

    public override async void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        // SelectedItemCount(캐시)는 OnClickCheckBoxItem→CheckSelectState 가 갱신하는데 미발화 시 0으로 남아
        // 삭제가 조용히 무시됨. 체크 상태(IsSelected)에서 직접 집계해 방어.
        var count = ViewModelProvider.Count(x => x.IsSelected);
        if (count == 0)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "사용자 삭제", Explain = "삭제할 계정을 체크박스로 선택하세요." });
            return;
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = $"선택한 {count}개 계정을 삭제하시겠습니까?",
            MessageModel = new CallDeleteAccountAdminProcessMessageModel()
        });
    }

    public override void OnClickSaveButton(object sender, RoutedEventArgs e)
    {
        // AccountManager는 인라인 저장 미사용 — 편집은 EditorDialog로 처리 (NotImplementedException 제거)
        _log?.Info("AccountManager: 인라인 저장 미사용");
    }

    public override async void OnClickReloadButton(object sender, RoutedEventArgs e)
    {
        // (MC-AM-4/INV-9) async void — 예외가 escape하면 앱 크래시. GetAllAccountsAsync는 null이 아니라 throw할 수 있음(서버 다운/타임아웃).
        try
        {
            var fetched = await _gateway.GetAllAccountsAsync();
            if (fetched == null)   // W4: 서버 실패 시 기존 목록 보존 + 안내(무조건 Clear 금지)
            {
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "갱신", Explain = "계정 목록을 불러오지 못했습니다. 기존 목록을 유지합니다." });
                return;
            }
            _accountProvider.Clear();
            fetched.OrderByDescending(a => a.Role == EnumUserRole.ADMIN).ThenBy(a => a.Id).ToList().ForEach(acc => _accountProvider.Add(acc)); // ADMIN 최상단 + 생성순(Id=SERIAL=created_at순)
            await DataInitialize().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Error($"[AccountManager] 갱신 실패: {ex.Message}");
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "갱신", Explain = "계정 목록 갱신 중 오류가 발생했습니다. 기존 목록을 유지합니다." });
        }
    }

    public async Task ClickCancel()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());

    public async void OnClickAccountDetail(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null) return;
        _editorDialogViewModel.ViewModel.Insert(SelectedItem.Model);
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenEditAccountDialogMessageModel());
    }

    /// <summary>계정 잠금 해제 클릭 — 즉시 실행 않고 Confirm. Yes 시 CallUnlockAccountMessageModel 발행(FR-02).</summary>
    public async Task OnClickUnlock(AccountViewModel account)
    {
        if (account is null || !account.IsLocked) return;
        // FR-05 백스톱: users:control 없으면 서버 호출 전 안내(콘솔은 IsAdmin 진입이 1차 게이팅, 서버가 최종 403 집행).
        //   ADMIN 대상 계정 해제는 base-ADMIN 전용(NOTIFY §4) — 비ADMIN이면 서버가 403, 아래 HandleAsync가 안내.
        if (!PermissionUiPolicy.Allowed(ResolvePermissionService(), "users", EnumPermissionVerb.Control))
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "계정 잠금 해제", Explain = "계정 잠금 해제 권한이 없습니다." });
            return;
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "계정 잠금 해제",
            Explain = $"'{account.Username}' 계정의 잠금을 해제하시겠습니까?",
            MessageModel = new CallUnlockAccountMessageModel { Account = account }
        });
    }
    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CallDeleteAccountAdminProcessMessageModel message, CancellationToken cancellationToken)
    {
        // (MC-AM-1/INV-4) Confirm 팝업의 ClickOk은 MessageModel만 발행하고 self-close 안 함(시블링 PM:227/Grant:108 동형).
        //   → 진입 시 Confirm 청산 + Progress는 예외/취소 무관 finally에서 청산. 기존엔 어떤 경로도 Close 없어 팝업 소프트락(HIGH-1).
        await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        string explain;
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

            // 결과(bool)를 확인해 실패를 사용자에게 노출(기존엔 무시해 실패해도 "성공" 표시).
            int selected = 0, deleted = 0;
            var failed = new System.Collections.Generic.List<string>();
            foreach (var item in ViewModelProvider)
            {
                if (!item.IsSelected) continue;
                selected++;
                var ok = await _gateway.RemoveAccountAsync(item.Model, string.Empty, cancellationToken);  // 관리자 일괄삭제(비번검증 없음)
                if (ok) deleted++; else failed.Add(item.Model.Username);
            }

            var fetched = await _gateway.GetAllAccountsAsync(cancellationToken);
            if (fetched != null)   // W4: 실패 시 기존 목록 보존
            {
                _accountProvider.Clear();
                fetched.OrderByDescending(a => a.Role == EnumUserRole.ADMIN).ThenBy(a => a.Id).ToList().ForEach(acc => _accountProvider.Add(acc)); // ADMIN 최상단 + 생성순(Id=SERIAL=created_at순)
            }

            await DataInitialize(cancellationToken).ConfigureAwait(false);

            explain = selected == 0 ? "선택된 계정이 없습니다."
                    : failed.Count == 0 ? $"{deleted}개 계정을 삭제했습니다."
                    : $"{deleted}/{selected}개 삭제. 실패: {string.Join(", ", failed)} (서버 거부/오류)";
        }
        catch (OperationCanceledException)   // (INV-9) 취소/타임아웃은 오류와 분리
        {
            _log?.Warning("계정 삭제 취소/타임아웃");
            explain = "계정 삭제가 취소되었습니다.";
        }
        catch (Exception ex)
        {
            _log?.Info(ex.Message);
            explain = "계정 삭제를 실패하였습니다.";
        }
        finally
        {
            // (MC-AM-1/INV-4) Progress는 예외/취소 무관 항상 청산 — 스피너 영구 잔존 차단.
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사용자 삭제", Explain = explain });
    }

    /// <summary>Confirm→Yes 후 실제 잠금 해제 — POST /users/{id}/unlock + 목록 갱신. 팝업은 finally 청산(FR-02).</summary>
    public async Task HandleAsync(CallUnlockAccountMessageModel message, CancellationToken cancellationToken)
    {
        var account = message.Account;
        if (account is null) return;
        // (INV-4) Confirm ClickOk은 self-close 안 함 → 진입 청산 + Progress는 finally 청산(삭제 핸들러 동형).
        await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        string explain;
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
            var ok = await _gateway.UnlockAccountAsync(account.Model.Id, cancellationToken);
            if (ok)
            {
                // 목록 재조회로 IsLocked 갱신 반영(NFR-03)
                var fetched = await _gateway.GetAllAccountsAsync(cancellationToken);
                if (fetched != null)
                {
                    _accountProvider.Clear();
                    fetched.OrderByDescending(a => a.Role == EnumUserRole.ADMIN).ThenBy(a => a.Id).ToList().ForEach(acc => _accountProvider.Add(acc));
                    await DataInitialize(cancellationToken).ConfigureAwait(false);
                }
                explain = $"'{account.Username}' 계정의 잠금을 해제했습니다.";
            }
            else   // 403(권한/ADMIN 대상) 또는 네트워크 실패 — 서버가 최종 집행
                explain = "잠금 해제에 실패했습니다. 권한(ADMIN)·서버 상태를 확인하세요.";
        }
        catch (OperationCanceledException) { _log?.Warning("잠금 해제 취소/타임아웃"); explain = "잠금 해제가 취소되었습니다."; }
        catch (Exception ex) { _log?.Error($"[AccountManager] 잠금 해제 실패: {ex.Message}"); explain = "잠금 해제 중 오류가 발생했습니다."; }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());   // Progress 항상 청산
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "계정 잠금 해제", Explain = explain });
    }

    public async Task HandleAsync(RefreshAccountsMessageModel message, CancellationToken cancellationToken)
    {
        // 편집/사진 변경은 EditorDialog 의 '복사본' Model 에 적용되므로, 인메모리 provider 재구성만으론
        //   목록·상세에 반영되지 않았다(기존 버그: 사진 업로드/삭제·정보수정 후 갱신 버튼 전까지 stale).
        //   → 서버에서 재조회(SSOT)해 목록을 갱신한다.
        try
        {
            var fetched = await _gateway.GetAllAccountsAsync(cancellationToken);
            if (fetched != null)
            {
                _accountProvider.Clear();
                fetched.OrderByDescending(a => a.Role == EnumUserRole.ADMIN).ThenBy(a => a.Id).ToList().ForEach(acc => _accountProvider.Add(acc)); // ADMIN 최상단 + 생성순
            }
        }
        catch (Exception ex) { _log?.Error($"[AccountManager] 새로고침 재조회 실패: {ex.Message}"); }
        await DataInitialize(cancellationToken).ConfigureAwait(false);
    }
    #endregion
    #region - Properties -
    public LoginViewModel ViewModel { get; }
    #endregion
    #region - Attributes -
    private readonly EditorDialogViewModel _editorDialogViewModel;
    private readonly AccountProvider _accountProvider;
    private readonly IUserDirectoryGateway _gateway;
    #endregion
}

/// <summary>계정 잠금 해제 확인 트리거 — Confirm 다이얼로그 Yes 시 발행되어 HandleAsync가 실제 unlock 수행(FR-02).</summary>
public class CallUnlockAccountMessageModel : IMessageModel
{
    public AccountViewModel Account { get; set; } = default!;
}
