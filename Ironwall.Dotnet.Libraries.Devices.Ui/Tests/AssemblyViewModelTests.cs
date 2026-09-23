using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 조립기 뷰모델(device-assembly-preset FR-01 ~ FR-08) — 끌어 놓기의 <b>처리기 경로</b>와 키보드 폴백이 같은 결과를 내는지,
/// 그리고 조립이 끝까지 Draft 인지(프리셋 파일 말고는 아무 데도 쓰지 않는다)를 본다. 끌기 제스처 자체는 커널이 검증한다.
/// </summary>
public class AssemblyViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-asm-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        // 남은 핸들 때문에 못 지워도 테스트를 실패로 만들지 않는다 — 임시 폴더다.
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }

    private DevicePresetStore NewStore()
    {
        var store = new DevicePresetStore(Path.Combine(_directory, "presets.json"), () => new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero));
        store.Load();
        return store;
    }

    private static async Task<AssemblyViewModel> OpenAsync(AssemblyViewModel vm)
    {
        await ((IActivate)vm).ActivateAsync();
        return vm;
    }

    private static DragPayload Payload(params object[] items) => new(null!, items, "test");

    [Fact]
    public async Task should_show_only_types_that_apply_to_the_category_when_opened()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));

        Assert.Contains(vm.Palette, p => p.Code == "DOOR_SENSOR");
        Assert.DoesNotContain(vm.Palette, p => p.Code == "PTZ_UNIT");      // 카메라 전용 — 붙였다가 422 를 받는 대신 애초에 못 고르게 한다

        vm.Category = EnumDeviceCategory.Camera;

        Assert.Contains(vm.Palette, p => p.Code == "PTZ_UNIT");
    }

    [Fact]
    public async Task should_add_at_the_end_when_enter_is_pressed_on_a_palette_item()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));

        vm.AddFromPaletteCommand.Execute(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
        vm.AddFromPaletteCommand.Execute(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));

        Assert.Equal(new[] { "door", "door_2" }, vm.BoardItems.Select(i => i.Slot.Key).ToArray());
        Assert.Same(vm.BoardItems[1], vm.Inspected);          // 방금 단 것이 속성 칸에 올라온다
    }

    [Fact]
    public async Task should_insert_at_the_dropped_gap_when_a_palette_block_is_dropped_on_the_board()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "HEATER"));

        var payload = Payload(vm.Palette.Single(p => p.Code == "FAN"));
        var target = new DropTarget(AssemblyViewModel.BoardZoneKey, null, 1);

        Assert.True(vm.CanDrop(payload, target));
        vm.Drop(payload, target);

        Assert.Equal(new[] { "DOOR_SENSOR", "FAN", "HEATER" }, vm.BoardItems.Select(i => i.Slot.TypeCode).ToArray());
    }

    [Fact]
    public async Task should_not_count_reordering_as_an_unsaved_change_when_slots_are_dragged()
    {
        var device = new EnclosureDeviceModel { Id = 7, DeviceNumber = 1, DeviceName = "함체", Axes = AxesWith(Part("door", "DOOR_SENSOR"), Part("heater", "HEATER")) };
        var vm = await OpenAsync(AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), NewApplyService(), new FakeDialogs()));

        vm.Drop(Payload(vm.BoardItems[0]), new DropTarget(AssemblyViewModel.BoardZoneKey, null, 2));

        Assert.Equal(new[] { "heater", "door" }, vm.BoardItems.Select(i => i.Slot.Key).ToArray());
        Assert.Contains("미저장 변경 0", vm.FooterText);         // 서버에 순서 계약이 없다 — 보기 순서일 뿐
        Assert.False(vm.CanCommit);
    }

    [Fact]
    public async Task should_move_items_instead_of_resetting_the_list_when_slots_are_reordered()
    {
        // 목록을 비우고 다시 채우면 화면의 선택이 풀리고 속성 칸이 닫힌다 — 순서를 바꿀 때마다, 되돌릴 때마다.
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "HEATER"));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "FAN"));
        var heater = vm.BoardItems[1];
        vm.OnBoardSelectionChanged(new[] { heater });
        var actions = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        vm.BoardItems.CollectionChanged += (_, e) => actions.Add(e.Action);

        vm.Drop(Payload(heater), new DropTarget(AssemblyViewModel.BoardZoneKey, null, 0));
        vm.Undo();

        Assert.DoesNotContain(System.Collections.Specialized.NotifyCollectionChangedAction.Reset, actions);
        Assert.All(actions, a => Assert.Equal(System.Collections.Specialized.NotifyCollectionChangedAction.Move, a));
        Assert.NotEmpty(actions);
        Assert.Same(heater, vm.Inspected);                     // 고르던 부품의 속성 칸이 그대로 열려 있다
    }

    [Fact]
    public async Task should_keep_the_old_channel_and_say_why_when_channel_text_is_not_a_number()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
        var item = vm.BoardItems.Single();

        item.ChannelInput = "3";
        item.ChannelInput = "3a";

        Assert.Equal(3, item.Slot.Channel);                    // 나가는 값은 3 — 그러니 화면도 까닭을 말해야 한다
        Assert.NotNull(item.ChannelError);
        Assert.Equal("3a", item.ChannelInput);

        item.ChannelInput = "";
        Assert.Null(item.Slot.Channel);
        Assert.Null(item.ChannelError);
    }

    [Fact]
    public async Task should_carry_type_axis_modes_and_hardware_defaults_when_a_device_is_saved_as_a_preset()
    {
        var store = NewStore();
        var axes = AxesWith(Part("door", "DOOR_SENSOR"));
        ((HardwareSpecModel)axes.HardwareSpec!).Manufacturer = "Sensorway";
        axes.DeviceConfig = new DeviceConfigModel { Modes = new JObject { ["night"] = "auto" }, Thresholds = new JObject { ["temp_high_c"] = 60 } };
        var device = new EnclosureDeviceModel { Id = 7, DeviceNumber = 1, DeviceName = "함체", TypeAxisCode = "Outdoor", Axes = axes };
        var vm = await OpenAsync(AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Enclosure, new FakeCatalog(), store, NewApplyService(), new FakeDialogs { TextAnswer = "현장에서 굳힌 것" }));

        await vm.SaveAsPresetAsync();

        var saved = store.ForCategory(EnumDeviceCategory.Enclosure).Single(p => p.Name == "현장에서 굳힌 것");
        Assert.Equal("Outdoor", saved.TypeAxisCode);
        Assert.Equal("Sensorway", saved.Manufacturer);
        Assert.Equal("auto", (string?)saved.Modes?["night"]);   // 안 가져가면 이 프리셋으로 만든 장비에 동작 모드가 없다
        Assert.Equal(60, (int?)saved.Thresholds?["temp_high_c"]);
    }

    [Fact]
    public async Task should_remove_and_restore_when_a_slot_is_dropped_on_the_bin_and_undone()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "HEATER"));
        var door = vm.BoardItems[0];

        vm.Drop(Payload(door), new DropTarget(AssemblyViewModel.BinZoneKey, null, -1));
        Assert.Equal(new[] { "heater" }, vm.BoardItems.Select(i => i.Slot.Key).ToArray());

        vm.Undo();
        Assert.Equal(new[] { "door", "heater" }, vm.BoardItems.Select(i => i.Slot.Key).ToArray());
        Assert.Same(door, vm.BoardItems[0]);                  // 같은 항목이 돌아온다(화면의 선택 · 포커스가 이어진다)
    }

    [Fact]
    public async Task should_refuse_palette_items_on_the_bin_and_unknown_zones()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));
        var block = vm.Palette.First();

        Assert.False(vm.CanDrop(Payload(block), new DropTarget(AssemblyViewModel.BinZoneKey, null, -1)));
        Assert.False(vm.CanDrop(Payload(block), new DropTarget("somewhere-else", null, -1)));
    }

    [Fact]
    public async Task should_block_commit_and_name_the_problem_when_keys_collide()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "HEATER"));

        vm.BoardItems[1].Slot.Key = "door";

        Assert.True(vm.HasProblems);
        Assert.False(vm.CanCommit);
        Assert.Contains("door", vm.CommitBlockedReason);
        Assert.True(vm.BoardItems.All(i => i.HasError));
    }

    [Fact]
    public async Task should_write_override_under_the_slot_and_keep_it_out_of_the_definition_when_override_row_is_edited()
    {
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), new FakeDialogs()));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "HEATER"));

        vm.OverrideRows.Single(r => r.Name == "enabled").Text = "false";

        var slot = vm.BoardItems.Single().Slot;
        Assert.Equal(false, (bool)slot.Overrides!["enabled"]!);
        Assert.Null(slot.ToDefinition().Spec);                 // enabled 는 부품에 실으면 422 — 재정의에만 있어야 한다

        vm.OverrideRows.Single(r => r.Name == "enabled").Text = "";
        Assert.Null(slot.Overrides);                           // 비우면 재정의하지 않음
    }

    /// <summary>
    /// 기존 장비에서 재정의 칸을 비우면 적용 본문에는 null 이 가야 하지만(asm.R6), 그 null 을 프리셋에 담으면 안 된다 —
    /// 프리셋의 null 은 새 장비에 보낼 뜻이 없고, 값 칸의 null 은 다음 등록 본문을 더럽힌다.
    /// </summary>
    [Fact]
    public async Task should_keep_cleared_override_nulls_out_of_a_preset_when_saved_from_a_device()
    {
        var store = NewStore();
        var axes = AxesWith(Part("heater", "HEATER"), Part("fan", "FAN"));
        axes.DeviceConfig = new DeviceConfigModel
        {
            ComponentOverrides = JObject.Parse("{\"heater\":{\"enabled\":true,\"on_below_c\":5},\"fan\":{\"enabled\":true}}"),
        };
        var device = new EnclosureDeviceModel { Id = 7, DeviceNumber = 1, DeviceName = "함체", Axes = axes };
        var vm = await OpenAsync(AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Enclosure, new FakeCatalog(), store, NewApplyService(), new FakeDialogs { TextAnswer = "비운 재정의" }));

        vm.OnBoardSelectionChanged(new[] { vm.BoardItems.Single(i => i.Slot.Key == "heater") });
        vm.OverrideRows.Single(r => r.Name == "on_below_c").Text = "";
        vm.OnBoardSelectionChanged(new[] { vm.BoardItems.Single(i => i.Slot.Key == "fan") });
        vm.OverrideRows.Single(r => r.Name == "enabled").Text = "";
        await vm.SaveAsPresetAsync();

        var saved = store.ForCategory(EnumDeviceCategory.Enclosure).Single(p => p.Name == "비운 재정의");
        Assert.Equal("{\"heater\":{\"enabled\":true}}", saved.ComponentOverrides!.ToString(Newtonsoft.Json.Formatting.None));
    }

    [Fact]
    public async Task should_save_structure_only_when_saved_as_a_preset()
    {
        var store = NewStore();
        var dialogs = new FakeDialogs { TextAnswer = "현장 표준 함체" };
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), store, dialogs));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));
        vm.BoardItems[0].Slot.Serial = "SN-0001";

        await vm.SaveAsPresetAsync();

        var saved = store.ForCategory(EnumDeviceCategory.Enclosure).Single(p => p.Name == "현장 표준 함체");
        Assert.Equal("door", saved.Components.Single().Key);
        Assert.Null(saved.Components.Single().Serial);         // 일련번호는 그 장비만의 것 — 프리셋에 담기지 않는다
        Assert.Contains(vm.CategoryPresets, p => p.Id == saved.Id);
    }

    [Fact]
    public async Task should_replace_the_board_after_confirmation_and_allow_undo_when_a_preset_is_loaded()
    {
        var store = NewStore();
        var dialogs = new FakeDialogs { ConfirmAnswer = true };
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), store, dialogs));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "FAN"));
        var preset = new DevicePreset { Id = DevicePresetStore.NewId(), Name = "P", Category = EnumDeviceCategory.Enclosure, Components = new[] { Part("door", "DOOR_SENSOR"), Part("heater", "HEATER") } };

        await vm.LoadPresetAsync(preset);
        Assert.Equal(new[] { "door", "heater" }, vm.BoardItems.Select(i => i.Slot.Key).ToArray());
        Assert.Equal(1, dialogs.ConfirmCount);

        vm.Undo();
        Assert.Equal(new[] { "fan" }, vm.BoardItems.Select(i => i.Slot.Key).ToArray());
    }

    [Fact]
    public async Task should_hand_a_transient_preset_to_the_register_window_when_compose_is_committed()
    {
        var dialogs = new FakeDialogs { RegisterAnswer = 321 };
        var vm = await OpenAsync(AssemblyViewModel.Compose(EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), dialogs));
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "DOOR_SENSOR"));

        await vm.CommitAsync();

        Assert.Equal(EnumDeviceCategory.Enclosure, dialogs.RegisteredPreset!.Category);
        Assert.Equal("door", dialogs.RegisteredPreset.Components.Single().Key);
        Assert.Equal(321, vm.RegisteredDeviceId);
    }

    [Fact]
    public async Task should_flag_a_type_missing_from_the_catalog_instead_of_dropping_it()
    {
        var device = new EnclosureDeviceModel { Id = 7, DeviceNumber = 1, DeviceName = "함체", Axes = AxesWith(Part("legacy", "RETIRED_TYPE")) };
        var vm = await OpenAsync(AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), NewApplyService(), new FakeDialogs()));

        Assert.Single(vm.BoardItems);                          // 말없이 빼지 않는다
        Assert.True(vm.BoardItems[0].IsUnknownType);
        Assert.True(vm.HasProblems);
        Assert.False(vm.CanCommit);
    }

    [Fact]
    public async Task should_describe_added_removed_and_changed_parts_when_editing_a_device()
    {
        var device = new EnclosureDeviceModel { Id = 7, DeviceNumber = 1, DeviceName = "함체", Axes = AxesWith(Part("door", "DOOR_SENSOR"), Part("heater", "HEATER")) };
        var vm = await OpenAsync(AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Enclosure, new FakeCatalog(), NewStore(), NewApplyService(), new FakeDialogs()));

        vm.OnBoardSelectionChanged(new[] { vm.BoardItems.Single(i => i.Slot.Key == "heater") });
        vm.RemoveSelected();
        vm.AddFromPalette(vm.Palette.Single(p => p.Code == "FAN"));
        vm.BoardItems.Single(i => i.Slot.Key == "door").Slot.Label = "정문";

        Assert.Contains("더함 1: fan", vm.DiffSummary);
        Assert.Contains("뺌 1: heater", vm.DiffSummary);
        Assert.Contains("관측 기록이 함께 삭제", vm.DiffSummary);
        Assert.Contains("고침 1: door(label)", vm.DiffSummary);
        Assert.True(vm.CanCommit);
    }

    [Theory]
    [InlineData("true", JTokenType.Boolean)]
    [InlineData("42", JTokenType.Integer)]
    [InlineData("1.5", JTokenType.Float)]
    [InlineData("auto", JTokenType.String)]
    public void should_keep_the_json_type_when_override_text_is_parsed(string text, JTokenType expected)
        => Assert.Equal(expected, OverrideRowViewModel.Parse(text).Type);

    #region - fakes -
    private static ComponentDefinitionModel Part(string key, string type) => new() { Key = key, Type = type };

    private static DeviceAxesModel AxesWith(params ComponentDefinitionModel[] parts)
    {
        var spec = new HardwareSpecModel();
        foreach (var part in parts) spec.Components.Add(part);
        return new DeviceAxesModel { HardwareSpec = spec };
    }

    private static Consoles.Assembly.Register.ComponentApplyService NewApplyService()
        => new(new MockDeviceApiService(), new MockDeviceProviderService(), new MockLogService());

    private sealed class FakeCatalog : IComponentCatalog
    {
        private static readonly EnumDeviceCategory[] Boxes = { EnumDeviceCategory.Enclosure, EnumDeviceCategory.Controller };

        private readonly List<ComponentTypeInfo> _types = new()
        {
            new() { Code = "DOOR_SENSOR", Label = "문 센서", Family = ComponentFamily.Sensing, AppliesTo = new[] { EnumDeviceCategory.Enclosure, EnumDeviceCategory.Gate }, States = new[] { "OPEN", "CLOSED" } },
            new() { Code = "HEATER", Label = "히터", Family = ComponentFamily.PowerEnvironment, AppliesTo = Boxes, States = new[] { "ON", "OFF" }, OverrideParams = new[] { "enabled", "on_below_c" } },
            new() { Code = "FAN", Label = "팬", Family = ComponentFamily.PowerEnvironment, AppliesTo = Boxes, OverrideParams = new[] { "enabled" } },
            new() { Code = "PTZ_UNIT", Label = "PTZ 구동부", Family = ComponentFamily.Actuation, AppliesTo = new[] { EnumDeviceCategory.Camera }, States = new[] { "IDLE", "MOVING" } },
        };

        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
        public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false) => _types.Where(t => t.AppliesToCategory(category)).ToList();
        public ComponentTypeInfo? Find(string? code) => _types.FirstOrDefault(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeDialogs : IAssemblyDialogs
    {
        public bool ConfirmAnswer { get; set; } = true;
        public int ConfirmCount { get; private set; }
        public string? TextAnswer { get; set; }
        public RepeatExpandSpec? RepeatAnswer { get; set; }
        public int? RegisterAnswer { get; set; }
        public DevicePreset? RegisteredPreset { get; private set; }

        public Task<bool> ConfirmAsync(string title, string message) { ConfirmCount++; return Task.FromResult(ConfirmAnswer); }
        public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult(TextAnswer);
        public Task<RepeatExpandSpec?> AskRepeatExpandAsync(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys) => Task.FromResult(RepeatAnswer);
        public Task<int?> OpenRegisterAsync(DevicePreset preset) { RegisteredPreset = preset; return Task.FromResult(RegisterAnswer); }
    }
    #endregion
}
