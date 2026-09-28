using Autofac;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Modules;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// N-11 · 부대 관계도 TEST-33 — 창 입구. 비모달 · 한 벌 · 지도에서 열기 · 로그아웃 닫기.
/// </summary>
/// <remarks>
/// <para><b>이 창이 열리지 못하는 서버에서 호스트를 다치게 하지 않는다</b>(N-11) — 6.3 · 7.0 에서는 창을 만들지 않고 지도에는 까닭을 회신한다.</para>
/// <para><b>비모달</b>(FR-43): 셸이 소유자라 셸 위에 뜨지만 셸을 막지 않는다. 다시 열면 새로 만들지 않고 있는 창을 앞으로 가져온다.</para>
/// <para><b>로그아웃</b>(FR-43 · ISSUE-45): 셸의 로그아웃 신호는 <c>ISessionLifecycle.ForceLogoutRequested(EnumRevokeReason)</c> 하나다
/// (호스트 <c>ConductorControlViewModel.OnForceLogoutRequested</c> 와 같은 신호 · 메뉴 로그아웃은 <c>Manual</c>). 메뉴 로그아웃은 닫기 가드를
/// 지나고, 강제(세션 만료 · 폐기 · 인증 실패 · 다른 곳 로그인)는 묻지 않고 닫는다 — 셸의 <c>ForceActivateItemAsync</c>(40727ec)와 같은 규칙.</para>
/// </remarks>
[Collection("CaliburnIoC")]
public sealed class UnitConsoleLauncherTests : IDisposable
{
    private readonly Func<Type, string?, object?> _savedGetInstance;

    public UnitConsoleLauncherTests()
    {
        // 권한 게이트는 IoC 로 권한 서비스를 찾는다 — 앞선 시험이 남긴 IoC 에 기대지 않게 "없음"(= 허용 폴백)으로 둔다.
        _savedGetInstance = IoC.GetInstance;
        IoC.GetInstance = (_, _) => null!;
    }

    public void Dispose() => IoC.GetInstance = _savedGetInstance!;

    #region - Fakes -
    private sealed class FakeWindows : IWindowManager
    {
        public List<(string Method, object Model, IDictionary<string, object>? Settings)> Calls { get; } = new();

        /// <summary>확인 창에 사람이 고르는 답.</summary>
        public bool ConfirmAnswer { get; set; } = true;

        public int WindowCount => Calls.Count(c => c.Method == "Window");
        public int DialogCount => Calls.Count(c => c.Method == "Dialog");
        public int ConfirmCount => Calls.Count(c => c.Model is ConfirmPromptViewModel);
        public IReadOnlyList<UnitConsoleViewModel> Consoles => Calls.Where(c => c.Method == "Window").Select(c => (UnitConsoleViewModel)c.Model).ToList();

        public async Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Calls.Add(("Dialog", rootModel, settings));
            if (rootModel is ConfirmPromptViewModel prompt && ConfirmAnswer) await prompt.AcceptAsync();
            return true;
        }

