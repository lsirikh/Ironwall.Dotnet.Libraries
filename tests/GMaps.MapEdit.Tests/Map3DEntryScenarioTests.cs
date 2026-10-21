using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 들어가는 방식(Map3D.EntryMode — 자동(줌) · 수동(단추), 2026-10-19) 시나리오 — 실제 MapViewModel 게이트(EvaluateMap3DGate) ·
/// 영역 표시(UpdateMap3DAreaDisplay) · 「3D 로 보기」(EnterMap3DCommand) · 「이 영역 3D 로 보기」(영역 항목 메뉴) · 「나가기」 · ESC 경로를 그대로 탄다.
/// 창 · WebView2 는 만들지 않는다 — 3D 호스트(Map3DViewHost)에는 "그 장면 ready · rendered" 상태만 꽂아(재사용 경로) 펼치기까지 간다.
/// 매 시나리오 끝에 WebView2 가 만들어지지 않았는지(HostMode "none") 확인한다.
/// </summary>
public class Map3DEntryScenarioTests
{
    private const double MapZoom = 17d;      // 이 장치의 지도 실효줌(실효줌은 0.5 단위라 경계 ±0.01 은 영역 진입 줌 쪽으로 옮겨 잰다)
    private const string AreaA = "area-a", AreaB = "area-b";

    // ─────────────── 장치 ───────────────

    internal sealed class Rig
    {
        public WiredMapRig W { get; }
        public MapViewModel Vm => W.Vm;
        public Map3DModel Settings { get; private set; } = null!;
        public PointLatLng Center { get; }
        private readonly string _zoomFile = Path.Combine(Path.GetTempPath(), $"map3d-areas-{Guid.NewGuid():N}.json");

