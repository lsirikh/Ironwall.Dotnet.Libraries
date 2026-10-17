using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;

/****************************************************************************
   Purpose      : 팝업 PTZ hold 버튼 제스처 파싱(WPF 무의존 단일 진실원) (CameraPopup_PressHold_PtzZoomFocus)
   Created By   : Claude Code
   Created On   : 2026-06-29
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>팝업 PTZ hold 버튼 제스처 종류(누름=연속이동/뗌=정지). 타입에 따라 정지 모터가 다름(PTZ vs Imaging).</summary>
public enum PtzHoldGesture { None, PanTilt, Zoom, Focus }

/// <summary>
/// 팝업 컨트롤의 <c>Button.Tag</c> 문자열 → PTZ hold 제스처 파싱(WPF 무의존 — 컨트롤/테스트 공용 단일 진실원). (FR-PH-06)
/// <para>포맷: "x,y"=PanTilt(-1/0/1), "zoom:±1"=Zoom, "focus:±1"=Focus. "stop"/null/미해당=false(호출부가 별도 처리).</para>
/// </summary>
public static class PtzGestureTag
{
    /// <summary>Tag를 (제스처, dx, dy)로 파싱. Zoom/Focus는 dx에 방향(±1), dy=0. 파싱 실패/비제스처면 false.</summary>
    public static bool TryParse(string? tag, out PtzHoldGesture gesture, out int dx, out int dy)
    {
        gesture = PtzHoldGesture.None; dx = 0; dy = 0;
        if (string.IsNullOrEmpty(tag)) return false;

        if (tag.StartsWith("zoom:", StringComparison.Ordinal))
        {
            if (!int.TryParse(tag.AsSpan(5), out dx)) return false;
            gesture = PtzHoldGesture.Zoom;
            return true;
        }
        if (tag.StartsWith("focus:", StringComparison.Ordinal))
        {
            if (!int.TryParse(tag.AsSpan(6), out dx)) return false;
            gesture = PtzHoldGesture.Focus;
            return true;
        }

        var parts = tag.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out dx) && int.TryParse(parts[1], out dy))
        {
            gesture = PtzHoldGesture.PanTilt;
            return true;
        }
        return false;
    }
}
