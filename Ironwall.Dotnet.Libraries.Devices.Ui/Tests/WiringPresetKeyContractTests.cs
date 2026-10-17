using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 펜스 보기 기존 키 · 끌기 계약 고정(2026-10-13 실창 회귀) — 저장 때 이관(FR-19)된 센서가 위치 프리셋 길로 새어
/// Alt+↑/↓ · Shift+Alt+↑/↓ · Ctrl+←/→ · 끌기 · 여러 대 끌기 · 3×3 고르개가 소리 없이 바뀌었던 것을 다시 막는다.
/// 창을 띄우지 않는다(부모 없는 캔버스 · 뷰모델 · 순수 함수 · 가짜 저장소).
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "FullyQualifiedName~WiringPresetKeyContractTests"</c>
/// </summary>
public class WiringPresetKeyContractTests
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

    /// <summary>부모 없는 펜스 캔버스(테마 토큰을 제 자원으로).</summary>
    private static FenceCanvas Canvas(WiringViewModel vm, double width = 1200, double height = 520)
    {
        var canvas = new FenceCanvas { SuppressMenuPopup = true };
        canvas.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml"))));
        canvas.ViewModel = vm;
        Layout(canvas, width, height);
        canvas.Rebuild();
        Layout(canvas, width, height);
        return canvas;
    }

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    /// <summary>프리셋을 읽은 결선 창.</summary>
    private static WiringViewModel WithPresets(string types = "SSSS")
    {
        var vm = WiringFenceHeightTests.Build(types);
        vm.AttachSensorPresets(new InMemorySensorPresetRepository());
        Assert.True(vm.LoadSensorPresetsAsync().GetAwaiter().GetResult());
        return vm;
    }

    /// <summary>
    /// 저장하고 다시 연 상태 — 모든 자리를 저장 때와 같은 규칙(<see cref="WiringPresetPlan.Migrate"/>)으로 이관한다(실창 회귀의 전제: 앞 시험이 저장한 시드).
    /// </summary>
    private static WiringViewModel SavedAndReopened(string types = "SSSS")
    {
        var vm = WithPresets(types);
        var smart = vm.MountPresetsOf(SensorPresetCatalog.SMART2);
        var fence = vm.MountPresetsOf(SensorPresetCatalog.FENCE);
        Assert.True(vm.Board.ApplyFenceEdit(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => WiringPresetPlan.Migrate(p.Value, types[p.Key - 101] == 'S' ? SensorPresetCatalog.SMART2 : SensorPresetCatalog.FENCE,
                                          types[p.Key - 101] == 'S' ? smart : fence, l.Panels)))));
        Assert.All(vm.FenceChain.Keys, k => Assert.NotNull(vm.FenceLayout.MountOf(k)!.MountPresetId));     // 전제 — 모두 이관됐다
        return vm;
    }

    private static int StandOnChosenPreset(WiringViewModel vm, int key)
    {
        vm.FenceSelect(key);
        vm.SelectedMountPreset = vm.PresetMountChoices.First(p => p.IsDefault);
        Assert.True(vm.IsPresetMounted(key));
        return key;
    }
    #endregion

    #region - 이관된 자리 = 9자리가 정본 -
    [Fact, Trait("Scenario", "SC-FEN24-004")]
    public void should_draw_migrated_sensors_exactly_where_the_nine_point_layout_draws_them_and_keep_the_preset_pane()
    {
        var legacy = WithPresets();
        var before = FenceWorld.FromLayout(legacy.FenceChain, legacy.FenceSensors(), legacy.FenceLayout, legacy.FenceSpacing, legacy.FencePoseOf);
        var vm = SavedAndReopened();
        var after = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), vm.FenceLayout, vm.FenceSpacing, vm.FencePoseOf);

        foreach (var key in vm.FenceChain.Keys)
        {
            Assert.False(vm.IsPresetMounted(key));                                                    // 끌기 · 키는 옛 길
            Assert.Null(vm.FencePoseOf(key));                                                         // 그림은 9자리
            Assert.Null(vm.FenceLayout.MountOf(key)!.MountPresetChosen);                             // 이관은 "고름" 표지를 적지 않는다
            Assert.Equal(before.X[key], after.X[key], 6);
            Assert.Equal(before.LiftOf(key), after.LiftOf(key), 6);
        }

        vm.FenceSelect(vm.FenceChain.Keys[0]);                                                        // 설치 위치 절은 이관값을 그대로 보인다
        Assert.NotNull(vm.SelectedMountPreset);
        Assert.NotEqual(string.Empty, vm.PresetHeightText);
    }

    [Fact, Trait("Scenario", "SC-FEN24-004")]
    public void should_ignore_the_combo_writing_back_the_same_preset_so_a_migrated_seat_stays_on_the_nine_point_layout()
    {
        var vm = SavedAndReopened();
        var key = vm.FenceChain.Keys[0];
        vm.FenceSelect(key);
        var before = vm.FenceLayout.MountOf(key)!;
        var undo = vm.CanUndo;

        vm.SelectedMountPreset = vm.SelectedMountPreset;                                              // 콤보 되쓰기(같은 값)
        vm.SelectedAttributePreset = vm.SelectedAttributePreset;

        Assert.Equal(before, vm.FenceLayout.MountOf(key));
        Assert.False(vm.IsPresetMounted(key));
        Assert.Equal(undo, vm.CanUndo);

        vm.ResetMountToPreset();                                                                       // 사람이 [프리셋으로] 를 누르면 그때 "고름"
        Assert.True(vm.IsPresetMounted(key));
    }

    [Fact, Trait("Scenario", "SC-FEN-020")]
    public void should_step_one_stop_with_alt_down_on_a_migrated_sensor_not_nudge_the_preset_height()
    {
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            var canvas = Canvas(vm);
            var before = vm.FenceLayout.MountOf(102)!;
            var handled = canvas.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt, canvas.SensorChips[102]);
            return (handled, before, After: vm.FenceLayout.MountOf(102)!, vm.StatusText);
        });

        Assert.True(result.handled);
        Assert.NotEqual(result.before.Spot, result.After.Spot);                                       // 높이 한 단계(자리가 바뀐다)
        Assert.StartsWith("높이 — ", result.StatusText);
        Assert.DoesNotContain("프리셋에서 바뀜", result.StatusText);
        Assert.Null(result.After.MountOverride);
    }

    [Fact, Trait("Scenario", "WIR-R27-135")]
    public void should_nudge_the_height_offset_with_shift_alt_arrows_on_a_migrated_sensor()
    {
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            var canvas = Canvas(vm);
            var up = canvas.HandleKeyDown(Key.System, Key.Up, ModifierKeys.Alt | ModifierKeys.Shift, canvas.SensorChips[102]);
            var raised = vm.FenceLayout.MountOf(102)!.HeightOffsetM;
            canvas.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift, canvas.SensorChips[102]);
            canvas.HandleKeyDown(Key.System, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift, canvas.SensorChips[102]);
            return (up, raised, Lowered: vm.FenceLayout.MountOf(102)!.HeightOffsetM, Override: vm.FenceLayout.MountOf(102)!.MountOverride);
        });

        Assert.True(result.up);
        Assert.Equal(FenceLayoutMath.NUDGE_M, result.raised, 6);                                      // 오프셋 칸이 0 → +0.1
        Assert.Equal(-FenceLayoutMath.NUDGE_M, result.Lowered, 6);
        Assert.Null(result.Override);                                                                // 프리셋 따로 고친 값이 아니다
    }

    [Fact, Trait("Scenario", "SC-FEN24-003")]
    public void should_step_to_the_next_grid_point_with_ctrl_right_and_come_back_with_ctrl_left_on_a_migrated_sensor()
    {
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            var canvas = Canvas(vm);
            var start = vm.FenceGridCellOf(102);
            canvas.HandleKeyDown(Key.Right, Key.None, ModifierKeys.Control, canvas.SensorChips[102]);
            var stepped = vm.FenceGridCellOf(102);
            canvas.HandleKeyDown(Key.Left, Key.None, ModifierKeys.Control, canvas.SensorChips[102]);
            return (start, stepped, Back: vm.FenceGridCellOf(102), vm.StatusText);
        });

        Assert.NotEqual(result.start, result.stepped);
        Assert.Equal(result.start, result.Back);                                                     // 같은 자리로 돌아온다
        Assert.StartsWith("옮김 — ", result.StatusText);
        Assert.DoesNotContain("에 붙음", result.StatusText);
    }

    [Fact, Trait("Scenario", "SC-FEN-025")]
    public void should_drag_a_single_migrated_sensor_on_the_red_grid_points_and_land_in_the_panel_it_was_dropped_on()
    {
        var target = new FenceGridCell(6, 1);                                                         // 망 2 가운데
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var preset = canvas.PresetDragKey;
            var at = canvas.WorldToScreen(canvas.SnapPoints.Single(p => p.Cell == target).At);
            canvas.OnPointerMoved(at + new Vector(3, -2));
            canvas.OnPointerReleased(at + new Vector(3, -2));
            return (preset, Mount: vm.FenceLayout.MountOf(102)!);
        });

        Assert.Null(result.preset);                                                                   // 붙는 점 끌기가 아니다
        Assert.Equal((1, FenceMountSpot.PanelCenter, FenceColumn.Center), (result.Mount.Panel, result.Mount.Spot, result.Mount.Column));
    }

    [Fact, Trait("Scenario", "SC-FEN-026")]
    public void should_move_two_migrated_sensors_together_by_the_same_grid_offset()
    {
        var result = OnSta(() =>
        {
            var vm = SavedAndReopened();
            var canvas = Canvas(vm);
            vm.FenceSelectSensors(new[] { 102, 103 });
            canvas.Rebuild();
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var at = canvas.WorldToScreen(canvas.SnapPoints.Single(p => p.Cell == new FenceGridCell(5, 2)).At);   // 기둥 1 → 망 1 왼쪽 위 · Δ(+1, 0)
            canvas.OnPointerMoved(at);
            canvas.OnPointerReleased(at);
            return (A: vm.FenceLayout.MountOf(102)!, B: vm.FenceLayout.MountOf(103)!, vm.StatusText);
        });

        Assert.Equal((1, FenceMountSpot.PanelTop, FenceColumn.Left), (result.A.Panel, result.A.Spot, result.A.Column));
        Assert.Equal((2, FenceMountSpot.PanelTop, FenceColumn.Left), (result.B.Panel, result.B.Spot, result.B.Column));
        Assert.DoesNotContain("놓지 않았습니다", result.StatusText);
    }
    #endregion

    #region - 고른 프리셋 센서도 9자리 키 · 고르개는 그대로 -
    [Fact, Trait("Scenario", "SC-FEN-024")]
    public void should_let_the_grid_picker_and_alt_arrows_move_a_chosen_preset_sensor_by_releasing_its_preset()
    {
        var result = OnSta(() =>
        {
            var vm = WithPresets();
            StandOnChosenPreset(vm, 102);
            var canvas = Canvas(vm);
            var x0 = canvas.Scene!.X[102];
            Assert.True(vm.FenceSetGridPoint(FenceColumn.Left, 0));                                   // [왼쪽 아래]
            canvas.Rebuild();
            var picked = vm.FenceLayout.MountOf(102)!;
            var x1 = canvas.Scene!.X[102];
            var pose = vm.FencePoseOf(102);
            var spot = picked.Spot;
            canvas.HandleKeyDown(Key.System, Key.Up, ModifierKeys.Alt, canvas.SensorChips[102]);     // Alt+↑ = 한 단계 위
            return (picked, x0, x1, pose, spot, Up: vm.FenceLayout.MountOf(102)!.Spot, vm.StatusText);
        });

        Assert.Equal(FenceColumn.Left, result.picked.Column);
        Assert.Null(result.picked.MountPresetId);                                                     // 9자리로 옮기면 프리셋을 놓는다
        Assert.Equal(SensorPresetCatalog.SMART2, result.picked.ModelId);                              // 모델은 둔다
        Assert.Null(result.pose);
        Assert.NotEqual(result.x0, result.x1);                                                        // 칩이 옮긴 곳에 선다
        Assert.NotEqual(result.spot, result.Up);
        Assert.StartsWith("높이 — ", result.StatusText);
    }

    [Fact, Trait("Scenario", "SC-FEN24-003")]
    public void should_keep_ctrl_arrows_on_the_nine_point_grid_for_a_chosen_preset_sensor_too()
    {
        var result = OnSta(() =>
        {
            var vm = WithPresets();
            StandOnChosenPreset(vm, 102);
            var canvas = Canvas(vm);
            var start = vm.FenceGridCellOf(102);
            canvas.HandleKeyDown(Key.Right, Key.None, ModifierKeys.Control, canvas.SensorChips[102]);
            return (start, After: vm.FenceGridCellOf(102), vm.StatusText);
        });

        Assert.NotEqual(result.start, result.After);
        Assert.StartsWith("옮김 — ", result.StatusText);
        Assert.DoesNotContain("에 붙음", result.StatusText);
    }

    [Fact, Trait("Scenario", "SC-FEN-025")]
    public void should_still_snap_a_chosen_preset_sensor_to_its_attach_points_when_dragged_alone()
    {
        var result = OnSta(() =>
        {
            var vm = WithPresets();
            StandOnChosenPreset(vm, 102);
            var canvas = Canvas(vm);
            var chip = canvas.SensorChips[102];
            var start = canvas.ScreenCenterOf(chip);
            canvas.OnPointerPressed(start, chip);
            canvas.OnPointerMoved(start + new Vector(12, 0));
            var key = canvas.PresetDragKey;
            canvas.HandleKeyDown(Key.Escape, Key.None, ModifierKeys.None, chip);
            return key;
        });

        Assert.Equal(102, result);                                                                    // 고른 프리셋 센서 한 대 = 붙는 점 끌기(FR-06)
    }
    #endregion

    #region - 순수 계산 -
    [Fact, Trait("Scenario", "SC-FEN-024")]
    public void should_release_the_preset_only_when_a_nine_point_edit_moved_the_seat()
    {
        var preset = SensorPresetCatalog.MountPresets.First(p => p.ModelId == SensorPresetCatalog.SMART2 && p.IsDefault);
        var chosen = new SensorMountSpec(1, FenceMountSpot.PostTop) { ModelId = preset.ModelId, MountPresetId = preset.Id, MountPresetChosen = true, AttrPresetId = 7 };
        var before = new Dictionary<int, SensorMountSpec> { [1] = chosen, [2] = chosen };

        var moved = WiringPresetPlan.ReleaseMovedPresets(before, new Dictionary<int, SensorMountSpec> { [1] = chosen with { Spot = FenceMountSpot.PanelTop }, [2] = chosen });
        Assert.Null(moved[1].MountPresetId);
        Assert.Null(moved[1].MountOverride);
        Assert.Null(moved[1].MountPresetChosen);
        Assert.Equal((preset.ModelId, 7), (moved[1].ModelId, moved[1].AttrPresetId));               // 모델 · 속성 프리셋은 둔다
        Assert.Same(chosen, moved[2]);

        var presetEdit = new Dictionary<int, SensorMountSpec> { [1] = chosen with { Panel = 2, MountOverride = new SensorMountOverride { HeightM = 1.5 } } };
        Assert.Same(presetEdit, WiringPresetPlan.ReleaseMovedPresets(before, presetEdit));            // 프리셋 칸이 같이 바뀐 편집은 그대로

        var same = new Dictionary<int, SensorMountSpec> { [1] = chosen };
        Assert.Same(same, WiringPresetPlan.ReleaseMovedPresets(before, same));
    }

    [Fact, Trait("Scenario", "SC-FEN-024")]
    public void should_mark_chosen_presets_but_not_migrated_seats()
    {
        var panels = new[] { FencePanelSpec.Default(Enums.EnumFenceStyle.ChainLink, 3), FencePanelSpec.Default(Enums.EnumFenceStyle.ChainLink, 3) };
        var presets = SensorPresetCatalog.MountPresets.Where(p => p.ModelId == SensorPresetCatalog.SMART2).ToList();
        var legacy = new SensorMountSpec(1, FenceMountSpot.PostTop);
        var migrated = WiringPresetPlan.Migrate(legacy, SensorPresetCatalog.SMART2, presets, panels);
        Assert.True(migrated.HasMountPreset);
        Assert.False(migrated.IsPresetChosen);

        var applied = WiringPresetPlan.Apply(migrated, presets[0], panels);
        Assert.True(applied.IsPresetChosen);
        Assert.True(WiringPresetPlan.WithOverride(migrated, presets[0], panels, p => p with { HeightM = 1.0 }).IsPresetChosen);
        Assert.True(WiringPresetPlan.MoveTo(migrated, presets[0], panels, 0, 0.5, 1.2).IsPresetChosen);
    }
    #endregion
}
