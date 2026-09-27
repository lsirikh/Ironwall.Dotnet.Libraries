using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// U-17 — 실창(GIS) 캡처에서 나온 잘림 두 갈래를 커널 판정으로 막는다.
/// ① 툴바 첫째 줄 — 검색이 최소폭(180) 아래로 눌리거나 오른쪽 끝 버튼이 잘림 → Extra · [열] 을 [⋯] 로 접기(<see cref="ConsoleLayoutMath.ResolveToolbarFit"/>).
/// ② 좁은 목록에서 식별 열이 잘리고 끝 열이 가로 스크롤 밖으로 밀림 → 덜 중요한 열부터 접기(<see cref="ConsoleColumns.CollapseBelowProperty"/>).
/// </summary>
public class ConsoleLayoutFitTests
{
    #region ① 툴바 첫째 줄 — 검색 폭 · [⋯] (ResolveToolbarFit, D-2026-09-27-71c353)
    // 장비 콘솔 실측(미리보기 toolbar-probe): [추가][삭제][갱신] 170 · [열 6/11] 81 ·
    // Extra = 제어기 레일(조립 · 프리셋 ▾ + 셋업 · 결선) 213 / 카메라 레일(조립 · 프리셋 ▾) 125.
    private const double Left = 170, Columns = 81, Extra = 213, CameraExtra = 125;

    [Fact]
    public void should_give_the_search_its_preferred_width_and_keep_extra_inline_when_docked_at_1280()
    {
        // Arrange — 1280 표면 · 도킹(상세 340): 목록 = 1280 − 184 − 340 = 756
        var list = ConsoleLayoutMath.Resolve(1280).ListWidth;

        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(list, Left, Extra, Columns, showSearch: true);

        // Assert — 756 − 24 − 170 − 213 − 81 − 12 = 256 → 편한 폭 240
        Assert.Equal(756, list);
        Assert.Equal(ConsoleToolbarOverflow.None, fit.Overflow);
        Assert.False(fit.IsSearchCompact);
        Assert.Equal(ConsoleLayoutMath.ToolbarSearchPreferredWidth, fit.SearchWidth);
    }

    [Fact]
    public void should_move_extra_behind_more_before_squeezing_the_search_below_its_minimum()
    {
        // Arrange — 1150 서랍이 열림: 목록 966 − 서랍 360 = 606. 제어기 레일 Extra(213) 를 두면 검색에 106 뿐이다.
        var layout = ConsoleLayoutMath.Resolve(1150);
        var width = ConsoleLayoutMath.EffectiveListWidth(layout, isDetailOpen: true);

        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(width, Left, Extra, Columns, showSearch: true);

        // Assert — 옛 한 줄 구조는 검색을 106 으로 눌렀다. 이제 Extra 가 [⋯] 로 가고 검색은 전체 폭을 받는다.
        Assert.Equal(606, width);
        Assert.Equal(ConsoleToolbarOverflow.Extra, fit.Overflow);
        Assert.False(fit.IsSearchCompact);
        Assert.True(fit.SearchWidth >= ConsoleLayoutMath.ToolbarSearchFullMinWidth);
    }

    [Fact]
    public void should_keep_a_narrow_extra_inline_when_the_search_still_gets_its_minimum()
    {
        // Arrange — 같은 606 이라도 카메라 레일(Extra 125)이면 검색에 194 가 남는다(≥ 180) — 아무것도 옮기지 않는다
        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(606, Left, CameraExtra, Columns, showSearch: true);

        // Assert
        Assert.Equal(ConsoleToolbarOverflow.None, fit.Overflow);
        Assert.Equal(606 - 24 - Left - CameraExtra - Columns - 12, fit.SearchWidth, 3);
    }

