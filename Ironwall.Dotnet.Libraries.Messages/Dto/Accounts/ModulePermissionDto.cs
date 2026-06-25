using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 모듈별 권한 동사 (UserGroup.permissions.modules 의 값). 실서버 확인: nested `{view,edit,control,delete}`.
/// ⚠ 로그인 응답의 user.permissions 는 flat-string(`"rw"`)로 다름(B-3 drift) — 그쪽은 raw JObject 로 받는다. — GOP-00 PRD FR-1
/// </summary>
public class ModulePermissionDto
{
    [JsonProperty("view")]    public bool View { get; set; }
    [JsonProperty("edit")]    public bool Edit { get; set; }
    [JsonProperty("control")] public bool Control { get; set; }
    [JsonProperty("delete")]  public bool Delete { get; set; }
}
