using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 2026-10-11 조립기 시험들이 함께 쓰는 가짜 카탈로그(<see cref="AssemblyViewModelTests"/> 의 것과 같은 네 유형 + 카메라 IR) — 서버 없음.
/// </summary>
internal sealed class AsmFakeCatalog : IComponentCatalog
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

/// <summary>조립기 대화 가짜 — 확인 답 · 글 답을 정해 두고, 물은 것을 남긴다.</summary>
internal sealed class AsmFakeDialogs : IAssemblyDialogs
{
    public bool ConfirmAnswer { get; set; } = true;
    public List<(string Title, string Message)> Confirms { get; } = new();
    public string? TextAnswer { get; set; }
    public RepeatExpandSpec? RepeatAnswer { get; set; }

    public Task<bool> ConfirmAsync(string title, string message) { Confirms.Add((title, message)); return Task.FromResult(ConfirmAnswer); }
    public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult(TextAnswer);
    public Task<RepeatExpandSpec?> AskRepeatExpandAsync(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys) => Task.FromResult(RepeatAnswer);
    public Task<int?> OpenRegisterAsync(DevicePreset preset) => Task.FromResult<int?>(null);
}
