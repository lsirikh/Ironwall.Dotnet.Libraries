using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Help;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 센서 설치 위치 · 속성 프리셋(sensor-mount-preset PRD) — 화면 뷰모델 · 결선맵 · 그림 계산 헤드리스 시험(창을 띄우지 않는다 · 가짜 저장소).
/// 실행: <c>dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "Scenario~MNT"</c>
/// </summary>
public class SensorPresetUiTests
{
    private static SensorModel Model(int id) => SensorPresetCatalog.Models.First(m => m.Id == id);
    private static SensorMountPreset DefaultMount(int model) => SensorPresetCatalog.MountPresets.First(p => p.ModelId == model && p.IsDefault);

    private static MountPresetEditorViewModel Editor(int model = SensorPresetCatalog.MULTI, bool razor = false, InMemorySensorPresetRepository? repo = null,
                                                     IReadOnlyList<SensorPresetUsage>? usages = null, PresetSaveChoice choice = PresetSaveChoice.Cancel)
        => new(repo ?? new InMemorySensorPresetRepository(), Model(model), DefaultMount(model), new FenceSampleBay(3.0, 2.4, razor), usages,
               _ => Task.FromResult(choice));

    #region - W2 편집기: 끌기 · 키보드 · 방향 -
    [Fact, Trait("Scenario", "MNT-101")]
    public void should_snap_preview_to_post_b_middle_and_restore_on_esc_when_dragging_in_front_view()
    {
        var vm = Editor();
        var before = vm.Draft;
        vm.BeginDrag();
        vm.DragTo(MountView.Front, 2.9, 1.75, free: false);
        Assert.Equal("post_b.mid", vm.TargetPoint!.Name);
        Assert.Equal("기둥 B · 중간 · 1.8 m 에 붙음", vm.AttachText);
        Assert.NotNull(vm.OriginalPose);                                                           // 원래 자리는 옅게 남는다
        Assert.True(vm.CancelDrag());                                                              // Esc — 끄는 중이면 소비
        Assert.Equal(before, vm.Draft);
        Assert.False(vm.CancelDrag());                                                             // 끄지 않을 때는 소비하지 않는다
    }

    [Fact, Trait("Scenario", "MNT-102")]
    public void should_place_freely_to_the_centimetre_when_alt_is_held()
    {
        var vm = Editor();
        vm.DragTo(MountView.Front, 1.234, 1.567, free: true);
        Assert.Null(vm.TargetPoint);
        vm.FinishDrag(commit: true);
        Assert.Null(vm.Draft.SnapPoint);
        Assert.Equal(1.23, vm.Pose.AlongM, 2);
        Assert.Equal(1.57, vm.Pose.HeightM, 2);
        Assert.Equal("자유 위치", vm.SnapText);
    }

    [Fact, Trait("Scenario", "MNT-103")]
    public void should_move_to_next_point_with_tab_and_by_ten_centimetres_with_arrows()
    {
        var vm = Editor(SensorPresetCatalog.FENCE);
        var first = vm.Draft.SnapPoint;
        vm.NextSnapPoint();
        Assert.NotEqual(first, vm.Draft.SnapPoint);
        Assert.True(Model(SensorPresetCatalog.FENCE).Allows(SensorSnapPoints.Find(vm.Bay, vm.Draft.SnapPoint)!.Kind));
        var h = vm.Pose.HeightM;
        vm.Nudge(0, -1, fine: false);
        Assert.Equal(h - 0.1, vm.Pose.HeightM, 3);
        vm.Nudge(0, -1, fine: true);
        Assert.Equal(h - 0.11, vm.Pose.HeightM, 3);
    }

