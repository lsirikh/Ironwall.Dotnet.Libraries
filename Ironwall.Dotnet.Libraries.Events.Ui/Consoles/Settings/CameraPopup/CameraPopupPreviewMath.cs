using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;

/****************************************************************************
   Purpose      : 설정 화면 모니터 미리보기 — 축척 · 끌기 · 키보드 이동 (PRD FR-12 · drag-first-ux)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>미리보기 안 사각형(DIU).</summary>
public readonly record struct PreviewRect(double Left, double Top, double Width, double Height);

/// <summary>
/// 모니터 작업 영역(물리 px) ↔ 미리보기(DIU) 축척과, 첫 창 끌기 · 키보드 이동의 순수 계산.
/// </summary>
/// <remarks>
/// 판정을 뷰 밖으로 뺀 까닭(drag-first-ux 규칙): UIA 에 드래그 패턴이 없어 끌기는 자동화로 단언할 수 없다.
/// 이 함수들의 헤드리스 시험과 키보드 경로가 회귀망이다.
/// </remarks>
public static class CameraPopupPreviewMath
{
    /// <summary>미리보기 최대 크기(SB §2 와이어프레임 320×180).</summary>
    public const double PreviewMaxWidth = 320;
    public const double PreviewMaxHeight = 180;

    /// <summary>방향키 한 번 = 10 px(모니터 픽셀). Shift = 10칸 = 100 px.</summary>
    public const int NudgeStepPx = 10;
    public const int NudgeBigMultiplier = 10;

    /// <summary>작업 영역을 미리보기 상자에 비율대로 넣는 축척(DIU / px). 빈 영역이면 0.</summary>
    public static double Scale(PixelRect workArea, double maxWidth = PreviewMaxWidth, double maxHeight = PreviewMaxHeight)
    {
        if (workArea.IsEmpty || maxWidth <= 0 || maxHeight <= 0) return 0;
        return Math.Min(maxWidth / workArea.Width, maxHeight / workArea.Height);
    }

    /// <summary>작업 영역 기준 절대 사각형(px) → 미리보기 사각형.</summary>
    public static PreviewRect ToPreview(PixelRect workArea, PixelRect window, double scale)
        => new((window.X - workArea.X) * scale, (window.Y - workArea.Y) * scale, window.Width * scale, window.Height * scale);

    /// <summary>
    /// 끌기 — 누른 때의 상대 위치(px) + 미리보기에서 움직인 양(DIU)을 축척으로 나눠 새 상대 위치(당긴 뒤).
    /// 데드존(<see cref="DragMath.DeadZone"/> = 8 DIU) 안이면 <c>null</c> — 클릭이지 끌기가 아니다.
    /// </summary>
    public static (int X, int Y)? DragTo(PixelRect workArea, int startX, int startY, int width, int height,
                                         double dxDiu, double dyDiu, double scale, bool alreadyDragging)
    {
        if (scale <= 0) return null;
        if (!alreadyDragging && !DragMath.IsDrag(dxDiu, dyDiu)) return null;

        var x = startX + (int)Math.Round(dxDiu / scale);
        var y = startY + (int)Math.Round(dyDiu / scale);
        var c = CameraPopupPlacement.ClampRelative(workArea, x, y, width, height);
        return (c.X, c.Y);
    }

    /// <summary>키보드 이동 — 방향 (−1/0/+1) × 10 px, Shift 면 × 10. 작업 영역 안으로 당긴다.</summary>
    public static (int X, int Y) Nudge(PixelRect workArea, int x, int y, int width, int height, int dirX, int dirY, bool big)
    {
        var step = NudgeStepPx * (big ? NudgeBigMultiplier : 1);
        var c = CameraPopupPlacement.ClampRelative(workArea, x + Math.Sign(dirX) * step, y + Math.Sign(dirY) * step, width, height);
        return (c.X, c.Y);
    }
}
