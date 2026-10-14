using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 2026-10-14 실창 7회차 펜스 보기 실마우스 끌기 3건(MNT-R28-420-flat · 422 · 426) — 실제 WiringView XAML 을 화면 밖 창(Left=-20000 · ShowActivated=false)에 띄워
/// 펜스 캔버스의 포인터 길(누름 → 걸음 이동 → 뗌 — 실창 R28Drag 와 같은 차례)로 다시 연다. 화면에 보이는 창은 띄우지 않는다.
/// 실창 전제: 405 의 "제어기 전체를 A 로 되돌림" 이 먹지 않아(406 '1대 적용' · 451 'A 쓰는 센서 2대') 센서들은 모델 ★자동 프리셋(기둥 A · 꼭대기)에 서 있었다
/// — 420-tilt 의 '가로 +3.00 m'(6 m 칸 × 0.5) 가 그 근거다.
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "FullyQualifiedName~RealWindowR28Round7Tests"</c>
/// </summary>
public class RealWindowR28Round7Tests
{
    private readonly ITestOutputHelper _out;
    public RealWindowR28Round7Tests(ITestOutputHelper output) => _out = output;

    #region - Rig -
    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static T ById<T>(DependencyObject root, string id) where T : FrameworkElement
        => Descendants<T>(root).First(e => AutomationProperties.GetAutomationId(e) == id);

