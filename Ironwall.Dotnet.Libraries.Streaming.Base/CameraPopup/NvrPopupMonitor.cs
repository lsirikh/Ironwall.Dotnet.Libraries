using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 브로커 요청 — NVR 관제석 모니터 한 대 (PRD FR-08, 명세 §11.5.9 G-51 POPUP_LAYOUT_GET)
   Created By   : Claude (T-07)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// NVR Manager 가 알려 준 관제석 모니터. <see cref="Index"/> 는 1부터이고 <c>CAMERA_POPUP_OPEN.monitor</c> 에 그대로 쓴다.
/// 해상도는 상대 PC 값이라 표시용일 뿐이다(픽셀은 보내지 않는다).
/// </summary>
public sealed record NvrPopupMonitor(int Index, bool IsPrimary, int Width, int Height)
{
    /// <summary>설정 칸 표시 — <c>모니터 2 · 2560×1440 (주)</c>.</summary>
    public string Describe()
    {
        var size = Width > 0 && Height > 0
            ? string.Create(CultureInfo.InvariantCulture, $" · {Width}×{Height}")
            : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"모니터 {Index}{size}{(IsPrimary ? " (주)" : string.Empty)}");
    }
}
