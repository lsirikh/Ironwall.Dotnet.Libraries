using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Microsoft.Web.WebView2.Wpf;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 지도 3D 경쟁 · 정리 결함(2026-10-20 정적 검토 후보 5 · 6 · 7 · 8 · 13) — 창 · WebView2 실행 없이 실제 MapViewModel · Map3DViewHost 경로로 재현한다.
/// map-3d 는 가짜 HttpMessageHandler. 새 공개 멤버는 리플렉션으로만 써서 고치기 전 코드에도 그대로 붙여 "고치기 전 실패"를 볼 수 있다.
/// </summary>
public class Map3DRaceCleanupScenarioTests
{
    private const string Job = "j-area", Old = "j-old", New = "j-new";

    // ─────────────── 장치 ───────────────

    private sealed class FakeServer : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();
        /// <summary>목록(GET /api/jobs)의 장면 — 요청을 받은 순간의 것을 돌려준다.</summary>
        public List<JObject> Scenes { get; } = new();
        /// <summary>목록의 running(만드는 중) 수.</summary>
        public int Running { get; set; }
        /// <summary>켜면 목록 요청마다 문을 하나 세우고 그 문이 열릴 때 답한다(응답 순서 바꾸기).</summary>
        public bool GateLists { get; set; }
        public List<TaskCompletionSource> ListGates { get; } = new();
        /// <summary>굽기 시작(POST /api/jobs)에 답하지 않는다(올리기가 오래 걸리는 것처럼) — 클라이언트가 끊을 때까지.</summary>
        public bool BakeHangs { get; set; }
        /// <summary>건물 높이 저장(PUT)을 붙잡아 둔다.</summary>
        public TaskCompletionSource? SaveGate { get; set; }

        public int Count(HttpMethod m, string path) { lock (Requests) return Requests.Count(r => r.Method == m && r.Path == path); }

        /// <summary>
        /// 그 목록 요청에 지금 답한다 — 이 STA 스레드에서 끝까지 이어지게 동기화 문맥을 잠시 뺀다(디스패처 문맥이 있으면 HttpClient 의
        /// ConfigureAwait(false) 이어짐이 스레드풀로 넘어가 지도를 다른 스레드에서 만지게 된다. 제품은 OnUiAsync 가 UI 스레드로 옮긴다).
        /// </summary>
        public void ReleaseList(int index)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try { ListGates[index].SetResult(); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? text = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.AbsolutePath;
            lock (Requests) Requests.Add((request.Method, path));
            if (request.Method == HttpMethod.Get && path == "/api/jobs")
            {
                var body = new JObject { ["scenes"] = new JArray(Scenes.Select(s => s.DeepClone()).ToArray()),
                                         ["running"] = new JArray(Enumerable.Range(0, Running).Select(i => new JObject { ["id"] = $"r{i}", ["status"] = "running" }).ToArray()) };
                if (GateLists) { var gate = new TaskCompletionSource(); ListGates.Add(gate); await gate.Task.ConfigureAwait(false); }
                return Json(HttpStatusCode.OK, body);
            }
            if (request.Method == HttpMethod.Post && path == "/api/jobs")
            {
                if (BakeHangs) await Task.Delay(Timeout.Infinite, ct);
                return Json(HttpStatusCode.Accepted, new JObject { ["id"] = New, ["status"] = "queued", ["progress"] = 0 });
            }
            if (request.Method == HttpMethod.Put && path.EndsWith("/height", StringComparison.Ordinal))
            {
                if (SaveGate != null) await SaveGate.Task;
                int id = int.Parse(path.Split('/')[^2]);
                var o = JObject.Parse(text!);
                return Json(HttpStatusCode.OK, new JObject
                {
                    ["building"] = new JObject { ["id"] = id, ["height"] = o.Value<double>("height"), ["height_source"] = "manual", ["height_from"] = "manual",
                                                 ["levels"] = o["floors"], ["origin"] = "map", ["center"] = new JArray(127.0, 37.5), ["manual"] = new JObject { ["uid"] = "u1" } },
                });
            }
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/jobs/", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^1], ["status"] = "running", ["progress"] = 0.1 });
            return Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" });
        }