        public Rig(string entryMode)
        {
            W = WiredMapRig.Create();
            Scenario.SetField(Vm, "_isEditModeEnabled", false);
            W.Map.SetEditMode(false);
            W.Map.SetEffectiveZoom(MapZoom);
            Assert.Equal(MapZoom, W.Map.EffectiveZoom);
            Center = W.Map.Position;

            // 생성자 없이 만든 VM — 3D 부분의 필드 초기값을 제품과 같게 채운다
            UseSettings(entryMode);
            Scenario.SetField(Vm, "_map3DScenes", new List<Map3DScene>());
            Scenario.SetField(Vm, "_map3DAreaMarkers", new Dictionary<string, Map3DAreaMarker>());
            Scenario.SetField(Vm, "<Map3DAreas>k__BackingField", new ObservableCollection<Map3DAreaItem>());
            Scenario.SetField(Vm, "_map3DAreaZooms", new Map3DAreaZoomStore(_zoomFile));
            // 나머지 3D 필드 중 초기값이 new() 인 것(목록 · 사전 · 계측 표본) — 생성자를 건너뛰어 null 이다
            foreach (var f in typeof(MapViewModel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (f.Name.StartsWith("_map3D", StringComparison.Ordinal) && f.GetValue(Vm) == null && !f.FieldType.IsAbstract
                    && f.FieldType.IsClass && f.FieldType != typeof(string) && f.FieldType.GetConstructor(Type.EmptyTypes) != null
                    && (f.FieldType.IsGenericType || f.FieldType.Name == "Map3DPerfSamples"))
                    f.SetValue(Vm, Activator.CreateInstance(f.FieldType));
        }

        /// <summary>설정 「3D 지도」 저장과 같은 결과(설정 모델을 바꿔 끼움 — 진짜 저장 · 다시 읽기는 설정 파일과 WebView2 미리 데우기를 부른다).</summary>
        public void UseSettings(string entryMode, double enterZoom = 17d, bool enabled = true)
        {
            Settings = new Map3DModel { EntryMode = entryMode, EnterZoom = enterZoom, ExitZoom = enterZoom - 0.5, GuideMinSeconds = 0, IsEnabled = enabled };
            Settings.Normalize();
            Scenario.SetField(Vm, "_map3DSettings", Settings);
        }

        /// <summary>서버 목록을 받은 것처럼 영역을 단다(실제 ApplyMap3DAreas — 장면 캐시 · 목록 항목 · 배지 마커).</summary>
        public void Areas(params Map3DSceneInfo[] scenes) => Scenario.Call(Vm, "ApplyMap3DAreas", (IReadOnlyList<Map3DSceneInfo>)scenes);

        /// <summary>화면 가운데를 덮는 영역(가로 · 세로 ±d 도). enterZoom 이 있으면 그 영역의 3D 줌.</summary>
        public Map3DSceneInfo AreaAround(string id, double? enterZoom = null, double d = 0.002, double dLon = 0)
            => new(id, "시험 " + id, new[] { Center.Lng + dLon - d, Center.Lat - d, Center.Lng + dLon + d, Center.Lat + d }, null, null, null) { EnterZoom = enterZoom };

        /// <summary>3D 호스트가 그 장면을 이미 열어 ready · rendered 까지 받은 상태(미리 불러온 장면 — 재사용 경로, WebView2 없음).</summary>
        public void HostReady(string job)
        {
            var host = Vm.Map3DHost;
            SetHost(host, "_job", job);
            SetHost(host, "_ready", true);
            SetHost(host, "_rendered", true);
            SetHost(host, "_failed", false);
            SetHost(host, "<ReadyAt>k__BackingField", (DateTime?)DateTime.UtcNow.AddSeconds(-10));
        }

        /// <summary>장면을 불러오는 중(ready 전) — 미리 불러오기 · 진입은 ready 를 기다린다.</summary>
        public void HostLoading(string job)
        {
            HostReady(job);
            SetHost(Vm.Map3DHost, "_ready", false);
            SetHost(Vm.Map3DHost, "_rendered", false);
        }

        private static void SetHost(Map3DViewHost host, string name, object? value)
            => typeof(Map3DViewHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, value);

        /// <summary>게이트 큐 한 번(제품은 Render 우선순위로 프레임당 1회 — 같은 두 메서드).</summary>
        public void Gate()
        {
            Scenario.Call(Vm, "EvaluateMap3DGate");
            Scenario.Call(Vm, "UpdateMap3DAreaDisplay");
        }

        /// <summary>전환 안내 타이머 한 틱(50 ms 마다) — 안내 최소 시간 0 이면 이 틱에서 펼친다.</summary>
        public void Tick() => Scenario.Call(Vm, "OnMap3DGuideTick");

        public string State => Scenario.GetField<object>(Vm, "_map3DState").ToString()!;
        public bool Shown => Vm.Map3DStatusText == "3D";
        public string? Job => Scenario.GetField<string?>(Vm, "_map3DJob");
        public string Status => Vm.AimStatusMessage ?? "";

        public void PressEnter() => Vm.EnterMap3DCommand.Execute(null);
        public void PressExit() => Vm.ExitMap3DCommand.Execute(null);
        public void ViewArea(string id) => Vm.Map3DAreas.Single(a => a.Id == id).Actions[0].Command.Execute(null);

        public bool PressEsc()
        {
            var source = new HeadlessSource { RootVisual = W.Deco };
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            return (bool)Scenario.Call(Vm, "TryHandleMap3DKey", e)!;
        }

        public void SetZoom(double zoom) { W.Map.SetEffectiveZoom(zoom); Assert.Equal(zoom, W.Map.EffectiveZoom); }

        /// <summary>시나리오 끝 — 3D 화면(WebView2)을 만들지 않았고 VM 이 Error 로그를 남기지 않았다. 3D 가 열려 있으면 닫는다.</summary>
        public void Done()
        {
            Assert.Equal("none", Vm.Map3DHost.HostMode);
            Assert.Empty(W.Errors);
            if (Shown) PressExit();
            if (File.Exists(_zoomFile)) File.Delete(_zoomFile);
        }

        private sealed class HeadlessSource : PresentationSource
        {
            private Visual? _root;
            public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
            public override bool IsDisposed => false;
            protected override CompositionTarget GetCompositionTargetCore() => null!;
        }
    }

    internal static void Run(string mode, Action<Rig> body) => Scenario.Sta(() =>
    {
        var rig = new Rig(mode);
        body(rig);
        rig.Done();
    });

    /// <summary>들어가서 펼쳐질 때까지(재사용 장면은 안내 타이머 한 틱 뒤 펼침).</summary>
    internal static void SettleEntry(Rig rig) { if (rig.State == "Pending") rig.Tick(); }

    // ─────────────── 1. 방식 × 줌 × 위치 × 장면 표 ───────────────

    public enum Where { Inside, Outside, Overlap, OnEdge }
    public enum Scene { Ready, None, Baking, BakeFailed }

    /// <summary>
    /// 영역 진입 줌 = 지도 줌(17) + delta. delta &gt; 0 이면 "진입 줌 아래", 0 이면 "같음", &lt; 0 이면 "위".
    /// 미리 준비 경계는 진입 줌을 미리 준비 줌 근처까지 올려(지도 줌이 미리 준비 줌 ±0.01) 잰다 — 거기선 어느 방식이든 줌으로는 안 들어간다.
    /// </summary>
    public static IEnumerable<object[]> Matrix()
    {
        // (영역 진입 줌 − 지도 줌, 설명) — 진입 줌 아래(+0.01 · +0.5) · 같음 · 위(−0.01 · −1) · 미리 준비 경계(진입 2 위 → 지도 줌 = 미리 준비 줌 ±0.01)
        var deltas = new[] { 0.01, 0.5, 0d, -0.01, -1d, 1.99, 2d, 2.01 };
        foreach (var mode in new[] { Map3DModel.EntryAuto, Map3DModel.EntryManual })
            foreach (var delta in deltas)
                foreach (var where in Enum.GetValues<Where>())
                    foreach (var scene in Enum.GetValues<Scene>())
                        yield return new object[] { mode, delta, where, scene };
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void should_enter_only_by_auto_zoom_and_show_button_only_inside_ready_area_when_gate_evaluated(string mode, double delta, Where where, Scene scene)
        => Run(mode, rig =>
        {
            double areaZoom = MapZoom + delta;
            // 미리 준비 줌 = min(세팅 15, 영역 진입 − 1) — 경계 행은 세팅 미리 준비 줌을 영역 진입 − 2 로 맞춰 지도 줌이 그 ±0.01 에 오게 한다
            if (delta >= 1.99) rig.Settings.PreloadZoom = areaZoom - 2;
            if (scene != Scene.None && scene != Scene.BakeFailed)   // 만들기 실패 = 서버 목록에 장면이 올라오지 않음
            {
                switch (where)
                {
                    case Where.Inside: rig.Areas(rig.AreaAround(AreaA, areaZoom)); break;
                    case Where.Outside: rig.Areas(rig.AreaAround(AreaA, areaZoom, dLon: 0.01)); break;
                    case Where.Overlap: rig.Areas(rig.AreaAround(AreaA, areaZoom), rig.AreaAround(AreaB, areaZoom + 3, d: 0.004)); break;
                    case Where.OnEdge:   // 화면 가운데가 영역 서쪽 경계 위(경계 포함)
                        rig.Areas(new Map3DSceneInfo(AreaA, null, new[] { rig.Center.Lng, rig.Center.Lat - 0.002, rig.Center.Lng + 0.004, rig.Center.Lat + 0.002 }, null, null, null) { EnterZoom = areaZoom });
                        break;
                }
                rig.HostReady(AreaA);   // 겹침이면 목록 앞 영역(A)이 화면 가운데 영역 — 그 장면만 열어 둔다
            }
            if (scene == Scene.Baking) Scenario.SetField(rig.Vm, "_isMap3DBaking", true);

            rig.Gate();
            SettleEntry(rig);

            bool inside = scene is Scene.Ready or Scene.Baking && where != Where.Outside;
            bool zoomReached = MapZoom >= Math.Clamp(areaZoom, Map3DModel.MinZoom, Map3DModel.MaxZoom);
            bool expect3D = mode == Map3DModel.EntryAuto && inside && scene == Scene.Ready && zoomReached;
            Assert.Equal(expect3D, rig.Shown);
            Assert.Equal(expect3D ? AreaA : null, rig.Shown ? rig.Job : null);
            // 「3D 로 보기」 = 2D 이고 화면 가운데가 만들어 둔 영역 안 — 만드는 중이어도 영역은 있다(누르면 아래 막힘 시나리오)
            Assert.Equal(!expect3D && inside, rig.Vm.IsMap3DEnterButtonVisible);
            Assert.Equal(expect3D, rig.Vm.IsMap3DShown);   // 「나가기」는 3D 일 때만
            // "줌 N 이상" 칩은 자동 · 2D · 영역 안 · 진입 줌 아래일 때만
            Assert.Equal(mode == Map3DModel.EntryAuto && inside && !expect3D && !zoomReached, rig.Vm.IsMap3DChipVisible);
        });

    // ─────────────── 2. 전환 흐름 ───────────────

    [Fact]
    public void should_enter_by_button_exit_and_never_reenter_by_zoom_when_manual()
        => Run(Map3DModel.EntryManual, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            rig.Gate();
            Assert.False(rig.Shown);
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);

            rig.PressEnter();
            Assert.Equal("Pending", rig.State);
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible, "준비 중에는 단추를 숨긴다");
            rig.Gate();                       // 준비 중 게이트(줌 판정) — 직접 진입은 취소하지 않는다
            Assert.Equal("Pending", rig.State);
            rig.Tick();
            Assert.True(rig.Shown);
            Assert.True(rig.Vm.IsMap3DShown);
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible, "3D 안에서는 「3D 로 보기」 없음");

