using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;

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
    internal static bool CanEdit() => Resolve()?.CanEdit("devices") ?? true;

    /// <summary>장비 삭제 가능 여부 (CanDelete "devices"). 미등록 → true(전체허용).</summary>
    internal static bool CanDelete() => Resolve()?.CanDelete("devices") ?? true;

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
