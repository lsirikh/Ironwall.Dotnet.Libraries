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

    /// <summary>
    /// 세 칸 바닥 띠(레일 바닥 · 목록 상태 줄 · 상세 적용 막대)의 최소 높이 — 상세 막대(위 여백 10 + 단추 32 + 아래 10 + 윗선 1)와 같다.
    /// 띠마다 제 높이로 서면 윗선이 계단처럼 어긋났다(2026-09-27 실창: 장비 716 / 733 / 707).
    /// </summary>
    public const double FooterBandHeight = 53;

    /// <summary>
    /// 이 높이를 넘는 띠 내용은 "띠" 가 아니라 <b>작업 판</b>(이벤트 콘솔 조치 트레이 · 계정 콘솔 배정 대기 막대까지 선 상태 줄)이다 —
    /// 다른 두 띠가 그 높이를 따라가지 않는다(따라가면 레일 바닥 · 상세 막대가 빈 판으로 200px 넘게 자란다, 2026-09-30 이벤트 미리보기 실측).
    /// 띠는 두 줄(= 바닥 높이 × 2)까지다.
    /// </summary>
    public const double FooterBandAlignLimit = FooterBandHeight * 2;

    /// <summary>띠 내용 높이가 세 칸 맞춤에 들어가는가(<see cref="FooterBandAlignLimit"/> 이하).</summary>
    public static bool IsAlignableFooterBand(double contentHeight)
        => contentHeight > 0 && contentHeight <= FooterBandAlignLimit + 0.01;

    /// <summary>
    /// 세 칸 바닥 띠가 <b>함께</b> 쓸 높이 — 맞춤에 드는 띠 내용 중 가장 큰 원하는 높이(<paramref name="tallestContent"/>)를
    /// 위로 올린 값, 단 <see cref="FooterBandHeight"/> 이상. 띠마다 제 높이로 서면 윗선이 어긋난다(2026-09-30 서버 콘솔: 75px).
    /// </summary>
    public static double AlignedFooterBandHeight(double tallestContent)
        => double.IsNaN(tallestContent) || double.IsInfinity(tallestContent)
            ? FooterBandHeight
            : Math.Max(FooterBandHeight, Math.Ceiling(tallestContent - 0.01));

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
    /// 목록 · 상태바가 <b>실제로</b> 쓸 수 있는 폭(DIU) — <see cref="ConsoleLayout.ListWidth"/> 에서
    /// <see cref="ListRightInset"/> 을 뺀 값. 도킹이면 상세가 제 칸을 가져 인셋이 0이라 <c>ListWidth</c> 와 같고,
    /// 서랍이 열려 있으면 겹친 만큼 줄어든다.
    /// <para>
    /// 열 우선순위 사다리 · 카드 개수처럼 "목록이 지금 몇 px 를 쓰는가"에 반응하는 소비자는
    /// <c>ConsoleShell.ActualWidth</c> 가 아니라 이 값(<see cref="ConsoleShell.EffectiveListWidth"/> /
    /// <see cref="ConsoleShell.EffectiveListWidthChanged"/> 로 노출)을 읽어야 한다 — 서랍이 목록 위에
    /// 겹칠 때 셸 자신의 폭은 바뀌지 않기 때문이다(D-03, 커밋 55d257a4 · 8fa2cb5e 에서 실증).
    /// </para>
    /// </summary>
    public static double EffectiveListWidth(ConsoleLayout layout, bool isDetailOpen)
        => Math.Max(0, layout.ListWidth - ListRightInset(layout, isDetailOpen));

    /// <summary>
    /// 경계가 <paramref name="splitterDelta"/> 만큼 움직인 뒤의 상세 폭. 경계가 왼쪽(음수)으로 가면 상세가 넓어진다.
    /// </summary>
    public static double DetailWidthAfterSplitterMove(double currentDetailWidth, double splitterDelta)
        => ClampDetailWidth(currentDetailWidth - splitterDelta);

    #region 툴바 두 줄 — 동작 줄 · 필터 줄 (D-2026-09-27-71c353)
    /// <summary>
    /// 검색창을 "전체 폭" 으로 보일 때의 최소 폭. 이보다 좁게는 절대 누르지 않는다 — 모자라면 아이콘 트리거로 접는다.
    /// <para>
    /// 옛 값 120 은 "남는 자리" 에서 늘 최솟값으로 눌려 안내 글 · 입력 글이 테두리에 붙었다(실창 022 · 024 · 007 · 032 —
    /// D-16 · D-23 · U-13 · U-18 네 번의 부분 수정이 모두 한 줄 예산을 나눠 먹는 구조 안에서 움직였다).
    /// </para>
    /// </summary>
    public const double ToolbarSearchFullMinWidth = 180;
    /// <summary>검색창의 편한 기본 폭 — 자리가 넉넉하면 이 폭으로 선다(내용에 따라 늘었다 줄었다 하지 않는다).</summary>
    public const double ToolbarSearchPreferredWidth = 240;
    /// <summary>아이콘으로 접힌 검색을 눌렀을 때 뜨는 오버레이 입력칸의 폭 — <c>Generic.xaml</c> PART_SearchExpanded.</summary>
    public const double ToolbarSearchFullMaxWidth = 320;
    /// <summary>검색창과 왼쪽 묶음 사이 여백 — <c>Generic.xaml</c> Search 의 Margin 왼쪽 값과 같다(예산 계산용).</summary>
    public const double ToolbarSearchLeftMargin = 12;
    /// <summary>툴바 띠 안쪽 여백의 좌우 합 — <c>Generic.xaml</c> 두 줄 모두 <c>Padding="12,…"</c>(좌 12 + 우 12).</summary>
    public const double ToolbarHorizontalPadding = 24;
    /// <summary>검색이 아이콘으로 접혀도 이 폭은 남아야 누를 수 있다(<c>Console.Button.Icon</c> 한 변과 같다).</summary>
    public const double ToolbarSearchCompactMinWidth = 32;
    /// <summary>[⋯] 버튼 — 한 변 32 + 왼쪽 간격 8.</summary>
    public const double ToolbarOverflowButtonWidth = 40;
    /// <summary>
    /// 둘째 줄(필터 칩 줄)의 최소 높이 — 칩(26) · 날짜 입력(32)이 위아래 여백을 갖고 선다.
    /// 필터가 없으면 이 줄은 높이 0 이다(<see cref="ConsoleToolbar.HasFilterRow"/>).
    /// </summary>
    public const double FilterRowMinHeight = 40;

    /// <summary>
    /// 첫째 줄(동작 줄)의 배치 판정 — [추가][삭제][갱신] · Extra · 검색 · [열 n/m] 만 예산을 나눈다.
    /// <b>필터 칩은 이 예산에 들어가지 않는다</b> — 둘째 줄에 따로 선다(D-2026-09-27-71c353).
    /// <para>
    /// 순서: ① 전부 제자리 + 검색 전체 폭(180~240) → ② Extra 를 [⋯] 로 → ③ [열 n/m] 도 [⋯] 로 →
    /// 그래도 검색 180 이 안 들어가면 검색을 아이콘으로 접고 ①②③ 을 같은 순서로 다시 본다.
    /// 검색이 전체 폭이면 그 폭은 늘 <see cref="ToolbarSearchFullMinWidth"/> 이상이다 — 180 과 0 사이의 어중간한 폭은 없다.
    /// [추가] · [삭제] · [갱신] 은 모든 콘솔의 기본 동작이라 옮기지 않는다.
    /// </para>
    /// <para>입력은 모두 <b>제자리일 때의 폭</b>(간격 포함)이다 — 접힘 상태에 따라 달라지는 실제 폭을 넣으면 진동한다.</para>
    /// </summary>
    /// <param name="toolbarWidth">툴바 자신의 폭(= 목록 칸의 실효 폭).</param>
    /// <param name="fixedLeftWidth">[추가] · [삭제] · [갱신] 의 폭(간격 포함) — 옮기지 않는 부분.</param>
    /// <param name="extraWidth">창 고유 동작(Extra)의 폭(간격 포함), 없으면 0.</param>
    /// <param name="columnsWidth">[열 n/m] 의 폭(간격 포함), 없으면 0.</param>
    /// <param name="showSearch">검색을 보이는 화면인가.</param>
    public static ConsoleToolbarFit ResolveToolbarFit(
        double toolbarWidth, double fixedLeftWidth, double extraWidth, double columnsWidth, bool showSearch)
    {
        static double Clean(double v) => double.IsNaN(v) || double.IsInfinity(v) || v < 0 ? 0 : v;
        toolbarWidth = Clean(toolbarWidth);
        fixedLeftWidth = Clean(fixedLeftWidth);
        extraWidth = Clean(extraWidth);
        columnsWidth = Clean(columnsWidth);

        if (toolbarWidth <= 0)                                          // 아직 재지 못했다 — 아무것도 옮기지 않는다
            return new ConsoleToolbarFit(ConsoleToolbarOverflow.None, showSearch ? ToolbarSearchPreferredWidth : 0, false);

        var available = toolbarWidth - ToolbarHorizontalPadding;

        double Actions(ConsoleToolbarOverflow moved)
        {
            var need = fixedLeftWidth;
            var anyMoved = false;
            if (moved.HasFlag(ConsoleToolbarOverflow.Extra)) anyMoved |= extraWidth > 0; else need += extraWidth;
            if (moved.HasFlag(ConsoleToolbarOverflow.Columns)) anyMoved |= columnsWidth > 0; else need += columnsWidth;
            return need + (anyMoved ? ToolbarOverflowButtonWidth : 0);
        }

        var stages = new List<ConsoleToolbarOverflow> { ConsoleToolbarOverflow.None };
        if (extraWidth > 0) stages.Add(ConsoleToolbarOverflow.Extra);
        if (columnsWidth > 0) stages.Add(stages[^1] | ConsoleToolbarOverflow.Columns);

        if (!showSearch)
        {
            foreach (var stage in stages)
                if (Actions(stage) <= available) return new ConsoleToolbarFit(stage, 0, false);
            return new ConsoleToolbarFit(stages[^1], 0, false);
        }

        // ① 검색 전체 폭(180 이상)을 지키는 가장 얕은 단계
        foreach (var stage in stages)
        {
            var budget = available - Actions(stage) - ToolbarSearchLeftMargin;
            if (budget >= ToolbarSearchFullMinWidth)
                return new ConsoleToolbarFit(stage, Math.Min(budget, ToolbarSearchPreferredWidth), false);
        }

        // ② 정말 좁다 — 검색을 아이콘으로 접고 다시 본다
        foreach (var stage in stages)
            if (Actions(stage) + ToolbarSearchLeftMargin + ToolbarSearchCompactMinWidth <= available)
                return new ConsoleToolbarFit(stage, 0, true);

        return new ConsoleToolbarFit(stages[^1], 0, true);
    }
    #endregion
}

/// <summary><see cref="ConsoleLayoutMath.ResolveToolbarFit"/> 의 결과 — 첫째 줄(동작 줄)의 모양.</summary>
/// <param name="Overflow">[⋯] 팝업으로 옮긴 묶음.</param>
/// <param name="SearchWidth">검색창 폭(전체 폭이면 180~240, 접혔거나 숨었으면 0).</param>
/// <param name="IsSearchCompact">검색이 아이콘 트리거로 접혔는가.</param>
public readonly record struct ConsoleToolbarFit(ConsoleToolbarOverflow Overflow, double SearchWidth, bool IsSearchCompact);

/// <summary>툴바 첫째 줄에서 [⋯] 팝업으로 옮겨진 묶음(<see cref="ConsoleLayoutMath.ResolveToolbarFit"/>). 필터 칩은 둘째 줄에 서므로 옮길 대상이 아니다.</summary>
[Flags]
public enum ConsoleToolbarOverflow
{
    None = 0,
    /// <summary>창 고유 동작(Extra).</summary>
    Extra = 1,
    /// <summary>[열 n/m].</summary>
    Columns = 4,
}
