using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
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

/// <summary>
/// 프리셋으로 등록 창(device-assembly-preset FR-12 ~ FR-14) — 개체 정보만 묻고, 문제가 있으면 보내지 않고, 미리보기가 곧 보낼 내용이다.
/// 본문의 모양(부품이 실리는가 · 금지 8개가 없는가)은 <c>PresetRegisterTests</c> 가 고정한다.
/// </summary>
public class RegisterFromPresetViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-reg-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private DevicePresetStore NewStore(params DevicePreset[] presets)
    {
        var store = new DevicePresetStore(Path.Combine(_directory, "presets.json"), () => new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero));
        store.Load();
        foreach (var preset in presets) Assert.True(store.Save(preset).IsSuccess);
        return store;
    }

    private static DevicePreset LampPreset(string name = "경광등 표준") => new()
    {
        Id = DevicePresetStore.NewId(),
        Name = name,
        Category = EnumDeviceCategory.Lamp,
        Components = new[] { new ComponentDefinitionModel { Key = "lamp", Type = "LAMP_UNIT" } },
    };

    private static RegisterFromPresetViewModel NewVm(DevicePresetStore store, EnumDeviceCategory category, IEnumerable<int>? used = null, DevicePreset? fixedPreset = null, IEnumerable<IControllerDeviceModel>? controllers = null)
        => new(store, new OneTypeCatalog(), new PresetRegistrar(new MockDeviceApiService(), new MockDeviceProviderService(), new MockLogService()),
            controllers ?? Array.Empty<IControllerDeviceModel>(), _ => (used ?? Array.Empty<int>()).ToHashSet(), category, fixedPreset);

    [Fact]
    public void should_suggest_the_first_free_number_and_a_name_when_opened()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp, used: new[] { 1, 2, 4 });

        Assert.Equal("3", vm.DeviceNumber);
        Assert.Contains("경광등", vm.DeviceName);
    }

    [Fact]
    public void should_select_the_first_preset_of_the_category_and_show_what_will_be_sent()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);

        Assert.Equal("경광등 표준", vm.SelectedPreset!.Name);
        Assert.Contains("LAMP_UNIT", vm.PreviewJson);               // 부품이 본문에 실린다 — 보이는 것이 곧 나가는 것
        Assert.True(vm.CanRegister);
    }

    [Fact]
    public void should_refuse_a_number_that_is_already_in_use()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp, used: new[] { 1 });

        vm.DeviceNumber = "1";

        Assert.Contains(vm.Problems, p => p.Contains("이미 쓰이고"));
        Assert.False(vm.CanRegister);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    public void should_refuse_a_number_that_is_not_a_number(string text)
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);

        vm.DeviceNumber = text;

        Assert.True(vm.HasProblems);
        Assert.False(vm.CanRegister);
    }

    [Fact]
    public void should_refuse_an_empty_name()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);

        vm.DeviceName = "   ";

        Assert.True(vm.HasProblems);
        Assert.False(vm.CanRegister);
    }

    [Fact]
    public void should_require_a_saved_controller_when_registering_a_sensor()
    {
        var preset = LampPreset("센서") with { Category = EnumDeviceCategory.Sensor };
        var saved = new ControllerDeviceModel { Id = 5, DeviceNumber = 1, DeviceName = "제어기 1" };
        var draft = new ControllerDeviceModel { Id = 0, DeviceNumber = 2, DeviceName = "새 제어기" };
        var vm = NewVm(NewStore(preset), EnumDeviceCategory.Sensor, controllers: new IControllerDeviceModel[] { saved, draft });

        Assert.True(vm.ShowsController);
        Assert.Equal(new[] { saved }, vm.Controllers.ToArray());     // 저장 전 제어기는 고를 수 없다
        Assert.False(vm.CanRegister);

        vm.Controller = saved;
        Assert.True(vm.CanRegister);
    }

    [Fact]
    public void should_hide_the_preset_list_when_the_assembly_handed_over_a_preset()
    {
        var handedOver = LampPreset("(조립기)");
        var vm = NewVm(NewStore(), EnumDeviceCategory.Enclosure, fixedPreset: handedOver);

        Assert.False(vm.ShowsPresetList);
        Assert.Same(handedOver, vm.SelectedPreset);
        Assert.Equal(EnumDeviceCategory.Lamp, vm.Category);           // 카테고리는 프리셋을 따른다

        vm.Category = EnumDeviceCategory.Camera;
        Assert.Equal(EnumDeviceCategory.Lamp, vm.Category);
    }

    [Fact]
    public void should_block_a_preset_whose_type_left_the_catalog()
    {
        var preset = LampPreset() with { Components = new[] { new ComponentDefinitionModel { Key = "old", Type = "RETIRED_TYPE" } } };
        var vm = NewVm(NewStore(preset), EnumDeviceCategory.Lamp);

        Assert.True(vm.HasProblems);                                  // 말없이 빼고 보내지 않는다
        Assert.False(vm.CanRegister);
    }

    [Fact]
    public void should_show_connection_fields_only_for_categories_that_have_them()
    {
        var vm = NewVm(NewStore(LampPreset()), EnumDeviceCategory.Lamp);
        Assert.True(vm.ShowsConnection);

        vm.Category = EnumDeviceCategory.Gate;
        Assert.False(vm.ShowsConnection);
        Assert.False(vm.ShowsController);
    }

    private sealed class OneTypeCatalog : IComponentCatalog
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
        public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false) => new[] { new ComponentTypeInfo { Code = "LAMP_UNIT", Label = "경광등" } };
        public ComponentTypeInfo? Find(string? code) => string.Equals(code, "LAMP_UNIT", StringComparison.OrdinalIgnoreCase) ? new ComponentTypeInfo { Code = "LAMP_UNIT", Label = "경광등" } : null;
    }
}