    [Fact, Trait("Scenario", "MNT-104")]
    public void should_turn_by_fifteen_degrees_or_one_with_alt_and_offer_three_shortcuts()
    {
        var vm = Editor();
        vm.RotateHandle(1, 1, fine: false);                                                        // 45°
        Assert.Equal(45, vm.Pose.YawDeg);
        vm.RotateHandle(Math.Cos(0.3), Math.Sin(0.3), fine: true);                                 // 17.2° → 17°
        Assert.Equal(17, vm.Pose.YawDeg);
        vm.FaceInward();
        Assert.Equal(-90, vm.Pose.YawDeg);
        Assert.Equal("안향", vm.FacingText);
        vm.FaceAlong();
        Assert.Equal("펜스 따라", vm.FacingText);
        vm.TiltHandle(-1, -0.18, fine: false);                                                     // 아래로 10° → 15° 단위
        Assert.Equal(-15, vm.Pose.TiltDeg);
    }

    [Fact, Trait("Scenario", "MNT-105")]
    public void should_show_coil_points_only_when_sample_has_razor()
    {
        var plain = Editor();
        Assert.DoesNotContain(plain.SnapPoints, s => s.Point.Kind == SensorAnchorKind.Razor);
        var razor = Editor(razor: true);
        Assert.Contains(razor.SnapPoints, s => s.Point.Anchor == SensorMountAnchor.RazorLoop && s.IsAllowed);
        plain.HasRazor = true;                                                                      // 샘플 조건을 바꾸면 점이 같이
        Assert.Contains(plain.SnapPoints, s => s.Point.Kind == SensorAnchorKind.Razor);
        var fence = Editor(SensorPresetCatalog.FENCE);
        Assert.All(fence.SnapPoints.Where(s => s.Point.Kind == SensorAnchorKind.Post), s => Assert.False(s.IsAllowed));   // 펜스센서는 기둥 점에 붙지 않는다(흐리게)
    }
    #endregion

    #region - W2 저장 · W5 확인 -
    private static readonly IReadOnlyList<SensorPresetUsage> FourUsing = new[]
    {
        new SensorPresetUsage("s", 1, 104, 2, 4, null, "GOP-CTRL-01 · 104"), new SensorPresetUsage("s", 1, 105, 2, 4, null, "GOP-CTRL-01 · 105"),
        new SensorPresetUsage("s", 3, 131, 2, 4, null, "GOP-CTRL-03 · 131"), new SensorPresetUsage("s", 3, 132, 2, 4, null, "GOP-CTRL-03 · 132"),
    };

    [Fact, Trait("Scenario", "MNT-110")]
    public async Task should_not_save_when_confirm_is_cancelled_while_sensors_use_the_preset()
    {
        var repo = new InMemorySensorPresetRepository();
        var vm = Editor(repo: repo, usages: FourUsing, choice: PresetSaveChoice.Cancel);
        Assert.Equal(4, vm.UsageCount);
        vm.Nudge(0, 1, fine: false);
        var writes = repo.SaveCount;
        Assert.False(await vm.SaveAsync());
        Assert.Equal(writes, repo.SaveCount);
        Assert.Equal(2, vm.ChangeCount);                                                            // 화면 값은 그대로(붙는 점에서 떨어짐 + 높이)
    }

    [Fact, Trait("Scenario", "MNT-111")]
    public async Task should_keep_old_preset_and_create_new_one_when_choosing_save_as_new()
    {
        var repo = new InMemorySensorPresetRepository();
        var vm = Editor(repo: repo, usages: FourUsing, choice: PresetSaveChoice.SaveAsNew);
        vm.Nudge(0, 1, fine: false);
        Assert.True(await vm.SaveAsync());
        var list = await repo.GetMountPresetsAsync(SensorPresetCatalog.MULTI);
        Assert.Equal(DefaultMount(SensorPresetCatalog.MULTI).HeightM, list.Single(p => p.Id == DefaultMount(SensorPresetCatalog.MULTI).Id).HeightM);
        Assert.Contains(list, p => p.Id != DefaultMount(SensorPresetCatalog.MULTI).Id && Math.Abs(p.HeightM - 1.9) < 1e-6);
        Assert.Equal(0, vm.ChangeCount);
    }

