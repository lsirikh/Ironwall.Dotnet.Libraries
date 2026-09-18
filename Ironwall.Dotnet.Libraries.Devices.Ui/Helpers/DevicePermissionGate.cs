using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/// <summary>
/// 장비 패널 권한 게이트 헬퍼 (FR-EN-09/11). 7패널 공유, DRY.
/// 미등록/오프라인/테스트 환경에서 IPermissionService null → 전체허용 폴백.
/// 모듈명 "devices" 고정 — 미정의 모듈 사용 시 영구차단 함정 방지.
/// DeviceGroup도 "devices" 사용 (별도 모듈 없음).
/// </summary>
internal static class DevicePermissionGate
{
    /// <summary>장비 추가·저장 가능 여부 (CanEdit "devices"). 미등록 → true(전체허용).</summary>
    internal static bool CanEdit() => PermissionUiPolicy.Allowed(Resolve(), "devices", EnumPermissionVerb.Edit);

    /// <summary>장비 삭제 가능 여부 (CanDelete "devices"). 미등록 → true(전체허용). T6 중앙 필터(PermissionUiPolicy) 경유.</summary>
    internal static bool CanDelete() => PermissionUiPolicy.Allowed(Resolve(), "devices", EnumPermissionVerb.Delete);

    /// <summary>
    /// 부대 편제 조회 가능 여부 (<c>units:view</c> — 서버 8.0 신설 모듈). 미등록 → true(전체허용).
    /// </summary>
    /// <remarks>
    /// <para>장비 쓰기에 실을 <c>unit_id</c> 를 얻으려면 <c>GET /api/units</c> 를 호출해야 하고 그 표면은
    /// <c>units:view</c> 로 보호된다. 권한이 없으면 <b>호출 전에</b> 접고(403 왕복 절약) 경고를 남긴다 —
    /// 그러면 <c>unit_id</c> 가 생략되고 서버가 기본 부대로 귀속시킨다.</para>
    /// <para>모듈 키는 <see cref="PermissionCatalog.ServerKey(EnumPermissionModule)"/> 로 얻는다(문자열 리터럴 중복 금지).
    /// 서버 백필이 <c>devices:view</c> 보유 그룹에 <c>units:view</c> 를 함께 켰으므로 정상 배포에서는 통과한다.</para>
    /// </remarks>
    internal static bool CanViewUnits()
        => PermissionUiPolicy.Allowed(Resolve(),
                                      PermissionCatalog.ServerKey(EnumPermissionModule.Units),
                                      EnumPermissionVerb.View);

    /// <summary>
    /// IPermissionService IoC lazy 해석. 미등록(오프라인/테스트/DI 미설정) 시 null 반환.
    /// 참고: GMaps.Ui MapViewModel.ResolvePermissionService() 패턴 미러.
    /// </summary>
    internal static IPermissionService? Resolve()
    {
        try { return IoC.Get<IPermissionService>(); }
        catch { return null; }
    }
}
