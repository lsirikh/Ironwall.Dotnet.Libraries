using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 부품 선언 한 개(<c>hardware_spec.components[]</c>).
/// </summary>
/// <remarks>
/// <see cref="Key"/> 는 장비 안에서만 유일한 자유 이름(<c>^[a-z][a-z0-9_]*$</c>)이고 <b>계약이 아니다</b> —
/// 코드는 key 가 아니라 <see cref="Type"/>(카탈로그 <c>component_type</c> 어휘)으로 부품을 찾는다.
/// </remarks>
public class ComponentDefinitionModel
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool? InService { get; set; }
    public int? Channel { get; set; }
    public string? Position { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Serial { get; set; }
    public string? Firmware { get; set; }
    public string? HardwareRev { get; set; }
    public string? InstalledAt { get; set; }
    public string? ReplacedAt { get; set; }
    public JObject? Spec { get; set; }
}
