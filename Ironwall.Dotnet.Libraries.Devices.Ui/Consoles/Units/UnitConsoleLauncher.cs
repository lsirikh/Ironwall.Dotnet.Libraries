using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔 창 입구 (N-11 FR-01 · 부대 관계도 FR-43 · FR-46)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>호스트(메뉴·셸)가 부대 콘솔을 여는 유일한 입구. 호스트는 이 인터페이스만 안다.</summary>
public interface IUnitConsoleLauncher
{
    /// <summary>서버 8.0 이상에서만 <c>true</c> — 6.3·7.0 에서는 메뉴에 내지 않는다.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 창을 연다 — 이미 열려 있으면 새로 만들지 않고 그 창을 앞으로 가져온다(최소화면 복원).
    /// <b>비모달</b>이라 창이 닫히기 전에 돌아온다(첫 적재가 끝나면 돌아온다) — 호출부는 "돌아왔다 = 닫혔다" 로 읽지 않는다.
    /// </summary>
    Task OpenAsync();
}

/// <summary>
/// 창 관리자로 라이브러리가 직접 콘솔 창을 연다 — 호스트의 메시지 계약을 늘리지 않는다(선례: <c>AssemblyLauncher</c>).
/// </summary>
/// <remarks>
/// <para><b>비모달 · 한 벌</b>(FR-43): <c>ShowWindowAsync</c> 로 띄우고 소유자는 셸이다 — 셸 위에 뜨지만 셸을 막지 않는다(<c>Topmost</c> 아님).
/// 열린 뷰모델을 쥐고 있다가 다시 열면 그 창을 앞으로 가져오고, 창이 닫히면(닫힘 비활성화) 놓는다 — 다음 열기는 새로 만든다.
/// 치수는 T1 규약 1280×760.</para>
/// <para><b>지도에서 열기</b>(FR-46 · ISSUE-43 · 44): <see cref="OpenUnitConsoleRequest"/> 를 받아 창을 열거나 앞으로 가져오고,
/// <b>첫 적재가 끝난 뒤</b> 콘솔의 <c>TryRevealAsync</c> 로 그 부대를 고른다. 결과는 언제나 <see cref="OpenUnitConsoleResult"/> 1건으로 회신한다
/// — 상세에 적용하지 않은 변경이 있어 막혀도, 서버가 8.0 미만이어도, 편제에 없는 부대여도 말없이 끝나지 않는다.
/// 이 구독은 호스트가 런처를 해석하지 않아도 살아 있어야 해서 DI 에서 <c>AutoActivate</c> 한다(V-13).</para>
/// <para><b>로그아웃</b>(FR-43 · ISSUE-45): 셸과 같은 신호 <see cref="ISessionLifecycle.ForceLogoutRequested"/> 를 듣는다.
/// 메뉴 로그아웃(<see cref="EnumRevokeReason.Manual"/>)은 콘솔의 닫기 가드(<c>CanCloseAsync</c> — 7a5dd396)를 지나고,
/// 강제(세션 만료 · 폐기 · 인증 실패 · 다른 곳 로그인)는 묻지 않고 닫는다 — 셸의 <c>ForceActivateItemAsync</c>(호스트 40727ec)와 같은 규칙.
/// 가드에서 "닫지 않음" 을 고른 창은 다음 <see cref="ISessionLifecycle.LoginSucceeded"/>(다른 계정일 수 있다)에서 묻지 않고 닫는다.</para>
/// <para><b>스레드</b>: 세션 신호는 작업 스레드에서 온다 — UI 스레드로 옮겨 처리한다. 요청은 지도(UI)에서 오므로 그 자리에서 처리하고 회신한다.</para>
/// </remarks>
public sealed class UnitConsoleLauncher : IUnitConsoleLauncher, IHandle<OpenUnitConsoleRequest>, IDisposable
{
    private const double WIDTH = 1280;
    private const double HEIGHT = 760;

    private readonly IWindowManager _windows;
    private readonly IUnitGraphApi _units;
    private readonly IUnitDeviceApi _devices;
    private readonly INatsSetupModel? _nats;
    private readonly ILogService? _log;
    private readonly IEventAggregator? _events;
    private readonly IUnitLayoutApi? _layout;
    private readonly ISessionLifecycle? _session;
    private readonly IUnitConsoleWindowHost _host;
    private readonly Func<Func<Task>, Task> _onUi;
    private readonly Func<(ConsolePrefEntry Entry, System.Action Save)?> _prefs;
    private readonly IGuardedWindowRegistry? _guardedWindows;
    private readonly Func<bool> _isLiveOff;
    private readonly Func<string?>? _operatorName;

