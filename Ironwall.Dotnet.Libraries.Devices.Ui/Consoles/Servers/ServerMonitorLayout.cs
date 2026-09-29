namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 모니터 목록 칸의 높이 바닥 (GIS 실창 2026-09-27 #1 · #51)
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 목록이 지표 띠 · 배정 칩에 밀려도 지켜야 하는 높이.
/// </summary>
/// <remarks>
/// 실창에서 서버를 고르면 목록이 0행이 됐다 — 목록과 지표 띠가 DockPanel 이라 띠가 원하는 높이를 먼저 가져갔다.
/// 이제 목록 줄에 이 값을 <c>MinHeight</c> 로 걸어 모자라면 띠 쪽이 아래에서 잘린다.
/// </remarks>
public static class ServerMonitorLayout
{
    /// <summary>열 머리 높이(Console.DataGrid 머리 실측).</summary>
    public const double ColumnHeaderHeight = 34;

    /// <summary>행 높이(Console.DataGrid RowHeight).</summary>
    public const double RowHeight = 38;

    /// <summary>늘 보여야 하는 행 수.</summary>
    public const int MinVisibleRows = 5;

    /// <summary>목록 줄의 바닥 높이 — 머리 + 다섯 줄 + 테두리 2.</summary>
    public const double MinListHeight = ColumnHeaderHeight + (RowHeight * MinVisibleRows) + 2;

    /// <summary>배정 트레이 칩 한 줄의 높이(ListBoxItem 에 못 박는다 — 칩 24 + 위아래 여백 4 + 항목 테두리 · 여백 4).</summary>
    public const double TrayRowHeight = 32;

    /// <summary>배정 트레이가 한 번에 보이는 칩 줄 수 — 넘치면 트레이 안에서 굴린다.</summary>
    public const int TrayVisibleRows = 2;

    /// <summary>
    /// 배정 트레이의 최대 높이 = 줄 높이 × 보이는 줄 수. 예전 96 은 세 줄째가 콘솔 바닥 선에 붙어 잘려 보였다(2026-09-30 GIS 실창 020).
    /// </summary>
    public const double TrayMaxHeight = TrayRowHeight * TrayVisibleRows;
}
