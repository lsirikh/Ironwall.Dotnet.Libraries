using System.Windows.Controls;
using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;
using Rig = GMaps.MapEdit.Tests.Map3DEntryScenarioTests.Rig;
using X = GMaps.MapEdit.Tests.Map3DExitInputScenarioTests;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 편집 모드와 3D(2026-10-21 사용자 요청 — "편집 모드가 돼도 굳이 3D 모드를 떠날 필요가 없어"):
/// WPF 안 3D 는 편집 모드를 켜고 꺼도 머물고, 편집 모드 중에도 들어간다(줌 · 단추 · 「이 영역 3D 로 보기」).
/// 2D 지도 캔버스 · 어도너가 꼭 필요한 도구(심볼 배치 · 측정 · 앵커 그리기 · 묶음 회전)는 시작할 때만 2D 로 가고 끝나면 기존 규칙(진입 보류)대로.
/// 네이티브 창 3D(hwnd)는 편집 판을 덮으므로 예전대로 2D. 3D 위 클릭 = 편집 선택, 방향키는 3D 몫.
/// 방향키 · ] 처리기가 보조키(Shift · Ctrl)를 읽으므로 키 시나리오는 숨은 데스크톱에서 돈다(<see cref="Scenario.StaOnHiddenDesktop"/>).
/// </summary>
public class Map3DEditModeScenarioTests
{
    private const string Area = "area-a";

    private static void RunHidden(string mode, Action<Rig> body) => Scenario.StaOnHiddenDesktop(() =>
    {
        var rig = new Rig(mode);
        body(rig);
        rig.Done();
    });

    private static List<string> Capture(Rig rig)
    {
        var lines = new List<string>();
        rig.W.Log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Callback<string, string, string, int>((m, _, _, _) => lines.Add(m));
        return lines;
    }

    private static bool Blocked(Rig rig) => (bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!;

    private static GMapGeometricMarker Symbol(Rig rig, int id, double dLng)
    {
        var marker = new GMapGeometricMarker(Mock.Of<ILogService>(), new GeometricSymbolModel
        {
            Id = id, Title = $"초소{id}", Latitude = rig.Center.Lat, Longitude = rig.Center.Lng + dLng, Width = 40, Height = 40,
        });
        rig.W.Map.Markers.Add(marker);
        return marker;
    }

    private static GroupSelectionService Group(Rig rig) => Scenario.GetField<GroupSelectionService>(rig.Vm, "_groupSelection");

    private static KeyEventArgs Key(Rig rig, Key key)
    {
        var e = EditRig.Key(key);
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.W.Map, e);
        return e;
    }

    // ─────────────── 1. 3D 중 편집 모드 켜기 · 끄기 ───────────────

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_stay_in_3d_and_show_edit_mode_when_edit_mode_toggled_in_wpf_3d(string mode)
        => Map3DEntryScenarioTests.Run(mode, rig =>
        {
            var infos = Capture(rig);
            X.Enter(rig);
            for (int i = 0; i < 2; i++)   // 켜고 끄기 두 번
            {
                rig.Vm.IsEditModeEnabled = true;
                Assert.True(rig.Shown, $"{mode} {i} — 편집 모드를 켜도 3D 그대로");
                Assert.True(rig.Vm.IsMap3DShown, "「나가기」 단추 그대로");
                Assert.True(rig.Vm.IsEditModeEnabled, "툴바 편집 스위치 · 편집 스트립이 켜진 상태로 보인다");
                Assert.True(rig.W.Map.IsEditMode, "지도 편집 상태도 켜진다(3D 를 나가면 그대로 2D 편집)");
                Assert.False(Blocked(rig));
                Assert.False(rig.W.Map.IsHitTestVisible, "3D 가 보이는 동안 2D 입력은 막혀 있다");
                rig.Gate(); rig.Tick();
                Assert.True(rig.Shown, "게이트 · 안내 타이머가 3D 를 접지 않는다");

                rig.Vm.IsEditModeEnabled = false;
                Scenario.Pump();
                rig.Gate(); rig.Tick();
                Assert.True(rig.Shown, $"{mode} {i} — 편집 모드를 꺼도 3D 그대로");
                Assert.False(rig.Vm.IsEditModeEnabled);
            }
            Assert.DoesNotContain(infos, l => l.StartsWith("[Map3D] 2D 복귀", StringComparison.Ordinal));
            rig.PressExit();
            X.AssertMap2DInputAlive(rig, "편집 모드를 켰다 끈 뒤 「나가기」");
        });

