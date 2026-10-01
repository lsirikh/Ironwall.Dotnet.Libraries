using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
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

    /// <summary>불러오는 중 · 기록 없음 · 불러오기 실패를 <b>서로 다른 문장</b>으로 말한다.</summary>
    public string EmptyText { get; private set; } = LoadingText;

    public const string LoadingText = "불러오는 중입니다…";
    public const string NoRecordsText = "계측 기록이 없습니다.";
    public const string LoadFailedText = "지표 이력을 불러오지 못했습니다. 잠시 후 창을 다시 여세요.";

    public bool IsEmpty => Rows.Count == 0;

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken).ConfigureAwait(true);

        // null = 불러오지 못함(서비스가 원문을 로그에 남겼다) · 빈 목록 = 기록이 없음.
        var metrics = await _service.MetricHistoryAsync(_serverId, 50, cancellationToken).ConfigureAwait(true);
        Rows.Clear();
        if (metrics is not null)
            foreach (var row in ServerMetricBand.History(metrics, _clock)) Rows.Add(row);

        EmptyText = metrics is null ? LoadFailedText
                  : Rows.Count == 0 ? NoRecordsText
                  : string.Empty;
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
            // 다른 확인 창 입구와 같은 S 규격 폭 · 크기 고정 — 높이는 틀이 내용에 맞춘다(ConsoleDialogFrame.IsWindowRoot).
            var settings = Settings(DialogSizeRules.WindowWidth(DialogSize.Small), 240);
            settings["ResizeMode"] = ResizeMode.NoResize;
            await _windows.ShowDialogAsync(vm, null, settings).ConfigureAwait(true);
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

    /// <remarks>
    /// <c>SizeToContent.Manual</c> 필수 — Caliburn 은 뷰를 새 창에 담을 때 <c>SizeToContent=WidthAndHeight</c> 로 만들어
    /// 너비 · 높이를 무시한다. 빠져 있던 동안 지표 이력 창이 별표 열 때문에 화면 폭만큼 늘고, 기록이 없으면 머리 줄 높이로
    /// 납작해졌다(2026-09-30 사용자 보고). 다른 창 입구(WiringLauncher · AssemblyLauncher · UnitConsoleLauncher 등)와 같게.
    /// </remarks>
    internal static Dictionary<string, object> Settings(double width, double height) => new()
    {
        ["Width"] = width,
        ["Height"] = height,
        ["SizeToContent"] = SizeToContent.Manual,
        ["ResizeMode"] = ResizeMode.CanResize,
        ["WindowStartupLocation"] = WindowStartupLocation.CenterOwner,
        ["ShowInTaskbar"] = false,
    };
}