    [Fact, Trait("Scenario", "MNT-112")]
    public async Task should_keep_screen_values_and_say_retry_when_repository_save_fails()
    {
        var repo = new InMemorySensorPresetRepository { FailNextSave = true };
        var vm = Editor(repo: repo);
        vm.Nudge(1, 0, fine: false);
        Assert.False(await vm.SaveAsync());
        Assert.Contains("다시 저장", vm.StatusText);
        Assert.Equal(1, vm.ChangeCount);
        Assert.True(await vm.SaveAsync());                                                          // 다시 저장하면 된다
        Assert.Equal(0, vm.ChangeCount);
    }

    [Fact, Trait("Scenario", "MNT-113")]
    public void should_default_to_cancel_and_tell_how_many_sensors_move_when_confirming()
    {
        var dialog = new PresetSaveConfirmViewModel("기둥 꼭대기 2.6 m · 밖향", FourUsing);
        Assert.Equal(PresetSaveChoice.Cancel, dialog.DefaultChoice);
        Assert.Equal("\"기둥 꼭대기 2.6 m · 밖향\" 을 쓰는 센서 4대가 새 자리로 옮겨집니다.", dialog.Message);
        Assert.Equal(4, dialog.Chips.Count);
        Assert.Equal("따로 고친 값은 그대로 남습니다.", dialog.Note);
    }
    #endregion

    #region - W1 센서 모델 창 · W3 속성 -
    [Fact, Trait("Scenario", "MNT-120")]
    public async Task should_list_seven_builtin_models_and_refuse_deleting_them()
    {
        var vm = new SensorModelsViewModel(new InMemorySensorPresetRepository());
        Assert.True(await vm.LoadAsync());
        Assert.Equal(7, vm.Models.Count);
        Assert.Equal("기본 7 · 추가 0", vm.BuiltinText);
        Assert.Equal("저장소: 이 창만(DB 없음)", vm.SourceText);
        vm.SelectedModel = vm.Models.First(m => m.Model.Id == SensorPresetCatalog.MULTI);
        Assert.Contains("91×108×70", vm.ModelHeaderText);
        Assert.False(vm.CanDeleteModel);
        Assert.False(await vm.DeleteModelAsync());
        Assert.True(await vm.ToggleHiddenAsync());
        Assert.Contains("(숨김)", vm.Models.First(m => m.Model.Id == SensorPresetCatalog.MULTI).Name);
    }

    [Fact, Trait("Scenario", "MNT-121")]
    public async Task should_add_user_model_with_default_presets_and_lock_edits_when_repository_is_read_only()
    {
        var vm = new SensorModelsViewModel(new InMemorySensorPresetRepository());
        await vm.LoadAsync();
        var added = await vm.AddModelAsync("레이저 X", EnumDeviceType.Laser, SensorShape.Smart);
        Assert.NotNull(added);
        Assert.Equal("기본 7 · 추가 1", vm.BuiltinText);
        Assert.Single(vm.MountPresets, r => r.Preset.IsDefault);
        Assert.True(vm.CanDeleteModel);
        Assert.Null(await vm.AddModelAsync("레이저 X", EnumDeviceType.Laser, SensorShape.Smart));     // 같은 이름 거절

        var locked = new SensorModelsViewModel(new GopApiSensorPresetRepository());
        Assert.False(locked.CanEdit);
        Assert.Equal("저장소: GOP API(읽기 전용)", locked.SourceText);
    }

    [Fact, Trait("Scenario", "MNT-122")]
    public async Task should_block_deleting_mount_preset_used_by_sensors()
    {
        var usages = new[] { new SensorPresetUsage("s", 1, 104, SensorPresetCatalog.MULTI, DefaultMount(SensorPresetCatalog.MULTI).Id, null) };
        var vm = new SensorModelsViewModel(new InMemorySensorPresetRepository(), new FakeUsage(usages));
        await vm.LoadAsync();
        vm.SelectedModel = vm.Models.First(m => m.Model.Id == SensorPresetCatalog.MULTI);
        vm.SelectedMountPreset = vm.MountPresets.First(r => r.Preset.IsDefault);
        Assert.Equal("1대", vm.SelectedMountPreset.CountText);
        Assert.False(vm.CanDeleteMountPreset);
        Assert.False(await vm.DeleteMountPresetAsync());
        vm.SelectedMountPreset = vm.MountPresets.First(r => !r.Preset.IsDefault);
        Assert.True(await vm.DeleteMountPresetAsync());
    }

