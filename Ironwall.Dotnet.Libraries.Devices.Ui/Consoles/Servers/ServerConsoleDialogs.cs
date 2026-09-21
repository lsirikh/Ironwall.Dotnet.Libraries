using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 모니터의 창 입구 — 확인 · 지표 이력 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 지표 이력 — <b>임계 배지를 그리지 않는다</b>(스토리보드 L1359).
/// </summary>
/// <remarks>
/// 과거 행의 임계 초과 배열은 <b>언제나 비어 있다</b>. 그 자리에 배지를 그리면 "그때는 정상이었다" 는
/// 거짓이 되고, 우리가 다시 판정하면 서버 임계와 갈린다. 그래서 값만 보이고 <b>판정은 보이지 않는다</b>.
/// </remarks>
public sealed class ServerMetricHistoryViewModel : Screen
{
    private readonly IServerConsoleService _service;
    private readonly IClock _clock;
    private readonly int _serverId;

    public ServerMetricHistoryViewModel(IServerConsoleService service, IClock clock, int serverId, string serverName)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _serverId = serverId;

        DisplayName = $"지표 이력 — {serverName}";
        Rows = new ObservableCollection<ServerMetricHistoryRow>();
    }

    public ObservableCollection<ServerMetricHistoryRow> Rows { get; }

    /// <summary>이력 화면이 늘 달고 있는 한 줄 — 왜 배지가 없는지 화면이 스스로 말한다.</summary>
    public string Note => "지난 계측은 다시 판정하지 않습니다 — 임계 배지를 그리지 않습니다.";

    public string EmptyText { get; private set; } = "불러오는 중입니다…";

    public bool IsEmpty => Rows.Count == 0;

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken).ConfigureAwait(true);

        var metrics = await _service.MetricHistoryAsync(_serverId, 50, cancellationToken).ConfigureAwait(true);
        Rows.Clear();
        foreach (var row in ServerMetricBand.History(metrics, _clock)) Rows.Add(row);

        EmptyText = Rows.Count == 0 ? "받은 계측이 없습니다 — 보고 없음" : string.Empty;
        NotifyOfPropertyChange(nameof(EmptyText));
        NotifyOfPropertyChange(nameof(IsEmpty));
    }

    public Task CloseAsync() => TryCloseAsync(true);
}

/// <summary>
/// 창 관리자로 라이브러리가 직접 창을 연다 — 호스트의 메시지 계약을 늘리지 않는다(선례: <c>AssemblyLauncher</c>).
/// </summary>
public sealed class ServerConsoleDialogs : IServerConsoleDialogs
{
    private readonly IWindowManager _windows;
    private readonly IServerConsoleService _service;
    private readonly IClock _clock;
    private readonly ILogService? _log;

    public ServerConsoleDialogs(IWindowManager windows, IServerConsoleService service, IClock clock, ILogService? log = null)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        try
        {
            var vm = new ConfirmPromptViewModel(title, message);
            await _windows.ShowDialogAsync(vm, null, Settings(440, 240)).ConfigureAwait(true);
            return vm.Result;
        }
        catch (Exception ex)
        {
            // 물을 수 없으면 <b>보내지 않는다</b> — 확인을 건너뛰는 쪽으로 기울지 않는다.
            _log?.Error($"[ServerConsole] 확인 창을 열지 못했습니다 — 보내지 않습니다: {ex.Message}");
            return false;
        }
    }

    public async Task ShowMetricHistoryAsync(int serverId, string serverName)
    {
        try
        {
            var vm = new ServerMetricHistoryViewModel(_service, _clock, serverId, serverName);
            await _windows.ShowDialogAsync(vm, null, Settings(560, 520)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 지표 이력 창을 열지 못했습니다: {ex.Message}");
        }
    }

    private static Dictionary<string, object> Settings(double width, double height) => new()
    {
        ["Width"] = width,
        ["Height"] = height,
        ["ResizeMode"] = ResizeMode.CanResize,
        ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
        ["ShowInTaskbar"] = false,
    };
}
