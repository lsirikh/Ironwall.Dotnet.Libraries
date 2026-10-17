using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// console-kernel FR-07 — "열" 메뉴. 기본 6열 + 전체 열 + 열별 끄기/켜기 + 계약 미지원 열은 감춤.
/// </summary>
public class ConsoleColumnsTests
{
    private static DataGridTextColumn Column(string? key, bool isDefault = true, bool isSupported = true)
    {
        var column = new DataGridTextColumn { Header = key ?? "(handle)" };
        if (key != null) ConsoleColumns.SetKey(column, key);
        ConsoleColumns.SetIsDefault(column, isDefault);
        ConsoleColumns.SetIsSupported(column, isSupported);
        return column;
    }

    private static List<DataGridColumn> Camera() => new()
    {
        Column(null),                          // 핸들 열 — 키 없음
        Column("status"), Column("number"), Column("name"), Column("kind"), Column("address"), Column("enabled"),
        Column("protocol", isDefault: false), Column("unit", isDefault: false), Column("location", isDefault: false),
    };

    [Theory]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, false, false, false, false)]   // 비기본 열은 전체 열을 켜야 보인다
    [InlineData(true, false, true, false, true)]
    [InlineData(true, true, true, true, false)]      // 사용자가 끈 열
    [InlineData(false, true, true, false, false)]    // 계약이 지원하지 않으면 무엇으로도 안 보인다
    public void should_decide_column_visibility(bool supported, bool isDefault, bool showAll, bool hidden, bool expected)
    {
        Assert.Equal(expected, ConsoleColumns.ShouldShow(supported, isDefault, showAll, hidden));
    }

    [Fact]
    public void should_show_default_six_of_nine_when_prefs_are_fresh()
    {
        var columns = Camera();

        var text = ConsoleColumns.Apply(columns, showAll: false, hiddenKeys: null);

        Assert.Equal("열 6/9", text);
        Assert.Equal(Visibility.Visible, columns[0].Visibility);                 // 핸들 열은 늘 보인다
        Assert.Equal(Visibility.Collapsed, columns.Single(c => ConsoleColumns.GetKey(c) == "unit").Visibility);
    }

    [Fact]
    public void should_show_every_column_when_show_all_is_on()
    {
        Assert.Equal("열 9/9", ConsoleColumns.Apply(Camera(), showAll: true, hiddenKeys: new List<string>()));
    }

    [Fact]
    public void should_hide_unsupported_columns_and_leave_them_out_of_the_count()
    {
        var columns = Camera();
        columns.Add(Column("door_position", isDefault: true, isSupported: false));    // 6.3 계약 — v7+ 전용 열

        var text = ConsoleColumns.Apply(columns, showAll: true, hiddenKeys: null);

        Assert.Equal("열 9/9", text);
        Assert.Equal(Visibility.Collapsed, columns[^1].Visibility);
        Assert.DoesNotContain(ConsoleColumns.Describe(columns), d => d.Key == "door_position");
    }

    [Fact]
    public void should_hide_a_default_column_when_user_toggles_it_off()
    {
        var columns = Camera();
        var prefs = new ConsolePrefEntry();

        ConsoleColumns.Toggle(columns, prefs, "enabled");

        Assert.Equal("열 5/9", ConsoleColumns.Apply(columns, prefs.ShowAllColumns, prefs.HiddenColumns));
        ConsoleColumns.Toggle(columns, prefs, "enabled");
        Assert.Equal("열 6/9", ConsoleColumns.Apply(columns, prefs.ShowAllColumns, prefs.HiddenColumns));
    }

    [Fact]
    public void should_turn_on_only_the_chosen_optional_column()
    {
        var columns = Camera();
        var prefs = new ConsolePrefEntry();

        ConsoleColumns.Toggle(columns, prefs, "unit");

        Assert.Equal("열 7/9", ConsoleColumns.Apply(columns, prefs.ShowAllColumns, prefs.HiddenColumns));
        Assert.Equal(Visibility.Visible, columns.Single(c => ConsoleColumns.GetKey(c) == "unit").Visibility);
        Assert.Equal(Visibility.Collapsed, columns.Single(c => ConsoleColumns.GetKey(c) == "protocol").Visibility);
    }

    [Fact]
    public void should_describe_menu_items_in_column_order()
    {
        var columns = Camera();
        ConsoleColumns.Apply(columns, showAll: false, hiddenKeys: null);

        var menu = ConsoleColumns.Describe(columns);

        Assert.Equal(9, menu.Count);
        Assert.Equal(("status", true, true), (menu[0].Key, menu[0].IsVisible, menu[0].IsDefault));
        Assert.Equal(("location", false, false), (menu[^1].Key, menu[^1].IsVisible, menu[^1].IsDefault));
    }

    [Fact]
    public void should_ignore_unknown_key_when_toggling()
    {
        var prefs = new ConsolePrefEntry();

        ConsoleColumns.Toggle(Camera(), prefs, "nope");

        Assert.Empty(prefs.HiddenColumns);
        Assert.False(prefs.ShowAllColumns);
    }
}