    [Fact]
    public void should_keep_edit_mode_on_when_leaving_3d_by_exit_button()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            X.Enter(rig);
            rig.Vm.IsEditModeEnabled = true;
            Assert.True(rig.Shown);
            rig.PressExit();
            Assert.False(rig.Shown);
            Assert.True(rig.Vm.IsEditModeEnabled, "3D 를 나가도 편집 모드는 그대로 — 2D 에서 이어 편집");
            Assert.True(rig.W.Map.IsEditMode);
            X.AssertMap2DInputAlive(rig, "편집 모드로 「나가기」");
        });

    // ─────────────── 2. 편집 모드 중 3D 로 들어가기 ───────────────

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_enter_3d_while_edit_mode_is_on_by_zoom_or_button_and_area_view(string mode)
        => Map3DEntryScenarioTests.Run(mode, rig =>
        {
            rig.Areas(rig.AreaAround(Area));
            rig.W.Map.Position = rig.Center;
            rig.HostReady(Area);
            rig.Vm.IsEditModeEnabled = true;                // 2D 에서 편집 모드를 켠 뒤
            Assert.False(Blocked(rig), "편집 모드는 WPF 안 3D 를 막지 않는다");

            rig.Gate(); Map3DEntryScenarioTests.SettleEntry(rig);
            if (mode == Map3DModel.EntryAuto) Assert.True(rig.Shown, "자동 — 편집 모드 중에도 진입 줌 · 영역 안이면 들어간다");
            else
            {
                Assert.False(rig.Shown, "수동 — 줌으로는 들어가지 않는다");
                rig.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig);
                Assert.True(rig.Shown, "수동 — 편집 모드 중에도 「3D 로 보기」로 들어간다");
            }
            Assert.DoesNotContain("들어가지 않습니다", rig.Status);
            Assert.True(rig.Vm.IsEditModeEnabled);

            rig.PressExit();                                // 나간 뒤 「이 영역 3D 로 보기」도 편집 모드 중에 들어간다
            rig.ViewArea(Area); rig.Gate(); Map3DEntryScenarioTests.SettleEntry(rig);
            Assert.True(rig.Shown, "편집 모드 중 「이 영역 3D 로 보기」");
            Assert.True(rig.Vm.IsEditModeEnabled);
        });

    // ─────────────── 3. 2D 캔버스가 꼭 필요한 도구 = 시작할 때만 2D ───────────────

    public enum Tool { SymbolPlacement, Measure, AnchorDraw, GroupRotationButton }

    [Theory]
    [InlineData(Tool.SymbolPlacement, Map3DModel.EntryManual)]
    [InlineData(Tool.Measure, Map3DModel.EntryManual)]
    [InlineData(Tool.AnchorDraw, Map3DModel.EntryManual)]
    [InlineData(Tool.GroupRotationButton, Map3DModel.EntryManual)]
    [InlineData(Tool.SymbolPlacement, Map3DModel.EntryAuto)]
    [InlineData(Tool.GroupRotationButton, Map3DModel.EntryAuto)]
    public void should_go_to_2d_only_when_2d_canvas_tool_starts_in_edit_mode_and_follow_reentry_rule_after_it_ends(Tool tool, string mode)
        => Map3DEntryScenarioTests.Run(mode, rig =>
        {
            var infos = Capture(rig);
            X.Enter(rig);
            GMapGeometricMarker? a = null, b = null;
            if (tool == Tool.GroupRotationButton)
            {
                a = Symbol(rig, 9101, -0.0002); b = Symbol(rig, 9102, 0.0002);
                Group(rig).SetSelection(new IEditableMarker[] { a, b });   // 레이어 판에서 둘을 고른 것처럼(3D 위에서도 고를 수 있다)
            }
            rig.Vm.IsEditModeEnabled = true;
            Assert.True(rig.Shown, "편집 모드만으로는 3D 그대로");
            try
            {
                switch (tool)
                {
                    case Tool.SymbolPlacement: Scenario.Call(rig.Vm, "EnterSymbolPlacementMode", EnumMarkerCategory.BASIC_SHAPES, new object(), "시험 심볼"); break;
                    case Tool.Measure: Scenario.Call(rig.Vm, "ToggleMeasure", MeasureKind.Length); break;
                    case Tool.AnchorDraw: Scenario.Call(rig.Vm, "EnterAnchorDrawMode"); break;
                    case Tool.GroupRotationButton:
                        Assert.True(rig.Vm.StartGroupRotationCommand.CanExecute(null), "편집 스트립 「회전」은 3D 위에서도 눌린다");
                        rig.Vm.StartGroupRotationCommand.Execute(null);
                        Assert.True(rig.Vm.IsGroupRotationPanelOpen);
                        break;
                }
                Assert.False(rig.Shown, $"{tool} 시작 — 2D 로");
                Assert.Contains(infos, l => l.StartsWith("[Map3D] 2D 복귀(input-mode) zoom=", StringComparison.Ordinal));
                Assert.True(rig.W.Map.IsHitTestVisible, "도구 입력을 2D 지도가 받는다");
                Assert.True(Blocked(rig));
                rig.PressEnter();
                Assert.Equal("Off", rig.State);              // 도구 중 — 「3D 로 보기」 거절
                Assert.Contains("들어가지 않습니다", rig.Status);

                switch (tool)
                {
                    case Tool.SymbolPlacement: Scenario.Call(rig.Vm, "ExitSymbolPlacementMode"); break;
                    case Tool.Measure: rig.W.Map.StopMeasure(); Scenario.Call(rig.Vm, "OnMeasureStopped"); break;
                    case Tool.AnchorDraw: Scenario.Call(rig.Vm, "ExitAnchorDrawMode"); break;
                    case Tool.GroupRotationButton:
                        rig.Vm.CancelGroupRotationCommand.Execute(null);
                        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
                        Assert.Equal(rig.Center.Lng - 0.0002, a!.Position.Lng, 9);   // 취소 = 원위치
                        break;
                }
                Scenario.Pump();
                Assert.False(Blocked(rig), "도구가 끝나면 편집 모드만 남는다 — 3D 를 막지 않는다");
                rig.Gate(); rig.Tick();
                Assert.False(rig.Shown, "끝난 그 자리에서 곧바로 다시 들어가지 않는다(기존 진입 보류)");
                Assert.True(rig.Vm.IsMap3DEnterButtonVisible, "다시 들어갈 길 = 「3D 로 보기」");
                Assert.True(rig.Vm.IsEditModeEnabled, "편집 모드는 그대로");
                rig.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig);
                Assert.True(rig.Shown, "편집 모드인 채 단추로 다시 3D");
            }
            finally { Mouse.OverrideCursor = null; }
        });

    [Fact]
    public void should_go_to_2d_when_group_rotation_starts_by_bracket_key_in_3d_edit_mode()
        => RunHidden(Map3DModel.EntryManual, rig =>
        {
            X.Enter(rig);
            var a = Symbol(rig, 9111, -0.0002);
            var b = Symbol(rig, 9112, 0.0002);
            rig.Vm.IsEditModeEnabled = true;
            Group(rig).SetSelection(new IEditableMarker[] { a, b });
            Assert.True(rig.Shown, "편집 모드 · 묶음 선택만으로는 3D 그대로");
            var e = Key(rig, System.Windows.Input.Key.OemCloseBrackets);   // ] = 묶음 회전 +1°(키보드 폴백)
            Assert.True(e.Handled);
            Assert.True(rig.Vm.IsGroupRotationPanelOpen);
            Assert.False(rig.Shown, "회전 미리보기 · 중심 손잡이는 2D 몫 — 2D 로");
            Key(rig, System.Windows.Input.Key.Escape);                     // Esc = 회전 취소
            Assert.False(rig.Vm.IsGroupRotationPanelOpen);
            rig.Gate(); rig.Tick();
            Assert.False(rig.Shown, "수동 — 저절로 다시 들어가지 않는다");
        });

    // ─────────────── 4. 3D 위 키 · 클릭 ───────────────

    [Fact]
    public void should_leave_arrow_keys_to_3d_ring_and_not_nudge_hidden_2d_selection_in_3d_edit_mode()
        => RunHidden(Map3DModel.EntryManual, rig =>
        {
            X.Enter(rig);
            var s = Symbol(rig, 9121, 0);
            rig.Vm.IsEditModeEnabled = true;
            rig.Vm.SelectedMarker = s;
            var before = s.Position;
            var e = Key(rig, System.Windows.Input.Key.Left);
            Assert.False(e.Handled, "3D 중 방향키는 3D 방향 링 몫");
            Assert.Equal(before, s.Position);
            Assert.True(rig.Shown);

            rig.PressExit();                                               // 2D 에서는 예전대로 선택을 옮긴다
            rig.Vm.SelectedMarker = s;
            Assert.True(Key(rig, System.Windows.Input.Key.Left).Handled, "2D 편집 모드 — 방향키 = 선택 이동");
            Assert.NotEqual(before, s.Position);
        });

    [Fact]
    public void should_select_marker_for_editing_when_clicked_in_3d_while_edit_mode_on()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            X.Enter(rig);
            // 실제 팩토리 · 배치(편집 어도너를 붙일 수 있게 — PropertyPanelScenarioTests 와 같은 준비)
            var s = new Ironwall.Dotnet.Libraries.GMaps.Ui.Factories.MarkerFactory(Mock.Of<ILogService>(), rig.W.Devices).CreateMarker(new GeometricSymbolModel
            {
                Id = 9131, Title = "초소", Latitude = rig.Center.Lat, Longitude = rig.Center.Lng, Width = 40, Height = 40, ShowShape = true,
            });
            rig.W.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)s);
            Scenario.Layout(rig.W.Deco, 1200, 800);
            rig.Vm.IsEditModeEnabled = true;
            var kind = Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D.Map3DOverlayBridge.KindOf((GMap.NET.WindowsPresentation.GMapMarker)s);
            Assert.NotNull(kind);
            X.From3D(rig, new JObject { ["type"] = "pick", ["action"] = "click", ["kind"] = kind, ["id"] = 9131 });
            Scenario.Pump();
            Assert.Same(s, rig.Vm.SelectedMarker);                       // 2D 클릭과 같은 편집 선택(속성 창)
            Assert.True(rig.Vm.IsPropertyPanelVisible);
            Assert.True(rig.Shown, "고르기만으로는 3D 그대로");
        });

    // ─────────────── 5. 네이티브 창 3D = 예전대로 2D ───────────────

    [Fact]
    public void should_leave_3d_and_refuse_entry_when_edit_mode_turned_on_in_native_window_3d()
        => Map3DEntryScenarioTests.Run(Map3DModel.EntryManual, rig =>
        {
            var infos = Capture(rig);
            X.Enter(rig);
            Map3DEntryScenarioTests.UseNativeWindowHost(rig, true);
            try
            {
                Assert.Equal("hwnd", rig.Vm.Map3DHost.HostMode);
                rig.Vm.IsEditModeEnabled = true;
                Assert.False(rig.Shown, "네이티브 창 3D 는 편집 스트립 · 속성 창을 덮는다 — 2D 로");
                Assert.Contains(infos, l => l.StartsWith("[Map3D] 2D 복귀(input-mode) zoom=", StringComparison.Ordinal));
                X.AssertMap2DInputAlive(rig, "네이티브 창 3D 에서 편집 모드");
                rig.PressEnter();
                Assert.Equal("Off", rig.State);
                Assert.Contains("들어가지 않습니다", rig.Status);
            }
            finally { Map3DEntryScenarioTests.UseNativeWindowHost(rig, false); rig.Vm.IsEditModeEnabled = false; }
            Assert.Equal("none", rig.Vm.Map3DHost.HostMode);
        });
}