    [Fact]
    public void should_fold_extra_then_columns_and_still_show_a_full_search_at_900_with_the_drawer_open()
    {
        // Arrange — 900 접힘 + 서랍 열림: 목록 844 − 서랍 360 = 484
        var layout = ConsoleLayoutMath.Resolve(900);
        var width = ConsoleLayoutMath.EffectiveListWidth(layout, isDetailOpen: true);

        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(width, Left, Extra, Columns, showSearch: true);

        // Assert — Extra 만 옮기면 157(< 180), [열] 까지 옮기면 238 — 검색은 전체 폭이다
        Assert.Equal(484, width);
        Assert.Equal(ConsoleToolbarOverflow.Extra | ConsoleToolbarOverflow.Columns, fit.Overflow);
        Assert.False(fit.IsSearchCompact);
        Assert.True(fit.SearchWidth >= ConsoleLayoutMath.ToolbarSearchFullMinWidth);
    }

    [Theory]
    [InlineData(1280, false)]   // 도킹 — 목록 756
    [InlineData(1150, false)]   // 서랍 닫힘 — 목록 966
    [InlineData(1150, true)]    // 서랍 열림 — 606
    [InlineData(900, false)]    // 접힘 · 서랍 닫힘 — 844
    [InlineData(900, true)]     // 접힘 · 서랍 열림 — 484
    public void should_never_show_the_search_box_narrower_than_its_minimum_at_the_standard_widths(double surface, bool drawerOpen)
    {
        // Arrange — 장비 · 보고서([+ 새 보고서] 가 넓다) · 이벤트(Extra 세 단추) 실측에 가까운 왼쪽 · Extra 폭
        var width = ConsoleLayoutMath.EffectiveListWidth(ConsoleLayoutMath.Resolve(surface), drawerOpen);
        var consoles = new (double Left, double Extra, double Columns)[]
        {
            (Left, Extra, Columns),     // 장비 · 제어기
            (201, 0, 81),               // 보고서 · 생성 이력(필터 칩은 둘째 줄 — 입력에 없다)
            (96, 330, 0),               // 이벤트 · 탐지([중단] [이벤트 맵핑] [트레이에 담기])
        };

        foreach (var (left, extra, columns) in consoles)
        {
            // Act
            var fit = ConsoleLayoutMath.ResolveToolbarFit(width, left, extra, columns, showSearch: true);

            // Assert — 이 폭들에서는 검색이 아이콘으로 접히지 않고, 전체 폭이면 늘 180 이상이다
            Assert.False(fit.IsSearchCompact, $"{surface}/{drawerOpen}: 목록 {width} · 왼쪽 {left} · Extra {extra}");
            Assert.InRange(fit.SearchWidth, ConsoleLayoutMath.ToolbarSearchFullMinWidth, ConsoleLayoutMath.ToolbarSearchPreferredWidth);
        }
    }

    [Theory]
    [InlineData(1400)]
    [InlineData(756)]
    [InlineData(606)]
    [InlineData(484)]
    [InlineData(300)]
    [InlineData(200)]
    public void should_never_ask_the_action_row_for_more_than_the_toolbar_width(double width)
    {
        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(width, Left, Extra, Columns, showSearch: true);

        // Assert — 제자리에 남은 것 + [⋯] + 검색(또는 아이콘) + 여백의 합이 툴바 폭을 넘지 않는다(= 오른쪽 끝이 잘리지 않는다).
        // [추가][삭제][갱신] 만으로 넘치는 극단(200)은 옮길 것이 없어 예외다.
        var moved = fit.Overflow != ConsoleToolbarOverflow.None;
        var need = 24 + Left
                   + (fit.Overflow.HasFlag(ConsoleToolbarOverflow.Extra) ? 0 : Extra)
                   + (fit.Overflow.HasFlag(ConsoleToolbarOverflow.Columns) ? 0 : Columns)
                   + (moved ? ConsoleLayoutMath.ToolbarOverflowButtonWidth : 0)
                   + 12 + (fit.IsSearchCompact ? ConsoleLayoutMath.ToolbarSearchCompactMinWidth : fit.SearchWidth);
        if (24 + Left + ConsoleLayoutMath.ToolbarOverflowButtonWidth + 12 + ConsoleLayoutMath.ToolbarSearchCompactMinWidth > width) return;
        Assert.True(need <= width + 0.01, $"폭 {width} 에 {need} 를 요구했다({fit})");
    }