    [Fact, Trait("Scenario", "MNT-123")]
    public async Task should_save_attribute_rows_and_reject_duplicate_keys()
    {
        var repo = new InMemorySensorPresetRepository();
        var vm = new SensorModelsViewModel(repo);
        await vm.LoadAsync();
        vm.SelectedModel = vm.Models.First(m => m.Model.Id == SensorPresetCatalog.MULTI);
        Assert.Contains(vm.AttributeRows, r => r.Key == SensorAttributePreset.KEY_RANGE && r.UseText == "그림");
        vm.AttributeRows.First(r => r.Key == SensorAttributePreset.KEY_RANGE).Value = "25";
        Assert.True(await vm.SaveAttributePresetAsync());
        Assert.Equal(25, (await repo.GetAttributePresetsAsync(SensorPresetCatalog.MULTI)).First(p => p.IsDefault).RangeM);
        vm.AddAttributeRow();
        vm.AttributeRows.Last().Key = SensorAttributePreset.KEY_RANGE;
        Assert.False(await vm.SaveAttributePresetAsync());
        Assert.Equal("항목 키가 겹칩니다.", vm.StatusText);
    }

    [Fact, Trait("Scenario", "MNT-124")]
    public async Task should_round_trip_export_and_import_through_the_models_window()
    {
        var a = new SensorModelsViewModel(new InMemorySensorPresetRepository());
        await a.LoadAsync();
        await a.AddModelAsync("현장 모델", EnumDeviceType.PIR, SensorShape.Pir);
        var json = await a.ExportAsync();
        var b = new SensorModelsViewModel(new InMemorySensorPresetRepository());
        await b.LoadAsync();
        var result = await b.ImportAsync(json!);
        Assert.Equal(1, result!.ModelsAdded);
        Assert.Equal(8, b.Models.Count);
        Assert.Null(await b.ImportAsync("{깨진"));
        Assert.Equal("읽을 수 없는 파일입니다.", b.StatusText);
    }
    #endregion

    #region - W4 결선맵 -
    private static async Task<WiringViewModel> WiringWithPresets(string types = "SSFF", InMemorySensorPresetRepository? repo = null)
    {
        var vm = WiringFenceHeightTests.Build(types);
        vm.AttachSensorPresets(repo ?? new InMemorySensorPresetRepository());
        Assert.True(await vm.LoadSensorPresetsAsync());
        return vm;
    }

    [Fact, Trait("Scenario", "MNT-130")]
    public async Task should_stand_sensor_on_chosen_preset_and_keep_override_until_reset()
    {
        var vm = await WiringWithPresets();
        var key = vm.FenceChain.Keys.First();                                                       // 스마트센서2
        vm.FenceSelect(key);
        Assert.True(vm.HasPresetRow);
        Assert.Equal(SensorPresetCatalog.SMART2, vm.SelectedPresetModel!.Id);
        var preset = vm.PresetMountChoices.First();
        vm.SelectedMountPreset = preset;
        Assert.Equal(preset.Id, vm.FenceLayout.MountOf(key)!.MountPresetId);
        Assert.NotNull(vm.FencePoseOf(key));
        Assert.False(vm.HasMountOverride);

        vm.PresetHeightText = "2.00";
        Assert.True(vm.ApplyPresetHeight());
        Assert.True(vm.HasMountOverride);
        Assert.StartsWith("프리셋에서 바뀜: 높이", vm.MountOverrideText);
        Assert.Equal(2.0, vm.FencePoseOf(key)!.HeightM, 3);

        Assert.True(vm.ResetMountToPreset());
        Assert.False(vm.HasMountOverride);

        // 기둥 위로 뜨지 않는다 — 9 m 는 기둥 꼭대기(= 이 프리셋 자리)로 눌려 따로 고친 값도 남지 않는다
        vm.PresetHeightText = "9";
        vm.ApplyPresetHeight();
        Assert.True(vm.FencePoseOf(key)!.HeightM <= FenceSampleBay.FromPanel(vm.FenceLayout.Panels[0]).TopLimitM + 1e-9);
        Assert.False(vm.HasMountOverride);
    }

