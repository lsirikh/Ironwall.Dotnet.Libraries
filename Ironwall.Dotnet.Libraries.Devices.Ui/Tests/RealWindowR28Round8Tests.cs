using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;
using Xunit.Abstractions;
using static Ironwall.Dotnet.Libraries.Devices.Ui.Tests.RealWindowR28Round7Tests;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 2026-10-15 실창 5~8회차 MNT-R28-426(윤형 칸 위로 끌었는데 붙을 점이 기둥 · 레일 9개뿐) — 실창 조건 그대로 다시 연다:
/// 망 10 근처 센서(FenceSensorIds[9])를 망 2 까지 26걸음 · 24걸음째 실제 키 라우팅으로 Tab · [+ 모델](Smart 모양) '새 모델' · 403~406 프리셋 차례 · [적용]/[프리셋으로] 단추 포커스.
/// 진단 기록(IRONWALL_DRAG_TRACE · <see cref="TestDragTrace"/>)으로 칸 모양 · 후보 점 · Tab 이 캔버스에 닿았나를 읽어 단언한다. 화면에 보이는 창은 띄우지 않는다.
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "FullyQualifiedName~RealWindowR28Round8Tests"</c>
/// </summary>
public class RealWindowR28Round8Tests
{
    private readonly ITestOutputHelper _out;
    public RealWindowR28Round8Tests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// 실창 시드 — 센서 12대 · [+ 모델] 단추 그대로(SmartSensor · Smart 모양 = 기둥 · 레일 · 철조망 · 담) · R28-MOUNT-A(메쉬 30 % · 1.2 m) ·
    /// 402 Ctrl+A 모델 → 403 [고른 센서] A 아닌 것 → 404 [같은 모델 전체] A → 405 [이 제어기 전체] A 아닌 것 → 되돌림 A → 406 한 대 A → 410 높이 고침 · [프리셋으로].
    /// </summary>
    private static (WiringViewModel Vm, SensorModel Model, SensorMountPreset A) Seed()
    {
        var repo = new InMemorySensorPresetRepository();
        var vm = WiringFenceHeightTests.Build(new string('S', 12));
        vm.AttachSensorPresets(repo);
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        var smart = vm.MountPresetsOf(SensorPresetCatalog.SMART2);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => WiringPresetPlan.Migrate(p.Value, SensorPresetCatalog.SMART2, smart, l.Panels)))));
        var models = new SensorModelsViewModel(new ChangeNotifyingSensorPresetRepository(repo, () => vm.LoadSensorPresetsAsync()));
        Assert.True(models.LoadAsync().GetAwaiter().GetResult());
        var model = models.AddModelAsync("새 모델 14", EnumDeviceType.SmartSensor, SensorShape.Smart).GetAwaiter().GetResult()!;
        var auto = vm.MountPresetsOf(model.Id).Single();
        var a = repo.SaveMountPresetAsync(new SensorMountPreset { ModelId = model.Id, Name = "R28-MOUNT-A", Anchor = SensorMountAnchor.Mesh, AlongRatio = 0.3, HeightM = 1.2, YawDeg = 90 })
            .GetAwaiter().GetResult();
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        var keys = vm.FenceChain.Keys.ToList();
        IReadOnlyDictionary<int, SensorMountPreset> By(SensorMountPreset p) => new Dictionary<int, SensorMountPreset> { [model.Id] = p };

        vm.FenceSelectAllSensors();
        vm.SelectedPresetModel = vm.PresetModelChoices.Single(m => m.Id == model.Id);
        vm.ApplyMountPreset(MountPresetApplyScope.Selected, By(auto));
        vm.FenceSelect(keys[1]);
        vm.ApplyMountPreset(MountPresetApplyScope.SameModel, By(a));
        vm.FenceSelect(keys[1]);
        vm.ApplyMountPreset(MountPresetApplyScope.Controller, By(auto));
        vm.FenceSelect(keys[1]);
        vm.ApplyMountPreset(MountPresetApplyScope.Controller, By(a));
        vm.FenceSelect(keys[1]);
        vm.ApplyMountPreset(MountPresetApplyScope.Selected, By(a));
        vm.FenceSelect(keys[4]);
        vm.PresetHeightText = "1.0";
        vm.ApplyPresetHeight();
        vm.ResetMountToPreset();
        Assert.All(keys, k => Assert.Equal(a.Id, vm.FenceLayout.MountOf(k)!.MountPresetId));
        Assert.True(model.Allows(SensorAnchorKind.Razor));
        return (vm, model, a);
    }

    /// <summary>
    /// 실창 키 — 지금 포커스 요소(키보드 포커스가 이 창에 없으면 창의 논리 포커스)에서 PreviewKeyDown → KeyDown. 처리됐나 · 받은 요소.
    /// </summary>
    private static (bool Handled, string Target) RealKey(Window window, Key key)
    {
        var target = Keyboard.FocusedElement is UIElement kf && Window.GetWindow(kf) == window ? kf
                   : FocusManager.GetFocusedElement(window) as UIElement ?? window;
        return (Press(target, key), AutomationProperties.GetAutomationId(target) is { Length: > 0 } id ? id : target.GetType().Name);
    }

    /// <summary>실창 426 차례 — 센서 Enter → 망 2 칩 Enter → [철조망과 윤형철조망] → [선택한 망에 적용] → R28ChipStart(센서 Enter · [프리셋으로]) · 포커스는 단추에 남는다.</summary>
    private static string RazorPanelTwo(Window window, WiringViewModel vm, FenceCanvas canvas, int key)
    {
        vm.FenceSelect(key);
        Pump();
        var panelChip = canvas.PanelChips[1];
        panelChip.Focus();
        Assert.True(Press(panelChip, Key.Enter));
        Pump();
        vm.ChoosePanelStyle(EnumFenceStyle.ChainLinkRazor);
        Pump();
        var apply = ById<Button>(window, "Devices.Wiring.Fence.PanelPane.Apply");
        apply.Focus();                                                                          // 실창 InvokeAny([적용]) — 포커스가 단추에 남는다
        Assert.True(vm.ApplyPanelEdit());
        Pump();
        Assert.Equal(EnumFenceStyle.ChainLinkRazor, vm.FenceLayout.Panels[1].Style);
        vm.FenceSelect(key);
        Pump();
        var reset = ById<Button>(window, "Devices.Wiring.Fence.Preset.Reset");
        reset.Focus();                                                                          // 실창 R28ChipStart — InvokeAny([프리셋으로])
        vm.ResetMountToPreset();
        Pump();
        return AutomationProperties.GetAutomationId(FocusManager.GetFocusedElement(window) as DependencyObject ?? window);
    }

    private sealed record DragRun(string FocusBefore, string FocusAtTab, List<string> Labels, List<bool> Handled, string Status, int? Bay, string Trace, int BayAtTab);

    /// <summary>실창 R28FenceRazorDrag — 칩 가운데 → 망 2 칩 사각형 위 1/12, <paramref name="steps"/> 걸음 · <paramref name="tabAt"/> 걸음째에 '철조망' 이 나올 때까지 실제 Tab(최대 20번).</summary>
    private static DragRun RazorDrag(Window window, WiringViewModel vm, FenceCanvas canvas, int key, string focusBefore, Point? toOverride, int steps, int tabAt)
    {
        var panelChip = canvas.PanelChips[1];
        var rect = panelChip.TransformToAncestor(canvas).TransformBounds(new Rect(0, 0, panelChip.ActualWidth, panelChip.ActualHeight));
        var to = toOverride ?? new Point(rect.Left + rect.Width / 2, rect.Top + Math.Max(3, rect.Height / 12));
        var mark = TestDragTrace.Mark();
        var chip = canvas.SensorChips[key];
        var from = canvas.ScreenCenterOf(chip);
        var labels = new List<string>();
        var handled = new List<bool>();
        var focusAtTab = string.Empty;
        var bayAtTab = -1;
        string Label() => vm.StatusText.Split(" — ")[0];
        canvas.OnPointerPressed(from, chip);
        for (var i = 1; i <= steps; i++)
        {
            canvas.OnPointerMoved(from + (to - from) * i / steps);
            if (i != tabAt) continue;
            bayAtTab = canvas.PresetPoints.Count > 0 ? canvas.PresetPoints[0].Point.Bay : -1;
            labels.Add(Label());
            for (var t = 0; t < 20 && !labels[^1].Contains("철조망", StringComparison.Ordinal); t++)
            {
                var (h, target) = RealKey(window, Key.Tab);
                handled.Add(h);
                focusAtTab = target;
                labels.Add(Label());
            }
        }
        canvas.OnPointerReleased(to);
        Pump();
        return new DragRun(focusBefore, focusAtTab, labels, handled, vm.StatusText, vm.FencePresetBayOf(key), TestDragTrace.ReadFrom(mark), bayAtTab);
    }

    private void Log(DragRun r)
    {
        _out.WriteLine($"누르기 전 포커스 '{r.FocusBefore}' · Tab 받은 요소 '{r.FocusAtTab}' · 처리 [{string.Join(",", r.Handled.Distinct())}] · Tab 때 칸 {r.BayAtTab}");
        _out.WriteLine($"붙을 점 [{string.Join(" | ", r.Labels.Distinct())}] → 놓음 '{r.Status}' · 칸 {r.Bay}");
        _out.WriteLine($"진단 기록:{Environment.NewLine}{r.Trace}");
    }

    #region - MNT-R28-426 실창 차례 그대로(재현) -
    [Fact, Trait("Scenario", "MNT-R28-426")]
    public void should_reproduce_the_real_window_426_where_tab_was_pressed_two_steps_before_the_pointer_reached_the_razor_panel()
    {
        var run = OnSta(() =>
        {
            var (vm, _, _) = Seed();
            var key = vm.FenceChain.Keys[9];
            return InWindow(vm, (window, canvas) => RazorDrag(window, vm, canvas, key, RazorPanelTwo(window, vm, canvas, key), null, 26, 24));
        });
        Log(run);

        Assert.NotNull(TestDragTrace.Path);
        Assert.All(run.Handled, Assert.True);                                                          // Tab 은 캔버스가 받았다(포커스는 누를 때 칩으로 옮겨진다)
        Assert.Contains("Tab 이 캔버스에 닿음 · 끄는 중 True", run.Trace);
        Assert.Equal(2, run.BayAtTab);                                                               // 24걸음째 포인터는 아직 망 3(윤형 아님) 위
        Assert.Contains("bay=2 style=ChainLink points", run.Trace);
        Assert.Contains("Tab bay=2", run.Trace);
        Assert.StartsWith("기둥 A · 레일 높이", run.Labels[0]);                                       // 실창 첫 레이블과 같다(망 2/3 사이 기둥)
        Assert.Equal(9, run.Labels.Distinct().Count());                                              // 실창과 같은 9점(기둥 8 + 레일) — 망 3 의 점
        Assert.DoesNotContain(run.Labels, l => l.Contains("철조망", StringComparison.Ordinal));
        Assert.Matches(@"bay=1 style=ChainLinkRazor points=\[[^\]]*Razor:", run.Trace);                // 남은 2걸음에 망 2 로 — 그 칸 후보엔 코일 점이 있다(허용)
        Assert.Equal(1, run.Bay);
        Assert.Contains("망 2 · 상단 레일 · 가운데", run.Status);                                       // 실창 놓음과 같다 — 망 칩 위 1/12 는 코일이 아니라 레일 곁
    }
    #endregion

    #region - MNT-R28-426 포인터가 윤형 칸에 닿은 뒤 Tab · 코일 위에 놓기 -
    [Fact, Trait("Scenario", "MNT-R28-426")]
    public void should_reach_a_coil_point_with_tab_once_the_far_sensor_is_over_the_razor_panel()
    {
        var run = OnSta(() =>
        {
            var (vm, _, _) = Seed();
            var key = vm.FenceChain.Keys[9];
            return InWindow(vm, (window, canvas) => RazorDrag(window, vm, canvas, key, RazorPanelTwo(window, vm, canvas, key), null, 26, 26));
        });
        Log(run);

        Assert.All(run.Handled, Assert.True);
        Assert.Equal(1, run.BayAtTab);
        Assert.Contains(run.Labels, l => l.Contains("철조망", StringComparison.Ordinal));
        Assert.Equal(1, run.Bay);
        Assert.Contains("망 2 · 철조망", run.Status);
    }

    [Fact, Trait("Scenario", "MNT-R28-426")]
    public void should_attach_to_the_coil_with_the_mouse_alone_when_the_far_sensor_is_dropped_on_the_drawn_coil()
    {
        var run = OnSta(() =>
        {
            var (vm, _, _) = Seed();
            var key = vm.FenceChain.Keys[9];
            return InWindow(vm, (window, canvas) =>
            {
                var focus = RazorPanelTwo(window, vm, canvas, key);
                var coil = SnapPointOnScreen(vm, canvas, key, 1, "razor.mid");
                return RazorDrag(window, vm, canvas, key, focus, coil, 26, 0);
            });
        });
        Log(run);

        Assert.Equal(1, run.Bay);
        Assert.Contains("망 2 · 철조망 · 가운데", run.Status);
    }
    #endregion
}
