using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
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
using Rig = GMaps.MapEdit.Tests.Map3DEntryScenarioTests.Rig;
using X = GMaps.MapEdit.Tests.Map3DExitInputScenarioTests;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 진입 · 이탈(영역 A, 2026-10-19 전수 시나리오) — 기존 Map3DEntryScenarioTests · Map3DExitInputScenarioTests 가 덮지 않은 길.
/// 설정 저장 이벤트로 3D 끔 · 다 만든 뒤 안내 · 불러오는 중 장면 진입(ready → rendered · 시간 초과) · 상태 알림 · 거절된 「영역 보기」 뒤 돌아갈 자리 ·
/// 다른 영역 「이 영역 3D 로 보기」 · 네이티브 창 ESC · 3D 카메라 줌 · 좌표 칩 고도 · 축척 · 남은 벤더 끌기 · 로컬 누름 판정 · 영역이 사라질 때 ·
/// 미리 불러오기 · 입력 모드 · 만들기 취소/실패 뒤 게이트 · 네이티브 창 전환.
/// 창 · WebView2 는 만들지 않는다 — 미리 불러오기는 가짜 IWebView2(기동이 끝나지 않음)를 꽂아 탐색까지 가지 않게 한다.
/// </summary>
public class Map3DEntryExitMoreScenarioTests
{
    private const string AreaA = "area-a", AreaB = "area-b";

    // ─────────────── 장치 ───────────────

    private static void RunAsync(string mode, Func<Rig, Task> body) => Scenario.StaAsync(async () =>
    {
        var rig = new Rig(mode);
        await body(rig);
        rig.Done();
    });

    private static readonly BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void SetHost(Rig rig, string name, object? value) => typeof(Map3DViewHost).GetField(name, Inst)!.SetValue(rig.Vm.Map3DHost, value);
    private static object? GetHost(Rig rig, string name) => typeof(Map3DViewHost).GetField(name, Inst)!.GetValue(rig.Vm.Map3DHost);

    /// <summary>3D 화면 페이지가 보낸 것처럼(WebMessageReceived → Map3DViewHost.OnWebMessage — ready · rendered 를 호스트가 먼저 적는다).</summary>
    private static void FromPage(Rig rig, JObject msg)
        => typeof(Map3DViewHost).GetMethod("OnWebMessage", Inst)!.Invoke(rig.Vm.Map3DHost, new object[] { msg.ToString(Newtonsoft.Json.Formatting.None) });

    /// <summary>
    /// 가짜 WebView2 — LoadAsync 가 EnsureWebView 에서 진짜 WebView2 를 만들지 않고, 기동(EnsureCoreWebView2)이 끝나지 않아 탐색까지 가지 않는다.
    /// HostMode 는 "none" 그대로(_el 없음).
    /// </summary>
    private static void FakeWebView(Rig rig)
    {
        SetHost(rig, "_web", Mock.Of<IWebView2>());
        SetHost(rig, "_coreTask", new TaskCompletionSource().Task);
    }

