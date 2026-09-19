using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>반복 펼치기 창 · 프리셋 관리 창의 뷰모델(device-assembly-preset FR-08 · FR-11).</summary>
public class AssemblyDialogViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-asm-dlg-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static PaletteItemViewModel Item(string code, string label) => new(new ComponentTypeInfo { Code = code, Label = label });

    #region - 반복 펼치기 -
    [Fact]
    public void should_suggest_a_key_rule_from_the_type_until_the_user_edits_it()
    {
        var contact = Item("CONTACT_INPUT", "접점 입력");
        var relay = Item("RELAY_OUTPUT", "릴레이 출력");
        var vm = new RepeatExpandViewModel(new[] { contact, relay }, contact, Array.Empty<string>());
        var suggestedForContact = vm.KeyFormat;

        vm.SelectedType = relay;
        Assert.NotEqual(suggestedForContact, vm.KeyFormat);           // 유형을 따라 바뀐다

        vm.KeyFormat = "out_{d}";
        vm.SelectedType = contact;
        Assert.Equal("out_{d}", vm.KeyFormat);                       // 손으로 고친 뒤에는 건드리지 않는다
    }

    [Fact]
    public void should_preview_every_row_and_allow_expand_when_the_rule_is_valid()
    {
        var contact = Item("CONTACT_INPUT", "접점 입력");
        var vm = new RepeatExpandViewModel(new[] { contact }, contact, Array.Empty<string>()) { Count = "4", StartChannel = "5", KeyFormat = "ci_{02d}" };

        Assert.Equal(new[] { "ci_05", "ci_06", "ci_07", "ci_08" }, vm.Preview.Select(r => r.Key).ToArray());
        Assert.Equal(new[] { 5, 6, 7, 8 }, vm.Preview.Select(r => r.Channel).ToArray());
        Assert.True(vm.CanExpand);
    }

    [Fact]
    public void should_block_expand_when_a_preview_key_is_already_on_the_board()
    {
        var contact = Item("CONTACT_INPUT", "접점 입력");
        var vm = new RepeatExpandViewModel(new[] { contact }, contact, new[] { "ci_02" }) { Count = "3", StartChannel = "1", KeyFormat = "ci_{02d}" };

        Assert.Equal(1, vm.ConflictCount);
        Assert.False(vm.CanExpand);                                   // 하나라도 겹치면 전부 안 만든다 — 반쯤 펼쳐진 보드를 남기지 않는다
    }

    [Theory]
    [InlineData("abc", "1")]
    [InlineData("0", "1")]
    [InlineData("4", "x")]
    public void should_explain_the_error_when_numbers_are_wrong(string count, string start)
    {
        var contact = Item("CONTACT_INPUT", "접점 입력");
        var vm = new RepeatExpandViewModel(new[] { contact }, contact, Array.Empty<string>()) { Count = count, StartChannel = start };

        Assert.True(vm.HasError);
        Assert.False(vm.CanExpand);
        Assert.Empty(vm.Preview);
    }

    [Fact]
    public async Task should_return_the_spec_only_when_expand_is_confirmed()
    {
        var contact = Item("CONTACT_INPUT", "접점 입력");
        var cancelled = new RepeatExpandViewModel(new[] { contact }, contact, Array.Empty<string>()) { Count = "2", KeyFormat = "ci_{d}" };
        await cancelled.CancelAsync();
        Assert.Null(cancelled.Result);

        var confirmed = new RepeatExpandViewModel(new[] { contact }, contact, Array.Empty<string>()) { Count = "2", StartChannel = "1", KeyFormat = "ci_{d}" };
        await confirmed.ExpandAsync();
        Assert.Equal(new RepeatExpandSpec("CONTACT_INPUT", 2, 1, "ci_{d}"), confirmed.Result);
    }
    #endregion

    #region - 프리셋 관리 -
    private DevicePresetStore NewStore()
    {
        var store = new DevicePresetStore(Path.Combine(_directory, "presets.json"), () => new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero));
        store.Load();
        return store;
    }

    private static DevicePreset Preset(string name, params string[] types) => new()
    {
        Id = DevicePresetStore.NewId(),
        Name = name,
        Category = EnumDeviceCategory.Lamp,
        Components = types.Select((t, i) => new ComponentDefinitionModel { Key = $"part_{i + 1}", Type = t }).ToList(),
    };

    [Fact]
    public void should_list_only_the_presets_of_the_chosen_category()
    {
        var store = NewStore();
        store.Save(Preset("경광등 표준", "LAMP_UNIT"));
        var vm = new PresetManagerViewModel(store, new TypesCatalog("LAMP_UNIT"), new AnswerDialogs(), EnumDeviceCategory.Lamp);

        Assert.Equal(new[] { "경광등 표준" }, vm.Presets.Select(p => p.Name).ToArray());

        vm.Category = EnumDeviceCategory.Enclosure;
        Assert.DoesNotContain(vm.Presets, p => p.Name == "경광등 표준");
    }

    [Fact]
    public void should_flag_a_preset_whose_type_left_the_catalog_instead_of_hiding_it()
    {
        var store = NewStore();
        store.Save(Preset("옛 프리셋", "LAMP_UNIT", "RETIRED_TYPE"));
        var vm = new PresetManagerViewModel(store, new TypesCatalog("LAMP_UNIT"), new AnswerDialogs(), EnumDeviceCategory.Lamp);

        vm.Selected = vm.Presets.Single();

        Assert.Contains("RETIRED_TYPE", vm.SelectedProblem);
    }

    [Fact]
    public async Task should_rename_duplicate_and_delete_through_the_store()
    {
        var store = NewStore();
        store.Save(Preset("A", "LAMP_UNIT"));
        var dialogs = new AnswerDialogs { Text = "B", Confirm = true };
        var vm = new PresetManagerViewModel(store, new TypesCatalog("LAMP_UNIT"), dialogs, EnumDeviceCategory.Lamp);
        vm.Selected = vm.Presets.Single();

        await vm.RenameAsync();
        Assert.Equal("B", vm.Selected!.Name);

        vm.Duplicate();
        Assert.Equal(2, store.ForCategory(EnumDeviceCategory.Lamp).Count);

        await vm.DeleteAsync();
        Assert.Single(store.ForCategory(EnumDeviceCategory.Lamp));
    }

    [Fact]
    public async Task should_keep_the_preset_when_delete_is_not_confirmed()
    {
        var store = NewStore();
        store.Save(Preset("A", "LAMP_UNIT"));
        var vm = new PresetManagerViewModel(store, new TypesCatalog("LAMP_UNIT"), new AnswerDialogs { Confirm = false }, EnumDeviceCategory.Lamp);
        vm.Selected = vm.Presets.Single();

        await vm.DeleteAsync();

        Assert.Single(store.ForCategory(EnumDeviceCategory.Lamp));
    }

    [Fact]
    public void should_ask_the_owner_to_open_the_assembly_when_requested()
    {
        var store = NewStore();
        store.Save(Preset("A", "LAMP_UNIT"));
        var vm = new PresetManagerViewModel(store, new TypesCatalog("LAMP_UNIT"), new AnswerDialogs(), EnumDeviceCategory.Lamp);
        vm.Selected = vm.Presets.Single();
        DevicePreset? asked = null;
        vm.OpenInAssemblyRequested += (_, preset) => asked = preset;

        vm.OpenInAssembly();

        Assert.Same(vm.Selected, asked);
    }
    #endregion

    #region - fakes -
    private sealed class TypesCatalog : IComponentCatalog
    {
        private readonly HashSet<string> _codes;
        public TypesCatalog(params string[] codes) => _codes = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);

        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
        public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false) => _codes.Select(c => new ComponentTypeInfo { Code = c, Label = c }).ToList();
        public ComponentTypeInfo? Find(string? code) => code is not null && _codes.Contains(code) ? new ComponentTypeInfo { Code = code.ToUpperInvariant(), Label = code } : null;
    }

    private sealed class AnswerDialogs : IAssemblyDialogs
    {
        public bool Confirm { get; set; } = true;
        public string? Text { get; set; }

        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(Confirm);
        public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult(Text);
        public Task<RepeatExpandSpec?> AskRepeatExpandAsync(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys) => Task.FromResult<RepeatExpandSpec?>(null);
        public Task<int?> OpenRegisterAsync(DevicePreset preset) => Task.FromResult<int?>(null);
    }
    #endregion
}