        /// <summary>진짜 창 관리자처럼 창을 보이기 전에 뷰모델을 활성화한다(첫 적재가 여기서 끝난다).</summary>
        public async Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Calls.Add(("Window", rootModel, settings));
            if (rootModel is IActivate activate) await activate.ActivateAsync();
        }

        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Calls.Add(("Popup", rootModel, settings));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHost : IUnitConsoleWindowHost
    {
        public object Shell { get; } = new();
        public List<object> BroughtToFront { get; } = new();
        public object? ShellOwner() => Shell;
        public object? WindowOf(object viewModel) => new ConsoleWindowToken(viewModel);
        public void BringToFront(object viewModel) => BroughtToFront.Add(viewModel);
    }

    private sealed record ConsoleWindowToken(object ViewModel);

    private sealed class FakeSession : ISessionLifecycle
    {
        public event System.Action<EnumRevokeReason>? ForceLogoutRequested;
        public event System.Action? LoginSucceeded;
        public int Subscribers => (ForceLogoutRequested?.GetInvocationList().Length ?? 0) + (LoginSucceeded?.GetInvocationList().Length ?? 0);
        public void ForceLogoutOnce(EnumRevokeReason reason) => ForceLogoutRequested?.Invoke(reason);
        public void ResetForLogin() { }
        public void NotifyLoginSucceeded() => LoginSucceeded?.Invoke();
    }

    private sealed class ResultRecorder : IHandle<OpenUnitConsoleResult>
    {
        public List<OpenUnitConsoleResult> Results { get; } = new();

        public Task HandleAsync(OpenUnitConsoleResult message, CancellationToken cancellationToken)
        {
            Results.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>편제 가짜 — 사단(1) 아래 6중대 · 27중대. 첫 적재를 붙잡을 수 있다.</summary>
    private sealed class StubUnits : IUnitGraphApi
    {
        private readonly Dictionary<int, UnitListDto> _nodes = new();

        public StubUnits(bool available)
        {
            IsAvailable = available;
            Add(1, "사단", "Division", null);
            Add(6, "6중대", "Company", 1);
            Add(27, "27중대", "Company", 1);
        }

        public bool IsAvailable { get; }
        public int GraphReads { get; private set; }

        /// <summary>있으면 편제 읽기가 이것이 풀릴 때까지 기다린다(첫 적재 전 선택 경합 시험).</summary>
        public TaskCompletionSource? HoldGraph { get; set; }

        private void Add(int id, string name, string echelon, int? parentId)
            => _nodes[id] = new UnitListDto { Id = id, Code = $"u{id:00}", Name = name, EchelonRaw = echelon, ParentId = parentId, IsEnable = true };

        public async Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
        {
            GraphReads++;
            if (HoldGraph is { } hold) await hold.Task.ConfigureAwait(true);
            var nodes = _nodes.Values.OrderBy(n => n.Id).ToList();
            return ApiResponse<UnitGraphDto>.CreateSuccess(new UnitGraphDto
            {
                Nodes = nodes,
                Edges = new UnitGraphEdgesDto
                {
                    Hierarchy = nodes.Where(n => n.ParentId is int).Select(n => new List<int> { n.ParentId!.Value, n.Id }).ToList(),
                    Adjacency = new List<List<int>>(),
                },
            });
        }

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
        {
            if (!_nodes.TryGetValue(unitId, out var node)) return Task.FromResult(ApiResponse<UnitDetailDto>.CreateError(ApiErrorCodes.NotFound, "없음"));
            return Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
            {
                Id = node.Id, Code = node.Code, Name = node.Name, EchelonRaw = node.EchelonRaw, ParentId = node.ParentId, IsEnable = true,
                AdjacentUnitIds = new List<int>(),
            }));
        }

        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto()));

        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto()));

        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto()));
    }

    private sealed class StubDevices : IUnitDeviceApi
    {
        public bool IsAvailable => true;

        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
            => Task.FromResult(UnitDeviceLoadResult.Empty);

        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
            => Task.FromResult(new UnitDeviceAssignResult(true, "ok"));
    }

    private sealed class Kit
    {
        public FakeWindows Windows { get; } = new();
        public FakeHost Host { get; } = new();
        public FakeSession Session { get; } = new();
        public EventAggregator Events { get; } = new();
        public ResultRecorder Replies { get; } = new();
        public StubUnits Units { get; }
        public UnitConsoleLauncher Launcher { get; }

        public Kit(bool available = true)
        {
            Units = new StubUnits(available);
            Events.SubscribeOnPublishedThread(Replies);
            // UI 스레드 옮김 = 그 자리 · 표시 설정 = 없음(시험이 디스크에 쓰지 않는다).
            Launcher = new UnitConsoleLauncher(Windows, Units, new StubDevices(), events: Events, session: Session, host: Host,
                                               onUi: work => work(), prefs: () => null);
        }

        public UnitConsoleViewModel Console => Windows.Consoles.Last();

        public Task RequestAsync(int unitId, bool openMap = true) => Events.PublishOnCurrentThreadAsync(new OpenUnitConsoleRequest(unitId, openMap));

        public async Task MakeDetailDirtyAsync(int selectUnitId)
        {
            await Console.SelectByIdAsync(selectUnitId);
            Console.Form.Description = "손댄 설명";
            Assert.True(Console.Detail.IsDirty);
        }
    }
    #endregion

    #region - 입구 (N-11) -
    [Fact]
    public void should_hide_the_entry_when_the_server_has_no_unit_surface()
        => Assert.False(new UnitConsoleLauncher(new FakeWindows(), new StubUnits(false), new StubDevices()).IsAvailable);

    [Fact]
    public void should_offer_the_entry_when_the_server_has_the_unit_surface()
        => Assert.True(new UnitConsoleLauncher(new FakeWindows(), new StubUnits(true), new StubDevices()).IsAvailable);

    [Fact]
    public async Task should_open_no_window_when_the_server_has_no_unit_surface()
    {
        var kit = new Kit(available: false);

        await kit.Launcher.OpenAsync();

        Assert.Empty(kit.Windows.Calls);      // 6.3 에서 눌려도 창을 만들지 않는다(이중 방어)
    }

    [Fact]
    public void should_refuse_to_be_built_without_a_window_manager()
        => Assert.Throws<ArgumentNullException>(() => new UnitConsoleLauncher(null!, new StubUnits(true), new StubDevices()));
    #endregion

    #region - 비모달 · 한 벌 (FR-43) -
    [Fact]
    public async Task should_show_a_modeless_window_owned_by_the_shell_when_opened()
    {
        var kit = new Kit();

        await kit.Launcher.OpenAsync();

        Assert.Equal(1, kit.Windows.WindowCount);
        Assert.Equal(0, kit.Windows.DialogCount);                          // ShowDialogAsync 0 — 셸을 막지 않는다
        var settings = kit.Windows.Calls.Single().Settings!;
        Assert.Same(kit.Host.Shell, settings["Owner"]);                    // 셸 위에 뜬다
        Assert.False(settings.TryGetValue("Topmost", out var topmost) && topmost is true);   // 다른 앱 위로는 뜨지 않는다
        Assert.Equal(false, settings["ShowInTaskbar"]);
    }

    [Fact]
    public async Task should_bring_the_open_console_to_front_instead_of_opening_another_when_opened_twice()
    {
        var kit = new Kit();

        await kit.Launcher.OpenAsync();
        await kit.Launcher.OpenAsync();

        Assert.Equal(1, kit.Windows.WindowCount);                          // 새 뷰모델 0
        Assert.Same(kit.Console, Assert.Single(kit.Host.BroughtToFront)); // 최소화돼 있으면 복원 + 활성화(기본 구현 — TEST-34)
    }

    [Fact]
    public async Task should_open_a_new_console_when_the_previous_one_was_closed()
    {
        var kit = new Kit();
        await kit.Launcher.OpenAsync();
        var first = kit.Console;

        await ((IDeactivate)first).DeactivateAsync(true);                   // 사용자가 ✕ 로 닫음(창 관리자가 닫힘 비활성화를 부른다)
        await kit.Launcher.OpenAsync();

        Assert.Equal(2, kit.Windows.WindowCount);
        Assert.NotSame(first, kit.Console);
        Assert.Empty(kit.Host.BroughtToFront);
    }

    [Fact]
    public async Task should_own_the_confirm_prompt_by_the_console_window_not_the_shell()
    {
        var kit = new Kit();
        await kit.Launcher.OpenAsync();

        await kit.Console.Confirm!("제목", "물음");

        var prompt = kit.Windows.Calls.Single(c => c.Model is ConfirmPromptViewModel);
        Assert.Equal(new ConsoleWindowToken(kit.Console), prompt.Settings!["Owner"]);
    }
    #endregion

    #region - 지도에서 열기 (FR-46 · ISSUE-43 · 44) -
    [Fact]
    public async Task should_open_the_console_on_the_map_rail_select_the_unit_and_reply_shown_when_requested_from_the_map()
    {
        var kit = new Kit();

        await kit.RequestAsync(27, openMap: true);

        Assert.Equal(1, kit.Windows.WindowCount);
        Assert.Equal(27, kit.Console.SelectedRow?.Id);
        Assert.True(kit.Console.IsAdjacencyView);
        Assert.Equal(new OpenUnitConsoleResult(27, OpenUnitConsoleOutcome.Shown), Assert.Single(kit.Replies.Results));
    }

    [Fact]
    public async Task should_bring_the_console_to_front_keep_the_selection_and_reply_blocked_when_the_detail_has_an_unsaved_edit()
    {
        var kit = new Kit();
        await kit.Launcher.OpenAsync();
        await kit.MakeDetailDirtyAsync(6);

        await kit.RequestAsync(27);

        Assert.Equal(1, kit.Windows.WindowCount);
        Assert.Same(kit.Console, Assert.Single(kit.Host.BroughtToFront));  // 창은 앞으로 온다 — 사용자가 까닭을 본다
        Assert.Equal(6, kit.Console.SelectedRow?.Id);                     // 선택 불변
        Assert.Equal(new OpenUnitConsoleResult(27, OpenUnitConsoleOutcome.BlockedByUnsavedEdit), Assert.Single(kit.Replies.Results));   // 말없는 실패 0
    }

    [Fact]
    public async Task should_reply_unavailable_when_the_requested_unit_is_not_in_the_graph()
    {
        var kit = new Kit();

        await kit.RequestAsync(999);

        Assert.Equal(new OpenUnitConsoleResult(999, OpenUnitConsoleOutcome.Unavailable), Assert.Single(kit.Replies.Results));
    }

    [Fact]
    public async Task should_open_nothing_and_reply_unavailable_when_the_server_has_no_unit_surface()
    {
        var kit = new Kit(available: false);

        await kit.RequestAsync(27);

        Assert.Empty(kit.Windows.Calls);
        Assert.Equal(new OpenUnitConsoleResult(27, OpenUnitConsoleOutcome.Unavailable), Assert.Single(kit.Replies.Results));
    }

    [Fact]
    public async Task should_select_only_after_the_first_load_when_a_request_opens_a_closed_console()
    {
        var kit = new Kit();
        kit.Units.HoldGraph = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = kit.RequestAsync(27);
        var second = kit.RequestAsync(6, openMap: false);                  // 적재 중에 온 두 번째 요청
        await Task.Delay(50);
        Assert.Empty(kit.Replies.Results);                                  // 적재 전에는 고르지도 회신하지도 않는다
        Assert.Equal(1, kit.Windows.WindowCount);                          // 두 번째 요청도 같은 창

        kit.Units.HoldGraph.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, kit.Units.GraphReads);
        Assert.Equal(2, kit.Replies.Results.Count);
        Assert.All(kit.Replies.Results, r => Assert.Equal(OpenUnitConsoleOutcome.Shown, r.Outcome));
    }

    [Fact]
    public async Task should_bring_a_minimized_console_back_when_requested_from_the_map_again()
    {
        var kit = new Kit();
        await kit.RequestAsync(27);

        await kit.RequestAsync(6, openMap: false);

        Assert.Equal(1, kit.Windows.WindowCount);
        Assert.Same(kit.Console, Assert.Single(kit.Host.BroughtToFront));  // 최소화 복원 + 활성화는 기본 구현이 한다(TEST-34 가 실제 창으로 확인)
        Assert.Equal(6, kit.Console.SelectedRow?.Id);
    }
    #endregion

    #region - 로그아웃 (FR-43 · ISSUE-45) -
    [Theory]
    [InlineData(EnumRevokeReason.TokenExpired)]
    [InlineData(EnumRevokeReason.SessionRevoked)]
    [InlineData(EnumRevokeReason.Unauthorized)]
    [InlineData(EnumRevokeReason.Superseded)]
    public async Task should_close_without_asking_when_the_session_is_forced_out(EnumRevokeReason reason)
    {
        var kit = new Kit();
        await kit.Launcher.OpenAsync();
        await kit.MakeDetailDirtyAsync(6);
        var console = kit.Console;

        kit.Session.ForceLogoutOnce(reason);
        await kit.Launcher.SessionWork;

        Assert.Equal(0, kit.Windows.ConfirmCount);                        // CanCloseAsync 를 부르지 않는다 — 세션이 이미 끝났다
        Assert.False(console.IsActive);                                    // 닫힘 비활성화(창 관리자가 창을 닫는다)
        await kit.Launcher.OpenAsync();
        Assert.Equal(2, kit.Windows.WindowCount);                          // 다음 열기는 새 인스턴스
    }

    [Fact]
    public async Task should_ask_through_the_close_guard_and_close_when_the_user_logs_out_from_the_menu()
    {
        var kit = new Kit();
        await kit.Launcher.OpenAsync();
        await kit.MakeDetailDirtyAsync(6);
        var console = kit.Console;

        kit.Session.ForceLogoutOnce(EnumRevokeReason.Manual);
        await kit.Launcher.SessionWork;

        Assert.Equal(1, kit.Windows.ConfirmCount);                        // 7a5dd396 의 가드 그대로
        Assert.False(console.IsActive);
    }

    [Fact]
    public async Task should_keep_the_console_after_a_refused_menu_logout_and_close_it_without_asking_at_the_next_login()
    {
        var kit = new Kit();
        await kit.Launcher.OpenAsync();
        await kit.MakeDetailDirtyAsync(6);
        var console = kit.Console;
        kit.Windows.ConfirmAnswer = false;

        kit.Session.ForceLogoutOnce(EnumRevokeReason.Manual);
        await kit.Launcher.SessionWork;
        Assert.True(console.IsActive);                                     // 사람이 "닫지 않음" 을 골랐다 — 편집을 옮겨 적을 수 있다

        kit.Session.NotifyLoginSucceeded();                                // 새 로그인(다른 계정일 수 있다)
        await kit.Launcher.SessionWork;
        Assert.Equal(1, kit.Windows.ConfirmCount);                        // 다시 묻지 않는다
        Assert.False(console.IsActive);
    }

    [Fact]
    public async Task should_close_a_clean_console_without_a_prompt_when_the_user_logs_out()
    {
        var kit = new Kit();
        await kit.Launcher.OpenAsync();
        var console = kit.Console;

        kit.Session.ForceLogoutOnce(EnumRevokeReason.Manual);
        await kit.Launcher.SessionWork;

        Assert.Equal(0, kit.Windows.ConfirmCount);
        Assert.False(console.IsActive);
    }

    [Fact]
    public async Task should_leave_a_console_opened_after_login_alone_when_login_succeeds()
    {
        var kit = new Kit();
        await kit.Launcher.OpenAsync();
        var console = kit.Console;

        kit.Session.NotifyLoginSucceeded();                                // 로그아웃 없이 온 로그인 알림(부팅 직후 등)
        await kit.Launcher.SessionWork;

        Assert.True(console.IsActive);
    }

    [Fact]
    public void should_stop_listening_to_the_session_and_requests_when_disposed()
    {
        var kit = new Kit();
        Assert.Equal(2, kit.Session.Subscribers);
        Assert.True(kit.Events.HandlerExistsFor(typeof(OpenUnitConsoleRequest)));

        kit.Launcher.Dispose();

        Assert.Equal(0, kit.Session.Subscribers);
        Assert.False(kit.Events.HandlerExistsFor(typeof(OpenUnitConsoleRequest)));
    }
    #endregion

    #region - DI 활성화 (V-13) -
    /// <summary>
    /// 런처는 <b>아무도 해석하지 않아도</b> 지도의 요청을 들어야 한다 — 호스트는 메뉴를 누를 때까지 런처를 해석하지 않는다.
    /// 해석되지 않은 싱글턴은 구독자 목록에 없으므로 [관계도에서 보기]가 말없이 사라진다(메모리 branch_wiring_must_be_verified).
    /// </summary>
    [Fact]
    public async Task should_answer_map_requests_when_the_host_container_is_built_without_resolving_the_launcher()
    {
        var log = new Mock<ILogService>().Object;
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new FakeWindows()).As<IWindowManager>().SingleInstance();
        builder.RegisterType<EventAggregator>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterInstance(log).As<ILogService>().SingleInstance();
        builder.RegisterType<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterInstance(new Mock<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>().Object)
               .As<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>().SingleInstance();
        builder.RegisterModule(new DeviceUiModule(new ApiSetupModel { Url = "https://localhost:8000/api" }, log, 20));
        using var container = builder.Build();

        var events = container.Resolve<IEventAggregator>();
        var replies = new ResultRecorder();
        events.SubscribeOnPublishedThread(replies);
        await events.PublishOnCurrentThreadAsync(new OpenUnitConsoleRequest(27, true));

        // 판본 판정(IServerContractProbe)이 없는 컨테이너 = 8.0 미만 취급 → 창 0 · 까닭 회신 1.
        Assert.Equal(new OpenUnitConsoleResult(27, OpenUnitConsoleOutcome.Unavailable), Assert.Single(replies.Results));
    }
    #endregion
}