        private static HttpResponseMessage Json(HttpStatusCode code, JObject o) => new(code) { Content = new StringContent(o.ToString(), Encoding.UTF8, "application/json") };
    }

    private sealed class Rig
    {
        public WiredMapRig W { get; }
        public MapViewModel Vm => W.Vm;
        public FakeServer Server { get; } = new();
        public List<JObject> Posts { get; } = new();
        public PointLatLng Center { get; }
        private readonly string _zoomFile = Path.Combine(Path.GetTempPath(), $"map3d-areas-{Guid.NewGuid():N}.json");

        /// <param name="zoom">17 = 3D 진입 시험, 12 = 굽기 · 목록 시험(미리 준비 줌 아래 — WebView2 를 미리 열지 않게).</param>
        public Rig(double zoom)
        {
            W = WiredMapRig.Create();
            W.Log.Setup(l => l.Warning(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
             .Callback<string, string, string, int>((m, _, _, _) => Logs.Add(m));
            W.Log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
             .Callback<string, string, string, int>((m, _, _, _) => Logs.Add(m));
            Scenario.SetField(Vm, "_isEditModeEnabled", false);
            W.Map.SetEditMode(false);
            W.Map.SetEffectiveZoom(zoom);
            Center = W.Map.Position;
            var settings = new Map3DModel { EntryMode = Map3DModel.EntryManual, EnterZoom = 17, ExitZoom = 16.5, GuideMinSeconds = 0, IsEnabled = true };
            settings.Normalize();
            Scenario.SetField(Vm, "_map3DSettings", settings);
            Scenario.SetField(Vm, "_map3DScenes", new List<Map3DScene>());
            Scenario.SetField(Vm, "_map3DAreaMarkers", new Dictionary<string, Map3DAreaMarker>());
            Scenario.SetField(Vm, "<Map3DAreas>k__BackingField", new ObservableCollection<Map3DAreaItem>());
            Scenario.SetField(Vm, "_map3DAreaZooms", new Map3DAreaZoomStore(_zoomFile));
            foreach (var f in typeof(MapViewModel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (f.Name.StartsWith("_map3D", StringComparison.Ordinal) && f.GetValue(Vm) == null && !f.FieldType.IsAbstract
                    && f.FieldType.IsClass && f.FieldType != typeof(string) && f.FieldType.GetConstructor(Type.EmptyTypes) != null
                    && (f.FieldType.IsGenericType || f.FieldType.Name == "Map3DPerfSamples"))
                    f.SetValue(Vm, Activator.CreateInstance(f.FieldType));
            Scenario.SetField(Vm, "_bhFloorHeightText", "3.0");
            Scenario.SetField(Vm, "_bhFloorsText", "");
            Scenario.SetField(Vm, "_bhHeightText", "");
            Client = new Map3DClient("http://127.0.0.1:8765", Server);
            Scenario.SetField(Vm, "_map3DClient", Client);
            Scenario.SetField(Vm, "_map3DBuildingPost", (Action<string>)(json => Posts.Add(JObject.Parse(json))));
        }

        public Map3DClient Client { get; }
        /// <summary>VM 이 남긴 Info · Warning 로그(실패 진단용).</summary>
        public List<string> Logs { get; } = new();
        public string LogText => string.Join("\n", Logs.Where(l => l.Contains("[Map3D]")));

        public double[] Bbox => new[] { Center.Lng - 0.002, Center.Lat - 0.002, Center.Lng + 0.002, Center.Lat + 0.002 };

        public JObject SceneJson(string id, double dLon = 0)
            => new() { ["id"] = id, ["bbox"] = new JArray(Bbox[0] + dLon, Bbox[1], Bbox[2] + dLon, Bbox[3]), ["created"] = "2026-10-20T01:00:00", ["counts"] = new JObject { ["buildings"] = 3 } };

        /// <summary>그 장면을 미리 불러 둔 3D 로 「3D 로 보기」 → 펼침(재사용 경로 · WebView2 없음).</summary>
        public void Enter3D()
        {
            Server.Scenes.Add(SceneJson(Job));   // 서버 목록에도 있다(저장 뒤 목록 새로 고침이 그 영역을 내리지 않게)
            Scenario.Call(Vm, "ApplyMap3DAreas", (IReadOnlyList<Map3DSceneInfo>)new[]
            {
                new Map3DSceneInfo(Job, "시험 영역", Bbox, null, 2, null),
            });
            foreach (var (name, value) in new (string, object?)[] { ("_job", Job), ("_ready", true), ("_rendered", true), ("_failed", false),
                                                                      ("<ReadyAt>k__BackingField", (DateTime?)DateTime.UtcNow.AddSeconds(-10)) })
                typeof(Map3DViewHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Vm.Map3DHost, value);
            Scenario.Call(Vm, "EvaluateMap3DGate");
            Scenario.Call(Vm, "UpdateMap3DAreaDisplay");
            Vm.EnterMap3DCommand.Execute(null);
            Scenario.Call(Vm, "OnMap3DGuideTick");
            Assert.Equal("3D", Vm.Map3DStatusText);
        }

        public void PickBuilding(int id, double height)
        {
            var msg = Map3DMath.ParseMessage(new JObject { ["type"] = "building", ["id"] = id, ["origin"] = "photo", ["height"] = height, ["height_source"] = "overlay",
                                                           ["height_from"] = "shadow", ["levels"] = null, ["area_m2"] = 320.4, ["lon"] = 127.0, ["lat"] = 37.5,
                                                           ["manual"] = false }.ToString());
            Scenario.Call(Vm, "OnMap3DMessage", msg!);
        }

        /// <summary>새 영역 그리기 끝 → 만들기 판 [만들기].</summary>
        public void BakeArea()
        {
            Scenario.Call(Vm, "TakeMap3DArea", new Map3DArea(Bbox, null));
            Assert.True(Vm.IsMap3DBakeOptionsOpen);
            Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
        }

        public bool Baking => Scenario.GetField<bool>(Vm, "_isMap3DBaking");
        public string Status => Vm.AimStatusMessage ?? "";

        public bool PressEsc()
        {
            var source = new HeadlessSource(W.Deco);
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            return (bool)Scenario.Call(Vm, "TryHandleMap3DKey", e)!;
        }

        public List<JObject> Sent(string type) => Posts.Where(p => p.Value<string>("type") == type).ToList();

        public static async Task<bool> Until(Func<bool> condition, double seconds = 6)
        {
            var end = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                await Task.Delay(20);
            }
            return condition();
        }

        public void Done(Func<string, bool>? ignoreError = null)
        {
            Assert.Equal("none", Vm.Map3DHost.HostMode);
            Assert.Empty(W.Errors.Where(e => ignoreError?.Invoke(e) != true));
            if (File.Exists(_zoomFile)) File.Delete(_zoomFile);
        }
    }

    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public HeadlessSource(Visual root) { RootVisual = root; }
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static void RunAsync(double zoom, Func<Rig, Task> body) => Scenario.StaAsync(async () => { var rig = new Rig(zoom); await body(rig); rig.Done(); });

    // ─────────────── 5. 네이티브 창 전환(SwitchToHwnd) 중 옛 불러오기 실패 ───────────────

    private static void SetHost(Map3DViewHost host, string name, object? value)
        => typeof(Map3DViewHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, value);
    private static object? GetHost(Map3DViewHost host, string name)
        => typeof(Map3DViewHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host);

    /// <summary>
    /// 불러오기가 WebView2 기동을 기다리는 사이 CompositionControl 이 실패해 네이티브 창으로 바뀌면(SwitchToHwnd — 옛 컨트롤을 버리고 새 컨트롤로 같은 장면을 다시 연다)
    /// 버린 컨트롤의 기동이 예외로 끝난다. 그 실패가 새 불러오기를 "실패"(_failed · LoadFailed → VM 은 3D 진입 취소 · 진입 보류)로 만들면 안 된다.
    /// SwitchToHwnd 를 그대로 부르면 새 컨트롤이 실제 WebView2 기동을 시작하므로(시험 금지) 그 메서드가 남기는 상태(새 컨트롤 · 새 기동 대기)를
    /// 필드로 똑같이 만든 뒤 옛 기동을 실패시킨다. 실제 WebView2 컨트롤 객체는 만들기만 하고 기동하지 않는다.
    /// </summary>
    [Theory]
    [InlineData(false)]   // 장면 불러오기(LoadAsync) 중
    [InlineData(true)]    // 미리 데우기(PrewarmAsync) 중
    public void should_not_fail_new_load_when_discarded_control_core_fails_after_switch_to_hwnd(bool prewarm) => Scenario.StaAsync(async () =>
    {
        var host = new Map3DViewHost { UseComposition = false };
        var failures = new List<string>();
        host.LoadFailed += failures.Add;
        var oldCore = new TaskCompletionSource();
        SetHost(host, "_coreTask", oldCore.Task);   // 옛 컨트롤의 기동(실제 EnsureCoreWebView2Async 를 부르지 않게 미리 꽂는다)
        var running = prewarm ? host.PrewarmAsync() : host.LoadAsync("j1", "http://127.0.0.1:8765/view?job=j1&embed=1");
        var old = (WebView2)GetHost(host, "_web")!;

        // SwitchToHwnd 가 남기는 상태 — 옛 컨트롤을 빼고 버린 뒤 새 컨트롤이 같은 장면을 다시 여는 중(새 기동 대기)
        host.Children.Remove(old);
        old.Dispose();
        var fresh = new WebView2();
        SetHost(host, "_web", fresh);
        SetHost(host, "_el", fresh);
        SetHost(host, "_coreTask", new TaskCompletionSource().Task);
        host.Children.Insert(0, fresh);
        if (prewarm) { SetHost(host, "_job", "j1"); SetHost(host, "_url", "http://127.0.0.1:8765/view?job=j1&embed=1"); }

        oldCore.SetException(new ObjectDisposedException("WebView2"));   // 버린 컨트롤의 기동이 끝내 실패
        await running;

        Assert.Empty(failures);                                       // ★ 고치기 전: "3D 화면(WebView2)을 준비하지 못했습니다 …" 한 번
        Assert.True(host.IsLoading, "버린 컨트롤의 실패가 새 불러오기를 실패로 만들었다");
        Assert.Equal("j1", host.Job);
        fresh.Dispose();
    });

    // ─────────────── 6. 화면 비활성화 때 3D 정리 ───────────────

    private static Delegate[] Map3DSavedHandlers()
        => ((Delegate?)typeof(MapSettingsHelper).GetField("Map3DSaved", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null))?.GetInvocationList() ?? Array.Empty<Delegate>();

    /// <summary>
    /// 3D 를 보는 중(이동 단추 누름 · 장면 조회 대기)에 지도 화면이 비활성화(OnDeactivateAsync)되면 3D 를 접고 구독 · 타이머 · 정적 이벤트를 푼다.
    /// 고치기 전: 정리에 3D 가 없어 "3D" 그대로 · 정적 Map3DSaved 가 VM 을 붙잡고 · 이동 · 조회 타이머가 계속 돌았다.
    /// </summary>
    [Fact]
    public void should_leave_3d_and_release_subscriptions_and_timers_when_map_view_deactivates() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig(17);
        var vm = rig.Vm;
        var saved = typeof(MapSettingsHelper).GetEvent("Map3DSaved")!;
        var handler = Delegate.CreateDelegate(typeof(Action), vm, typeof(MapViewModel).GetMethod("OnMap3DSettingsSaved", BindingFlags.Instance | BindingFlags.NonPublic)!);
        try
        {
            saved.AddEventHandler(null, handler);   // LoadMap3DFromSettings 와 같은 구독(설정 파일을 읽지 않게 구독만)
            rig.Enter3D();
            vm.StartMap3DMove(Map3DNavMove.Forward);
            Scenario.Call(vm, "QueueMap3DSceneLookup", new PointLatLng(rig.Center.Lat + 0.01, rig.Center.Lng));
            Assert.True(Scenario.GetField<DispatcherTimer>(vm, "_map3DNavTimer").IsEnabled);
            Assert.True(Scenario.GetField<DispatcherTimer>(vm, "_map3DLookupTimer").IsEnabled);
            Assert.Contains(Map3DSavedHandlers(), d => ReferenceEquals(d.Target, vm));

            var deactivate = typeof(MapViewModel).GetMethod("OnDeactivateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)deactivate.Invoke(vm, new object[] { false, CancellationToken.None })!;

            Assert.Equal("2D", vm.Map3DStatusText);                                        // ★ 고치기 전: "3D"
            Assert.DoesNotContain(Map3DSavedHandlers(), d => ReferenceEquals(d.Target, vm));
            Assert.False(Scenario.GetField<DispatcherTimer>(vm, "_map3DNavTimer").IsEnabled);
            Assert.False(Scenario.GetField<DispatcherTimer>(vm, "_map3DLookupTimer").IsEnabled);
            Assert.False(Scenario.GetField<DispatcherTimer?>(vm, "_map3DGuideTimer")?.IsEnabled == true);
            Assert.False(vm.IsMap3DBusy);
            Assert.NotNull(Scenario.GetField<Map3DClient?>(vm, "_map3DClient"));             // 닫기가 아니면 창구는 남긴다(다시 활성화)
        }
        finally { saved.RemoveEventHandler(null, handler); }
        // 이 장치에는 지도 화면 비활성화의 3D 밖 단계(커스텀 맵 서비스 등)가 없어 그 단계가 "비활성화 실패" 를 남긴다 — 3D 정리와 무관
        rig.Done(e => e.StartsWith("MapViewModel 비활성화 실패", StringComparison.Ordinal));
    });

    /// <summary>닫기(close)면 map-3d 창구도 닫는다 · 다시 불러도 안전.</summary>
    [Fact]
    public void should_dispose_client_and_be_repeatable_when_map3d_cleanup_closes() => RunAsync(17, rig =>
    {
        rig.Enter3D();
        Scenario.Call(rig.Vm, "CleanupMap3D", true);
        Scenario.Call(rig.Vm, "CleanupMap3D", true);
        Assert.Equal("2D", rig.Vm.Map3DStatusText);
        Assert.Null(Scenario.GetField<Map3DClient?>(rig.Vm, "_map3DClient"));
        Assert.False(Scenario.GetField<bool>(rig.Vm, "_map3DLoginSubscribed"));
        return Task.CompletedTask;
    });

    // ─────────────── 7. 영역 목록 응답 순서 뒤바뀜 ───────────────

    /// <summary>
    /// 영역 목록 요청 두 개가 겹쳐 늦게 보낸 것(새 영역 포함)의 답이 먼저 오고, 먼저 보낸 것(새 영역 없음)의 답이 나중에 오면
    /// 옛 목록이 새 영역을 "목록에서 빠짐"(list-missing)으로 내렸다. 요청 순번으로 늦게 온 옛 답을 버린다.
    /// </summary>
    [Fact]
    public void should_keep_new_area_when_older_area_list_answer_arrives_after_newer_one() => RunAsync(12, async rig =>
    {
        rig.Server.GateLists = true;
        rig.Server.Scenes.Add(rig.SceneJson(Old));
        var first = (Task)Scenario.Call(rig.Vm, "RefreshMap3DAreasAsync")!;          // 먼저 보낸 요청 — 옛 목록
        rig.Server.Scenes.Add(rig.SceneJson(New, dLon: 0.01));
        var second = (Task)Scenario.Call(rig.Vm, "RefreshMap3DAreasAsync")!;         // 나중 보낸 요청 — 새 영역 포함
        Assert.True(await Rig.Until(() => rig.Server.ListGates.Count == 2, 3));

        rig.Server.ReleaseList(1);                                          // 나중 요청의 답이 먼저
        await second;
        Assert.True(rig.Vm.Map3DAreas.Any(a => a.Id == New), rig.LogText);

        rig.Server.ReleaseList(0);                                          // 먼저 보낸 요청의 답이 늦게
        await first;
        Assert.Equal(new[] { New, Old }, rig.Vm.Map3DAreas.Select(a => a.Id).OrderBy(s => s, StringComparer.Ordinal).ToArray());   // ★ 고치기 전: New 가 빠짐
        Assert.Contains(Scenario.GetField<List<Map3DScene>>(rig.Vm, "_map3DScenes"), s => s.Id == New);

        rig.Server.GateLists = false;                                                 // 그 뒤 새 요청의 답은 그대로 단다(영역이 정말 빠졌으면 내린다)
        rig.Server.Scenes.RemoveAll(s => s.Value<string>("id") == New);
        await (Task)Scenario.Call(rig.Vm, "RefreshMap3DAreasAsync")!;
        Assert.Equal(new[] { Old }, rig.Vm.Map3DAreas.Select(a => a.Id).ToArray());
    });

    // ─────────────── 8. 「건물 높이」 저장 중 취소 ───────────────

    /// <summary>
    /// 저장(PUT) 중 [취소] · ✕ · Esc — 고치기 전: 판은 닫혀 "취소" 처럼 보이는데 저장은 그대로 끝나 3D 에 저장된 높이로 바뀌었다.
    /// 결정: 저장 중에는 취소를 막는다(단추 흐림 + 한 줄 알림, 판 안내 줄 "저장하는 중…"), 저장이 끝나면 다시 닫을 수 있다.
    /// </summary>
    [Fact]
    public void should_block_cancel_close_and_escape_while_building_height_is_saving() => RunAsync(17, async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";                                          // 미리보기 30 m
        rig.Server.SaveGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = (Task)Scenario.Call(vm, "ApplyBuildingHeightAsync")!;
        await Task.Delay(20);

        vm.CancelBuildingHeightCommand.Execute(null);                                // [취소]
        Assert.True(vm.IsBuildingHeightPanelOpen, "저장 중 [취소]로 판이 닫혔다 — 그런데 저장은 계속된다");
        vm.BuildingHeightPanel.CloseCommand!.Execute(null);                           // ✕
        Assert.True(rig.PressEsc());                                                 // Esc — 판 몫으로 먹고 3D 는 그대로
        Assert.True(vm.IsBuildingHeightPanelOpen);
        Assert.Equal("3D", vm.Map3DStatusText);
        Assert.False(vm.CancelBuildingHeightCommand.CanExecute(null));               // 단추 흐림
        Assert.False(vm.BuildingHeightPanel.CloseCommand!.CanExecute(null));
        Assert.Contains("저장하는 중", vm.BuildingHeightSummary);
        Assert.Contains("저장하는 중", rig.Status);
        Assert.Empty(rig.Sent("building.select"));                                   // 강조를 지우지 않았다(판이 그대로)

        rig.Server.SaveGate.SetResult();
        await save;
        await Task.Delay(20);
        Assert.Single(rig.Sent("building.commit"));
        Assert.Equal(30.0, vm.BuildingHeightBuilding!.Height);                       // 판 = 저장된 값(화면 = 서버)
        Assert.True(vm.CancelBuildingHeightCommand.CanExecute(null));                // 끝나면 다시 닫을 수 있다
        vm.CancelBuildingHeightCommand.Execute(null);
        Assert.False(vm.IsBuildingHeightPanelOpen);
        Assert.Empty(rig.Sent("building.preview").Where(p => p["height"]!.Type == JTokenType.Null));   // 저장된 값 그대로라 되돌릴 미리보기 없음
    });

    // ─────────────── 13. 굽기 시작 시간 초과 → 중복 굽기 ───────────────

    /// <summary>굽기 시작 시간 제한을 짧게(고친 뒤 공개 속성 — 고치기 전에는 없으니 HttpClient 10 초 그대로).</summary>
    private static void ShortBakeStartTimeout(Map3DClient client)
        => typeof(Map3DClient).GetProperty("BakeStartTimeout")?.SetValue(client, TimeSpan.FromMilliseconds(300));

    /// <summary>
    /// 굽기 시작(큰 이미지 본문) 답을 시간 안에 못 받았는데 서버는 이미 굽기를 시작했다 — 같은 영역을 다시 누르면 고치기 전에는 그대로 또 보내
    /// 같은 영역을 두 번 구웠다. 이제는 서버 작업 목록을 먼저 보고 만드는 중이면 막고, 그 영역 장면이 새로 생겼으면 이미 끝났다고 알린다.
    /// </summary>
    [Fact]
    public void should_not_send_same_area_bake_again_when_previous_start_timed_out_and_server_is_baking() => RunAsync(12, async rig =>
    {
        ShortBakeStartTimeout(rig.Client);
        rig.Server.BakeHangs = true;
        rig.BakeArea();
        Assert.True(await Rig.Until(() => !rig.Baking, 15), "굽기 시작 시간 초과가 끝나지 않음");
        Assert.Equal(1, rig.Server.Count(HttpMethod.Post, "/api/jobs"));

        rig.Server.Running = 1;                                                       // 서버는 그 요청으로 만드는 중
        rig.BakeArea();                                                               // 같은 영역 다시
        Assert.True(await Rig.Until(() => !rig.Baking, 15));
        Assert.Equal(1, rig.Server.Count(HttpMethod.Post, "/api/jobs"));              // ★ 고치기 전: 2(같은 영역 두 번 굽기)
        Assert.Contains("아직 하고 있습니다", rig.Status);

        rig.Server.Running = 0;                                                       // 다 만들었다 — 목록에 그 영역 장면이 생겼다
        rig.Server.Scenes.Add(rig.SceneJson(New));
        rig.BakeArea();
        Assert.True(await Rig.Until(() => !rig.Baking, 15));
        Assert.Equal(1, rig.Server.Count(HttpMethod.Post, "/api/jobs"));
        Assert.Contains("이미 끝났습니다", rig.Status);
        Assert.True(await Rig.Until(() => rig.Vm.Map3DAreas.Any(a => a.Id == New), 3), "다 만든 영역이 목록에 나타나지 않음");
    });

    /// <summary>앞 굽기 시작이 시간 초과였어도 서버가 받지 못했으면(만드는 중 없음 · 새 장면 없음) 다시 보낸다 · 다른 영역은 막지 않는다.</summary>
    [Fact]
    public void should_send_again_when_previous_start_timed_out_but_server_has_nothing() => RunAsync(12, async rig =>
    {
        ShortBakeStartTimeout(rig.Client);
        rig.Server.BakeHangs = true;
        rig.BakeArea();
        Assert.True(await Rig.Until(() => !rig.Baking, 15));
        Assert.Contains("답을 받지 못했습니다", rig.Status);                          // "꺼져 있습니다" 가 아니다(서버는 받았을 수 있다)
        rig.BakeArea();
        Assert.True(await Rig.Until(() => !rig.Baking, 15));
        Assert.Equal(2, rig.Server.Count(HttpMethod.Post, "/api/jobs"));
    });
}
