using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 사용자 목록 13열 → <b>기본 6열</b> + "열" 메뉴(설계 정본 L1153-L1156 · 결정 L-D2).
/// 커널의 <see cref="ConsoleColumns"/> 위에서 실제로 그렇게 되는지를 본다.
/// </summary>
public class AccountColumnsTests
{
    [Fact]
    public void should_keep_exactly_six_default_columns_in_the_design_order()
    {
        Assert.Equal(new[] { "lock", "identity", "name", "role", "org", "used" },
                     AccountColumns.Defaults.Select(c => c.Key));
        Assert.All(AccountColumns.Defaults, column => Assert.True(column.IsDefault));
    }

    [Fact]
    public void should_move_employee_phone_email_and_number_out_of_the_default_list()
    {
        var extras = AccountColumns.Extras.Select(c => c.Key).ToList();

        Assert.Contains("employee", extras);
        Assert.Contains("phone", extras);
        Assert.Contains("email", extras);
        Assert.Contains("no", extras);
        Assert.All(AccountColumns.Extras, column => Assert.False(column.IsDefault));
    }

    [Fact]
    public void should_report_six_of_eleven_when_only_the_defaults_are_shown()
    {
        var columns = Build();

        var text = ConsoleColumns.Apply(columns, showAll: false, hiddenKeys: null);

        Assert.Equal("열 6/11", text);
        Assert.Equal(6, columns.Count(c => c.Visibility == System.Windows.Visibility.Visible));
    }

    [Fact]
    public void should_show_every_column_when_the_user_asks_for_all_of_them()
    {
        var columns = Build();

        var text = ConsoleColumns.Apply(columns, showAll: true, hiddenKeys: null);

        Assert.Equal("열 11/11", text);
    }

    [Fact]
    public void should_turn_a_single_extra_column_on_without_showing_the_rest()
    {
        var columns = Build();
        var prefs = new ConsolePrefEntry();

        ConsoleColumns.Toggle(columns, prefs, "phone");
        ConsoleColumns.Apply(columns, prefs.ShowAllColumns, prefs.HiddenColumns);

        Assert.Equal(System.Windows.Visibility.Visible, Column(columns, "phone").Visibility);
        Assert.Equal(System.Windows.Visibility.Collapsed, Column(columns, "email").Visibility);
        Assert.Equal(System.Windows.Visibility.Visible, Column(columns, "name").Visibility);
    }

    [Fact]
    public void should_hide_a_default_column_the_user_turned_off()
    {
        var columns = Build();
        var prefs = new ConsolePrefEntry();

        ConsoleColumns.Toggle(columns, prefs, "org");
        var text = ConsoleColumns.Apply(columns, prefs.ShowAllColumns, prefs.HiddenColumns);

        Assert.Equal("열 5/11", text);
        Assert.Equal(System.Windows.Visibility.Collapsed, Column(columns, "org").Visibility);
    }

    [Fact]
    public void should_accept_the_declared_keys_when_they_match_the_catalog()
        => Assert.True(AccountColumns.Matches(AccountColumns.All.Select(c => c.Key)));

    [Fact]
    public void should_reject_the_declared_keys_when_one_is_missing()
        => Assert.False(AccountColumns.Matches(AccountColumns.All.Select(c => c.Key).Skip(1)));

    private static DataGridColumn Column(IReadOnlyList<DataGridColumn> columns, string key)
        => columns.Single(c => ConsoleColumns.GetKey(c) == key);

    /// <summary>명세 표 그대로 열을 만든다 — 화면이 선언한 열과 같은 모양이다.</summary>
    private static List<DataGridColumn> Build()
    {
        var columns = new List<DataGridColumn>();
        foreach (var spec in AccountColumns.All)
        {
            var column = new DataGridTextColumn { Header = spec.Header };
            ConsoleColumns.SetKey(column, spec.Key);
            ConsoleColumns.SetIsDefault(column, spec.IsDefault);
            columns.Add(column);
        }
        return columns;
    }
}
