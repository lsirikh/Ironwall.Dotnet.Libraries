using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// U-17 — 실창(GIS) 캡처에서 나온 잘림 두 갈래를 커널 판정으로 막는다.
/// ① 툴바 오른쪽 묶음(Extra)이 테두리 밖으로 잘림 → [⋯] 로 접기(<see cref="ConsoleLayoutMath.ShouldOverflowToolbarExtra"/>).
/// ② 좁은 목록에서 식별 열이 잘리고 끝 열이 가로 스크롤 밖으로 밀림 → 덜 중요한 열부터 접기(<see cref="ConsoleColumns.CollapseBelowProperty"/>).
/// </summary>
public class ConsoleLayoutFitTests
{
    #region ① 툴바 [⋯]
    // 장비 콘솔 실측(미리보기 toolbar-probe): 왼쪽 묶음 204 · 열 버튼 81 · Extra(조립기 · 프리셋으로 등록 · 프리셋… · 셋업 · 결선) 368.
    private const double Left = 204, Columns = 81, Extra = 368;

    [Fact]
    public void should_keep_extra_inline_when_docked_device_console_has_room_for_the_search_icon()
    {
        // Arrange — 1280 표면 · 도킹(상세 340): 목록 = 1280 − 184 − 340 = 756. 예산 = 756 − 24 − 12 − 204 − 81 − 368 = 67 ≥ 32

        // Act
        var overflow = ConsoleLayoutMath.ShouldOverflowToolbarExtra(756, Left, Columns, Extra, showSearch: true);

        // Assert
        Assert.False(overflow);
    }

    [Fact]
    public void should_overflow_extra_when_the_drawer_leaves_less_than_the_search_icon()
    {
        // Arrange — 옛 호스트 카드(1240 · 서랍 360): 목록 = 1240 − 184 − 360 = 696. 예산 7 — "셋업 · 결선" 이 잘리던 폭

        // Act
        var overflow = ConsoleLayoutMath.ShouldOverflowToolbarExtra(696, Left, Columns, Extra, showSearch: true);

        // Assert
        Assert.True(overflow);
    }

    [Fact]
    public void should_not_reserve_room_for_the_search_icon_when_search_is_hidden()
    {
        // Arrange — 예산 20: 검색을 보이면 아이콘(32)이 안 들어가 접지만, 검색이 꺼졌으면 20 도 남는 폭이다
        var width = 24 + 12 + Left + Columns + Extra + 20;

        // Act / Assert
        Assert.True(ConsoleLayoutMath.ShouldOverflowToolbarExtra(width, Left, Columns, Extra, showSearch: true));
        Assert.False(ConsoleLayoutMath.ShouldOverflowToolbarExtra(width, Left, Columns, Extra, showSearch: false));
    }

    [Theory]
    [InlineData(0, 368)]            // 아직 재지 못한 툴바
    [InlineData(double.NaN, 368)]
    [InlineData(500, 0)]            // 접을 것이 없다(Extra 가 비었거나 전부 숨었다)
    public void should_not_overflow_when_nothing_is_measured_or_there_is_nothing_to_fold(double toolbarWidth, double extraWidth)
    {
        // Act
        var overflow = ConsoleLayoutMath.ShouldOverflowToolbarExtra(toolbarWidth, Left, Columns, extraWidth, showSearch: true);

        // Assert
        Assert.False(overflow);
    }

    [Fact]
    public void should_decide_from_state_independent_widths_so_the_toolbar_does_not_oscillate()
    {
        // Arrange — 접힌 뒤 오른쪽 묶음의 실제 폭은 [⋯](40) 로 줄지만, 판정은 Extra 의 제자리 폭으로 한다 —
        // 같은 입력이면 같은 답이어야 한다(접힘 → 폭 늘어남 → 펼침 → 다시 접힘 진동 방지).
        var first = ConsoleLayoutMath.ShouldOverflowToolbarExtra(696, Left, Columns, Extra, showSearch: true);

        // Act
        var second = ConsoleLayoutMath.ShouldOverflowToolbarExtra(696, Left, Columns, Extra, showSearch: true);

        // Assert
        Assert.Equal(first, second);
        Assert.True(696 - 24 - 12 - Left - Columns - ConsoleLayoutMath.ToolbarOverflowButtonWidth >= ConsoleLayoutMath.ToolbarSearchFullMinWidth,
            "접은 뒤에는 검색이 다시 전체 폭을 받을 만큼 남아야 한다");
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
    #endregion
}
