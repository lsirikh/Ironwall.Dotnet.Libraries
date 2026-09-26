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
/// 툴바 검색창의 밀도(D-23) — 폭이 모자라면 검색을 아이콘 트리거로 접는다.
/// 필터 · 기본 액션(Extra)은 절대 줄이지 않는다 — Grid 의 Auto 칸이라 애초에 줄지 않기 때문에
/// 줄일 수 있는 건 가운데 Star 칸(검색)뿐이다.
/// </summary>
public enum ConsoleToolbarSearchMode
{
    /// <summary>전체 폭 검색창(placeholder 포함).</summary>
    Full,
    /// <summary>아이콘 전용 — 포커스를 받으면 그 순간만 넓어진다(ConsoleToolbar 코드비하인드).</summary>
    IconOnly,
}

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

    #region 툴바 오버플로 — 검색 밀도 (D-23)
    /// <summary>검색창이 전체 폭일 때 필요한 최소 폭 — <c>Generic.xaml</c> Search 의 기본 MinWidth 와 같다.</summary>
    public const double ToolbarSearchFullMinWidth = 120;
    /// <summary>검색창이 전체 폭일 때 허용하는 최대 폭 — <c>Generic.xaml</c> Search 의 기본 MaxWidth 와 같다.</summary>
    public const double ToolbarSearchFullMaxWidth = 320;
    /// <summary>검색창과 왼쪽 클러스터 사이 여백 — <c>Generic.xaml</c> Search 의 Margin 왼쪽 값과 같다(예산 계산용).</summary>
    public const double ToolbarSearchLeftMargin = 12;
    /// <summary>툴바 띠 안쪽 여백의 좌우 합 — <c>Generic.xaml</c> Border 의 <c>Padding="12,8"</c> 중 좌 12 + 우 12.</summary>
    public const double ToolbarHorizontalPadding = 24;

    /// <summary>
    /// 검색창에 실제로 내줄 수 있는 최소 폭(px, 0~<see cref="ToolbarSearchFullMinWidth"/>) — <b>항상 안전하다</b>:
    /// 이 값 + 왼쪽 마진을 검색 칸의 <c>MinWidth</c> 로 그대로 써도 Grid 의 총 요구 폭이 툴바 자신의 폭을
    /// 넘지 않는다. 왼쪽 클러스터(추가 · 삭제 · 갱신 · 필터)와 오른쪽 필수 클러스터(열 버튼 · Extra = 창
    /// 고유 기본 액션)는 둘 다 Grid 의 <c>Auto</c> 칸이라 폭이 모자라도 줄지 않고 제 몫을 그대로 가져간다 —
    /// 안쪽 여백 · 검색 왼쪽 마진 · 그 둘을 뺀 "예산"이 바로 검색이 가질 수 있는 전부다.
    /// <para>
    /// D-23 — 옛 구조는 가운데 칸에 고정 <c>MinWidth 132</c> 를 걸어 두어, 예산이 132 보다 적어도 132 를
    /// 강제로 채우려다 총 폭이 넘쳐 오른쪽(기본 액션 · 조치보고 버튼)이 화면 밖으로 밀렸다(서랍 1150px ·
    /// "직접" 에서 실측). <b>고정폭이면 뭐든(120 이든 32 든) 같은 병이 재발한다</b> — 실측으로 확인:
    /// 왼쪽 750.7px · 오른쪽 84px 인 채 900px 로 좁히면 예산이 13.3px 뿐인데, 검색을 32(아이콘 고정)로
    /// 접어도 32 &gt; 13.3 이라 여전히 18.7px 이 넘쳤다. 그래서 고정 두 단계(전체/아이콘) 대신 예산을
    /// 그대로 상한으로 쓴다 — 얼마가 남았든 그 이상은 절대 요구하지 않는다.
    /// </para>
    /// </summary>
    public static double ResolveToolbarSearchMinWidth(double toolbarWidth, double leftClusterWidth, double rightClusterWidth)
    {
        if (double.IsNaN(toolbarWidth) || toolbarWidth < 0) toolbarWidth = 0;
        if (double.IsNaN(leftClusterWidth) || leftClusterWidth < 0) leftClusterWidth = 0;
        if (double.IsNaN(rightClusterWidth) || rightClusterWidth < 0) rightClusterWidth = 0;

        var budget = toolbarWidth - ToolbarHorizontalPadding - ToolbarSearchLeftMargin - leftClusterWidth - rightClusterWidth;
        return Math.Max(0, Math.Min(budget, ToolbarSearchFullMinWidth));
    }

    /// <summary>검색창의 밀도 — <see cref="ResolveToolbarSearchMinWidth"/> 가 전체 폭을 다 주지 못하면 접힌 것이다.</summary>
    public static ConsoleToolbarSearchMode ResolveToolbarSearchMode(double toolbarWidth, double leftClusterWidth, double rightClusterWidth)
        => ResolveToolbarSearchMinWidth(toolbarWidth, leftClusterWidth, rightClusterWidth) >= ToolbarSearchFullMinWidth
            ? ConsoleToolbarSearchMode.Full
            : ConsoleToolbarSearchMode.IconOnly;

    /// <summary>검색이 아이콘으로 접혀도 이 폭은 남아야 누를 수 있다(<c>Console.Button.Icon</c> 한 변과 같다).</summary>
    public const double ToolbarSearchCompactMinWidth = 32;

    /// <summary>오른쪽 묶음을 접었을 때 그 자리에 서는 [⋯] 버튼 — 한 변 32 + 왼쪽 간격 8.</summary>
    public const double ToolbarOverflowButtonWidth = 40;

    /// <summary>
    /// 오른쪽 묶음(Extra = 창 고유 동작)을 [⋯] 뒤로 접어야 하는가 — <b>마지막 수단</b>이다(U-17).
    /// <para>
    /// D-23 은 "기본 액션은 줄이지 않는다" 고 정했다 — 그래서 먼저 검색이 아이콘으로, 그다음 0 까지 접힌다.
    /// 그래도 모자라면 Grid 의 Auto 칸은 줄지 않으므로 오른쪽 끝 버튼이 <b>테두리 밖으로 잘려 나가</b> 누를 수 없게 된다
    /// (장비 콘솔 1240 서랍 실창: "셋업 · 결선" 이 반쯤 잘렸다). 누를 수 없는 버튼보다 한 번 더 누르는 버튼이 낫다 —
    /// 이 판정이 참이면 Extra 전체를 [⋯] 팝업으로 옮긴다. 폭이 돌아오면 제자리로 돌아온다.
    /// </para>
    /// <para>
    /// 입력은 <b>상태와 무관한 폭</b>이어야 한다(접힘 여부에 따라 달라지는 오른쪽 묶음의 실제 폭을 넣으면 접었다 폈다 진동한다):
    /// <paramref name="rightFixedWidth"/> = 열 버튼(간격 포함), <paramref name="extraWidth"/> = Extra 가 제자리에 있을 때의 폭(간격 포함).
    /// </para>
    /// </summary>
    public static bool ShouldOverflowToolbarExtra(double toolbarWidth, double leftClusterWidth, double rightFixedWidth, double extraWidth, bool showSearch)
    {
        static double Clean(double v) => double.IsNaN(v) || double.IsInfinity(v) || v < 0 ? 0 : v;
        toolbarWidth = Clean(toolbarWidth);
        extraWidth = Clean(extraWidth);
        if (toolbarWidth <= 0 || extraWidth <= 0) return false;       // 아직 재지 못했거나 접을 것이 없다

        var budget = toolbarWidth - ToolbarHorizontalPadding - ToolbarSearchLeftMargin
                     - Clean(leftClusterWidth) - Clean(rightFixedWidth) - extraWidth;
        return budget < (showSearch ? ToolbarSearchCompactMinWidth : 0);
    }

    /// <summary>
    /// 툴바에서 [⋯] 뒤로 접을 묶음들 — <see cref="ShouldOverflowToolbarExtra"/> 를 넓힌 단계형 판정(U-18).
    /// <para>
    /// Extra 만 접어서는 모자랄 때가 있다(보고서 1120 서랍: 상태 칩 다섯 개가 왼쪽 묶음을 키워 [열 n/m] 이 반쯤 잘렸다 ·
    /// 이벤트 900 접힘: 기간 칩 때문에 [⋯] 자신이 테두리 밖으로 밀렸다 — 잘림 감사 실측). Grid 의 Auto 칸은 줄지 않으므로
    /// 줄일 수 없는 것을 옮기는 수밖에 없다. 순서는 창 고유 동작(Extra) → 필터 칩 → [열 n/m] 이다 — [추가] · [삭제] · [갱신] 은
    /// 모든 콘솔의 기본 동작이라 옮기지 않는다.
    /// </para>
    /// <para>입력은 모두 <b>제자리일 때의 폭</b>(간격 포함)이다 — 접힘 상태에 따라 달라지는 실제 폭을 넣으면 진동한다.</para>
    /// </summary>
    /// <param name="fixedLeftWidth">[추가] · [삭제] · [갱신] 의 폭(간격 포함) — 옮기지 않는 부분.</param>
    public static ConsoleToolbarOverflow ResolveToolbarOverflow(
        double toolbarWidth, double fixedLeftWidth, double filtersWidth, double columnsWidth, double extraWidth, bool showSearch)
    {
        static double Clean(double v) => double.IsNaN(v) || double.IsInfinity(v) || v < 0 ? 0 : v;
        toolbarWidth = Clean(toolbarWidth);
        fixedLeftWidth = Clean(fixedLeftWidth);
        filtersWidth = Clean(filtersWidth);
        columnsWidth = Clean(columnsWidth);
        extraWidth = Clean(extraWidth);
        if (toolbarWidth <= 0) return ConsoleToolbarOverflow.None;      // 아직 재지 못했다

        var available = toolbarWidth - ToolbarHorizontalPadding - ToolbarSearchLeftMargin - (showSearch ? ToolbarSearchCompactMinWidth : 0);

        double Need(ConsoleToolbarOverflow moved)
        {
            var need = fixedLeftWidth;
            var anyMoved = false;
            if (moved.HasFlag(ConsoleToolbarOverflow.Filters)) anyMoved |= filtersWidth > 0; else need += filtersWidth;
            if (moved.HasFlag(ConsoleToolbarOverflow.Columns)) anyMoved |= columnsWidth > 0; else need += columnsWidth;
            if (moved.HasFlag(ConsoleToolbarOverflow.Extra)) anyMoved |= extraWidth > 0; else need += extraWidth;
            return need + (anyMoved ? ToolbarOverflowButtonWidth : 0);
        }

        var result = ConsoleToolbarOverflow.None;
        if (Need(result) <= available) return result;

        if (extraWidth > 0)
        {
            result |= ConsoleToolbarOverflow.Extra;
            if (Need(result) <= available) return result;
        }
        if (filtersWidth > 0)
        {
            result |= ConsoleToolbarOverflow.Filters;
            if (Need(result) <= available) return result;
        }
        if (columnsWidth > 0) result |= ConsoleToolbarOverflow.Columns;
        return result;
    }
    #endregion
}

/// <summary>툴바에서 [⋯] 팝업으로 옮겨진 묶음(<see cref="ConsoleLayoutMath.ResolveToolbarOverflow"/>).</summary>
[Flags]
public enum ConsoleToolbarOverflow
{
    None = 0,
    /// <summary>창 고유 동작(Extra).</summary>
    Extra = 1,
    /// <summary>필터 칩(Filters).</summary>
    Filters = 2,
    /// <summary>[열 n/m].</summary>
    Columns = 4,
}
