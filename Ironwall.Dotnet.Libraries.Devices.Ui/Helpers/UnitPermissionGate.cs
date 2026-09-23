using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/// <summary>
/// 부대 편제 권한 게이트 — <c>units:view · units:edit · units:delete</c> 의 <b>단일 출처</b>.
/// </summary>
/// <remarks>
/// <para>서버는 부대 편제를 장비와 다른 모듈로 지킨다(읽기 전용 서버 소스 <c>app/security/permission_map.py</c>:
/// <c>GET /api/units[/graph|/{id}]</c> = <c>units:view</c> · <c>POST/PATCH/PUT /api/units[/{id}]</c> = <c>units:edit</c> ·
/// <c>DELETE /api/units/{id}</c> = <c>units:delete</c>). 부대 콘솔이 예전엔 장비 게이트(<c>devices:edit/delete</c>)를
/// 기본값으로 써서, 장비 권한만 있는 계정에게는 쓰기 버튼이 켜진 채 서버가 403 을 냈고, 부대 권한만 있는 계정은
/// 쓸 수 있는데도 막혔다.</para>
/// <para>모듈 키는 <see cref="PermissionCatalog.ServerKey(EnumPermissionModule)"/> 로 얻는다(문자열 리터럴 중복 금지).
/// <c>IPermissionService</c> 미등록(오프라인/테스트)이면 <see cref="PermissionUiPolicy"/> 의 폴백대로 허용이다.</para>
/// <para>장비를 부대에 <b>배치</b>하는 것은 장비 쓰기(<c>PATCH /api/devices/…</c> = <c>devices:edit</c>)라 이 게이트가 아니라
/// <see cref="DevicePermissionGate.CanEdit"/> 가 지킨다.</para>
/// </remarks>
internal static class UnitPermissionGate
{
    private static string Module => PermissionCatalog.ServerKey(EnumPermissionModule.Units);

    /// <summary>편제 조회(<c>units:view</c>).</summary>
    internal static bool CanView() => PermissionUiPolicy.Allowed(DevicePermissionGate.Resolve(), Module, EnumPermissionVerb.View);

    /// <summary>부대 등록 · 수정 · 상위 바꾸기 · 인접(<c>units:edit</c>).</summary>
    internal static bool CanEdit() => PermissionUiPolicy.Allowed(DevicePermissionGate.Resolve(), Module, EnumPermissionVerb.Edit);

    /// <summary>부대 삭제(<c>units:delete</c>).</summary>
    internal static bool CanDelete() => PermissionUiPolicy.Allowed(DevicePermissionGate.Resolve(), Module, EnumPermissionVerb.Delete);
}
