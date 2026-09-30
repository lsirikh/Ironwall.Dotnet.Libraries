namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// PTZ 항목(제어 · 프리셋 이동 · 복귀) 비활성 이유(순수, FR-14). 우선순위: 고정 카메라 → 권한 → 제공자.
/// null 이면 PTZ 가능.
/// </summary>
internal static class PtzAvailability
{
    public const string FixedCameraReason = "고정 카메라 — PTZ 없음";
    public const string NoPermissionReason = "PTZ 권한 없음";

    public static string? DisabledReason(bool isPtz, bool ptzAllowed, string? providerReason)
    {
        if (!isPtz) return FixedCameraReason;
        if (!ptzAllowed) return NoPermissionReason;
        return string.IsNullOrWhiteSpace(providerReason) ? null : providerReason;
    }
}
