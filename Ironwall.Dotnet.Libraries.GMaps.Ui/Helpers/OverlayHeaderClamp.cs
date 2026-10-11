using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 지도 겹창 머리 끌기 위치 클램프(공용) — 결함 AUX-D7(MAP-191)
                  레이어 패널(LayerPanelControl.ClampHeaderInside, LAY-D3)과 같은 규칙을
                  상세 · 센서 정보 · 추적 설정 · 재생 · 사이트 고정 · 방송 · TTS · 지도 등록 · 속성창 겹창이 함께 쓴다.
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 겹창을 머리로 끌 때 새 위치를 지도(Canvas) 안으로 자른다 — 겹창 폭 전체와 머리 띠가 지도 안에 남아
/// 머리를 다시 잡을 수 있고, 위쪽(호스트 제목줄 · 툴바)을 덮지 않는다.
/// 예전엔 레이어 패널 · 팔레트만 잘랐고 나머지 겹창은 창 끝 너머로 끌면 음수 좌표(지도 밖)로 나갔다.
/// 지도 크기를 아직 모르면(ActualWidth/Height 0) 자르지 않는다.
/// </summary>
internal static class OverlayHeaderClamp
{
    /// <param name="left">끌기로 계산한 새 Canvas.Left.</param>
    /// <param name="top">끌기로 계산한 새 Canvas.Top.</param>
    /// <param name="overlay">끄는 겹창(폭 = Width, 없으면 ActualWidth).</param>
    /// <param name="canvas">겹창 자리(ContentPresenter)의 부모 Canvas.</param>
    /// <param name="headerHeight">머리 띠 높이 — 끌기를 받는 영역(각 겹창의 OnDragDown 기준과 같게).</param>
    public static (double Left, double Top) Clamp(double left, double top, FrameworkElement overlay, Canvas canvas, double headerHeight)
    {
        if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return (left, top);
        double width = double.IsNaN(overlay.Width) ? overlay.ActualWidth : overlay.Width;
        return (Math.Clamp(left, 0, Math.Max(0, canvas.ActualWidth - width)),
                Math.Clamp(top, 0, Math.Max(0, canvas.ActualHeight - headerHeight)));
    }
}