    /// <summary>지금 떠 있는 콘솔(한 벌). 닫히면 <c>null</c>.</summary>
    private UnitConsoleViewModel? _open;

    /// <summary><see cref="_open"/> 을 띄우는 중(첫 적재 포함) — 끝나기 전에 온 요청은 이것을 기다린 뒤 고른다.</summary>
    private Task? _opening;

    /// <summary>메뉴 로그아웃의 가드에서 "닫지 않음" 을 고른 콘솔 — 다음 로그인에서 묻지 않고 닫는다.</summary>
    private UnitConsoleViewModel? _keptAfterLogout;

    /// <summary><see cref="_open"/> 을 셸 종료 관문에 올린 것(U-27). 창이 닫히면 내린다.</summary>
    private IDisposable? _exitRegistration;

    private bool _disposed;

    /// <param name="events">있으면 콘솔이 떠 있는 동안 <c>UnitTopologyChangedMessage</c>(서버 <c>SYNC_UNIT</c>)를 듣고, 런처는 지도의 <see cref="OpenUnitConsoleRequest"/> 를 듣는다.</param>
    /// <param name="layout">관계도 배치 문서 통로(S-1). 없으면 관계도가 세션 전용으로 동작한다.</param>
    /// <param name="session">로그아웃 · 로그인 신호. 없으면(DB 모드) 로그아웃 닫기를 하지 않는다.</param>
    /// <param name="host">창 다루기(셸 소유자 · 앞으로 가져오기). 기본은 WPF.</param>
    /// <param name="onUi">세션 신호를 UI 스레드로 옮기는 곳(시험용). 기본은 WPF 디스패처.</param>
    /// <param name="prefs">개인 표시 설정(마지막 레일 · 관계도 보기). 기본은 <c>%LocalAppData%\Ironwall\console-prefs.json</c> 의 <c>Units</c> 키. <c>null</c> 을 돌려주면 쓰지 않는다.</param>
    /// <param name="guardedWindows">셸 종료 관문(U-27). 있으면 떠 있는 동안 콘솔을 올려 두어 셸이 끝나기 전에 닫기 가드를 묻게 한다. 없으면 올리지 않는다.</param>
    public UnitConsoleLauncher(
        IWindowManager windows,
        IUnitGraphApi units,
        IUnitDeviceApi devices,
        INatsSetupModel? nats = null,
        ILogService? log = null,
        IEventAggregator? events = null,
        IUnitLayoutApi? layout = null,
        ISessionLifecycle? session = null,
        IUnitConsoleWindowHost? host = null,
        Func<Func<Task>, Task>? onUi = null,
        Func<(ConsolePrefEntry Entry, System.Action Save)?>? prefs = null,
        IGuardedWindowRegistry? guardedWindows = null,
        Func<bool>? isLiveOff = null,
        Func<string?>? operatorName = null)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _nats = nats;
        _log = log;
        _events = events;
        _layout = layout;
        _session = session;
        _host = host ?? new WpfUnitConsoleWindowHost();
        _onUi = onUi ?? RunOnUi;
        _prefs = prefs ?? DiskPrefs;
        _guardedWindows = guardedWindows;
        // 관계도 배치 문구의 " · 실시간 반영 꺼짐" 꼬리표(ISSUE-32 · REVIEW-01 MEDIUM-6). 라이브러리에는 NATS 연결 상태를 공개하는 서비스가 없다
        // (MessageService.Connection 은 protected) — 호스트가 주지 않으면 알림 통로(NATS 설정 · 이벤트 버스)가 아예 없을 때만 꺼짐으로 본다.
        _isLiveOff = isLiveOff ?? (() => _nats is null || _events is null);
        _operatorName = operatorName;