    [Fact, Trait("Scenario", "MNT-131")]
    public async Task should_apply_model_presets_to_whole_controller_in_one_undo_step()
    {
        var vm = await WiringWithPresets();
        vm.FenceSelect(vm.FenceChain.Keys.Last());
        var map = new Dictionary<int, SensorMountPreset>
        {
            [SensorPresetCatalog.FENCE] = vm.MountPresetsOf(SensorPresetCatalog.FENCE).First(p => p.SnapPoint == "mesh.low"),
            [SensorPresetCatalog.SMART2] = vm.MountPresetsOf(SensorPresetCatalog.SMART2).First(),
        };
        var applied = vm.ApplyMountPreset(MountPresetApplyScope.Controller, map);
        Assert.Equal(4, applied);
        Assert.All(vm.FenceChain.Keys, k => Assert.NotNull(vm.FenceLayout.MountOf(k)!.MountPresetId));
        Assert.Equal(2, vm.ApplyScopeModels(MountPresetApplyScope.Controller).Count);               // 모델이 섞이면 모델별로
        Assert.Single(vm.ApplyScopeModels(MountPresetApplyScope.SameModel));
    }

    [Fact, Trait("Scenario", "MNT-132")]
    public async Task should_count_current_unsaved_usages_for_the_models_window()
    {
        var vm = await WiringWithPresets();
        vm.FenceSelect(vm.FenceChain.Keys.First());
        vm.SelectedMountPreset = vm.PresetMountChoices.First();
        var usages = vm.CurrentPresetUsages();
        Assert.Single(usages);
        Assert.Equal(vm.PresetMountChoices.First().Id, usages[0].MountPresetId);
    }

    [Fact, Trait("Scenario", "MNT-133")]
    public void should_keep_position_when_migrating_legacy_post_mount_to_preset()
    {
        var panels = new[] { FencePanelSpec.Default(EnumFenceStyle.ChainLink, 3), FencePanelSpec.Default(EnumFenceStyle.ChainLink, 3) };
        var presets = SensorPresetCatalog.MountPresets.Where(p => p.ModelId == SensorPresetCatalog.MULTI).ToList();
        var geometry = FenceLayoutMath.Geometry(panels);
        foreach (var post in new[] { 0, 1, 2 })
        {
            var legacy = new SensorMountSpec(post, FenceMountSpot.PostTop);
            var migrated = WiringPresetPlan.Migrate(legacy, SensorPresetCatalog.MULTI, presets, panels);
            Assert.NotNull(migrated.MountPresetId);
            var pose = WiringPresetPlan.PoseOf(migrated, panels, id => presets.First(p => p.Id == id))!;
            var bay = geometry.Panels[SensorMountMigration.BayIndexOf(migrated, panels.Length, pose.AlongRatio)];
            Assert.Equal(geometry.Posts[post].XM, bay.StartM + bay.SpanM * pose.AlongRatio, 3);        // 기둥이 옮겨지지 않는다
        }
    }

    [Fact, Trait("Scenario", "MNT-134")]
    public void should_put_razor_mounted_sensor_on_the_coil_and_plain_sensor_below_the_post_top_in_fence_world()
    {
        var vm = WiringFenceHeightTests.Razor(WiringFenceHeightTests.Build("SSFF"));
        var layout = vm.FenceLayout;
        var keys = vm.FenceChain.Keys;
        var razorPreset = SensorPresetCatalog.MountPresets.First(p => p.SnapPoint == "razor.loop_b");
        var postPreset = DefaultMount(SensorPresetCatalog.MULTI);
        SensorMountPose? PoseOf(int k) => k == keys[0]
            ? WiringPresetPlan.PoseOf(WiringPresetPlan.Apply(layout.MountOf(k)!, razorPreset, layout.Panels), layout.Panels, _ => razorPreset)
            : k == keys[1] ? WiringPresetPlan.PoseOf(WiringPresetPlan.Apply(layout.MountOf(k)!, postPreset, layout.Panels), layout.Panels, _ => postPreset) : null;
        var world = FenceWorld.FromLayout(vm.FenceChain, vm.FenceSensors(), layout, vm.FenceSpacing, PoseOf);
        Assert.True(world.CoilOf(keys[0]) > 0);                                                      // 코일 위
        Assert.Equal(0, world.CoilOf(keys[1]));
        Assert.True(world.LiftOf(keys[0]) > world.LiftOf(keys[1]));
    }

