using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
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
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 2026-10-13 통합 실창 회차(round-r28)가 잡은 센서 모델 창(W1) · 위치 프리셋 편집기(W2) 결함 — 헤드리스(창을 띄우지 않는다 · 가짜 저장소).
/// 컴파일된 뷰를 부모 없이 Measure/Arrange 하고, 키는 HWND 없는 입력 원천(<see cref="HeadlessSource"/>)에서 터널로 울린다(틀 → 그림 순서 그대로).
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "Scenario~MNT-R28"</c>
/// </summary>
public class SensorPresetR28Tests
{
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

    private static ResourceDictionary Theme()
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml")));

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
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

    private static T? ById<T>(DependencyObject root, string id) where T : FrameworkElement
        => Descendants<T>(root).FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id);

    /// <summary>HWND 없는 입력 원천 — 키 사건의 뿌리.</summary>
    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual
        {
            get => _root!;
            set { var old = _root; _root = value; RootChanged(old!, value); }
        }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    /// <summary>키 하나 — 뿌리(틀)부터 터널로 내려와 <paramref name="target"/> 에 닿는다. 참 = 누군가 소비.</summary>
    private static bool Press(HeadlessSource source, UIElement target, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private static SensorModelsViewModel LoadedModels(InMemorySensorPresetRepository? repo = null)
    {
        var vm = new SensorModelsViewModel(repo ?? new InMemorySensorPresetRepository());
        Assert.True(vm.LoadAsync().GetAwaiter().GetResult());
        return vm;
    }

    private static SensorModelsView ModelsView(SensorModelsViewModel vm, double width = 880, double height = 420)
    {
        var view = new SensorModelsView();
        view.Resources.MergedDictionaries.Add(Theme());
        view.DataContext = vm;
        Layout(view, width, height);
        return view;
    }

    /// <summary>W1 [+ 모델] 이 만드는 것과 같은 사용자 모델(스마트 모양 → 기둥 · 레일 · 철조망 · 담).</summary>
    private static (InMemorySensorPresetRepository Repo, SensorModel Model) UserModel()
    {
        var repo = new InMemorySensorPresetRepository();
        var vm = LoadedModels(repo);
        var model = vm.AddModelAsync(vm.NextNewModelName(), EnumDeviceType.SmartSensor, SensorShape.Smart).GetAwaiter().GetResult();
        Assert.NotNull(model);
        return (repo, model!);
    }

    private static MountPresetEditorViewModel Editor(SensorModel? model = null, InMemorySensorPresetRepository? repo = null, FenceSampleBay? bay = null)
    {
        repo ??= new InMemorySensorPresetRepository();
        model ??= SensorPresetCatalog.Models.First(m => m.Id == SensorPresetCatalog.MULTI);
        var preset = repo.GetMountPresetsAsync(model.Id).GetAwaiter().GetResult().FirstOrDefault(p => p.IsDefault)
                     ?? SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.MULTI && p.IsDefault);
        return new MountPresetEditorViewModel(repo, model, preset, bay ?? new FenceSampleBay(3.0, 2.4, false));
    }

    private static MountPresetEditorView EditorView(MountPresetEditorViewModel vm)
    {
        var view = new MountPresetEditorView();
        view.Resources.MergedDictionaries.Add(Theme());
        view.DataContext = vm;
        Layout(view, 980, 680);
        return view;
    }
    #endregion

    #region - W1 센서 모델 창 -
    [Fact, Trait("Scenario", "MNT-R28-102")]
    public void should_show_select_and_name_the_new_row_in_the_models_list_when_a_model_is_added()
    {
        var result = OnSta(() =>
        {
            var vm = LoadedModels();
            var view = ModelsView(vm);                                                              // 목록 칸이 7줄 남짓인 낮은 창(실창 640 과 같은 상황)
            var name = vm.NextNewModelName();
            var added = vm.AddModelAsync(name, EnumDeviceType.SmartSensor, SensorShape.Smart).GetAwaiter().GetResult()!;
            Layout(view, 880, 420);
            var list = ById<ListBox>(view, "Devices.SensorPresets.Models.List")!;
            var rows = Descendants<ListBoxItem>(list).Select(r => (Id: AutomationProperties.GetAutomationId(r), Name: AutomationProperties.GetName(r))).ToList();
            var nameBox = ById<TextBox>(view, "Devices.SensorPresets.Models.Name");
            return (name, added.Id, rows, Selected: (list.SelectedItem as SensorModelRow)?.Model.Id, vm.StatusText,
                    NameBox: (Found: nameBox is not null, Text: nameBox?.Text, ReadOnly: nameBox?.IsReadOnly));
        });

        Assert.Equal("새 모델 8", result.name);
        Assert.Equal(8, result.rows.Count);                                                         // 목록에 새 줄(가상화로 화면 밖 줄이 빠지지 않는다)
        Assert.Contains(result.rows, r => r.Id == $"Devices.SensorPresets.Models.Row.{result.Id}" && r.Name == "새 모델 8");
        Assert.Equal(result.Id, result.Selected);                                                   // 새 줄을 고른 채
        Assert.Equal("모델을 추가했습니다 — 새 모델 8", result.StatusText);
        Assert.Equal((true, "새 모델 8", (bool?)false), result.NameBox);                           // 이름 칸 — 바로 고칠 수 있다
    }

    [Fact, Trait("Scenario", "MNT-R28-102")]
    public async System.Threading.Tasks.Task should_rename_user_models_from_the_name_field_and_keep_builtin_names_fixed()
    {
        var repo = new InMemorySensorPresetRepository();
        var vm = new SensorModelsViewModel(repo);
        await vm.LoadAsync();
        var added = (await vm.AddModelAsync(vm.NextNewModelName(), EnumDeviceType.SmartSensor, SensorShape.Smart))!;
        Assert.True(vm.CanRenameModel);
        Assert.False(vm.IsModelNameReadOnly);

        vm.ModelNameText = "  R28-MODEL-A  ";
        Assert.True(await vm.RenameModelAsync());
        Assert.Equal("R28-MODEL-A", (await repo.GetModelsAsync()).Single(m => m.Id == added.Id).Name);
        Assert.Equal("R28-MODEL-A", vm.SelectedModel!.Model.Name);                                // 다시 읽은 뒤에도 같은 줄을 고른 채
        Assert.Equal("이름을 바꿨습니다 — 새 모델 8 → R28-MODEL-A", vm.StatusText);

        vm.ModelNameText = "펜스센서 MEMS";                                                         // 이미 있는 이름
        Assert.False(await vm.RenameModelAsync());
        Assert.Equal("R28-MODEL-A", vm.ModelNameText);
        vm.ModelNameText = "   ";
        Assert.False(await vm.RenameModelAsync());
        Assert.Equal("모델 이름을 적어 주세요.", vm.StatusText);

        vm.SelectedModel = vm.Models.First(m => m.Model.IsBuiltin);                                  // 회사 기본 = 이름 고정
        Assert.False(vm.CanRenameModel);
        Assert.True(vm.IsModelNameReadOnly);
        var builtin = vm.SelectedModel.Model.Name;
        vm.ModelNameText = "바꾼 이름";
        Assert.False(await vm.RenameModelAsync());
        Assert.Equal(builtin, (await repo.GetModelsAsync()).First(m => m.Id == vm.SelectedModel!.Model.Id).Name);
    }

    [Fact, Trait("Scenario", "MNT-R28-104")]
    public async System.Threading.Tasks.Task should_name_the_selected_model_after_hiding_and_showing_it()
    {
        var vm = new SensorModelsViewModel(new InMemorySensorPresetRepository());
        await vm.LoadAsync();
        await vm.AddModelAsync("R28-MODEL-H", EnumDeviceType.SmartSensor, SensorShape.Smart);
        Assert.True(await vm.ToggleHiddenAsync());
        Assert.Equal(("R28-MODEL-H", "R28-MODEL-H (숨김)", "보이기", "숨겼습니다 — R28-MODEL-H"),
                     (vm.SelectedModelName, vm.SelectedModel!.Name, vm.HideText, vm.StatusText));
        Assert.Equal("R28-MODEL-H", vm.ModelNameText);
        Assert.True(await vm.ToggleHiddenAsync());
        Assert.Equal(("R28-MODEL-H", "R28-MODEL-H", "숨기기", "보이게 했습니다 — R28-MODEL-H"),
                     (vm.SelectedModelName, vm.SelectedModel!.Name, vm.HideText, vm.StatusText));
    }

    [Fact, Trait("Scenario", "MNT-R28-224")]
    public void should_expose_the_gis_db_id_of_every_mount_preset_row_as_its_automation_id()
    {
        var result = OnSta(() =>
        {
            var vm = LoadedModels();
            vm.SelectedModel = vm.Models.First(m => m.Model.Id == SensorPresetCatalog.MULTI);
            var view = ModelsView(vm, 880, 520);
            var list = ById<ListBox>(view, "Devices.SensorPresets.Models.Mount.List")!;
            return (Ids: Descendants<ListBoxItem>(list).Select(r => AutomationProperties.GetAutomationId(r)).ToList(),
                    Expected: vm.MountPresets.Select(r => $"Devices.SensorPresets.Models.Mount.Row.{r.Preset.Id}").ToList(),
                    Names: Descendants<ListBoxItem>(list).Select(r => AutomationProperties.GetName(r)).ToList(),
                    ExpectedNames: vm.MountPresets.Select(r => r.Name).ToList());
        });

        Assert.NotEmpty(result.Expected);
        Assert.Equal(result.Expected, result.Ids);
        Assert.Equal(result.ExpectedNames, result.Names);
    }
    #endregion

    #region - W2 수치 칸 범위 (MNT-R28-207) -
    [Fact, Trait("Scenario", "MNT-R28-207")]
    public void should_not_bury_an_above_ground_model_when_a_negative_height_is_typed()
    {
        var (repo, model) = UserModel();
        var vm = Editor(model, repo);
        vm.HeightText = "-1";
        Assert.True(vm.ApplyFields());
        Assert.True(vm.Pose.HeightM >= SensorMountResolver.MIN_ABOVE_GROUND_M - 1e-9, $"높이 {vm.Pose.HeightM}");
        Assert.NotEqual(SensorMountAnchor.Ground, vm.Pose.Anchor);                                   // 바닥 갈래로 바뀌지 않는다
        Assert.Contains("땅속에는 둘 수 없는", vm.StatusText);
        Assert.Contains("0.05 m", vm.FieldNotice);
        Assert.True(SensorMountSurfaces.IsAttached(vm.Pose, vm.Bay));                                 // 공중 · 땅속 금지 불변식(FR-22)
    }

    [Fact, Trait("Scenario", "MNT-R28-207")]
    public void should_keep_an_underground_only_model_in_the_ground_when_a_positive_height_is_typed()
    {
        var model = SensorPresetCatalog.Models.Single(m => m.AllowedKinds.SequenceEqual(new[] { SensorAnchorKind.Ground }));
        var repo = new InMemorySensorPresetRepository();
        var vm = Editor(model, repo);
        vm.HeightText = "1.2";
        Assert.True(vm.ApplyFields());
        Assert.True(vm.Pose.HeightM <= 0, $"높이 {vm.Pose.HeightM}");
        Assert.Equal(SensorMountAnchor.Ground, vm.Pose.Anchor);
        Assert.Contains("바닥(지중)에만", vm.FieldNotice);
    }

    [Fact, Trait("Scenario", "MNT-R28-207")]
    public void should_tell_which_out_of_range_values_were_trimmed_instead_of_trimming_silently()
    {
        var vm = Editor();
        vm.TiltText = "200";
        vm.YawText = "200";
        vm.BracketText = "1";
        vm.DepthText = "9";
        Assert.True(vm.ApplyFields());
        Assert.Equal(90, vm.Pose.TiltDeg);
        Assert.Equal(-160, vm.Pose.YawDeg);
        Assert.True(Math.Abs(vm.Pose.DepthM) <= 1.0 + 1e-9);                                        // 5 m 로 자른 뒤 브래킷 끝(FR-22)
        Assert.Contains("위아래 각도는 −90~90° — 200° 를 90° 로 맞췄습니다", vm.FieldNotice);
        Assert.Contains("바라보는 방향은 −180~180° — 200° 는 -160° 와 같아", vm.FieldNotice);
        Assert.Contains("펜스에서 떨어진 거리는 ±5 m — 9 m 를 5 m 로 맞췄습니다", vm.FieldNotice);
        Assert.Contains("브래킷", vm.FieldNotice);
        Assert.Equal(vm.FieldNotice, vm.StatusText);

        vm.AlongPercentText = "abc";                                                                  // 숫자가 아니면 받지 않고 안내
        Assert.False(vm.ApplyFields());
        Assert.Equal("칸 안 가로 칸은 숫자로 적어 주세요.", vm.StatusText);
        Assert.Equal(vm.StatusText, vm.FieldNotice);

        vm.HeightText = "1.5";                                                                        // 범위 안 — 안내 없음
        Assert.True(vm.ApplyFields());
        Assert.Equal(string.Empty, vm.FieldNotice);
    }
    #endregion

    #region - W2 그림 키보드 · Esc (MNT-R28-211 · 214 · 216 · 217) -
    [Fact, Trait("Scenario", "MNT-R28-214")]
    public void should_keep_focus_on_the_front_view_when_its_empty_area_is_clicked()
    {
        var handled = OnSta(() =>
        {
            var view = EditorView(Editor());
            var front = ById<MountBayCanvas>(view, "Devices.SensorPresets.MountEditor.FrontView")!;
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent };
            front.RaiseEvent(args);                                                                   // 빈 곳 누름(아이콘 · 손잡이 밖)
            return args.Handled;
        });

        Assert.True(handled, "MouseDown 이 처리되지 않으면 바깥 ScrollViewer 가 포커스를 가져가 Tab 이 입력 칸으로 빠진다");
    }

    [Fact, Trait("Scenario", "MNT-R28-214,MNT-R28-216")]
    public void should_cycle_snap_points_with_tab_and_move_ten_centimetres_with_arrows_when_the_front_view_has_focus()
    {
        var result = OnSta(() =>
        {
            var vm = Editor();
            var view = EditorView(vm);
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var front = ById<MountBayCanvas>(view, "Devices.SensorPresets.MountEditor.FrontView")!;
                var tabs = new List<(bool Handled, string Snap, string Status)>();
                for (var i = 0; i < 3; i++)
                {
                    var h = Press(source, front, Key.Tab);
                    tabs.Add((h, vm.SnapText, vm.StatusText));
                }
                var before = vm.Pose.HeightM;
                var down = Press(source, front, Key.Down);
                var after = vm.Pose.HeightM;
                return (tabs, before, down, after, Status: vm.StatusText);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.All(result.tabs, t => Assert.True(t.Handled));                                         // 틀 · 포커스 이동이 먹지 않는다
        Assert.Equal(3, result.tabs.Select(t => t.Snap).Distinct().Count());                          // 세 번 모두 다른 붙는 점
        Assert.All(result.tabs, t => Assert.NotEqual("자유 위치", t.Snap));
        Assert.All(result.tabs, t => Assert.EndsWith("에 붙음", t.Status));                             // 붙는 점 이름이 상태 줄에
        Assert.True(result.down);
        Assert.Equal(result.before - 0.10, result.after, 3);                                          // ↓ = 10 cm
        Assert.Contains("에 붙음 · ", result.Status);
    }

    [Fact, Trait("Scenario", "MNT-R28-216")]
    public void should_move_one_centimetre_with_shift_arrow_and_name_the_object_it_is_on()
    {
        var vm = Editor();
        vm.NextSnapPoint();
        var h = vm.Pose.HeightM;
        vm.Nudge(0, -1, fine: true);                                                                  // Shift+↓
        Assert.Equal(h - 0.01, vm.Pose.HeightM, 3);
        Assert.Matches(@"^.+\(자유 위치\)에 붙음 · \d+\.\d\d m$", vm.StatusText);
    }

    [Fact, Trait("Scenario", "MNT-R28-217")]
    public void should_cycle_only_wall_top_and_face_with_tab_when_the_sample_is_a_wall()
    {
        var result = OnSta(() =>
        {
            var (repo, model) = UserModel();
            var vm = Editor(model, repo);
            var view = EditorView(vm);
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                vm.IsWall = true;                                                                     // [담(벽)] 켬
                var front = ById<MountBayCanvas>(view, "Devices.SensorPresets.MountEditor.FrontView")!;
                var snaps = new List<string>();
                for (var i = 0; i < 4; i++) { Press(source, front, Key.Tab); snaps.Add(vm.SnapText); }
                return snaps;
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Equal(new[] { "담 위", "담 앞면", "담 위", "담 앞면" }, result);                         // 기둥 · 메쉬 없음
    }

    [Fact, Trait("Scenario", "MNT-R28-211")]
    public void should_restore_the_drag_on_esc_without_closing_the_editor_and_close_it_when_not_dragging()
    {
        var result = OnSta(() =>
        {
            var vm = Editor();
            var view = EditorView(vm);
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var cancels = 0;
                view.AddHandler(ConsoleDialogFrame.SecondaryInvokedEvent, new RoutedEventHandler((_, _) => cancels++), handledEventsToo: true);
                var front = ById<MountBayCanvas>(view, "Devices.SensorPresets.MountEditor.FrontView")!;
                var before = vm.Draft;
                vm.BeginDrag();
                vm.DragTo(MountView.Front, 0.05, 1.2, free: false);                                   // 끄는 중(기둥 A 쪽에 붙을 참)
                var duringDrag = (Frame: front.IsDragging, Moved: vm.Pose != SensorMountResolver.Resolve(before, null, vm.Bay));
                var escHandled = Press(source, front, Key.Escape);
                var afterEsc = (cancels, vm.IsDragging, Same: vm.Draft == before, vm.StatusText);
                var enterWhileIdle = cancels;
                var escIdle = Press(source, front, Key.Escape);                                        // 끌지 않을 때 Esc = 창 닫기(지금처럼)
                return (duringDrag, escHandled, afterEsc, escIdle, Cancels: cancels, enterWhileIdle);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(result.duringDrag.Frame);
        Assert.True(result.duringDrag.Moved);
        Assert.True(result.escHandled);                                                               // 그림이 받았다
        Assert.Equal((0, false, true, "원래 자리로 돌렸습니다."), result.afterEsc);                   // 창은 닫히지 않고 원위치
        Assert.True(result.escIdle);
        Assert.Equal(1, result.Cancels);                                                              // 끌지 않을 때는 닫기
    }

    [Fact, Trait("Scenario", "MNT-R28-211")]
    public void should_leave_escape_and_enter_to_the_dragging_element_in_the_dialog_key_rules()
    {
        Assert.Equal(DialogKeyAction.None, DialogKeyRules.Decide(Key.Escape, true, false, false, innerDragActive: true));
        Assert.Equal(DialogKeyAction.None, DialogKeyRules.Decide(Key.Enter, true, false, false, innerDragActive: true));
        Assert.Equal(DialogKeyAction.Cancel, DialogKeyRules.Decide(Key.Escape, true, false, false, innerDragActive: false));
        Assert.Equal(DialogKeyAction.Primary, DialogKeyRules.Decide(Key.Enter, true, false, false, innerDragActive: false));
    }
    #endregion
}