    /// <summary>
    /// 실창 시드와 같은 전제 — 저장하고 다시 연 결선 창 · 센서 모델 창 [+ 모델](★자동 위치 프리셋 = 기둥 A · 꼭대기) · 사용자 프리셋 R28-MOUNT-A(메쉬 30 % · 1.2 m) ·
    /// 모든 센서가 그 모델 ★자동 프리셋에 선다(실창 403 · 405 "A 아닌 프리셋").
    /// </summary>
    private static (WiringViewModel Vm, SensorMountPreset Auto, SensorMountPreset A) Seed()
    {
        var repo = new InMemorySensorPresetRepository();
        var vm = WiringFenceHeightTests.Build("SSSSSS");
        vm.AttachSensorPresets(repo);
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        var smart = vm.MountPresetsOf(SensorPresetCatalog.SMART2);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => WiringPresetPlan.Migrate(p.Value, SensorPresetCatalog.SMART2, smart, l.Panels)))));
        var models = new SensorModelsViewModel(new ChangeNotifyingSensorPresetRepository(repo, () => vm.LoadSensorPresetsAsync()));
        Assert.True(models.LoadAsync().GetAwaiter().GetResult());
        var model = models.AddModelAsync("새 모델 13", EnumDeviceType.SmartSensor2, SensorShape.Smart2).GetAwaiter().GetResult()!;
        var auto = vm.MountPresetsOf(model.Id).Single();
        var a = repo.SaveMountPresetAsync(new SensorMountPreset { ModelId = model.Id, Name = "R28-MOUNT-A", Anchor = SensorMountAnchor.Mesh, AlongRatio = 0.3, HeightM = 1.2, YawDeg = 90 })
            .GetAwaiter().GetResult();
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        vm.FenceSelectAllSensors();
        vm.SelectedPresetModel = vm.PresetModelChoices.Single(m => m.Id == model.Id);
        Assert.All(vm.FenceChain.Keys, k => Assert.Equal(auto.Id, vm.FenceLayout.MountOf(k)!.MountPresetId));
        return (vm, auto, a);
    }

    /// <summary>결선 창 몸(WiringView.xaml 그대로 — 처리기만 뺀 느슨한 읽기)을 화면 밖 창에 띄운다(<see cref="RealWindowR28Tests"/> 와 같다).</summary>
    private static T InWindow<T>(WiringViewModel vm, Func<Window, FenceCanvas, T> body)
    {
        _ = Application.Current;
        vm.UseLayoutPrefs(null, true, (_, _) => { });
        vm.GoWiring();
        vm.ShowFenceView();
        var view = WiringNarrowLayoutTests.LooseWiringView();
        view.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml"))));
        view.DataContext = vm;
        var window = new Window
        {
            Content = view,
            Width = 1280,
            Height = 820,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        Pump();
        try { return body(window, Descendants<FenceCanvas>(window).Single()); }
        finally { window.Close(); }
    }

    /// <summary>실창 R28Drag 와 같은 차례 — 누름 → 걸음 이동(<paramref name="midStep"/> 번째 걸음에서 상태 줄을 읽는다) → 뗌. 끄는 중 상태 줄을 돌려준다.</summary>
    private static string Drag(WiringViewModel vm, FenceCanvas canvas, int key, Point to, int steps = 24, int midStep = 20, Action? atMid = null)
    {
        var chip = canvas.SensorChips[key];
        var from = canvas.ScreenCenterOf(chip);
        canvas.OnPointerPressed(from, chip);
        var mid = string.Empty;
        for (var i = 1; i <= steps; i++)
        {
            canvas.OnPointerMoved(from + (to - from) * i / steps);
            if (i == midStep) { mid = vm.StatusText; atMid?.Invoke(); }
        }
        canvas.OnPointerReleased(to);
        Pump();
        return mid;
    }

    /// <summary>실창 R28OverrideOf — 칩 Enter(= 그 센서 고르기) 뒤 속성 칸 "바뀜"(보일 때만 읽는다).</summary>
    private static string OverrideOf(Window window, WiringViewModel vm, int key)
    {
        vm.FenceSelect(key);
        Pump();
        var text = ById<TextBlock>(window, "Devices.Wiring.Fence.Preset.Override");
        return text.IsVisible ? text.Text : string.Empty;
    }

    /// <summary>포커스 요소에 키 하나 — 창 입력 원천의 PreviewKeyDown → KeyDown 차례(<see cref="RealWindowR28Tests"/> 와 같다).</summary>
    private static bool Press(UIElement target, Key key)
    {
        var source = PresentationSource.FromVisual(target)!;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        if (args.Handled) return true;
        args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    /// <summary>칸 <paramref name="bay"/> 의 붙는 점 <paramref name="name"/> 의 화면 좌표.</summary>
    private static Point SnapPointOnScreen(WiringViewModel vm, FenceCanvas canvas, int key, int bay, string name)
    {
        var point = vm.FencePresetSnapPoints(key, bay).Single(p => p.Point.Name == name);
        return canvas.WorldToScreen(FencePresetDrag.PointAt(point, canvas.Projector, canvas.Scene!.Upm, canvas.Scene.Vpm));
    }
    #endregion

    #region - MNT-R28-420-flat 놓은 자리가 프리셋 자리와 같으면 -
    [Fact, Trait("Scenario", "MNT-R28-420")]
    public void should_not_say_changed_from_the_preset_in_the_status_when_the_drop_lands_on_the_preset_point_of_another_bay_in_the_flat_view()
    {
        var result = OnSta(() =>
        {
            var (vm, auto, a) = Seed();
            var key = vm.FenceChain.Keys[2];
            return InWindow(vm, (window, canvas) =>
            {
                vm.ChooseFlat();
                vm.FenceSelect(key);                                                                 // 실창 R28ChipStart — 칩 Enter(한 대)
                Pump();
                var from = vm.FencePresetBayOf(key)!.Value;
                var to = from + 1 < vm.FenceLayout.Panels.Count ? from + 1 : from - 1;
                var mid = Drag(vm, canvas, key, SnapPointOnScreen(vm, canvas, key, to, "post_a.top"));
                var status = vm.StatusText;
                var ov = OverrideOf(window, vm, key);
                var bay = vm.FencePresetBayOf(key);

                // 대조 — A(메쉬 30 % · 1.2 m)에 선 센서를 같은 점에 놓으면 따로 고친 값이 생긴다
                var other = vm.FenceChain.Keys[4];
                vm.FenceSelect(other);
                vm.SelectedMountPreset = a;
                Pump();
                var otherBay = vm.FencePresetBayOf(other)!.Value;
                var otherTo = otherBay + 1 < vm.FenceLayout.Panels.Count ? otherBay + 1 : otherBay - 1;
                Drag(vm, canvas, other, SnapPointOnScreen(vm, canvas, other, otherTo, "post_a.top"));
                var status2 = vm.StatusText;
                var ov2 = OverrideOf(window, vm, other);
                return (auto.SnapPoint, from, to, bay, mid, status, ov, status2, ov2);
            });
        });

        _out.WriteLine($"★자동 프리셋 점 '{result.SnapPoint}' · 칸 {result.from} → {result.bay}(놓을 칸 {result.to})");
        _out.WriteLine($"끄는 중 '{result.mid}' → 놓음 '{result.status}' · 바뀜 '{result.ov}'");
        _out.WriteLine($"대조(A) 놓음 '{result.status2}' · 바뀜 '{result.ov2}'");
        Assert.Equal("post_a.top", result.SnapPoint);                                                  // 새 모델 ★자동 = 기둥 A · 꼭대기
        Assert.Contains("에 붙음", result.mid);
        Assert.Equal(result.to, result.bay);                                                          // 옆 칸으로 옮겨졌다
        Assert.Equal(string.Empty, result.ov);                                                        // 같은 점 = 따로 고친 값 없음(실창 '바뀜 ''')
        Assert.DoesNotContain("프리셋에서 바뀜", result.status);                                        // 고치기 전: 상태줄만 '프리셋에서 바뀜 · [프리셋으로] 로 되돌림'
        Assert.Contains("프리셋 자리 그대로", result.status);
        Assert.Contains("프리셋에서 바뀜", result.status2);                                              // 다른 자리면 둘 다 같은 말
        Assert.StartsWith("프리셋에서 바뀜: ", result.ov2);
    }
    #endregion

    #region - MNT-R28-422 Alt 자유 위치를 망 위 허공에 놓으면 -
    [Fact, Trait("Scenario", "MNT-R28-422")]
    public void should_land_an_alt_free_drop_above_the_mesh_on_the_top_rail_and_say_the_same_spot_while_dragging()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _) = Seed();
            var key = vm.FenceChain.Keys[3];
            return InWindow(vm, (window, canvas) =>
            {
                vm.FenceSelect(key);                                                                 // 실창 R28ChipStart — 칩 Enter(한 대)
                Pump();
                var bay = vm.FencePresetBayOf(key)!.Value;
                var panel = vm.FenceLayout.Geometry.Panels[bay];
                var sample = FenceSampleBay.FromPanel(vm.FenceLayout.Panels[bay]);
                // 실창 끄는 중 알약 '자유 위치 · 가로 1.27 m · 높이 3.28 m' — 기둥 꼭대기(2.6 m)보다 높은 망 위 허공
                var to = canvas.WorldToScreen(canvas.Projector.P((panel.StartM + 1.27) * canvas.Scene!.Upm, 3.28 * canvas.Scene.Vpm, 0));
                canvas.ForceFreeMove = true;                                                        // Alt 를 누른 채
                var mid = Drag(vm, canvas, key, to);
                canvas.ForceFreeMove = null;
                var status = vm.StatusText;
                var pose = vm.FencePoseOf(key)!;
                var ov = OverrideOf(window, vm, key);
                return (mid, status, ov, pose.AlongM, pose.HeightM, sample.FenceHeightM, sample.PostTopM, Bay: vm.FencePresetBayOf(key), From: bay);
            });
        });

        _out.WriteLine($"끄는 중 '{result.mid}' → 놓음 '{result.status}' · 자리 가로 {result.AlongM:0.00} · 높이 {result.HeightM:0.00} · 바뀜 '{result.ov}'");
        Assert.Contains("자유 위치", result.mid);
        Assert.Equal(result.From, result.Bay);
        Assert.Equal(result.FenceHeightM, result.HeightM, 2);                                          // 바로 아래 상단 레일에 붙는다(편집기 Alt 와 같은 규칙)
        Assert.Equal(1.27, result.AlongM, 1);                                                         // 고치기 전: 기둥 A 꼭대기(0 m · 2.6 m)로 되돌아갔다
        Assert.StartsWith("프리셋에서 바뀜: ", result.ov);                                              // 실창 '바뀜 '''
        Assert.Contains($"높이 {result.HeightM:0.00} m", result.mid);                                  // 끄는 중 알약도 놓으면 설 자리 높이를 말한다(허공 높이 아님 · 가로는 아직 끄는 중)
        Assert.Contains("가로 1.27 m", result.status);
        Assert.Contains($"높이 {result.HeightM:0.00} m", result.status);
        Assert.Contains("프리셋에서 바뀜", result.status);
    }
    #endregion

    #region - MNT-R28-426 철조망 칸 위로 끌기(실창 차례 — 망 칩 Enter · 윤형 · 적용) -
    [Fact, Trait("Scenario", "MNT-R28-426")]
    public void should_offer_coil_points_over_the_panel_chip_whose_style_was_set_to_razor_through_the_real_panel_pane()
    {
        var result = OnSta(() =>
        {
            var (vm, _, _) = Seed();
            var key = vm.FenceChain.Keys[1];
            return InWindow(vm, (window, canvas) =>
            {
                // 실창 505~510행: 센서 Enter → Esc → 'Devices.Wiring.Fence.Panel.1' 에 포커스 · Enter → [철조망과 윤형철조망] → [선택한 망에 적용]
                vm.FenceSelect(key);
                Pump();
                var panelChip = canvas.PanelChips[1];
                var panelId = AutomationProperties.GetAutomationId(panelChip);
                var panelName = AutomationProperties.GetName(panelChip);
                panelChip.Focus();
                var entered = Press(panelChip, Key.Enter);
                Pump();
                var selected = vm.IsPanelSelected(1);
                var style = ById<Button>(window, "Devices.Wiring.Fence.PanelPane.Style.ChainLinkRazor");
                var styleVisible = style.IsVisible;
                vm.ChoosePanelStyle(EnumFenceStyle.ChainLinkRazor);                                 // = OnPanelStyle(Tag)
                Pump();
                var apply = ById<Button>(window, "Devices.Wiring.Fence.PanelPane.Apply");
                var applyEnabled = apply.IsEnabled;
                vm.ApplyPanelEdit();                                                                // = OnApplyPanelEdit
                Pump();
                var styles = vm.FenceLayout.Panels.Select(p => p.Style).ToList();

                // 실창 R28FenceRazorDrag — 칩 가운데 → 'Panel.1' 사각형 위 1/12 지점
                vm.FenceSelect(key);
                Pump();
                var rect = panelChip.TransformToAncestor(canvas).TransformBounds(new Rect(0, 0, panelChip.ActualWidth, panelChip.ActualHeight));
                var to = new Point(rect.Left + rect.Width / 2, rect.Top + Math.Max(3, rect.Height / 12));
                var points = new List<(int Bay, SensorAnchorKind Kind, bool Allowed)>();
                var labels = new List<string>();
                string Label() => vm.StatusText.Split(" — ")[0];
                Drag(vm, canvas, key, to, 26, 24, () =>
                {
                    points.AddRange(canvas.PresetPoints.Select(p => (p.Point.Bay, p.Point.Point.Kind, p.Point.Allowed)));
                    labels.Add(Label());
                    // 실창과 같이 '철조망' 이 나올 때까지 Tab(최대 20번)
                    for (var i = 0; i < 20 && !labels[^1].Contains("철조망", StringComparison.Ordinal); i++)
                    {
                        canvas.HandleKeyDown(Key.Tab, Key.None, ModifierKeys.None, canvas);
                        labels.Add(Label());
                    }
                });
                return (panelId, panelName, entered, selected, styleVisible, applyEnabled, styles, points, labels, Status: vm.StatusText, Bay: vm.FencePresetBayOf(key));
            });
        });

        _out.WriteLine($"망 칩 '{result.panelId}' = '{result.panelName}' · Enter 처리 {result.entered} · 망 2 고름 {result.selected} · 윤형 단추 보임 {result.styleVisible} · [적용] 켬 {result.applyEnabled}");
        _out.WriteLine($"칸 모양 [{string.Join(", ", result.styles)}]");
        _out.WriteLine($"점 [{string.Join(", ", result.points.Select(p => $"{p.Bay}:{p.Kind}{(p.Allowed ? "" : "(×)")}"))}]");
        _out.WriteLine($"붙을 점 [{string.Join(" | ", result.labels)}] → 놓음 '{result.Status}'");
        Assert.Equal("Devices.Wiring.Fence.Panel.1", result.panelId);
        Assert.StartsWith("망 2,", result.panelName);                                                  // 자동화 id 의 1 은 0 부터 센 번호 — 사람 말로 '망 2'(실창 시험 이름 '망 1' 과 다르다)
        Assert.True(result.entered);
        Assert.True(result.selected);
        Assert.True(result.styleVisible);
        Assert.True(result.applyEnabled);
        Assert.Equal(EnumFenceStyle.ChainLinkRazor, result.styles[1]);
        Assert.Contains(result.points, p => p.Bay == 1 && p.Kind == SensorAnchorKind.Razor && p.Allowed);
        Assert.StartsWith("상단 레일 · 가운데", result.labels[0]);                                      // 시험이 놓는 점(망 칩 사각형 위 1/12)은 코일 적중 영역이 아니다 — 처음엔 레일
        Assert.Contains(result.labels, l => l.Contains("철조망", StringComparison.Ordinal));            // Tab 으로 코일 점에 간다(실창 7회차엔 끝내 없었다 — 그 칸이 윤형이 아니었다)
        Assert.Equal(1, result.Bay);
        Assert.Contains("망 2 · 철조망", result.Status);
    }
    #endregion
}