        // 지도(UI 스레드)가 보낸 그 자리에서 처리한다 — 창 조작과 회신이 같은 스레드에 있다.
        _events?.SubscribeOnPublishedThread(this);
        if (_session is not null)
        {
            _session.ForceLogoutRequested += OnForceLogoutRequested;
            _session.LoginSucceeded += OnLoginSucceeded;
        }
    }

    public bool IsAvailable => _units.IsAvailable;

    /// <summary>마지막 세션 신호 처리(시험이 기다린다).</summary>
    internal Task SessionWork { get; private set; } = Task.CompletedTask;

    #region - Open -
    public async Task OpenAsync() => await OpenOrFocusAsync().ConfigureAwait(true);

    /// <summary>열거나 앞으로 가져오고, 첫 적재가 끝난 콘솔을 돌려준다. 열 수 없으면(8.0 미만 · 그 사이 닫힘) <c>null</c>.</summary>
    private async Task<UnitConsoleViewModel?> OpenOrFocusAsync()
    {
        if (!IsAvailable)
        {
            _log?.Warning("[UnitConsole] 서버 판본이 8.0 미만이라 부대 콘솔을 열지 않습니다.");
            return null;
        }

        if (_open is { } existing)
        {
            _host.BringToFront(existing);          // 최소화면 복원 + 활성화(ShowInTaskbar=false 라 작업 표시줄로는 되찾을 수 없다)
            if (_opening is { } loading) await loading.ConfigureAwait(true);   // 첫 적재 전에 온 두 번째 요청 — 적재 뒤에 고른다
            return ReferenceEquals(_open, existing) ? existing : null;
        }

        var viewModel = CreateConsole();
        _open = viewModel;
        viewModel.Deactivated += OnConsoleDeactivated;
        // 셸이 끝나기 전에 이 창에 묻게 올린다(U-27 · V-17: 셸이 닫히면 WPF 는 소유 창을 Closing 없이 없애 가드가 불리지 않는다).
        _exitRegistration = _guardedWindows?.Register(new ShellExitEntry(this, viewModel));

        // 진짜 창 관리자는 뷰모델을 활성화(= 첫 편제 적재)한 뒤 창을 보인다 — 이 await 가 끝나면 첫 적재가 끝났다.
        var showing = _windows.ShowWindowAsync(viewModel, null, Settings(_host.ShellOwner()));
        _opening = showing;
        try
        {
            await showing.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] 부대 콘솔 창을 열지 못했습니다: {ex.Message}");
            Forget(viewModel);
            throw;
        }
        finally
        {
            if (ReferenceEquals(_opening, showing)) _opening = null;
        }
        return ReferenceEquals(_open, viewModel) ? viewModel : null;
    }

    private UnitConsoleViewModel CreateConsole()
    {
        var prefs = SafePrefs();
        // GroupNats 가 '내 부대 코드' 의 유일한 출처다 — 트리에서 그 부대를 강조하는 데만 쓴다.
        var viewModel = new UnitConsoleViewModel(_units, _devices, _log, () => _nats?.GroupNats, events: _events,
                                                 layoutApi: _layout, prefs: prefs?.Entry, savePrefs: prefs?.Save,
                                                 isLiveOff: _isLiveOff, operatorName: _operatorName);
        // 삭제 · 운용 중지 · 닫기 전에 묻는 창 — 조립기와 같은 확인 창(작은 모달, 부대 창을 소유자로).
        viewModel.Confirm = (title, message) => ConfirmAsync(viewModel, title, message);
        return viewModel;
    }

    /// <summary>창이 닫혔다(✕ · 로그아웃 닫기 모두 닫힘 비활성화로 온다) — 인스턴스를 놓는다. 다음 열기는 새로 만든다.</summary>
    private Task OnConsoleDeactivated(object? sender, DeactivationEventArgs e)
    {
        if (e.WasClosed && sender is UnitConsoleViewModel viewModel) Forget(viewModel);
        return Task.CompletedTask;
    }

    private void Forget(UnitConsoleViewModel viewModel)
    {
        viewModel.Deactivated -= OnConsoleDeactivated;
        if (ReferenceEquals(_open, viewModel))
        {
            _open = null;
            _exitRegistration?.Dispose();
            _exitRegistration = null;
        }
        if (ReferenceEquals(_keptAfterLogout, viewModel)) _keptAfterLogout = null;
    }
    #endregion

    #region - Map request (FR-46) -
    /// <summary>지도 [관계도에서 보기] — 열거나 앞으로 가져오고, 첫 적재 뒤 그 부대를 고르고, 결과를 회신한다(말없는 실패 0).</summary>
    public async Task HandleAsync(OpenUnitConsoleRequest message, CancellationToken cancellationToken)
    {
        if (message is null || _disposed) return;

        var outcome = OpenUnitConsoleOutcome.Unavailable;
        try
        {
            if (!IsAvailable)
            {
                _log?.Warning($"[UnitConsole] 서버 판본이 8.0 미만이라 부대 #{message.UnitId} 을(를) 보여 주지 않습니다.");
            }
            else if (await OpenOrFocusAsync().ConfigureAwait(true) is { } console)
            {
                outcome = await console.TryRevealAsync(message.UnitId, message.OpenMap, cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) { outcome = OpenUnitConsoleOutcome.Unavailable; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] 부대 #{message.UnitId} 보여 주기 실패: {ex.Message}");
            outcome = OpenUnitConsoleOutcome.Unavailable;
        }

        if (_events is null) return;
        try { await _events.PublishOnCurrentThreadAsync(new OpenUnitConsoleResult(message.UnitId, outcome), cancellationToken).ConfigureAwait(true); }
        catch (Exception ex) { _log?.Warning($"[UnitConsole] 회신 발행 실패: {ex.Message}"); }
    }
    #endregion

    #region - Logout (FR-43) -
    private void OnForceLogoutRequested(EnumRevokeReason reason)
        => SessionWork = Marshal(() => CloseForLogoutAsync(reason));

    private void OnLoginSucceeded()
        => SessionWork = Marshal(CloseKeptAfterLogoutAsync);

    private async Task CloseForLogoutAsync(EnumRevokeReason reason)
    {
        if (_open is not { } console) return;

        if (reason == EnumRevokeReason.Manual)
        {
            // 사람이 고른 로그아웃 — 닫기 가드를 지난다(적용하지 않은 변경 · 배치 대기가 있으면 묻는다).
            if (!await console.CanCloseAsync().ConfigureAwait(true))
            {
                _keptAfterLogout = console;
                _log?.Info("[UnitConsole] 로그아웃 — 사용자가 부대 콘솔을 닫지 않기로 했습니다(다음 로그인에서 닫습니다).");
                return;
            }
        }
        // 강제(세션 만료 · 폐기 · 인증 실패 · 다른 곳 로그인)는 묻지 않는다 — 세션이 이미 끝났다. 미적용 편집 · 배치 대기는 버린다.
        await CloseAsync(console).ConfigureAwait(true);
    }

    private async Task CloseKeptAfterLogoutAsync()
    {
        if (_keptAfterLogout is not { } kept) return;
        _keptAfterLogout = null;
        if (ReferenceEquals(_open, kept)) await CloseAsync(kept).ConfigureAwait(true);   // 새 로그인(다른 계정일 수 있다) — 묻지 않는다
    }

    /// <summary>
    /// 가드를 다시 묻지 않고 닫는다 — 닫힘 비활성화를 받은 Caliburn 창 지휘자가 창을 닫고(<c>CanCloseAsync</c> 를 부르지 않는다),
    /// <see cref="OnConsoleDeactivated"/> 가 인스턴스를 놓는다. 로그아웃 닫기와 셸 종료(동의 뒤 · 강제)가 쓴다.
    /// </summary>
    private async Task CloseAsync(UnitConsoleViewModel console)
    {
        try { await ((IDeactivate)console).DeactivateAsync(true).ConfigureAwait(true); }
        catch (Exception ex) { _log?.Error($"[UnitConsole] 묻지 않고 닫기 실패: {ex.Message}"); }
        finally { Forget(console); }
    }

    private Task Marshal(Func<Task> work)
    {
        if (_disposed) return Task.CompletedTask;
        try
        {
            return _onUi(async () =>
            {
                try { await work().ConfigureAwait(true); }
                catch (Exception ex) { _log?.Error($"[UnitConsole] 세션 신호 처리 실패: {ex.Message}"); }
            });
        }
        catch (Exception ex)
        {
            _log?.Error($"[UnitConsole] 세션 신호를 UI 스레드로 옮기지 못했습니다: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    private static Task RunOnUi(Func<Task> work)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) return work();
        return dispatcher.InvokeAsync(work).Task.Unwrap();
    }
    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _events?.Unsubscribe(this);
        if (_session is not null)
        {
            _session.ForceLogoutRequested -= OnForceLogoutRequested;
            _session.LoginSucceeded -= OnLoginSucceeded;
        }
    }

    #region - Shell exit (U-27) -
    /// <summary>
    /// 셸 종료 관문에 올리는 콘솔 한 벌 — 셸이 끝나기 전에 ✕ 와 같은 가드(<see cref="UnitConsoleViewModel.CanCloseAsync"/>)를 묻고,
    /// 막히면 이 창을 앞으로 가져오고, 동의 뒤(또는 강제 종료)에는 다시 묻지 않고 닫는다.
    /// </summary>
    private sealed class ShellExitEntry : IGuardedWindow
    {
        private readonly UnitConsoleLauncher _launcher;
        private readonly UnitConsoleViewModel _console;

        public ShellExitEntry(UnitConsoleLauncher launcher, UnitConsoleViewModel console)
        {
            _launcher = launcher;
            _console = console;
        }

        public string Name => "부대 편제";

        public Task<bool> CanCloseAsync(CancellationToken cancellationToken = default) => _console.CanCloseAsync(cancellationToken);

        public void BringToFront() => _launcher._host.BringToFront(_console);

        public Task CloseWithoutAskingAsync()
            => ReferenceEquals(_launcher._open, _console) ? _launcher.CloseAsync(_console) : Task.CompletedTask;
    }
    #endregion

    #region - Helpers -
    private async Task<bool> ConfirmAsync(UnitConsoleViewModel owner, string title, string message)
    {
        var prompt = new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.ConfirmPromptViewModel(title, message);
        var settings = new Dictionary<string, object>
        {
            ["Width"] = 420.0,
            ["Height"] = 260.0,
            ["SizeToContent"] = SizeToContent.Manual,
            ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
            ["ResizeMode"] = ResizeMode.NoResize,
            ["ShowInTaskbar"] = false,
        };
        // 소유자는 부대 창 — 비모달이 된 뒤로 "지금 활성 창" 은 셸일 수도 있다. 셸 가운데에 뜨면 무엇을 묻는지 놓친다.
        if (_host.WindowOf(owner) is { } window) settings["Owner"] = window;
        await _windows.ShowDialogAsync(prompt, null, settings);
        return prompt.Result;
    }

    private static IDictionary<string, object> Settings(object? owner)
    {
        var settings = new Dictionary<string, object>
        {
            ["Width"] = WIDTH,
            ["Height"] = HEIGHT,
            ["MinWidth"] = 860.0,
            ["MinHeight"] = 480.0,
            ["SizeToContent"] = SizeToContent.Manual,
            ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
            ["ResizeMode"] = ResizeMode.CanResize,
            ["ShowInTaskbar"] = false,
            // 창의 바탕은 여기서 정하지 않는다 — 한 번 찾아 넣은 브러시는 테마를 바꿔도 옛 색으로 굳는다.
            // Topmost 는 두지 않는다 — 셸 위에만 뜨면 된다(다른 앱 위로 뜨지 않는다).
        };
        if (owner is not null) settings["Owner"] = owner;
        return settings;
    }

    private (ConsolePrefEntry Entry, System.Action Save)? SafePrefs()
    {
        try { return _prefs(); }
        catch (Exception ex)
        {
            _log?.Warning($"[UnitConsole] 표시 설정을 읽지 못했습니다(기본값으로 엽니다): {ex.Message}");
            return null;
        }
    }

    private static (ConsolePrefEntry Entry, System.Action Save)? DiskPrefs()
    {
        var store = new ConsolePrefs(ConsolePrefs.DefaultPath);
        return (store.Get(UnitConsoleViewModel.PREFS_KEY), () => store.Save());
    }
    #endregion
}

/// <summary>런처가 창을 다루는 곳 — 셸(소유자) 찾기 · 콘솔 창 찾기 · 앞으로 가져오기. 시험은 가짜를 넣는다.</summary>
public interface IUnitConsoleWindowHost
{
    /// <summary>콘솔 창의 소유자 — 셸 창. 아직 보인 적 없는 창이면 <c>null</c>(소유자로 둘 수 없다).</summary>
    object? ShellOwner();

    /// <summary>이 뷰모델을 담은 창(확인 창의 소유자). 없으면 <c>null</c>.</summary>
    object? WindowOf(object viewModel);

    /// <summary>이 뷰모델의 창을 앞으로 — 최소화돼 있으면 복원하고 활성화한다.</summary>
    void BringToFront(object viewModel);
}

/// <summary><see cref="IUnitConsoleWindowHost"/> 의 WPF 구현(UI 스레드에서 부른다).</summary>
public sealed class WpfUnitConsoleWindowHost : IUnitConsoleWindowHost
{
    private readonly Func<Window?> _shell;

    /// <param name="shell">셸 창을 찾는 곳. 기본은 <c>Application.Current.MainWindow</c>.</param>
    public WpfUnitConsoleWindowHost(Func<Window?>? shell = null)
        => _shell = shell ?? (() => Application.Current?.MainWindow);

    public object? ShellOwner()
    {
        var shell = _shell();
        // 한 번도 보이지 않은 창은 Owner 로 둘 수 없다(InvalidOperationException) — 그때는 소유자 없이 연다.
        if (shell is null || new WindowInteropHelper(shell).Handle == IntPtr.Zero) return null;
        return shell;
    }

    public object? WindowOf(object viewModel)
    {
        if (viewModel is not IViewAware aware || aware.GetView() is not DependencyObject view) return null;
        return view as Window ?? Window.GetWindow(view);
    }

    public void BringToFront(object viewModel)
    {
        if (WindowOf(viewModel) is not Window window) return;
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        if (!window.IsVisible) window.Show();
        window.Activate();
    }
}
