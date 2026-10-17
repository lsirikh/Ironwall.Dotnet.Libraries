using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// GET /api/auth/me/permissions 응답 data 페이로드 (Grant Scheduling v5.2, FR-GS-01/02).
/// <para>로그인 응답 <c>user.permissions</c> 와 동형(modules/device_groups)에 <c>valid_until</c>·<c>server_time</c> 추가.
/// 서버 <c>app/routers/auth.py</c> <c>get_my_permissions</c> 와 필드 일치.</para>
/// <para>날짜는 ApiMessageHelper.JsonSettings 의 <c>DateParseHandling.None</c> 때문에 반드시 <see cref="string"/> 으로 수신하고
/// 파싱(<c>DateTimeOffset</c>)은 PermissionService 가 수행한다(AuthUserDto 컨벤션 일치).</para>
/// <para>modules 는 PermissionsFlattener 재사용 위해 raw <see cref="JObject"/> 로 관용 수용(flat/nested/envelope).</para>
/// </summary>
public class PermissionsSnapshotDto
{
    /// <summary>모듈별 권한 raw(<c>{module:{verb:bool}}</c>). Flatten 재사용. null=권한없음(안전기본).</summary>
    [JsonProperty("modules")] public JObject? Modules { get; set; }

    /// <summary>접근 가능 디바이스 그룹 ID. null=없음.</summary>
    [JsonProperty("device_groups")] public List<int>? DeviceGroups { get; set; }

    /// <summary>가장 임박한 grant 만료(KST ISO8601 offset). null=상시(만료없음). 클라 재조회 타이머 기준(FR-GS-03).</summary>
    [JsonProperty("valid_until")] public string? ValidUntil { get; set; }

    /// <summary>서버 현재 시각(KST ISO8601 offset). clockSkew = server_time − localNow 산출용(FR-GS-01).</summary>
    [JsonProperty("server_time")] public string? ServerTime { get; set; }
}
