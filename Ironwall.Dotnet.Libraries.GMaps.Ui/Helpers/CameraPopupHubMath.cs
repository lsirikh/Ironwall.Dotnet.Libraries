using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 제어 허브 순수 계산(WPF 무의존) — 뱃지 상한·경계 clamp·드래그 데드존 (CameraPopup_ControlHub NFR-05)
   Created By   : Claude Code
   Created On   : 2026-07-27
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 제어 허브(<c>CameraPopupControlHub</c>)의 순수 계산 헬퍼 — WPF/컨트롤 무의존이라 단위테스트 소스링크 대상.
/// </summary>
public static class CameraPopupHubMath
{
    /// <summary>뱃지 텍스트 — <paramref name="max"/>(기본 9) 초과 시 "N+" 상한 표기. 0 이하는 빈 문자열(허브 숨김).</summary>
    public static string BadgeText(int count, int max = 9)
        => count <= 0 ? string.Empty : (count > max ? $"{max}+" : count.ToString());

    /// <summary>드래그 여부 — 시작점 대비 이동량이 데드존(기본 8px) 초과면 true(클릭과 구분).</summary>
    public static bool IsDrag(double dx, double dy, double threshold = 8.0)
        => (dx * dx + dy * dy) > (threshold * threshold);

    /// <summary>
    /// 허브 좌상단(x,y)을 캔버스 경계 안으로 clamp — 허브가 맵 밖으로 이탈/실종하는 것 방지(FR-07·R-4 복원 clamp).
    /// 캔버스가 허브보다 작으면(초기 미측정 등) 0으로 고정. 음수/NaN 방어.
    /// </summary>
    public static (double X, double Y) ClampToBounds(double x, double y, double hubW, double hubH, double canvasW, double canvasH)
    {
        double maxX = canvasW - hubW;
        double maxY = canvasH - hubH;
        double cx = maxX <= 0 ? 0 : Clamp(x, 0, maxX);
        double cy = maxY <= 0 ? 0 : Clamp(y, 0, maxY);
        return (cx, cy);
    }

    private static double Clamp(double v, double lo, double hi)
    {
        if (double.IsNaN(v)) return lo;
        return v < lo ? lo : (v > hi ? hi : v);
    }
}