    private static List<string> Capture(Rig rig, bool warnings = false)
    {
        var lines = new List<string>();
        if (warnings)
            rig.W.Log.Setup(l => l.Warning(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
               .Callback<string, string, string, int>((m, _, _, _) => lines.Add(m));
        else
            rig.W.Log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
               .Callback<string, string, string, int>((m, _, _, _) => lines.Add(m));
        return lines;
    }

    private static Map3DCamera? Return(Rig rig) => Scenario.GetField<Map3DCamera?>(rig.Vm, "_map3DReturn");
    private static string? AreaViewJob(Rig rig) => Scenario.GetField<string?>(rig.Vm, "_map3DAreaViewJob");
    private static bool Baking(Rig rig) => Scenario.GetField<bool>(rig.Vm, "_isMap3DBaking");

    private static PointLatLng CenterOf(Map3DSceneInfo s) => new((s.Bbox[1] + s.Bbox[3]) / 2, (s.Bbox[0] + s.Bbox[2]) / 2);

    private static void AssertAt(Rig rig, PointLatLng where, double zoom, string because)
    {
        Assert.True(zoom == rig.W.Map.EffectiveZoom, $"{because}: 줌 {zoom} 이어야 하는데 {rig.W.Map.EffectiveZoom}");
        Assert.True(Math.Abs(rig.W.Map.Position.Lat - where.Lat) < 1e-6 && Math.Abs(rig.W.Map.Position.Lng - where.Lng) < 1e-6,
                    $"{because}: 자리 ({where.Lat:F6},{where.Lng:F6}) 이어야 하는데 ({rig.W.Map.Position.Lat:F6},{rig.W.Map.Position.Lng:F6})");
    }

    /// <summary>가짜 map-3d 서버(굽기 · 진행 조회 · 취소 · 목록 · 지우기).</summary>
    private sealed class FakeServer : HttpMessageHandler
    {
        public string Status { get; set; } = "running";
        public List<JObject> Scenes { get; } = new();
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();
        private int _jobs;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Requests) Requests.Add((request.Method, path));
            if (request.Method == HttpMethod.Post && path == "/api/jobs")
                return Json(HttpStatusCode.Accepted, new JObject { ["id"] = $"j-new{++_jobs}", ["status"] = "queued", ["progress"] = 0 });
            if (request.Method == HttpMethod.Get && path == "/api/jobs")
                return Json(HttpStatusCode.OK, new JObject { ["scenes"] = new JArray(Scenes.ToArray()), ["running"] = new JArray() });
            if (request.Method == HttpMethod.Post && path.EndsWith("/cancel", StringComparison.Ordinal))
            {
                Status = "cancelled";
                return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^2], ["result"] = "requested", ["status"] = Status });
            }
            if (request.Method == HttpMethod.Delete && path.StartsWith("/api/jobs/", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^1], ["deleted"] = true });
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/jobs/", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^1], ["status"] = Status, ["progress"] = 0.4, ["message"] = Status == "error" ? "OSM 조회 실패" : "OSM 조회" });
            return Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" });
        }

        private static Task<HttpResponseMessage> Json(HttpStatusCode code, JObject o)
            => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(o.ToString(), Encoding.UTF8, "application/json") });
    }

    private static FakeServer UseServer(Rig rig)
    {
        var server = new FakeServer();
        Scenario.SetField(rig.Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", server));
        return server;
    }

    /// <summary>새 영역(지도 가운데에서 동쪽으로 dLon) 그리기 끝 → 만들기 판 [만들기].</summary>
    private static void BakeNewArea(Rig rig, double dLon = 0.03)
    {
        var c = rig.Center;
        Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(new[] { c.Lng + dLon - 0.001, c.Lat - 0.001, c.Lng + dLon + 0.001, c.Lat + 0.001 }, null));
        Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
        rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
        Assert.True(Baking(rig), "만들기를 시작하지 않음");
    }

    private static async Task<bool> Until(Func<bool> condition, double seconds = 6)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return condition();
    }

    // ─────────────── A-08 설정 저장으로 3D 끔 ───────────────

    /// <summary>
    /// 설정 화면 「3D 지도」에서 3D 를 끄고 저장 — 실제 저장(MapSettingsHelper.SaveMap3DAsync → Map3DSaved → OnMap3DSettingsSaved → 파일 다시 읽기).
    /// 설정 파일은 이 시험 bin 의 Config\appsettings.json(앱 설정 아님) — 원본을 떠 두고 끝나면 되돌린다. 정적 이벤트 구독도 끝나면 뗀다.
    /// </summary>
    [Fact]
    public void should_return_to_2d_with_live_input_and_cancel_pending_when_settings_saved_with_3d_off()
        => RunAsync(Map3DModel.EntryManual, async rig =>
        {
            var file = AppSettingsLocation.PrepareAppSettingsWrite();
            var backup = File.Exists(file) ? File.ReadAllText(file) : null;
            var handler = (Action)Delegate.CreateDelegate(typeof(Action), rig.Vm, typeof(MapViewModel).GetMethod("OnMap3DSettingsSaved", Inst)!);
            MapSettingsHelper.Map3DSaved += handler;
            try
            {
                if (backup == null) File.WriteAllText(file, "{ \"AppSettings\": { } }");
                var off = new Map3DModel { IsEnabled = false, EntryMode = Map3DModel.EntryManual };

                // ① 3D 를 보는 중 → 2D · 입력 살아 있음
                X.Enter(rig);
                await MapSettingsHelper.SaveMap3DAsync(off);
                Assert.False(rig.Shown, "3D 를 끄고 저장했는데 3D 가 그대로");
                X.AssertMap2DInputAlive(rig, "설정 끔(3D 보던 중)");
                Assert.False(rig.Vm.IsMap3DEnterButtonVisible, "3D 를 껐다 — 「3D 로 보기」 없음");
                Assert.False(Scenario.GetField<Map3DModel>(rig.Vm, "_map3DSettings").IsEnabled, "파일에서 다시 읽은 설정");

                // ② 준비 중(Pending) → 취소 · 안내 닫힘
                rig.UseSettings(Map3DModel.EntryManual);
                rig.W.Map.Position = rig.Center;
                rig.SetZoom(17);
                rig.Gate();
                rig.PressEnter();
                Assert.Equal("Pending", rig.State);
                await MapSettingsHelper.SaveMap3DAsync(off);
                Assert.Equal("Off", rig.State);
                Assert.False(rig.Vm.IsMap3DBusy, "준비 안내가 남았다");
                rig.Tick();
                Assert.False(rig.Shown);
                Assert.Null(Return(rig));
            }
            finally
            {
                MapSettingsHelper.Map3DSaved -= handler;
                if (backup != null) File.WriteAllText(file, backup); else if (File.Exists(file)) File.Delete(file);
            }
        });

    // ─────────────── A-09 다 만든 뒤 안내 ───────────────

    [Theory]
    [InlineData(Map3DModel.EntryAuto, "3D 준비됨 — 줌 17 이상에서 3D")]
    [InlineData(Map3DModel.EntryManual, "3D 준비됨 — 지도 왼쪽 위 「3D 로 보기」로 들어갑니다")]
    public void should_tell_how_to_enter_by_entry_mode_when_bake_done(string mode, string expected)
        => RunAsync(mode, async rig =>
        {
            var server = UseServer(rig);
            rig.SetZoom(12);   // 미리 준비 줌 밑 — 다 만든 뒤 게이트가 3D 화면(WebView2)을 미리 열지 않게
            BakeNewArea(rig);
            server.Status = "done";
            Assert.True(await Until(() => !Baking(rig), 4), "다 만들었는데 아직 만드는 중");
            Assert.Equal(expected, rig.Status);
        });

    // ─────────────── A-21 · A-22 불러오는 중 장면 진입 ───────────────

    [Fact]
    public void should_reveal_after_ready_then_rendered_when_entered_while_scene_loading()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var infos = Capture(rig);
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostLoading(AreaA);   // 미리 불러오는 중(ready 전)
            rig.Gate();
            rig.PressEnter();
            Assert.Equal("Pending", rig.State);
            Assert.Contains(infos, l => l.StartsWith($"[Map3D] 진입 시작 job={AreaA}", StringComparison.Ordinal) && l.Contains("preloaded=True"));
            rig.Tick();
            Assert.Equal("Pending", rig.State);

            FromPage(rig, new JObject { ["type"] = "ready", ["job"] = "other-job" });   // 내려놓은 옛 페이지의 늦은 ready — 무시
            Assert.False(rig.Vm.Map3DHost.IsReadyFor(AreaA));
            Assert.Equal("Pending", rig.State);

            FromPage(rig, new JObject { ["type"] = "ready", ["job"] = AreaA });
            Assert.Equal("Pending", rig.State);   // rendered 를 기다린다(오버레이 적용 + 한 프레임)
            Assert.Equal(Map3DGuide.StageText("rendering"), rig.Vm.Map3DBusyDetail);
            rig.Tick();
            Assert.Equal("Pending", rig.State);

            FromPage(rig, new JObject { ["type"] = "rendered", ["job"] = AreaA, ["items"] = 3 });
            Assert.True(rig.Shown, "rendered 를 받았는데 펼치지 않음");
            Assert.False(rig.Vm.IsMap3DBusy);
            Assert.Contains(infos, l => l == $"[Map3D] 3D 표시 job={AreaA} host=none");
            Assert.Contains(infos, l => l.StartsWith("[Map3D] 전환 시간 total=", StringComparison.Ordinal) && l.EndsWith("(prewarmed=false, preloaded=true)", StringComparison.Ordinal));
        });

    [Fact]
    public void should_warn_and_reveal_when_rendered_not_received_within_timeout()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var warnings = Capture(rig, warnings: true);
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostLoading(AreaA);
            rig.Gate();
            rig.PressEnter();
            FromPage(rig, new JObject { ["type"] = "ready", ["job"] = AreaA });
            rig.Tick();
            Assert.Equal("Pending", rig.State);
            Assert.Empty(warnings);

            SetHost(rig, "<ReadyAt>k__BackingField", (DateTime?)DateTime.UtcNow - Map3DGuide.RenderedTimeout - TimeSpan.FromMilliseconds(50));
            rig.Tick();   // 안내 타이머 50 ms 틱 — 1.5 s 가 지났다
            Assert.True(rig.Shown, "rendered 가 안 와도 시간이 지나면 펼친다");
            Assert.Single(warnings, w => w == $"[Map3D] rendered 를 1.5 s 안에 받지 못함 → 그냥 펼침 job={AreaA}");
        });

    [Fact]
    public void should_show_page_progress_while_waiting_for_ready_when_entered_while_scene_loading()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostLoading(AreaA);
            rig.Gate();
            rig.PressEnter();
            FromPage(rig, new JObject { ["type"] = "progress", ["stage"] = "model", ["pct"] = 42 });
            Assert.Equal(Map3DGuide.EnterTitle, rig.Vm.Map3DBusyText);
            Assert.Equal(Map3DGuide.WithPercent(Map3DGuide.StageText("model"), 42), rig.Vm.Map3DBusyDetail);
            Assert.Equal(42d, rig.Vm.Map3DBusyPercent);
            Assert.Equal("Pending", rig.State);
        });

    [Fact]
    public void should_stay_2d_and_keep_page_parked_when_preloaded_scene_becomes_ready_in_2d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostLoading(AreaA);   // 미리 불러오는 중 — 2D
            rig.Gate();
            FromPage(rig, new JObject { ["type"] = "ready", ["job"] = AreaA });
            FromPage(rig, new JObject { ["type"] = "rendered", ["job"] = AreaA });
            Assert.Equal("Off", rig.State);   // 미리 불러온 장면이 ready 여도 저절로 펼치지 않는다
            Assert.False(rig.Vm.IsMap3DBusy);
            Assert.True(rig.Vm.Map3DHost.IsRenderedFor(AreaA));
            rig.PressEnter();
            Map3DEntryScenarioTests.SettleEntry(rig);   // 다 그려 둔 장면 — 재사용(안내 0.4 s → 이 장치는 0)
            Assert.True(rig.Shown);
        });

    [Fact]
    public void should_cancel_pending_hold_reentry_and_tell_reason_when_page_reports_load_error()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryAuto, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA, enterZoom: 19));   // 자동이어도 진입 줌 아래 — 단추로 들어간다
            rig.HostLoading(AreaA);
            rig.Gate();
            rig.PressEnter();
            FromPage(rig, new JObject { ["type"] = "error", ["job"] = "other-job", ["reason"] = "scene-broken" });   // 다른 장면 — 무시
            Assert.Equal("Pending", rig.State);
            FromPage(rig, new JObject { ["type"] = "error", ["job"] = AreaA, ["reason"] = "scene-broken" });
            Assert.Equal("Off", rig.State);
            Assert.False(rig.Vm.IsMap3DBusy);
            Assert.Equal(Map3DGuide.SceneLoadFailedText("scene-broken"), rig.Status);
            Assert.True(Scenario.GetField<bool>(rig.Vm, "_map3DEntrySuppressed"), "같은 줌에서 곧바로 다시 시도하지 않는다");
            Assert.Single(rig.Vm.Map3DAreas);   // 깨진 장면은 지운 장면이 아니다 — 영역은 남는다
        });

    // ─────────────── A-24 상태 바뀔 때 알림 ───────────────

    [Fact]
    public void should_notify_status_exit_zoom_and_enter_bindings_when_state_changes()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            rig.Vm.IsNotifying = true;   // 생성자 없이 만들면 Caliburn 통지 스위치가 꺼져 있다
            var names = new List<string>();
            rig.Vm.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? "");
            var all = new[] { nameof(MapViewModel.Map3DStatusText), nameof(MapViewModel.IsMap3DShown), nameof(MapViewModel.IsMap3DZoomControlVisible), nameof(MapViewModel.IsMap3DEnterButtonVisible) };

            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            names.Clear();
            rig.Gate();
            Assert.Contains(nameof(MapViewModel.IsMap3DEnterButtonVisible), names);   // 화면 가운데가 영역 안으로

            names.Clear();
            rig.PressEnter();   // Off → Pending
            Assert.All(all, n => Assert.Contains(n, names));
            names.Clear();
            rig.Tick();         // Pending → Shown
            Assert.All(all, n => Assert.Contains(n, names));
            Assert.Equal("3D", rig.Vm.Map3DStatusText);
            names.Clear();
            rig.PressExit();    // Shown → Off
            Assert.All(all, n => Assert.Contains(n, names));
            Assert.Equal("2D", rig.Vm.Map3DStatusText);
        });

    // ─────────────── A-28 거절된 「이 영역 3D 로 보기」 뒤 돌아갈 자리 ───────────────

    /// <summary>
    /// ★ 결함 D-1: 편집 모드 중 「이 영역 3D 로 보기」는 거절되는데 그 호출이 잡은 돌아갈 자리(_map3DReturn)와 영역 보기 표지가 남아,
    /// 편집을 끄고 다른 자리 · 줌 18 에서 「3D 로 보기」로 들어갔다 나가면 거절된 누름 전 자리(줌 17)로 돌아갔다.
    /// </summary>
    [Fact]
    public void should_return_to_button_press_spot_when_area_view_was_refused_before_in_edit_mode()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var areaB = rig.AreaAround(AreaB, dLon: 0.02);
            rig.Areas(rig.AreaAround(AreaA, dLon: 0.05), areaB);
            Scenario.SetField(rig.Vm, "_isEditModeEnabled", true);
            rig.W.Map.SetEditMode(true);

            rig.ViewArea(AreaA);   // 편집 중 — 거절
            Assert.Equal("Off", rig.State);
            Assert.Contains("들어가지 않습니다", rig.Status);

            Scenario.SetField(rig.Vm, "_isEditModeEnabled", false);
            rig.W.Map.SetEditMode(false);
            var spot = CenterOf(areaB);
            rig.W.Map.Position = spot;
            rig.SetZoom(18);
            rig.HostReady(AreaB);   // B 장면은 이미 열어 둔 것으로 — 게이트가 미리 불러오기(WebView2)를 하지 않게
            rig.Gate();
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
            rig.PressEnter();
            Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.True(rig.Shown);
            Assert.Equal(AreaB, rig.Job);

            rig.PressExit();
            AssertAt(rig, spot, 18, "「3D 로 보기」를 누른 자리로 돌아와야 한다");
        });

    /// <summary>D-1 자동(줌) 변형 — 거절된 「영역 보기」가 옮겨 둔 자리에서 편집을 끄면 게이트가 들어간다. 나가면 그 진입 직전 자리(영역 가운데 · 줌 17.5).</summary>
    [Fact]
    public void should_return_to_gate_entry_spot_when_area_view_was_refused_then_edit_mode_ended_in_auto()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryAuto, rig =>
        {
            var areaA = rig.AreaAround(AreaA, dLon: 0.05);
            rig.Areas(areaA);
            rig.HostReady(AreaA);
            Scenario.SetField(rig.Vm, "_isEditModeEnabled", true);
            rig.W.Map.SetEditMode(true);
            rig.ViewArea(AreaA);   // 거절 — 지도는 영역 가운데 · 줌 17.5 로 옮겨졌다
            Assert.Equal("Off", rig.State);
            var moved = rig.W.Map.Position;
            double movedZoom = rig.W.Map.EffectiveZoom;

            Scenario.SetField(rig.Vm, "_isEditModeEnabled", false);
            rig.W.Map.SetEditMode(false);
            rig.Gate();
            Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.True(rig.Shown, "자동 — 진입 줌 위 · 영역 안이면 게이트가 들어간다");
            rig.PressExit();
            AssertAt(rig, moved, movedZoom, "게이트가 들어온 직전 자리로 돌아와야 한다");
        });

    // ─────────────── A-29 · A-30 · A-31 다른 영역 「이 영역 3D 로 보기」 ───────────────

    [Fact]
    public void should_open_other_area_and_return_to_spot_before_first_entry_when_area_view_chosen_in_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var areaB = rig.AreaAround(AreaB, dLon: 0.02);
            rig.Areas(rig.AreaAround(AreaA), areaB);
            rig.HostReady(AreaA);
            rig.Gate();
            rig.PressEnter();
            Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.Equal(AreaA, rig.Job);

            rig.HostReady(AreaB);   // B 장면은 이미 열어 둔 것으로(WebView2 없이 재사용 경로)
            rig.ViewArea(AreaB);
            Assert.Equal(AreaB, rig.Job);
            Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.True(rig.Shown);
            Assert.Equal(AreaB, rig.Job);

            rig.PressExit();
            AssertAt(rig, rig.Center, 17, "처음 3D 로 들어가기 전 자리로");
        });

    [Fact]
    public void should_open_other_area_and_return_to_spot_before_first_area_view_when_chosen_while_pending()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA, dLon: 0.05), rig.AreaAround(AreaB, dLon: 0.02));
            rig.HostReady(AreaA);
            rig.SetZoom(12);
            rig.Gate();
            rig.ViewArea(AreaA);   // 준비 중(안내 표시) — 지도는 A 가운데로 옮겨졌다
            Assert.Equal("Pending", rig.State);

            rig.HostReady(AreaB);
            rig.ViewArea(AreaB);   // 준비 중에 다른 영역
            Assert.Equal("Pending", rig.State);
            Assert.Equal(AreaB, rig.Job);
            Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.True(rig.Shown);

            rig.PressExit();
            AssertAt(rig, rig.Center, 12, "처음 「영역 보기」를 누르기 전 자리로");
        });

    [Fact]
    public void should_refuse_area_view_with_reason_and_leave_no_return_spot_when_baking()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA, dLon: 0.05));
            rig.HostReady(AreaA);
            Scenario.SetField(rig.Vm, "_isMap3DBaking", true);
            rig.ViewArea(AreaA);
            Assert.Equal("Off", rig.State);
            Assert.Contains("만드는 중", rig.Status);
            Assert.False(rig.Vm.IsMap3DBusy, "들어가지 않으니 전환 안내도 없다");
            Assert.Null(Return(rig));
            Assert.Null(AreaViewJob(rig));
            Scenario.SetField(rig.Vm, "_isMap3DBaking", false);
        });

    // ─────────────── A-41 네이티브 창 3D 의 ESC ───────────────

    [Fact]
    public void should_exit_hold_reentry_and_restore_input_when_esc_pressed_in_native_window_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryAuto, rig =>
        {
            var infos = Capture(rig);
            X.Enter(rig);
            SetHost(rig, "_el", new Border());   // 네이티브 창(WebView2 HWND) 3D 인 것처럼 — HostMode "hwnd"
            Assert.Equal("hwnd", rig.Vm.Map3DHost.HostMode);
            try
            {
                Assert.True(rig.PressEsc(), "네이티브 창 3D 는 ESC 로 나간다(그 창이 「나가기」 단추를 가린다)");
                X.AssertMap2DInputAlive(rig, "네이티브 창 ESC");
                Assert.Contains(infos, l => l.StartsWith("[Map3D] 2D 복귀(esc) zoom=", StringComparison.Ordinal));
                rig.Gate(); rig.Tick();
                Assert.False(rig.Shown, "같은 줌에서 곧바로 다시 들어가지 않는다(보류)");
                Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
                Assert.False(rig.PressEsc(), "2D 에서 ESC 는 3D 경로가 받지 않는다");
            }
            finally { SetHost(rig, "_el", null); }
        });

    // ─────────────── A-52 3D 카메라 줌 ───────────────

    [Theory]
    [InlineData(10d)]
    [InlineData(3d)]
    [InlineData(16.4)]
    public void should_stay_in_3d_when_3d_camera_zoom_drops_below_exit_zoom(double zoom)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryAuto, rig =>
        {
            X.Enter(rig);
            X.Camera3D(rig, 15, zoom);
            Assert.True(rig.Shown, $"3D 는 줌으로 나가지 않는다(카메라 줌 {zoom})");
            rig.Gate();
            Assert.True(rig.Shown);
            rig.PressExit();
            Assert.Equal(17d, rig.W.Map.EffectiveZoom);
        });

    // ─────────────── A-54 줌 컨트롤 화면 바인딩 ───────────────

    [Fact]
    public void should_bind_zoom_control_visibility_to_3d_state_when_map_view_read()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));
        int at = xaml.IndexOf("<ctrl:MapZoomControl", StringComparison.Ordinal);
        Assert.True(at > 0, "지도 줌 컨트롤이 없다");
        string block = xaml.Substring(at, xaml.IndexOf("/>", at, StringComparison.Ordinal) - at);
        Assert.Contains("Visibility=\"{Binding IsMap3DZoomControlVisible, Converter={StaticResource BooleanToVisibilityConverter}}\"", block);
        Assert.Equal(1, CountOf(xaml, "<ctrl:MapZoomControl"));   // 다른 줌 컨트롤이 3D 위에 남지 않는다
        Assert.Contains("AutomationProperties.ItemStatus=\"{Binding Map3DStatusText}\"", xaml);   // UIA "3D"/"2D"
    }

    private static int CountOf(string s, string part)
    {
        int n = 0;
        for (int i = s.IndexOf(part, StringComparison.Ordinal); i >= 0; i = s.IndexOf(part, i + part.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string? path = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    // ─────────────── A-56 좌표 칩 고도 · A-58 축척 ───────────────

    [Fact]
    public void should_show_cursor_coordinate_and_altitude_only_in_3d_when_cursor_messages_arrive()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var cursor = new JObject { ["type"] = "cursor", ["lon"] = rig.Center.Lng + 0.001, ["lat"] = rig.Center.Lat + 0.001, ["alt"] = 58.33 };
            X.From3D(rig, cursor);
            Assert.Null(rig.Vm.CurrentAltitudeText);   // 2D 에서는 받지 않는다(늦게 온 메시지)

            X.Enter(rig);
            X.From3D(rig, cursor);
            Assert.Equal("58.3 m", rig.Vm.CurrentAltitudeText);
            Assert.False(string.IsNullOrEmpty(rig.Vm.CurrentMGRS));
            X.From3D(rig, new JObject { ["type"] = "cursor", ["lon"] = rig.Center.Lng, ["lat"] = rig.Center.Lat });   // 하늘 · 땅 밖(고도 없음)
            Assert.Null(rig.Vm.CurrentAltitudeText);
            X.From3D(rig, new JObject { ["type"] = "cursor", ["lon"] = rig.Center.Lng, ["lat"] = rig.Center.Lat, ["alt"] = -3.04 });
            Assert.Equal("-3.0 m", rig.Vm.CurrentAltitudeText);

            rig.PressExit();
            Assert.Null(rig.Vm.CurrentAltitudeText);   // 나가면 고도 줄을 숨긴다
        });

    [Fact]
    public void should_draw_scale_from_3d_camera_and_restore_2d_scale_when_exited()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            Scenario.Call(rig.Vm, "CreateScaleBar");
            string? scale2D = rig.Vm.Scale;
            Assert.False(string.IsNullOrEmpty(scale2D));

            X.Enter(rig);
            X.Camera3D(rig, 20, 18.3);
            var bar = Map3DMath.ScaleBar(Map3DMath.MetersPerPixel(18.3, rig.Center.Lat))!.Value;
            Assert.Equal(bar.Label, rig.Vm.Scale);
            X.Camera3D(rig, 20, 15.2);
            Assert.Equal(Map3DMath.ScaleBar(Map3DMath.MetersPerPixel(15.2, rig.Center.Lat))!.Value.Label, rig.Vm.Scale);

            rig.PressExit();
            Assert.Equal(scale2D, rig.Vm.Scale);   // 2D 줌 그대로 — 2D 축척으로
        });

    // ─────────────── A-64 남은 벤더 끌기 · A-65 로컬 누름 판정 ───────────────

    private static readonly FieldInfo CoreField = typeof(GMapControl).GetField("_core", Inst)!;

    /// <summary>
    /// ★ 결함 D-3: 3D 가 펼쳐질 때 2D 를 끌던 중이면(벤더 코어 _core.IsDragging) 뗌을 3D 화면이 받아 끌기가 남는다. 옛 ResetTransientInput 은
    /// 이 상태를 비우지 않았다 — 벤더 코어는 끌기 중 정수 줌 · 위치 이동(GoToCurrentPosition)을 받지 않고, 벤더 휠 줌도 끌기 중에는 하지 않는다.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_clear_vendor_core_drag_when_exited_after_drag_left_over(bool controlDraggingToo)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var warnings = Capture(rig, warnings: true);
            X.Enter(rig);
            var map = rig.W.Map;
            var core = CoreField.GetValue(map)!;
            var coreDragging = core.GetType().GetField("IsDragging")!;
            coreDragging.SetValue(core, true);   // 펼치기 직전 2D 끌기 — 뗌은 3D 화면이 받았다
            if (controlDraggingToo) Scenario.SetField(map, "<IsDragging>k__BackingField", true);

            rig.PressExit();
            Assert.False((bool)coreDragging.GetValue(core)!, "나간 뒤에도 벤더 코어 끌기가 남았다");
            Assert.False(map.IsDragging, "나간 뒤에도 지도 끌기 표시가 남았다");
            Assert.Contains(warnings, w => w.StartsWith("[Map3D] 나가며 남은 2D 입력 상태 정리:", StringComparison.Ordinal) && w.Contains("coreDrag"));
            X.AssertMap2DInputAlive(rig, "남은 코어 끌기");

            rig.SetZoom(18);   // 정수 줌 — 코어가 받는다
            Assert.Equal(18, (int)core.GetType().GetProperty("Zoom")!.GetValue(core)!);
        });

    [Fact]
    public void should_restore_local_hit_test_false_when_it_was_false_before_entering()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            rig.W.Map.IsHitTestVisible = false;   // 들어가기 전에 다른 코드가 꺼 둠(로컬 값)
            X.Enter(rig);
            rig.PressExit();
            Assert.Equal(false, rig.W.Map.ReadLocalValue(UIElement.IsHitTestVisibleProperty));   // 막기 전 값 그대로 — 3D 가 켜 버리지 않는다
            rig.W.Map.ClearValue(UIElement.IsHitTestVisibleProperty);
        });

    // ─────────────── A-66 영역이 사라져 나갈 때 ───────────────

    public enum Gone { Deleted, ListMissing, SceneMissing }

    [Theory]
    [InlineData(Gone.Deleted)]
    [InlineData(Gone.ListMissing)]
    [InlineData(Gone.SceneMissing)]
    public void should_restore_2d_input_and_hold_reentry_when_shown_area_disappears(Gone gone)
        => RunAsync(Map3DModel.EntryAuto, async rig =>
        {
            var server = UseServer(rig);
            var infos = Capture(rig);
            X.Enter(rig);
            switch (gone)
            {
                case Gone.Deleted:   // 배지 · 목록 「지우기」 → 확인
                    Scenario.SetField(rig.Vm, "_pendingMap3DAreaDelete", rig.Vm.Map3DAreas.Single(a => a.Id == "area-a"));
                    await rig.Vm.HandleAsync(new CallDeleteMap3DAreaProcessMessageModel(), CancellationToken.None);
                    Assert.Contains(server.Requests, r => r.Method == HttpMethod.Delete && r.Path == "/api/jobs/area-a");
                    break;
                case Gone.ListMissing: rig.Areas(); break;   // 서버 목록에서 빠짐(다른 곳에서 지움)
                case Gone.SceneMissing:
                    X.From3D(rig, new JObject { ["type"] = "error", ["job"] = "area-a", ["reason"] = Map3DGuide.SceneMissingReason });
                    break;
            }
            X.AssertMap2DInputAlive(rig, gone.ToString());
            Assert.Contains(infos, l => l.StartsWith("[Map3D] 2D 입력 상태(", StringComparison.Ordinal) && l.Contains(" · now) hit=True"));
            Assert.Empty(rig.Vm.Map3DAreas);
            Assert.Null(rig.Vm.Map3DHost.Job);   // 열어 둔 장면도 내려놓음
            rig.Gate(); rig.Tick();
            Assert.False(rig.Shown);
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
        });

    // ─────────────── A-70 · A-71 · A-72 미리 불러오기 ───────────────

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]   // 수동이어도 미리 불러온다(지금 동작 — 보고서 "결정 필요")
    public void should_preload_once_and_enter_without_new_navigation_when_zoom_reaches_preload_zoom_inside_area(string mode)
        => Map3DEntryScenarioTests.Run(mode, rig =>
        {
            var infos = Capture(rig);
            FakeWebView(rig);
            rig.Areas(rig.AreaAround(AreaA, enterZoom: 19));   // 진입 19 · 미리 준비 = min(15, 18) = 15 · 지도 줌 17
            rig.Gate();
            Assert.Equal("Off", rig.State);
            Assert.Equal(AreaA, rig.Vm.Map3DHost.Job);
            Assert.True(rig.Vm.Map3DHost.IsLoading);
            Assert.Single(infos, l => l.StartsWith($"[Map3D] 미리 불러오기 job={AreaA} zoom=17.0", StringComparison.Ordinal));
            var url = (string?)GetHost(rig, "_url");
            Assert.Contains(AreaA, url);

            rig.Gate(); rig.SetZoom(18); rig.Gate(); rig.SetZoom(14); rig.Gate(); rig.SetZoom(17); rig.Gate();
            Assert.Single(infos, l => l.StartsWith("[Map3D] 미리 불러오기", StringComparison.Ordinal));   // 같은 장면을 다시 열지 않는다

            rig.PressEnter();
            Assert.Equal("Pending", rig.State);
            Assert.Same(url, GetHost(rig, "_url"));   // 다시 탐색하지 않는다
            Assert.Contains(infos, l => l.StartsWith($"[Map3D] 진입 시작 job={AreaA}", StringComparison.Ordinal) && l.Contains("preloaded=True"));
            FromPage(rig, new JObject { ["type"] = "ready", ["job"] = AreaA });
            FromPage(rig, new JObject { ["type"] = "rendered", ["job"] = AreaA });
            Assert.True(rig.Shown);
            Assert.Contains(infos, l => l.StartsWith("[Map3D] 전환 시간 total=", StringComparison.Ordinal) && l.Contains("preloaded=true"));
            Assert.Equal("none", rig.Vm.Map3DHost.HostMode);
        });

    public enum NoPreload { Baking, EditMode, Disabled, OtherSceneLoading, BelowPreloadZoom, OutsideArea, Measuring }

    [Theory]
    [InlineData(NoPreload.Baking)]
    [InlineData(NoPreload.EditMode)]
    [InlineData(NoPreload.Disabled)]
    [InlineData(NoPreload.OtherSceneLoading)]
    [InlineData(NoPreload.BelowPreloadZoom)]
    [InlineData(NoPreload.OutsideArea)]
    [InlineData(NoPreload.Measuring)]
    public void should_not_preload_when_blocked(NoPreload why)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryAuto, rig =>
        {
            var infos = Capture(rig);
            FakeWebView(rig);
            rig.Areas(why == NoPreload.OutsideArea ? rig.AreaAround(AreaA, enterZoom: 19, dLon: 0.02) : rig.AreaAround(AreaA, enterZoom: 19));
            switch (why)
            {
                case NoPreload.Baking: Scenario.SetField(rig.Vm, "_isMap3DBaking", true); break;
                case NoPreload.EditMode: Scenario.SetField(rig.Vm, "_isEditModeEnabled", true); rig.W.Map.SetEditMode(true); break;
                case NoPreload.Disabled: rig.UseSettings(Map3DModel.EntryAuto, enabled: false); break;
                case NoPreload.OtherSceneLoading: rig.HostLoading(AreaB); break;
                case NoPreload.BelowPreloadZoom: rig.SetZoom(14); break;
                case NoPreload.Measuring: rig.W.Map.IsAnchorDrawMode = true; break;   // 독점 입력 모드(그리기 · 측정 · 배치 등 — 같은 게이트)
            }
            rig.Gate();
            Assert.DoesNotContain(infos, l => l.StartsWith("[Map3D] 미리 불러오기", StringComparison.Ordinal));
            Assert.Equal(why == NoPreload.OtherSceneLoading ? AreaB : null, rig.Vm.Map3DHost.Job);
            Assert.Equal("Off", rig.State);
            rig.W.Map.IsAnchorDrawMode = false;
            Scenario.SetField(rig.Vm, "_isMap3DBaking", false);
        });

    // ─────────────── A-74 · A-76 · A-77 입력 모드 ───────────────

    [Fact]
    public void should_refuse_group_rotation_in_3d_and_leave_3d_when_edit_mode_turned_on()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var infos = Capture(rig);
            X.Enter(rig);
            Assert.False(rig.Vm.StartGroupRotationCommand.CanExecute(null), "3D 중(운영 모드)에는 묶음 회전 불가");
            rig.Vm.IsEditModeEnabled = true;   // 편집 도구줄 — 켜면 3D 를 먼저 접는다
            Assert.False(rig.Shown);
            Assert.True(rig.W.Map.IsHitTestVisible);
            Assert.Contains(infos, l => l.StartsWith("[Map3D] 2D 복귀(input-mode) zoom=", StringComparison.Ordinal));
            rig.PressEnter();
            Assert.Equal("Off", rig.State);   // 편집 중 — 「3D 로 보기」 거절
            Assert.Contains("들어가지 않습니다", rig.Status);
            rig.Vm.IsEditModeEnabled = false;
            Scenario.Pump();
            rig.Gate(); rig.Tick();
            Assert.False(rig.Shown, "수동 — 편집을 끝내도 저절로 다시 들어가지 않는다");
            X.AssertMap2DInputAlive(rig, "편집 모드 끈 뒤");
        });

    public enum InputMode { Measure, TargetAim, SymbolPlacement, AnchorDraw }

    [Theory]
    [InlineData(InputMode.Measure)]
    [InlineData(InputMode.TargetAim)]
    [InlineData(InputMode.SymbolPlacement)]
    [InlineData(InputMode.AnchorDraw)]
    public void should_return_to_2d_and_not_reenter_in_manual_when_input_mode_started_in_3d(InputMode mode)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var infos = Capture(rig);
            X.Enter(rig);
            try
            {
                switch (mode)
                {
                    case InputMode.Measure: Scenario.Call(rig.Vm, "ToggleMeasure", MeasureKind.Length); break;
                    case InputMode.TargetAim: rig.W.Map.IsTargetAimMode = true; Scenario.Call(rig.Vm, "SyncMap3DWithInputModes"); break;   // EnterTargetAimMode 와 같은 순서
                    case InputMode.SymbolPlacement: Scenario.Call(rig.Vm, "EnterSymbolPlacementMode", EnumMarkerCategory.BASIC_SHAPES, new object(), "시험 심볼"); break;
                    case InputMode.AnchorDraw: Scenario.Call(rig.Vm, "EnterAnchorDrawMode"); break;
                }
                Assert.False(rig.Shown, $"{mode} 시작 — 3D 를 먼저 접는다");
                Assert.Contains(infos, l => l.StartsWith("[Map3D] 2D 복귀(input-mode) zoom=", StringComparison.Ordinal));
                Assert.True(rig.W.Map.IsHitTestVisible, "모드 입력을 2D 지도가 받는다");
                rig.PressEnter();
                Assert.Equal("Off", rig.State);   // 모드 중 — 거절

                switch (mode)
                {
                    case InputMode.Measure: rig.W.Map.StopMeasure(); Scenario.Call(rig.Vm, "OnMeasureStopped"); break;
                    case InputMode.TargetAim: rig.W.Map.IsTargetAimMode = false; Scenario.Call(rig.Vm, "SyncMap3DWithInputModes"); break;
                    case InputMode.SymbolPlacement: Scenario.Call(rig.Vm, "ExitSymbolPlacementMode"); break;
                    case InputMode.AnchorDraw: Scenario.Call(rig.Vm, "ExitAnchorDrawMode"); break;
                }
                Scenario.Pump();
                rig.Gate(); rig.Tick();
                Assert.False(rig.Shown, "수동 — 모드가 끝나도 다시 들어가지 않는다");
                Assert.True(rig.Vm.IsMap3DEnterButtonVisible, "다시 들어갈 길 = 「3D 로 보기」");
            }
            finally { System.Windows.Input.Mouse.OverrideCursor = null; }
        });

    // 2026-10-19 사용자 결정 — 자동(줌)에서도 3D 중 편집 · 측정 등을 켰다 끄면 그 자리에서는 다시 들어가지 않는다(「나가기」와 같은 진입 보류).
    // 지도를 움직여 진입 줌 · 영역을 새로 만족하면(한 번 게이트 밖 → 다시 안) 다시 들어간다.
    [Theory]
    [InlineData(InputMode.Measure)]
    [InlineData(InputMode.TargetAim)]
    [InlineData(InputMode.SymbolPlacement)]
    [InlineData(InputMode.AnchorDraw)]
    [InlineData(null)]   // 편집 모드
    public void should_not_reenter_at_same_place_in_auto_until_gate_is_satisfied_again_when_input_mode_ends(InputMode? mode)
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryAuto, rig =>
        {
            X.Enter(rig);
            Assert.True(rig.Shown, "자동 — 진입 줌 영역 안에서 3D");
            try
            {
                switch (mode)
                {
                    case InputMode.Measure: Scenario.Call(rig.Vm, "ToggleMeasure", MeasureKind.Length); break;
                    case InputMode.TargetAim: rig.W.Map.IsTargetAimMode = true; Scenario.Call(rig.Vm, "SyncMap3DWithInputModes"); break;
                    case InputMode.SymbolPlacement: Scenario.Call(rig.Vm, "EnterSymbolPlacementMode", EnumMarkerCategory.BASIC_SHAPES, new object(), "시험 심볼"); break;
                    case InputMode.AnchorDraw: Scenario.Call(rig.Vm, "EnterAnchorDrawMode"); break;
                    case null: rig.Vm.IsEditModeEnabled = true; break;
                }
                Assert.False(rig.Shown, $"{mode} 시작 — 3D 를 먼저 접는다");

                switch (mode)
                {
                    case InputMode.Measure: rig.W.Map.StopMeasure(); Scenario.Call(rig.Vm, "OnMeasureStopped"); break;
                    case InputMode.TargetAim: rig.W.Map.IsTargetAimMode = false; Scenario.Call(rig.Vm, "SyncMap3DWithInputModes"); break;
                    case InputMode.SymbolPlacement: Scenario.Call(rig.Vm, "ExitSymbolPlacementMode"); break;
                    case InputMode.AnchorDraw: Scenario.Call(rig.Vm, "ExitAnchorDrawMode"); break;
                    case null: rig.Vm.IsEditModeEnabled = false; break;
                }
                Scenario.Pump();
                rig.Gate(); rig.Tick();
                Assert.Equal("Off", rig.State);
                Assert.False(rig.Shown, "자동 — 모드를 끝낸 그 자리에서 곧바로 다시 들어가지 않는다");
                Assert.True(rig.Vm.IsMap3DEnterButtonVisible, "다시 들어갈 길 = 「3D 로 보기」");

                double zoom = rig.W.Map.EffectiveZoom;
                rig.SetZoom(14); rig.Gate();                                     // 진입 줌 밑으로 — 보류 풀림
                Assert.False(rig.Shown);
                rig.SetZoom(zoom); rig.Gate();                                   // 진입 줌을 새로 만족 — 다시 들어간다
                Map3DEntryScenarioTests.SettleEntry(rig);
                Assert.True(rig.Shown, "지도를 움직여 진입 줌을 새로 만족하면 다시 3D");
            }
            finally { System.Windows.Input.Mouse.OverrideCursor = null; }
        });

    [Fact]
    public void should_cancel_pending_entry_and_close_guide_when_input_mode_starts_while_pending()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var infos = Capture(rig);
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            rig.Gate();
            rig.PressEnter();
            Assert.Equal("Pending", rig.State);
            try
            {
                Scenario.Call(rig.Vm, "EnterAnchorDrawMode");   // 준비 안내 중 영역 그리기 시작
                Assert.Equal("Off", rig.State);
                Assert.False(rig.Vm.IsMap3DBusy, "준비 안내가 남았다");
                Assert.Contains("[Map3D] 로딩 중 입력 모드 시작 → 취소", infos);
                rig.Tick();
                Assert.False(rig.Shown, "모드 중에 펼치지 않는다");
                Assert.Null(Return(rig));
                Scenario.Call(rig.Vm, "ExitAnchorDrawMode");
            }
            finally { System.Windows.Input.Mouse.OverrideCursor = null; }
            Scenario.Pump();
            rig.Gate(); rig.Tick();
            Assert.False(rig.Shown, "수동 — 모드가 끝나도 저절로 들어가지 않는다");
        });

    // ─────────────── A-78 · A-79 만들기 취소 · 실패 뒤 ───────────────

    [Fact]
    public void should_enter_previous_scene_when_rebake_cancelled()
        => RunAsync(Map3DModel.EntryManual, async rig =>
        {
            var server = UseServer(rig);
            var areaA = new JObject { ["id"] = AreaA, ["bbox"] = new JArray(rig.Center.Lng - 0.002, rig.Center.Lat - 0.002, rig.Center.Lng + 0.002, rig.Center.Lat + 0.002) };
            server.Scenes.Add(areaA);
            rig.Areas(Map3DClientTestAccess.Scenes(server.Scenes).ToArray());
            rig.HostReady(AreaA);
            rig.Vm.Map3DAreas.Single(a => a.Id == AreaA).Actions.Single(a => a.Header == "다시 만들기").Command.Execute(null);
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            Assert.True(await Until(() => rig.Vm.IsMap3DCancelBakeVisible), "「만들기 취소」가 안 보임");
            rig.Vm.CancelMap3DBakeCommand.Execute(null);
            Assert.True(await Until(() => !Baking(rig), 2));
            Scenario.Pump();
            rig.Gate();
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible, "옛 장면이 그대로 — 단추가 있다");
            rig.PressEnter();
            Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.True(rig.Shown);
            Assert.Equal(AreaA, rig.Job);
        });

    /// <summary>
    /// ★ 결함 D-4: 자동 · 영역 안 · 진입 줌 이상에서 새 영역을 만드는 동안은 게이트가 막는다. 만들기를 취소하거나 만들기가 실패하면
    /// 옛 코드는 게이트를 다시 부르지 않아(다 만든 경우만 부름) 지도를 움직이기 전까지 3D 로 들어가지 않았다.
    /// </summary>
    [Theory]
    [InlineData("cancel")]
    [InlineData("error")]
    [InlineData("cancelled-elsewhere")]
    public void should_reenter_3d_without_moving_map_when_bake_ends_without_new_scene_inside_area_in_auto(string end)
        => RunAsync(Map3DModel.EntryAuto, async rig =>
        {
            var server = UseServer(rig);
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            BakeNewArea(rig);   // 옆에 새 영역 — 만드는 동안 게이트는 막는다
            rig.Gate();
            Assert.Equal("Off", rig.State);
            Assert.True(await Until(() => rig.Vm.IsMap3DCancelBakeVisible), "굽기 진행이 안 보임");
            // 영역 표시 쪽 첫 방위 통지(OnMap3DAreaBearingChanged — 게이트 큐)가 만드는 동안 지나가게 한다(그 통지가 우연히 끝난 뒤 오면 결함을 가린다)
            await Task.Delay(1200);
            rig.Gate();
            Assert.Equal("Off", rig.State);

            switch (end)
            {
                case "cancel": rig.Vm.CancelMap3DBakeCommand.Execute(null); break;
                case "error": server.Status = "error"; break;
                case "cancelled-elsewhere": server.Status = "cancelled"; break;
            }
            Assert.True(await Until(() => !Baking(rig), 4), "만들기가 끝나지 않음");
            Scenario.Pump();   // 게이트 큐(Render)
            Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.True(rig.Shown, $"만들기가 끝났는데({end}) 지도를 움직이기 전까지 3D 로 들어가지 않는다");
            Assert.Equal(AreaA, rig.Job);
        });

    // ─────────────── A-81 네이티브 창 전환 ───────────────

    [Fact]
    public void should_return_to_2d_with_live_input_when_host_falls_back_to_native_window_while_shown()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var warnings = Capture(rig, warnings: true);
            X.Enter(rig);
            var fallback = (Action<string>)typeof(Map3DViewHost).GetField("HostFallback", Inst)!.GetValue(rig.Vm.Map3DHost)!;
            fallback("CompositionControl 실패 → 네이티브 창으로: 시험");
            Assert.False(rig.Shown, "3D 가 접혔는데 「나가기」로 남았다");
            X.AssertMap2DInputAlive(rig, "네이티브 창 전환");
            Assert.Contains(warnings, w => w == "[Map3D] CompositionControl 실패 → 네이티브 창으로: 시험");

            // 준비 중 전환은 상태를 건드리지 않는다(호스트가 같은 주소를 다시 연다 → ready 로 이어진다)
            rig.Gate();
            rig.PressEnter();
            Assert.Equal("Pending", rig.State);
            fallback("시험 — 준비 중");
            Assert.Equal("Pending", rig.State);
            rig.Tick();
            Assert.True(rig.Shown);
        });
}