            rig.PressExit();
            Assert.False(rig.Shown);
            Assert.False(rig.Vm.IsMap3DShown, "2D 에서 「나가기」는 절대 안 보인다");
            Assert.Equal(MapZoom, rig.W.Map.EffectiveZoom);   // 들어가기 직전 2D 자리

            foreach (var z in new[] { 17.5, 18d, 19d, 20d })
            {
                rig.SetZoom(z);
                rig.Gate(); rig.Tick();
                Assert.False(rig.Shown, $"수동 — 줌 {z} 로 올려도 다시 들어가지 않는다");
                Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
            }
        });

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_leave_by_esc_and_not_reenter_at_same_zoom_when_3d_shown(string mode)
        => Run(mode, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            rig.PressEnter();
            Assert.Equal("Off", rig.State);   // 게이트가 화면 가운데 영역을 아직 모른다 = 단추가 없다 → 누름은 아무 일 없음
            rig.Gate();
            if (rig.State == "Off") rig.PressEnter();   // 자동은 진입 줌 17 이라 게이트가 이미 들어갔다
            SettleEntry(rig);
            Assert.True(rig.Shown);

            Assert.False(rig.PressEsc(), "3D 중 ESC 로는 나가지 않는다(2026-10-19 — 「나가기」 단추로만)");
            Assert.True(rig.Shown);
            rig.Vm.ExitMap3DCommand.Execute(null);
            Assert.False(rig.Shown);
            rig.Gate(); rig.Tick();
            Assert.False(rig.Shown, "같은 줌(자동이면 진입 줌 17)에서 곧바로 다시 들어가지 않는다");
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible, "다시 들어갈 길 = 「3D 로 보기」");
            Assert.False(rig.PressEsc(), "2D 에서 ESC 는 3D 경로가 받지 않는다");

            rig.PressEnter(); SettleEntry(rig);
            Assert.True(rig.Shown, "나간 뒤에도 단추로는 바로 들어간다");
        });

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_enter_directly_when_area_view_chosen_from_badge_or_list(string mode)
        => Run(mode, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA, dLon: 0.05));   // 화면 밖 먼 영역
            rig.HostReady(AreaA);
            rig.SetZoom(12);
            rig.Gate();
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible, "화면 가운데가 영역 밖");

            rig.ViewArea(AreaA);   // 배지 우클릭 메뉴와 「3D 영역 목록」은 같은 항목 · 같은 명령
            Assert.Equal("Pending", rig.State);
            Assert.Equal(AreaA, rig.Job);
            rig.Gate();            // 옮긴 자리에서 게이트가 돌아도 직접 진입을 취소하지 않는다
            SettleEntry(rig);
            Assert.True(rig.Shown);

            rig.PressExit();
            Assert.Equal(12d, rig.W.Map.EffectiveZoom);       // 「영역 보기」 누르기 전 자리로
            Assert.True(Math.Abs(rig.W.Map.Position.Lng - rig.Center.Lng) < 1e-6);
        });

    [Fact]
    public void should_keep_3d_and_apply_new_rule_after_exit_when_mode_switched_while_shown()
        => Run(Map3DModel.EntryAuto, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            rig.Gate(); SettleEntry(rig);
            Assert.True(rig.Shown, "자동 — 진입 줌 17 에서 들어간다");

            rig.UseSettings(Map3DModel.EntryManual);   // 3D 중 수동으로 바꿔 저장
            rig.Gate();
            Assert.True(rig.Shown, "보던 3D 는 그대로");
            rig.PressExit();
            rig.SetZoom(17.5); rig.Gate(); rig.Tick();
            rig.SetZoom(17); rig.Gate(); rig.Tick();
            Assert.False(rig.Shown, "나간 뒤에는 수동 규칙 — 줌으로 안 들어간다");

            rig.PressEnter(); SettleEntry(rig);
            Assert.True(rig.Shown);
            rig.UseSettings(Map3DModel.EntryAuto);     // 3D 중 자동으로 되돌림
            rig.Gate();
            Assert.True(rig.Shown);
            rig.PressExit();
            rig.SetZoom(16); rig.Gate();               // 한 번 진입 줌 밑으로 — 나가기 뒤 보류가 풀린다
            rig.SetZoom(17); rig.Gate(); SettleEntry(rig);
            Assert.True(rig.Shown, "나간 뒤 자동 규칙 — 진입 줌에서 다시 들어간다");
        });

    /// <summary>지도 편집 모드로(세터 없이 — 선택 정리 등 부수 효과 없이). 2026-10-21 부터 건물 고르기 · 그리기 · 꾸미기는 편집 모드에서만.</summary>
    internal static void UseEditMode(Rig rig)
    {
        Scenario.SetField(rig.Vm, "_isEditModeEnabled", true);
        rig.W.Map.SetEditMode(true);
    }

    /// <summary>네이티브 창(WebView2 HWND) 3D 인 것처럼(HostMode "hwnd") — 끄면 "none"(기본 WPF 안 3D 로 열린다).</summary>
    internal static void UseNativeWindowHost(Rig rig, bool on)
        => typeof(Map3DViewHost).GetField("_el", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(rig.Vm.Map3DHost, on ? new System.Windows.Controls.Border() : null);

    public enum Block { EditModeOnNativeWindow, AnchorDraw, SymbolPlacement }

    private static void SetBlock(Rig rig, Block block, bool on)
    {
        switch (block)
        {
            // 2026-10-21 — 편집 모드는 네이티브 창 3D 에서만 막는다(WPF 안 3D 는 편집 스트립 · 속성 창이 위에 보여 머문다)
            case Block.EditModeOnNativeWindow: Scenario.SetField(rig.Vm, "_isEditModeEnabled", on); UseNativeWindowHost(rig, on); break;
            case Block.AnchorDraw: rig.W.Map.IsAnchorDrawMode = on; break;                     // 영역 그리기(드로잉) 진행 중
            case Block.SymbolPlacement: rig.W.Map.IsSymbolPlacementMode = on; break;
        }
    }

    [Theory]
    [InlineData(Map3DModel.EntryAuto, Block.EditModeOnNativeWindow)]
    [InlineData(Map3DModel.EntryAuto, Block.AnchorDraw)]
    [InlineData(Map3DModel.EntryAuto, Block.SymbolPlacement)]
    [InlineData(Map3DModel.EntryManual, Block.EditModeOnNativeWindow)]
    [InlineData(Map3DModel.EntryManual, Block.AnchorDraw)]
    [InlineData(Map3DModel.EntryManual, Block.SymbolPlacement)]
    public void should_refuse_button_and_area_view_with_reason_when_input_mode_active(string mode, Block block)
        => Run(mode, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            SetBlock(rig, block, true);
            Assert.True((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!, "제품 판정과 같은 막힘");

            rig.Gate(); rig.Tick();
            Assert.False(rig.Shown, "모드 중에는 줌으로도 안 들어간다");
            rig.PressEnter(); rig.Tick();
            Assert.Equal("Off", rig.State);
            Assert.Contains("들어가지 않습니다", rig.Status);
            rig.ViewArea(AreaA); rig.Gate(); rig.Tick();
            Assert.Equal("Off", rig.State);

            SetBlock(rig, block, false);
            rig.PressEnter(); SettleEntry(rig);
            Assert.True(rig.Shown, "모드를 끝내면 단추로 들어간다");
        });

    // ─────────────── 3. 단추 보임 회귀 ───────────────

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_never_show_exit_in_2d_nor_enter_in_3d_when_states_cycle(string mode)
        => Run(mode, rig =>
        {
            Assert.False(rig.Vm.IsMap3DShown, "처음 2D — 「나가기」 없음");
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            for (int i = 0; i < 3; i++)
            {
                rig.Gate();
                if (!rig.Shown) { Assert.False(rig.Vm.IsMap3DShown); rig.PressEnter(); }
                if (rig.State == "Pending") { Assert.False(rig.Vm.IsMap3DShown, "준비 중(2D 가 보임)에도 「나가기」 없음"); Assert.False(rig.Vm.IsMap3DEnterButtonVisible); }
                SettleEntry(rig);
                Assert.True(rig.Vm.IsMap3DShown);
                Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
                Assert.False(rig.Vm.IsMap3DZoomControlVisible, "3D 에는 줌 단추 없음");
                rig.PressExit();
                Assert.False(rig.Vm.IsMap3DShown);
                Assert.True(rig.Vm.IsMap3DZoomControlVisible);
                rig.Gate();
                Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
            }
        });

    [Fact]
    public void should_hide_enter_button_when_area_deleted_or_3d_disabled()
        => Run(Map3DModel.EntryManual, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            rig.Gate();
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);

            rig.UseSettings(Map3DModel.EntryManual, enabled: false);   // 3D 사용 끔
            rig.Gate();
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
            rig.PressEnter(); rig.Tick();
            Assert.Equal("Off", rig.State);

            rig.UseSettings(Map3DModel.EntryManual);
            rig.Gate();
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
            Scenario.Call(rig.Vm, "RemoveMap3DArea", AreaA);   // 지운 영역(목록 · 캐시에서 빠짐)
            rig.Gate();
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
            rig.PressEnter();
            Assert.Equal("Off", rig.State);
        });

    [Fact]
    public void should_block_entry_but_keep_button_while_area_is_being_rebaked()
        => Run(Map3DModel.EntryAuto, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            Scenario.SetField(rig.Vm, "_isMap3DBaking", true);   // 다시 만들기 중
            rig.Gate(); rig.Tick();
            Assert.False(rig.Shown, "만드는 중에는 줌으로 안 들어간다(진행 패널을 가리지 않게)");
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);

            rig.PressEnter();               // 만드는 중에는 처음부터 들어가지 않고 이유를 알린다(안내 깜빡임 없음)
            Assert.Equal("Off", rig.State);
            Assert.False(rig.Vm.IsMap3DBusy);
            Assert.Contains("만드는 중", rig.Status);
            rig.Gate();
            Assert.Equal("Off", rig.State);

            Scenario.SetField(rig.Vm, "_isMap3DBaking", false);
            rig.Gate(); SettleEntry(rig);
            Assert.True(rig.Shown, "다 만든 뒤 자동 진입");
        });

    // ─────────────── 5. 반복 · 경쟁 ───────────────

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_start_one_entry_when_button_pressed_many_times(string mode)
        => Run(mode, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA, enterZoom: 19));   // 자동이어도 진입 줌 아래
            rig.HostReady(AreaA);
            rig.Gate();
            for (int i = 0; i < 10; i++) rig.PressEnter();
            Assert.Equal("Pending", rig.State);
            var ret = Scenario.GetField<Map3DCamera?>(rig.Vm, "_map3DReturn");
            Assert.NotNull(ret);
            rig.Tick();
            for (int i = 0; i < 5; i++) rig.PressEnter();       // 3D 중 눌러도 무시
            Assert.True(rig.Shown);
            Assert.Equal(ret, Scenario.GetField<Map3DCamera?>(rig.Vm, "_map3DReturn"));
            rig.PressExit();
            for (int i = 0; i < 5; i++) rig.PressExit();        // 2D 에서 나가기 반복 — 아무 일 없음
            Assert.Equal("Off", rig.State);
            Assert.Equal(MapZoom, rig.W.Map.EffectiveZoom);
        });

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_ignore_exit_and_esc_while_entry_guide_is_showing(string mode)
        => Run(mode, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA, enterZoom: 19));
            rig.HostReady(AreaA);
            rig.Gate();
            rig.PressEnter();
            Assert.True(rig.Vm.IsMap3DBusy, "전환 안내 표시 중");
            rig.PressExit();
            Assert.False(rig.PressEsc(), "준비 중 ESC 는 3D 나가기가 아니다");
            Assert.Equal("Pending", rig.State);
            rig.Tick();
            Assert.True(rig.Shown);
        });

    [Theory]
    [InlineData(Map3DModel.EntryAuto, 12d)]
    [InlineData(Map3DModel.EntryAuto, 20d)]
    [InlineData(Map3DModel.EntryManual, 12d)]
    [InlineData(Map3DModel.EntryManual, 20d)]
    public void should_keep_direct_entry_when_zoom_changed_while_pending(string mode, double zoom)
        => Run(mode, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA, enterZoom: 19));
            rig.HostReady(AreaA);
            rig.Gate();
            rig.PressEnter();
            rig.SetZoom(zoom);
            rig.Gate();
            Assert.Equal("Pending", rig.State);
            rig.Tick();
            Assert.True(rig.Shown);
        });

    [Fact]
    public void should_cancel_direct_entry_when_center_moved_out_of_area_while_pending()
        => Run(Map3DModel.EntryManual, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA));
            rig.HostReady(AreaA);
            rig.Gate();
            rig.PressEnter();
            rig.W.Map.Position = new PointLatLng(rig.Center.Lat, rig.Center.Lng + 0.05);
            rig.Gate();
            Assert.Equal("Off", rig.State);
            Assert.False(rig.Vm.IsMap3DBusy, "안내도 닫는다");
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
        });

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_hold_entry_until_flight_ends_when_button_pressed_during_fly_to(string mode)
        => Run(mode, rig =>
        {
            rig.Areas(rig.AreaAround(AreaA, enterZoom: 19));
            rig.HostReady(AreaA);
            rig.Gate();
            var anim = typeof(MapViewModel).GetField("_flyAnimation", BindingFlags.Instance | BindingFlags.NonPublic)!;
            anim.SetValue(rig.Vm, RuntimeHelpersUninit(anim.FieldType));   // 이벤트 시 지도 이동(flyTo) 중
            rig.PressEnter();
            Assert.Equal("Off", rig.State);   // 날아가는 중엔 들어가지 않는다(끝난 자리에서 다시) — 안내도 띄우지 않는다
            Assert.False(rig.Vm.IsMap3DBusy);
            rig.Gate();
            Assert.Equal("Off", rig.State);
            anim.SetValue(rig.Vm, null);
            rig.Gate();
            Assert.False(rig.Shown);
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible, "끝난 자리가 영역 안이면 단추가 다시 있다");
            rig.PressEnter(); SettleEntry(rig);
            Assert.True(rig.Shown);
        });

    private static object RuntimeHelpersUninit(Type t) => System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t);

    // ─────────────── 화면(XAML) — 「3D 로 보기」 단추가 「나가기」와 같은 디자인 · 바인딩 ───────────────

    [Fact]
    public void should_place_enter_button_next_to_exit_with_same_styles_when_map_view_read()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));
        int enter = xaml.IndexOf("AutomationProperties.AutomationId=\"Map.Map3D.EnterButton\"", StringComparison.Ordinal);
        int exit = xaml.IndexOf("AutomationProperties.AutomationId=\"GMaps.Map3D.ExitButton\"", StringComparison.Ordinal);
        Assert.True(enter > 0 && exit > enter, "「3D 로 보기」는 「나가기」 바로 앞(같은 왼쪽 위 자리)");
        int coord = xaml.IndexOf("<ctrl:MapCoordinateControl", StringComparison.Ordinal);
        Assert.True(coord > 0 && coord < enter, "좌표 표시가 맨 위, 3D 단추는 그 아래(좌표 줄이 늘어나면 같이 내려간다)");
        string block = xaml.Substring(xaml.LastIndexOf("<Border", enter, StringComparison.Ordinal), exit - xaml.LastIndexOf("<Border", enter, StringComparison.Ordinal));
        Assert.Contains("Visibility=\"{Binding IsMap3DEnterButtonVisible", block);
        Assert.Contains("Command=\"{Binding EnterMap3DCommand}\"", block);
        Assert.Contains("Style=\"{StaticResource Map3DPillButtonStyle}\"", block);   // 바람 위젯과 같은 알약 모양(2026-10-20)
        Assert.Contains("HelpKey=\"Map.Map3D.Enter\"", block);
        Assert.DoesNotContain("x:Name", block);
        Assert.DoesNotContain("#", block.Replace("Map.Map3D", ""));   // 색은 토큰만
    }

    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string? path = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
