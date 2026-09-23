namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>콘솔(T1 — 레일 · 목록 · 상세)의 폭별 배치.</summary>
public enum ConsoleLayoutMode
{
    /// <summary>1280 이상 — 세 칸이 나란히 붙는다. 경계를 끌어 상세 폭을 바꿀 수 있다.</summary>
    Docked,
    /// <summary>960~1279 — 상세는 서랍. 행을 고르거나 등록할 때만 오른쪽에서 밀려 나와 목록 위에 겹친다.</summary>
    Drawer,
    /// <summary>960 미만 — 레일이 아이콘(56)으로 접힌다. 상세는 서랍 그대로.</summary>
    Compact,
}

/// <summary><see cref="ConsoleLayoutMath.Resolve"/> 의 결과. 단위는 DIU.</summary>
public readonly record struct ConsoleLayout(
    ConsoleLayoutMode Mode,
    double RailWidth,
    double ListWidth,
    double DetailWidth,
    bool IsDetailDocked,
    bool IsSplitterVisible);

/// <summary>
/// 콘솔 배치 판정 — <b>이 한 곳에서만</b> 한다. 화면 없이 단위 테스트할 수 있게 WPF 에 기대지 않는다.
/// (설계 정본 window-layout-system-storyboard.html L116-127 · L2175-2197)
/// </summary>
public static class ConsoleLayoutMath
{
    public const double DockedMinWidth = 1280;
    public const double CompactBelowWidth = 960;

    public const double RailExpanded = 184;
    public const double RailCollapsed = 56;

    public const double DetailDefault = 340;
    public const double DetailMin = 300;
    public const double DetailMax = 480;
    /// <summary>머리의 S · M · L 버튼.</summary>
    public const double DetailSmall = 300, DetailMedium = 340, DetailLarge = 480;

    public const double DrawerMax = 360;
    public const double DrawerRatio = 0.86;

    public const double ListMinWidth = 360;
    public const double HeaderHeight = 40, ToolbarHeight = 48, RowHeight = 38, StatusBarHeight = 30, InputHeight = 32;

    /// <summary>경계를 방향키로 옮기는 한 걸음.</summary>
    public const double SplitterKeyStep = 10;

    public static ConsoleLayoutMode ModeFor(double width)
        => width >= DockedMinWidth ? ConsoleLayoutMode.Docked
         : width >= CompactBelowWidth ? ConsoleLayoutMode.Drawer
         : ConsoleLayoutMode.Compact;

    public static double ClampDetailWidth(double requested)
        => double.IsNaN(requested) || double.IsInfinity(requested)
            ? DetailDefault
            : Math.Max(DetailMin, Math.Min(DetailMax, Math.Round(requested)));

    public static ConsoleLayout Resolve(double width, double requestedDetailWidth = DetailDefault)
    {
        if (double.IsNaN(width) || width < 0) width = 0;

        var mode = ModeFor(width);
        var rail = mode == ConsoleLayoutMode.Compact ? RailCollapsed : RailExpanded;

        if (mode == ConsoleLayoutMode.Docked)
        {
            var detail = ClampDetailWidth(requestedDetailWidth);
            return new ConsoleLayout(mode, rail, Math.Max(0, width - rail - detail), detail, IsDetailDocked: true, IsSplitterVisible: true);
        }

        // 서랍은 목록 위에 겹친다 — 목록 폭을 깎지 않는다.
        var drawer = Math.Min(DrawerMax, width * DrawerRatio);
        return new ConsoleLayout(mode, rail, Math.Max(0, width - rail), drawer, IsDetailDocked: false, IsSplitterVisible: false);
    }

    /// <summary>상세 칸이 지금 보여야 하는가. 도킹이면 늘 보인다("선택 없음" 상태를 그린다).</summary>
    public static bool IsDetailOpen(ConsoleLayoutMode mode, int selectedCount, bool isCreating)
        => mode == ConsoleLayoutMode.Docked || selectedCount > 0 || isCreating;

    /// <summary>
    /// 서랍(또는 컴팩트)이 목록 위에 떠 있을 때, 목록 · 상태바가 <b>실제로</b> 피해야 할 오른쪽 폭(DIU).
    /// 도킹이면 상세가 제 칸에 있어 겹치지 않으므로 0 — 서랍이 닫혀 있어도(선택 없음) 덮을 게 없으므로 0.
    /// 이 값만큼 목록 쪽 컨테이너에 오른쪽 여백을 주면, 목록은 덮인 채로 전체 폭을 잰 척하지 않고
    /// 실제로 줄어든 폭으로 다시 잰다(열이 접히거나 스크롤이 생긴다 — 잘려서 사라지지 않는다).
    /// </summary>
    public static double ListRightInset(ConsoleLayout layout, bool isDetailOpen)
        => !layout.IsDetailDocked && isDetailOpen ? layout.DetailWidth : 0;

    /// <summary>
    /// 경계가 <paramref name="splitterDelta"/> 만큼 움직인 뒤의 상세 폭. 경계가 왼쪽(음수)으로 가면 상세가 넓어진다.
    /// </summary>
    public static double DetailWidthAfterSplitterMove(double currentDetailWidth, double splitterDelta)
        => ClampDetailWidth(currentDetailWidth - splitterDelta);
}
