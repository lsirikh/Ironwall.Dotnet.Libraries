using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
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
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        // 최초 진입 시에도 서버에서 계정을 로드한다. 기존엔 _accountProvider(인메모리)에서 복사만 해
        // '갱신' 버튼을 눌러야만 fetch 돼 첫 화면이 비어 보였음.
        var fetched = await _gateway.GetAllAccountsAsync(cancellationToken).ConfigureAwait(false);
        if (fetched != null)
        {
            _accountProvider.Clear();
            fetched.ForEach(acc => _accountProvider.Add(acc));
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
        var fetched = await _gateway.GetAllAccountsAsync();
        _accountProvider.Clear();
        fetched?.ForEach(acc => _accountProvider.Add(acc));
        await DataInitialize().ConfigureAwait(false);
    }

    public async Task ClickCancel()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());

    public async void OnClickAccountDetail(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null) return;
        _editorDialogViewModel.ViewModel.Insert(SelectedItem.Model);
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenEditAccountDialogMessageModel());
    }
    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CallDeleteAccountAdminProcessMessageModel message, CancellationToken cancellationToken)
    {
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
            _accountProvider.Clear();
            fetched?.ForEach(acc => _accountProvider.Add(acc));

            await DataInitialize(cancellationToken).ConfigureAwait(false);

            var explain = selected == 0 ? "선택된 계정이 없습니다."
                        : failed.Count == 0 ? $"{deleted}개 계정을 삭제했습니다."
                        : $"{deleted}/{selected}개 삭제. 실패: {string.Join(", ", failed)} (서버 거부/오류)";
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사용자 삭제", Explain = explain });
        }
        catch (Exception ex)
        {
            _log?.Info(ex.Message);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "사용자 삭제", Explain = "계정 삭제를 실패하였습니다." });
        }
    }

    public async Task HandleAsync(RefreshAccountsMessageModel message, CancellationToken cancellationToken)
        => await DataInitialize(cancellationToken).ConfigureAwait(false);
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
