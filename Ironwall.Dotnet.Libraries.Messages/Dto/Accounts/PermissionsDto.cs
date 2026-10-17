using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// UserGroup 권한 구조 (실서버 확인: `{modules:{module:{view,edit,control,delete}}, device_groups:[int]|null}`).
/// 컬렉션은 비-null 초기화(누락 시 NRE 방지). — GOP-00 PRD FR-1
/// </summary>
public class PermissionsDto
{
    [JsonProperty("modules")]       public Dictionary<string, ModulePermissionDto> Modules { get; set; } = new();
    [JsonProperty("device_groups")] public List<int>? DeviceGroups { get; set; }
}