    [Fact, Trait("Scenario", "MNT-135")]
    public void should_draw_pir_and_optical_cable_with_their_own_shapes_not_smart_box()
    {
        FenceSensor S(EnumDeviceType type) => new(1, 1, 1, "s", type, 1, 1, 1, null, false, false, false);
        Assert.Equal(SensorShape.Pir, S(EnumDeviceType.PIR).DrawShape);
        Assert.Equal(SensorShape.Optical, S(EnumDeviceType.OpticalCable).DrawShape);
        Assert.Equal(SensorShape.Smart2, S(EnumDeviceType.SmartMultisensor2).DrawShape);
        Assert.Equal(SensorShape.Fence, (S(EnumDeviceType.SmartSensor) with { ModelShape = SensorShape.Fence }).DrawShape);
        var pir = FenceScene.Sensor(S(EnumDeviceType.PIR), WiringShape.Ring, FenceProjector.Tilt, selected: false);
        var smart = FenceScene.Sensor(S(EnumDeviceType.SmartSensor), WiringShape.Ring, FenceProjector.Tilt, selected: false);
        Assert.NotEqual(smart.Hit.Width, pir.Hit.Width);                                             // 다른 몸
        var cable = FenceScene.Sensor(S(EnumDeviceType.OpticalCable), WiringShape.Ring, FenceProjector.Tilt, selected: false);
        Assert.True(cable.Hit.Width > cable.Hit.Height);                                              // 가로로 긴 줄
    }
    #endregion

    #region - 그림 계산 · 도움말 -
    [Fact, Trait("Scenario", "MNT-140")]
    public void should_map_screen_back_to_the_same_metres_in_every_view()
    {
        var bay = new FenceSampleBay(3.0, 2.4, true);
        foreach (var view in new[] { MountView.Front, MountView.Top, MountView.Side })
        {
            var p = new MountBayProjection(view, 400, 300, bay);
            var screen = p.ToScreen(1.2, 1.8, -0.3);
            var (u, v) = p.FromScreen(screen);
            var expected = view switch { MountView.Front => (1.2, 1.8), MountView.Top => (1.2, -0.3), _ => (-0.3, 1.8) };
            Assert.Equal(expected.Item1, u, 6);
            Assert.Equal(expected.Item2, v, 6);
        }
        var top = new MountBayProjection(MountView.Top, 400, 300, bay);
        Assert.True(top.ToScreen(1, 0, -1).Y < top.ToScreen(1, 0, 1).Y);                             // 밖이 위
        var side = new MountBayProjection(MountView.Side, 400, 300, bay);
        Assert.True(side.ToScreen(0, 1, -1).X < side.ToScreen(0, 1, 1).X);                           // 밖이 왼쪽
    }

    [Fact, Trait("Scenario", "MNT-141")]
    public void should_register_help_balloons_for_every_preset_screen()
    {
        var keys = DevicesHelp.Entries.Select(e => e.Key).ToHashSet();
        foreach (var key in new[] { "Devices.SensorPresets.Models", "Devices.SensorPresets.MountEditor", "Devices.SensorPresets.Attributes", "Devices.Wiring.MountPreset" })
            Assert.Contains(key, keys);
    }
    #endregion

    private sealed class FakeUsage : ISensorPresetUsageSource
    {
        private readonly IReadOnlyList<SensorPresetUsage> _list;
        public FakeUsage(IReadOnlyList<SensorPresetUsage> list) => _list = list;
        public Task<IReadOnlyList<SensorPresetUsage>> GetUsagesAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(_list);
    }
}