    [Fact]
    public void should_fold_the_search_to_an_icon_only_when_even_folding_everything_leaves_less_than_its_minimum()
    {
        // Arrange — 300: 모두 옮겨도 300 − 24 − 170 − 40 − 12 = 54 < 180
        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(300, Left, Extra, Columns, showSearch: true);

        // Assert — 180 과 0 사이의 어중간한 폭으로 누르지 않는다: 아이콘이다
        Assert.True(fit.IsSearchCompact);
        Assert.Equal(0, fit.SearchWidth);
    }

    [Fact]
    public void should_bring_extra_back_inline_when_the_search_is_folded_anyway_and_extra_fits_beside_the_icon()
    {
        // Arrange — Extra 가 작고(60) [열] 이 없는 좁은 툴바: 검색 180 은 어떤 단계에서도 안 들어가지만,
        // 아이콘(32)이면 Extra 를 제자리에 둘 수 있다 — 접을 것만 접는다.
        const double width = 24 + 170 + 60 + 12 + 32 + 10;

        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(width, Left, 60, 0, showSearch: true);

        // Assert
        Assert.True(fit.IsSearchCompact);
        Assert.Equal(ConsoleToolbarOverflow.None, fit.Overflow);
    }

    [Fact]
    public void should_not_reserve_room_for_the_search_when_search_is_hidden()
    {
        // Arrange — 검색이 꺼진 레일(장비 부품으로 찾기 · 서버 시스템 이벤트): 동작만 들어가면 아무것도 옮기지 않는다
        const double width = 24 + 170 + 213 + 81 + 5;

        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(width, Left, Extra, Columns, showSearch: false);

        // Assert
        Assert.Equal(ConsoleToolbarOverflow.None, fit.Overflow);
        Assert.False(fit.IsSearchCompact);
        Assert.Equal(0, fit.SearchWidth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(-5)]
    public void should_not_fold_anything_before_the_toolbar_is_measured(double toolbarWidth)
    {
        // Act
        var fit = ConsoleLayoutMath.ResolveToolbarFit(toolbarWidth, Left, Extra, Columns, showSearch: true);

        // Assert
        Assert.Equal(ConsoleToolbarOverflow.None, fit.Overflow);
        Assert.False(fit.IsSearchCompact);
    }

    [Fact]
    public void should_decide_from_state_independent_widths_so_the_toolbar_does_not_oscillate()
    {
        // Arrange — 접힌 뒤 실제 폭은 [⋯](40) 로 줄지만 판정은 제자리 폭으로 한다 — 같은 입력이면 같은 답
        var first = ConsoleLayoutMath.ResolveToolbarFit(606, Left, Extra, Columns, showSearch: true);

        // Act
        var second = ConsoleLayoutMath.ResolveToolbarFit(606, Left, Extra, Columns, showSearch: true);

        // Assert
        Assert.Equal(first, second);
    }
    #endregion

    #region ② 열 접기
    private static DataGridTextColumn Column(string key, double collapseBelow = 0)
    {
        var column = new DataGridTextColumn { Header = key };
        ConsoleColumns.SetKey(column, key);
        ConsoleColumns.SetCollapseBelow(column, collapseBelow);
        return column;
    }

    [Theory]
    [InlineData(640, 576, true)]     // 계정 콘솔 1120 + 서랍: 목록 576 → 부서·직급 접힘
    [InlineData(640, 936, false)]    // 같은 창 서랍 닫힘: 목록 936 → 보인다
    [InlineData(640, 640, false)]    // 문턱과 같으면 접지 않는다(미만일 때만)
    [InlineData(0, 100, false)]      // 문턱 없음 = 접지 않는 열
    [InlineData(640, 0, false)]      // 아직 재지 못한 폭
    [InlineData(640, double.NaN, false)]
    public void should_collapse_only_below_the_threshold(double collapseBelow, double listWidth, bool expected)
    {
        // Act
        var collapse = ConsoleColumns.ShouldCollapse(collapseBelow, listWidth);

        // Assert
        Assert.Equal(expected, collapse);
    }

    [Fact]
    public void should_list_only_the_keys_whose_threshold_is_above_the_list_width()
    {
        // Arrange — 서버 모니터: 마지막 변화 680 · 부대 780, 나머지는 문턱 없음
        var columns = new List<DataGridColumn>
        {
            new DataGridTextColumn(),                     // 키 없는 열(표지 · 손잡이)은 셈에 없다
            Column("name"), Column("type"), Column("address"), Column("status"),
            Column("last_change", 680), Column("unit", 780),
        };

        // Act
        var narrow = ConsoleColumns.CollapsedAt(columns, 576);
        var middle = ConsoleColumns.CollapsedAt(columns, 700);
        var wide = ConsoleColumns.CollapsedAt(columns, 936);

        // Assert
        Assert.Equal(new[] { "last_change", "unit" }, narrow);
        Assert.Equal(new[] { "unit" }, middle);
        Assert.Empty(wide);
    }

    [Fact]
    public void should_hide_collapsed_columns_without_touching_user_preferences_when_merged_into_apply()
    {
        // Arrange — 사용자가 숨긴 열(employee)과 폭으로 접힌 열(org)을 합쳐 Apply 에 넘긴다(계정 콘솔 배선과 같다)
        var org = Column("org", 640);
        var columns = new List<DataGridColumn> { Column("identity"), Column("name"), org, Column("employee") };
        var userHidden = new List<string> { "employee" };
        var hidden = new List<string>(userHidden);
        hidden.AddRange(ConsoleColumns.CollapsedAt(columns, 576));

        // Act
        var text = ConsoleColumns.Apply(columns, showAll: false, hidden);

        // Assert
        Assert.Equal(System.Windows.Visibility.Collapsed, org.Visibility);
        Assert.Equal("열 2/4", text);
        Assert.Equal(new[] { "employee" }, userHidden);   // 설정 쪽 목록은 그대로 — 넓어지면 돌아온다
    }

    [Fact]
    public void should_toggle_only_threshold_columns_when_auto_collapse_applies_a_width()
    {
        // Arrange — 이벤트 탐지 목록(U-18): 신호 960 · 구역 700, 장비는 문턱 없음(끝까지 남는다)
        var signal = Column("signal", 960);
        var zone = Column("zone", 700);
        var device = Column("device");
        var columns = new List<DataGridColumn> { signal, zone, device };

        // Act — 도킹 1340(목록 816) → 서랍 1150(목록 606) → 다시 넓게(966)
        ConsoleColumns.ApplyCollapse(columns, 816);
        var at816 = (signal.Visibility, zone.Visibility);
        ConsoleColumns.ApplyCollapse(columns, 606);
        var at606 = (signal.Visibility, zone.Visibility);
        ConsoleColumns.ApplyCollapse(columns, 966);

        // Assert
        Assert.Equal((System.Windows.Visibility.Collapsed, System.Windows.Visibility.Visible), at816);
        Assert.Equal((System.Windows.Visibility.Collapsed, System.Windows.Visibility.Collapsed), at606);
        Assert.Equal(System.Windows.Visibility.Visible, signal.Visibility);
        Assert.Equal(System.Windows.Visibility.Visible, zone.Visibility);
        Assert.Equal(System.Windows.Visibility.Visible, device.Visibility);
    }
    #endregion

}
