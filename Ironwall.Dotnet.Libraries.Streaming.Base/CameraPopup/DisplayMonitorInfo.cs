using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 조회된 모니터 한 대 (camera-popup-modes PRD FR-12 · §3 모니터)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 모니터 한 대. 좌표는 <b>물리 픽셀</b>(가상 화면 좌표계 — 주 모니터 왼쪽 위가 0,0).
/// </summary>
/// <param name="DeviceName">Win32 장치 이름(<c>\\.\DISPLAY2</c>). 재부팅 뒤 순서가 바뀌어도 이름 + 해상도로 다시 찾는다.</param>
/// <param name="Bounds">모니터 전체.</param>
/// <param name="WorkArea">작업 표시줄을 뺀 영역 — 창 배치 기준.</param>
/// <param name="IsPrimary">주 모니터인가.</param>
/// <param name="Dpi">유효 DPI(96 = 100%).</param>
public sealed record DisplayMonitorInfo(string DeviceName, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary, int Dpi = 96)
{
    /// <summary>식별자 — <see cref="CameraPopupMonitorId"/> 형식.</summary>
    public string Id => CameraPopupMonitorId.Format(DeviceName, Bounds.Width, Bounds.Height);

    /// <summary>배율(%) — 96 DPI = 100.</summary>
    public int ScalePercent => Dpi <= 0 ? 100 : (int)Math.Round(Dpi * 100d / 96d);

    /// <summary>
    /// 사람이 부르는 번호 — 장치 이름 끝 숫자(<c>\\.\DISPLAY2</c> → 2), 없으면 목록 순번.
    /// <see cref="Describe"/> 와 모니터 식별 카드가 같은 번호를 쓰도록 한 곳에서 정한다.
    /// </summary>
    public int Number(int ordinal) => CameraPopupMonitorId.DisplayNumber(DeviceName) ?? ordinal;

    /// <summary>
    /// 목록 글자 —<c>모니터 2 · 2560×1440 · 주</c>. 번호는 장치 이름 끝 숫자(없으면 목록 순번).
    /// </summary>
    public string Describe(int ordinal)
    {
        var number = Number(ordinal);
        var text = string.Create(CultureInfo.InvariantCulture, $"모니터 {number} · {Bounds.Width}×{Bounds.Height}");
        if (ScalePercent != 100) text += string.Create(CultureInfo.InvariantCulture, $" · {ScalePercent}%");
        if (IsPrimary) text += " · 주";
        return text;
    }
}
