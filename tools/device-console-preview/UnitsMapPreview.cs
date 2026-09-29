using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DeviceConsolePreview;

/****************************************************************************
   Purpose      : 부대 관계도 미리보기 — 진짜 UnitMapCanvas + UnitMapViewModel 을 가짜 배치 서버 · 200 부대 위에 (IMPL-35)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

// ─────────────────────────────────────────────────────────────────────────────
//  dotnet run -- --units-map [--layout supported|unsupported|fail|two-client] [--dark] [--visible]
//                            [--view-only] [--call-log <파일>] [--snapshot <폴더>] [--bench <폴더>] [--scale 1.0|1.25|1.5]
//
//  · 서버에 단 한 줄도 나가지 않는다 — 배치 서버 · 부대 콘솔은 전부 메모리 가짜(NFR-13).
//  · --call-log 는 이 미리보기 도구 전용이다(제품 코드의 NFR-14 "디스크 쓰기 0" 과 무관) — 헤드 시험(UnitsMapPreviewFixture)이
//    쓰기 횟수 · If-Match · 본문을 읽어 단언한다.
//  · --scale 은 창 내용에 배율을 건다(눈으로 보는 용 — 실제 DPI 판정은 --snapshot 의 dpi125 · dpi150 렌더가 한다).
//  · --visible 이 없으면 화면 밖에 뜬다(OffscreenStage — 사용자 화면을 가리지 않는다). 헤드 시험은 --visible 로 띄운다.
//  · 미리보기 단추(창 위 줄, AutomationId Preview.Units.*): 다른 운영자 쓰기 · 다음 쓰기 412 · 배치 알림 · 서버 모드.
//    같은 일을 F5 · F6 · F7 · F8 로도 한다.
// ─────────────────────────────────────────────────────────────────────────────

internal enum UnitsMapLayoutMode
{
    Supported,
    Unsupported,
    Failing,
    TwoClient,
}

/// <summary>명령 줄 선택.</summary>
internal sealed record UnitsMapPreviewOptions(
    UnitsMapLayoutMode Mode,
    bool Dark,
    bool Visible,
    bool ViewOnly,
    string? CallLog,
    string? SnapshotFolder,
    string? BenchFolder,
    double Scale = 1.0)
{
    public static UnitsMapPreviewOptions Parse(string[] args)
    {
        string? Value(string name) => Array.IndexOf(args, name) is var at and >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        var mode = Value("--layout") switch
        {
            "unsupported" => UnitsMapLayoutMode.Unsupported,
            "fail" => UnitsMapLayoutMode.Failing,
            "two-client" => UnitsMapLayoutMode.TwoClient,
            _ => UnitsMapLayoutMode.Supported,
        };
        return new UnitsMapPreviewOptions(mode, args.Contains("--dark"), args.Contains("--visible"), args.Contains("--view-only"),
                                          Value("--call-log"), Value("--snapshot"), Value("--bench"),
                                          double.TryParse(Value("--scale"), System.Globalization.NumberStyles.Float,
                                                          System.Globalization.CultureInfo.InvariantCulture, out var scale)
                                          && scale is >= 1.0 and <= 2.0 ? scale : 1.0);
    }
}

#region - 데이터(Devices.Ui 시험의 UnitMapTestData 와 같은 생성 규칙) -
/// <summary>
/// 200 부대(사단 1 · 연대 3 · 대대 9 · 중대 36 · 소초 151, 인접 60) · 장비 2,000(상태 섞임). 시험 빌더(<c>UnitMapTestData</c>)는
/// 시험 어셈블리 안의 internal 이라 여기서 같은 규칙을 그대로 되풀이한다 — id · 코드 · 이름이 같다.
/// </summary>
internal static class UnitsMapPreviewData
{
    public static UnitGraphDto Standard200()
    {
        var nodes = new List<UnitListDto>();
        var nextId = 1;
        int Add(string code, string name, string echelon, int? parent)
        {
            var id = nextId++;
            nodes.Add(new UnitListDto { Id = id, Code = code, Name = name, EchelonRaw = echelon, ParentId = parent, IsEnable = true });
            return id;
        }

        var division = Add("d01", "제○○사단", "Division", null);
        var regiments = new List<int>();
        var battalions = new List<int>();
        var companies = new List<int>();
        var firstTwoOutposts = new List<(int, int)>();
        int b = 0, c = 0;
        for (var r = 1; r <= 3; r++)
        {
            var regiment = Add($"r{r:00}", $"{r}연대", "Regiment", division);
            regiments.Add(regiment);
            for (var i = 0; i < 3; i++)
            {
                b++;
                var battalion = Add($"b{b:00}", $"{b}대대", "Battalion", regiment);
                battalions.Add(battalion);
                for (var j = 0; j < 4; j++)
                {
                    c++;
                    var company = Add($"c{b:00}{c:00}", $"{c}중대", "Company", battalion);
                    companies.Add(company);
                    var outposts = c <= 7 ? 5 : 4;
                    var first = 0;
                    for (var q = 1; q <= outposts; q++)
                    {
                        var outpost = Add($"p{c:00}{q}", $"{c}{q}소초", "Outpost", company);
                        if (q == 1) first = outpost;
                        if (q == 2 && c <= 15) firstTwoOutposts.Add((first, outpost));
                    }
                }
            }
        }

        var adjacency = new List<(int, int)>();
        for (var k = 0; k + 1 < companies.Count; k++) adjacency.Add((companies[k], companies[k + 1]));
        for (var k = 0; k + 1 < battalions.Count; k++) adjacency.Add((battalions[k], battalions[k + 1]));
        for (var k = 0; k + 1 < regiments.Count; k++) adjacency.Add((regiments[k], regiments[k + 1]));
        adjacency.AddRange(firstTwoOutposts);

        // 한 부대는 운용 중지로 둔다(해치 표지가 스냅숏에 보이게).
        nodes.First(n => n.Name == "84소초").IsEnable = false;

        return new UnitGraphDto
        {
            Nodes = nodes,
            Edges = new UnitGraphEdgesDto
            {
                Hierarchy = nodes.Where(n => n.ParentId is int).Select(n => new List<int> { n.ParentId!.Value, n.Id }).ToList(),
                Adjacency = adjacency.Select(p => new List<int> { p.Item1, p.Item2 }).ToList(),
            },
        };
    }

    /// <summary>장비 — 50 대마다 1 대 미배치 · 17 번째마다 오류 · 23 번째마다 비활성(시험 빌더와 같은 규칙).</summary>
    public static IReadOnlyList<UnitDeviceItem> Devices(UnitTreeModel tree, int count = 2000)
    {
        var categories = new[] { EnumDeviceCategory.Camera, EnumDeviceCategory.Controller, EnumDeviceCategory.Enclosure,
                                 EnumDeviceCategory.Gate, EnumDeviceCategory.Lamp };
        var units = tree.Ordered.Select(n => n.Id).ToList();
        var items = new List<UnitDeviceItem>(count);
        for (var i = 1; i <= count; i++)
        {
            int? unitId = units.Count == 0 || i % 50 == 0 ? null : units[(i * 7) % units.Count];
            var status = i % 17 == 0 ? UnitDeviceStatus.Error : i % 23 == 0 ? UnitDeviceStatus.Deactivated : UnitDeviceStatus.Normal;
            items.Add(new UnitDeviceItem(1000 + i, i, $"장비{i:0000}", categories[i % categories.Length], unitId, status));
        }
        return items;
    }
}
#endregion

#region - 호출 기록(도구 전용) -
/// <summary>가짜 서버 · 가짜 콘솔이 받은 호출을 JSON 줄로 적는다 — 헤드 시험의 단언 재료(<c>--call-log</c>).</summary>
internal sealed class PreviewCallLog
{
    private readonly string? _path;
    private readonly object _gate = new();

    public PreviewCallLog(string? path)
    {
        _path = path;
        if (_path is not null) File.WriteAllText(_path, string.Empty);
    }

    public List<string> Lines { get; } = new();

    public void Write(object entry)
    {
        var line = JsonConvert.SerializeObject(entry, Formatting.None);
        lock (_gate)
        {
            Lines.Add(line);
            if (_path is not null) File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}
#endregion

#region - 가짜 배치 서버(FakeUnitLayoutApi 규칙) -
/// <summary>
/// 공유 배치 서버 흉내 — 판정 · 412 · 다른 운영자 쓰기 · 알림(<see cref="UnitLayoutChangedMessage"/>)을 한 문서로.
/// 규칙은 Devices.Ui 시험의 <c>FakeUnitLayoutApi</c> 와 같다(버전 +1 · If-Match 불일치 = 412 · 알림이 응답보다 먼저).
/// </summary>
internal sealed class PreviewLayoutServer
{
    private readonly Queue<UnitLayoutWrite> _injected = new();
    private UnitLayoutSnapshot _current = UnitLayoutSnapshot.Empty();

    public PreviewLayoutServer(UnitsMapLayoutMode mode, PreviewCallLog log)
    {
        Mode = mode == UnitsMapLayoutMode.TwoClient ? UnitsMapLayoutMode.Supported : mode;
        Log = log;
    }

    public UnitsMapLayoutMode Mode { get; set; }
    public PreviewCallLog Log { get; }
    public UnitLayoutSnapshot Current => _current;

    /// <summary>버전이 바뀔 때마다 — <c>SYNC_UNIT_LAYOUT {version}</c> 알림 흉내.</summary>
    public Action<long>? Published { get; set; }

    public int Reads { get; private set; }
    public int Writes { get; private set; }

    public IUnitLayoutApi ForClient(string client, string operatorName) => new ClientPort(this, client, operatorName);

    /// <summary>다음 쓰기를 412 로(다른 운영자가 그 사이 문서를 바꾼 것처럼).</summary>
    public void FailNextWithConflict() => _injected.Enqueue(new UnitLayoutWrite.Conflict(_current.Version + 1));

    /// <summary>다른 운영자가 그 부대를 옮겼다(버전 +1). <paramref name="publish"/> 면 알림도.</summary>
    public long SimulateOtherWrite(int unitId, double dx, double dy, string by = "다른 운영자", bool publish = true)
    {
        _current = Bump(UnitLayoutChange.SetOne(unitId, new Vector(dx, dy)).ApplyTo(_current), by);
        Log.Write(new { client = "other", op = "write", by, unit = unitId, dx, dy, version = _current.Version });
        if (publish) Published?.Invoke(_current.Version);
        return _current.Version;
    }

    /// <summary>배치 판을 바꾼다(판 불일치 문구 스냅숏).</summary>
    public void SetLayoutVersion(int layoutVersion) => _current = _current with { LayoutVersion = layoutVersion };

    /// <summary>지금 버전으로 알림 한 번.</summary>
    public void PublishNow() => Published?.Invoke(_current.Version);

    private UnitLayoutRead Read(string client)
    {
        Reads++;
        UnitLayoutRead result = Mode switch
        {
            UnitsMapLayoutMode.Unsupported => new UnitLayoutRead.Unsupported("이 서버는 배치 저장을 지원하지 않습니다."),
            UnitsMapLayoutMode.Failing => new UnitLayoutRead.Failed(UnitLayoutFailureKind.Server, "미리보기 서버 실패"),
            _ => new UnitLayoutRead.Supported(_current),
        };
        Log.Write(new { client, op = "read", result = result.GetType().Name, version = _current.Version });
        return result;
    }

    private UnitLayoutWrite Write(string client, string operatorName, long ifMatch, UnitLayoutChange change)
    {
        Writes++;
        UnitLayoutWrite result;
        long? published = null;
        if (_injected.Count > 0) result = _injected.Dequeue();
        else if (Mode == UnitsMapLayoutMode.Unsupported) result = new UnitLayoutWrite.Unsupported("이 서버는 배치 저장을 지원하지 않습니다.");
        else if (Mode == UnitsMapLayoutMode.Failing) result = new UnitLayoutWrite.Failed(UnitLayoutFailureKind.Server, "미리보기 서버 실패");
        else if (ifMatch != _current.Version) result = new UnitLayoutWrite.Conflict(_current.Version);
        else
        {
            _current = Bump(change.ApplyTo(_current), operatorName);
            result = new UnitLayoutWrite.Saved(_current);
            published = _current.Version;
        }

        Log.Write(new
        {
            client,
            op = "write",
            ifMatch = $"\"{ifMatch}\"",
            set = change.Set.Select(p => new { unit_id = p.Key, dx = p.Value.X, dy = p.Value.Y }).ToList(),
            clear = change.Clear.ToList(),
            clear_all = change.ClearAll,
            result = result.GetType().Name,
            version = _current.Version,
        });
        if (published is long v) Published?.Invoke(v);      // 메아리가 응답보다 먼저(V-11)
        return result;
    }

    private static UnitLayoutSnapshot Bump(UnitLayoutSnapshot snapshot, string by)
        => snapshot with { Version = snapshot.Version + 1, UpdatedByName = by, UpdatedAt = DateTimeOffset.Now };

    private sealed class ClientPort : IUnitLayoutApi
    {
        private readonly PreviewLayoutServer _server;
        private readonly string _client;
        private readonly string _operator;

        public ClientPort(PreviewLayoutServer server, string client, string operatorName)
        {
            _server = server;
            _client = client;
            _operator = operatorName;
        }

        public Task<UnitLayoutRead> ReadAsync(CancellationToken token = default) => Task.FromResult(_server.Read(_client));

        public Task<UnitLayoutWrite> WriteAsync(long ifMatchVersion, UnitLayoutChange change, CancellationToken token = default)
            => Task.FromResult(_server.Write(_client, _operator, ifMatchVersion, change));
    }
}
#endregion

#region - 가짜 부대 콘솔(IUnitMapCommands) -
/// <summary>
/// 부대 콘솔 VM 흉내 — 선택 · 상위 바꾸기 · 되돌리기 · 인접 · 재조회를 메모리 편제에 한다. 편제 쓰기는 호출 기록에 남는다.
/// </summary>
/// <remarks>실제 콘솔 결선(IMPL-31 · 32, 레인 A)이 들어오면 이 미리보기는 진짜 <c>UnitConsoleView</c> 로 바꿀 수 있다.</remarks>
internal sealed class PreviewUnitConsole : IUnitMapCommands, IUnitMapConsoleBridge
{
    private readonly UnitGraphDto _graph;
    private readonly string _client;
    private readonly PreviewCallLog _log;
    private int? _selected;
    public PreviewUnitConsole(string client, PreviewCallLog log, bool canEdit)
    {
        _client = client;
        _log = log;
        _graph = UnitsMapPreviewData.Standard200();
        CanEdit = canEdit;
        Tree = UnitTreeBuilder.Build(_graph);
        Devices = UnitsMapPreviewData.Devices(Tree);
    }

    public UnitTreeModel Tree { get; private set; }
    public IReadOnlyList<UnitDeviceItem> Devices { get; }

    /// <summary>편제가 바뀌었다 — 창이 관계도 VM 에 다시 싣는다(<c>SetData</c>).</summary>
    public event EventHandler? Changed;

    #region - IUnitMapCommands -
    public int? SelectedUnitId => _selected;
    public event EventHandler? SelectedUnitChanged;
    public bool CanView => true;
    public bool CanEdit { get; }

    public bool TrySelect(int unitId)
    {
        if (_selected == unitId) return true;
        _selected = unitId;
        SelectedUnitChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <remarks>관계도 되돌리기도 이 한 길로 온다(<paramref name="targetId"/> <c>null</c> = 최상위) — REVIEW-01 HIGH-1.</remarks>
    public Task<bool> MoveAsync(int movingId, int? targetId, CancellationToken token = default)
    {
        var node = _graph.Nodes.First(n => n.Id == movingId);
        SetParent(node, targetId);
        _log.Write(new { client = _client, op = "unit-patch", unit = movingId, parent_id = targetId });
        Rebuild();
        return Task.FromResult(true);
    }

    public Task<bool> ChangeAdjacencyAsync(int unitId, int? add, int? remove, CancellationToken token = default)
    {
        var pairs = _graph.Edges.Adjacency;
        if (add is int a && !pairs.Any(p => p.Contains(unitId) && p.Contains(a))) pairs.Add(new List<int> { Math.Min(unitId, a), Math.Max(unitId, a) });
        if (remove is int r) pairs.RemoveAll(p => p.Contains(unitId) && p.Contains(r));
        _log.Write(new { client = _client, op = "unit-patch", unit = unitId, adjacent_add = add, adjacent_remove = remove });
        Rebuild();
        return Task.FromResult(true);
    }

    public Task ReloadAsync(bool quiet, CancellationToken token = default)
    {
        _log.Write(new { client = _client, op = "graph-read", quiet });
        Rebuild();
        return Task.CompletedTask;
    }
    #endregion

    #region - IUnitMapConsoleBridge -
    public bool IsDetailDirty => false;
    public Task RefreshDetailAsync(CancellationToken token = default) => Task.CompletedTask;
    public bool HasDeferredReload => false;
    public bool IsBusy => false;
    public event EventHandler? BusyChanged { add { } remove { } }
    public string? LastWriteFailureReason => null;       // 가짜 콘솔은 편제 쓰기가 실패하지 않는다
    #endregion

    private void SetParent(UnitListDto node, int? parentId)
    {
        node.ParentId = parentId;
        _graph.Edges.Hierarchy.RemoveAll(e => e.Count == 2 && e[1] == node.Id);
        if (parentId is int p) _graph.Edges.Hierarchy.Add(new List<int> { p, node.Id });
    }

    private void Rebuild()
    {
        Tree = UnitTreeBuilder.Build(_graph);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
#endregion

#region - 창 -
/// <summary>관계도 한 칸(운영자 한 사람) — 진짜 캔버스 + 진짜 뷰모델.</summary>
internal sealed class UnitsMapClient
{
    public UnitsMapClient(string name, PreviewUnitConsole console, UnitMapViewModel viewModel, UnitMapCanvas canvas, GroupBox pane)
    {
        Name = name;
        Console = console;
        ViewModel = viewModel;
        Canvas = canvas;
        Pane = pane;
    }

    public string Name { get; }
    public PreviewUnitConsole Console { get; }
    public UnitMapViewModel ViewModel { get; }
    public UnitMapCanvas Canvas { get; }
    public GroupBox Pane { get; }
}

/// <summary>
/// 관계도 미리보기 창 — 운영자 한 명(지원 · 세션 전용 · 실패) 또는 두 명(two-client, 같은 가짜 서버).
/// </summary>
internal sealed class UnitsMapPreview
{
    public const double CanvasWidth = 756;       // 콘솔 캔버스 폭(1280 − 184 − 340 — PRD §3.3)
    public const double CanvasHeight = 620;

    private UnitsMapPreview(UnitsMapPreviewOptions options, EventAggregator events, PreviewLayoutServer server, PreviewCallLog log)
    {
        Options = options;
        Events = events;
        Server = server;
        Log = log;
    }

    public UnitsMapPreviewOptions Options { get; }
    public EventAggregator Events { get; }
    public PreviewLayoutServer Server { get; }
    public PreviewCallLog Log { get; }
    public List<UnitsMapClient> Clients { get; } = new();
    public Window Window { get; private set; } = null!;
    public UnitsMapClient Main => Clients[0];

    public static async Task<UnitsMapPreview> CreateAsync(Application app, UnitsMapPreviewOptions options)
    {
        var log = new PreviewCallLog(options.CallLog);
        var events = new EventAggregator();
        var server = new PreviewLayoutServer(options.Mode, log);
        server.Published = version => _ = events.PublishOnUIThreadAsync(new UnitLayoutChangedMessage(version));
        var preview = new UnitsMapPreview(options, events, server, log);

        var names = options.Mode == UnitsMapLayoutMode.TwoClient ? new[] { "A", "B" } : new[] { "A" };
        var panes = new Grid();
        foreach (var name in names)
        {
            var client = preview.BuildClient(name);
            panes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(client.Pane, preview.Clients.Count);
            panes.Children.Add(client.Pane);
            preview.Clients.Add(client);
        }

        var root = new DockPanel();
        var strip = preview.BuildControlStrip();
        DockPanel.SetDock(strip, Dock.Top);
        root.Children.Add(strip);
        root.Children.Add(panes);

        var window = new Window
        {
            Title = options.Mode == UnitsMapLayoutMode.TwoClient ? "부대 관계도 미리보기 — 두 운영자" : "부대 관계도 미리보기",
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = new Border
            {
                Margin = new Thickness(12),
                Child = root,
                LayoutTransform = options.Scale == 1.0 ? Transform.Identity : new ScaleTransform(options.Scale, options.Scale),
            },
        };
        window.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");
        AutomationProperties.SetAutomationId(window, "Preview.Units.Map");
        window.PreviewKeyDown += preview.OnWindowKey;
        preview.Window = window;

        if (options.Visible)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            PreviewTools.Shared.OffscreenStage.Hide(window);
        }
        window.Show();

        foreach (var client in preview.Clients)
        {
            client.ViewModel.SetData(client.Console.Tree, client.Console.Devices);
            await client.ViewModel.OpenAsync();
        }
        return preview;
    }

    private UnitsMapClient BuildClient(string name)
    {
        var console = new PreviewUnitConsole(name, Log, canEdit: !Options.ViewOnly);
        var port = Server.ForClient(name, $"운영자 {name}");
        var clock = new SystemClock();
        var viewModel = new UnitMapViewModel(console, port, new UnitMapViewModelOptions
        {
            Events = Events,
            Console = console,
            MyUnitId = console.Tree.Ordered.FirstOrDefault(n => n.Name == "7중대")?.Id,
            Clock = clock,
            CurrentOperatorName = $"운영자 {name}",
        });
        console.Changed += (_, _) => viewModel.SetData(console.Tree, console.Devices);

        var canvas = new UnitMapCanvas { Width = CanvasWidth, Height = CanvasHeight, Clock = clock };
        Bind(canvas, UnitMapCanvas.SceneProperty, viewModel, nameof(UnitMapViewModel.Scene));
        Bind(canvas, UnitMapCanvas.SelectedUnitIdProperty, viewModel, nameof(UnitMapViewModel.SelectedUnitId));
        Bind(canvas, UnitMapCanvas.ConfirmPromptProperty, viewModel, nameof(UnitMapViewModel.ConfirmPrompt));
        Bind(canvas, UnitMapCanvas.BarProperty, viewModel, nameof(UnitMapViewModel.Bar));
        Bind(canvas, UnitMapCanvas.LayoutStatusTextProperty, viewModel, nameof(UnitMapViewModel.LayoutStatusText));
        Bind(canvas, UnitMapCanvas.CanRetryLayoutProperty, viewModel, nameof(UnitMapViewModel.CanRetryLayout));
        Bind(canvas, UnitMapCanvas.MoveModeTextProperty, viewModel, nameof(UnitMapViewModel.MoveModeText));
        canvas.Interaction = viewModel;         // 캔버스가 스스로 표면으로 붙고 뷰모델의 포커스 요청을 받는다

        var pane = new GroupBox
        {
            Header = Options.Mode == UnitsMapLayoutMode.TwoClient ? $"운영자 {name} — 부대 관계도" : "부대 관계도",
            Margin = new Thickness(0, 0, 8, 0),
            Content = BuildPaneContent(viewModel, canvas),
        };
        AutomationProperties.SetAutomationId(pane, $"Units.Map.Preview.Client.{name}");
        return new UnitsMapClient(name, console, viewModel, canvas, pane);
    }

    /// <summary>콘솔 툴바 둘째 줄 흉내(레이어 토글 · [배치 초기화]) + 캔버스 + 한 줄 상태. id 는 콘솔 뷰(IMPL-32)와 같다.</summary>
    private static UIElement BuildPaneContent(UnitMapViewModel viewModel, UnitMapCanvas canvas)
    {
        CheckBox Layer(string text, string id, Func<UnitMapLayers, bool> read, Func<UnitMapLayers, bool, UnitMapLayers> write)
        {
            var box = new CheckBox { Content = text, IsChecked = read(viewModel.Layers), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            box.SetResourceReference(FrameworkElement.StyleProperty, "Console.CheckBox");
            AutomationProperties.SetAutomationId(box, id);
            box.Click += (_, _) => viewModel.SetLayers(write(viewModel.Layers, box.IsChecked == true));
            return box;
        }

        var reset = new Button { Content = "배치 초기화", Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2) };
        reset.SetResourceReference(FrameworkElement.StyleProperty, "Console.Button.Mini");
        AutomationProperties.SetAutomationId(reset, "Units.Map.ResetLayout");
        reset.Click += (_, _) => viewModel.RequestResetLayout();

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        toolbar.Children.Add(Layer("계층선", "Units.Map.Layer.Hierarchy", l => l.Hierarchy, (l, v) => l with { Hierarchy = v }));
        toolbar.Children.Add(Layer("인접선", "Units.Map.Layer.Adjacency", l => l.Adjacency, (l, v) => l with { Adjacency = v }));
        toolbar.Children.Add(Layer("장비 배지", "Units.Map.Layer.Devices", l => l.DeviceBadges, (l, v) => l with { DeviceBadges = v }));
        toolbar.Children.Add(reset);

        var status = new ConsoleText { Margin = new Thickness(0, 6, 0, 0), FontSize = 11.5 };
        status.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        AutomationProperties.SetAutomationId(status, "Units.Map.Preview.StatusText");
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(UnitMapViewModel.StatusText)) { Source = viewModel, Mode = BindingMode.OneWay });

        var frame = new Border { BorderThickness = new Thickness(1), Child = canvas };
        frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var dock = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(status, Dock.Bottom);
        dock.Children.Add(toolbar);
        dock.Children.Add(status);
        dock.Children.Add(frame);
        return dock;
    }

    /// <summary>미리보기 전용 단추 — 헤드 시험이 UIA Invoke 로 누른다(운영 쓰기 0 — 전부 가짜 서버).</summary>
    private UIElement BuildControlStrip()
    {
        Button Tool(string text, string id, System.Action run)
        {
            var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 2, 10, 2) };
            button.SetResourceReference(FrameworkElement.StyleProperty, "Console.Button.Mini");
            AutomationProperties.SetAutomationId(button, id);
            button.Click += (_, _) => run();
            return button;
        }

        var strip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        strip.Children.Add(Tool("다른 운영자 쓰기 (F5)", "Preview.Units.OtherWrite", OtherOperatorWrite));
        strip.Children.Add(Tool("다음 쓰기 412 (F6)", "Preview.Units.Next412", Server.FailNextWithConflict));
        strip.Children.Add(Tool("배치 알림 (F7)", "Preview.Units.Notice", Server.PublishNow));
        strip.Children.Add(Tool("서버 모드 전환 (F8)", "Preview.Units.CycleMode", CycleServerMode));
        var mode = new ConsoleText { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        mode.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        AutomationProperties.SetAutomationId(mode, "Preview.Units.Mode");
        ModeText = mode;
        UpdateModeText();
        strip.Children.Add(mode);
        return strip;
    }

    private ConsoleText? ModeText { get; set; }

    private void UpdateModeText()
    {
        if (ModeText is not null) ModeText.Text = $"서버: {Server.Mode} · 버전 {Server.Current.Version} · 쓰기 {Server.Writes}";
    }

    /// <summary>다른 운영자가 첫 운영자의 선택 부대(없으면 7중대)를 옮겼다 — 알림까지.</summary>
    public void OtherOperatorWrite()
    {
        var unit = Main.ViewModel.SelectedUnitId ?? Main.Console.Tree.Ordered.First(n => n.Name == "7중대").Id;
        Server.SimulateOtherWrite(unit, 60, 20);
        UpdateModeText();
    }

    private void CycleServerMode()
    {
        Server.Mode = Server.Mode switch
        {
            UnitsMapLayoutMode.Supported => UnitsMapLayoutMode.Unsupported,
            UnitsMapLayoutMode.Unsupported => UnitsMapLayoutMode.Failing,
            _ => UnitsMapLayoutMode.Supported,
        };
        UpdateModeText();
    }

    /// <summary>
    /// 벤치(TEST-37 ①) — 레일 전환 흉내: 이미 떠 있는 창의 관계도 칸에 <b>새</b> 뷰모델 + 캔버스를 넣고 첫 그림까지.
    /// 창 · HWND 생성은 빼고 잰다(레일 전환은 창을 새로 만들지 않는다). 배치 GET 은 기다리지 않는다(NFR-01).
    /// </summary>
    public async Task<double> MeasureRailSwitchAsync()
    {
        var frame = ((DockPanel)Main.Pane.Content).Children.OfType<Border>().Last();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var console = Main.Console;
        var viewModel = new UnitMapViewModel(console, Server.ForClient("bench", "bench"), new UnitMapViewModelOptions { Console = console, Clock = new SystemClock() });
        var canvas = new UnitMapCanvas { Width = CanvasWidth, Height = CanvasHeight };
        Bind(canvas, UnitMapCanvas.SceneProperty, viewModel, nameof(UnitMapViewModel.Scene));
        canvas.Interaction = viewModel;
        viewModel.SetData(console.Tree, console.Devices);
        _ = viewModel.OpenAsync();
        frame.Child = canvas;
        await System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        watch.Stop();
        canvas.Interaction = null;
        return watch.Elapsed.TotalMilliseconds;
    }

    private void OnWindowKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F5: OtherOperatorWrite(); break;
            case Key.F6: Server.FailNextWithConflict(); break;
            case Key.F7: Server.PublishNow(); break;
            case Key.F8: CycleServerMode(); break;
            default: return;
        }
        UpdateModeText();
        e.Handled = true;
    }

    private static void Bind(DependencyObject target, DependencyProperty property, object source, string path)
        => BindingOperations.SetBinding(target, property, new Binding(path) { Source = source, Mode = BindingMode.OneWay });
}
#endregion
