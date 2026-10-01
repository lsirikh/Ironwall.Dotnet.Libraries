using System.Windows;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// "?" 말풍선의 순수 부분(help-callout H-1) — 배치 판정 · 글 표기 · 설명 목록 · 항목 합치기. 화면 없이 돈다.
/// </summary>
public class HelpModelTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1040);
    private static readonly Size Callout = new(436, 200);      // 카드 420 + 꼬리 자리 8×2

    #region - Placement -
    [Fact]
    public void should_open_below_and_point_the_tail_at_the_button_when_there_is_room_below()
    {
        // Arrange — 화면 위쪽의 "?"
        var target = new Rect(300, 100, 20, 20);

        // Act
        var r = HelpCalloutPlacement.Resolve(target, Callout, Screen);

        // Assert
        Assert.Equal(HelpCalloutSide.Below, r.Side);
        Assert.Equal(target.Bottom + HelpCalloutPlacement.Gap, r.Position.Y);
        Assert.Equal(target.Left + target.Width / 2, r.Position.X + r.TailOffset, 3);   // 꼬리 중심 = "?" 가운데
    }

    [Fact]
    public void should_flip_above_when_the_callout_would_run_past_the_bottom()
    {
        var target = new Rect(300, 960, 20, 20);

        var r = HelpCalloutPlacement.Resolve(target, Callout, Screen);

        Assert.Equal(HelpCalloutSide.Above, r.Side);
        Assert.Equal(target.Top - HelpCalloutPlacement.Gap - Callout.Height, r.Position.Y);
        Assert.True(r.Position.Y >= Screen.Top);
    }

    [Fact]
    public void should_open_to_the_left_when_neither_above_nor_below_fits()
    {
        // 화면 높이가 말풍선보다 조금 큰 가운데 — 위 · 아래 어느 쪽도 통째로 안 들어간다
        var shortScreen = new Rect(0, 0, 1920, 300);
        var target = new Rect(1500, 140, 20, 20);

        var r = HelpCalloutPlacement.Resolve(target, Callout, shortScreen);

        Assert.Equal(HelpCalloutSide.Left, r.Side);
        Assert.Equal(target.Left - HelpCalloutPlacement.Gap - Callout.Width, r.Position.X);
        Assert.True(r.Position.Y >= shortScreen.Top && r.Position.Y + Callout.Height <= shortScreen.Bottom);
        Assert.Equal(target.Top + target.Height / 2, r.Position.Y + r.TailOffset, 3);
    }

    [Fact]
    public void should_open_to_the_right_when_only_the_right_side_fits()
    {
        var shortScreen = new Rect(0, 0, 1920, 300);
        var target = new Rect(100, 140, 20, 20);

        var r = HelpCalloutPlacement.Resolve(target, Callout, shortScreen);

        Assert.Equal(HelpCalloutSide.Right, r.Side);
        Assert.Equal(target.Right + HelpCalloutPlacement.Gap, r.Position.X);
    }

    [Fact]
    public void should_slide_inside_the_screen_and_keep_the_tail_on_the_button_when_the_button_is_at_the_right_edge()
    {
        var target = new Rect(1860, 100, 20, 20);

        var r = HelpCalloutPlacement.Resolve(target, Callout, Screen);

        Assert.Equal(HelpCalloutSide.Below, r.Side);
        Assert.Equal(Screen.Right - Callout.Width, r.Position.X);                        // 화면 안으로 밀었다
        Assert.Equal(1870, r.Position.X + r.TailOffset, 3);                               // 꼬리는 여전히 "?" 가운데
    }

    [Fact]
    public void should_keep_the_tail_off_the_rounded_corner_when_the_button_sits_past_the_card_end()
    {
        // 말풍선보다 "?" 가 더 왼쪽(화면 왼쪽 끝) — 꼬리를 귀퉁이 안쪽으로 자른다
        var target = new Rect(0, 100, 4, 20);

        var r = HelpCalloutPlacement.Resolve(target, Callout, Screen);

        Assert.Equal(0, r.Position.X);
        Assert.Equal(HelpCalloutPlacement.TailMargin + HelpCalloutPlacement.TailEdgeInset, r.TailOffset, 3);
    }

    [Fact]
    public void should_not_pull_the_callout_onto_the_screen_when_the_button_is_off_screen()
    {
        // 화면 밖 시험 창(-10000) — 말풍선을 화면 안으로 끌어와 깜빡이지 않는다
        var target = new Rect(-10000, -10000, 20, 20);

        var r = HelpCalloutPlacement.Resolve(target, Callout, Screen);

        Assert.Equal(HelpCalloutSide.Below, r.Side);
        Assert.True(r.Position.X < -9000 && r.Position.Y < -9000);
    }

    [Fact]
    public void should_choose_the_roomier_vertical_side_and_stay_inside_when_nothing_fits()
    {
        var tiny = new Rect(0, 0, 300, 150);
        var target = new Rect(140, 100, 20, 20);

        var r = HelpCalloutPlacement.Resolve(target, Callout, tiny);

        Assert.Equal(HelpCalloutSide.Above, r.Side);                                      // 위가 100, 아래가 30
        Assert.Equal(0, r.Position.Y);                                                    // 경계 안으로 밀었다
    }
    #endregion

    #region - Markup -
    [Fact]
    public void should_split_key_chips_and_bold_runs_when_the_text_uses_the_markup()
    {
        var runs = HelpText.Parse("{Alt}+{↑} 순서 · **손댄 칸만** 적용");

        Assert.Equal(new[]
        {
            new HelpRun(HelpRunKind.Key, "Alt"),
            new HelpRun(HelpRunKind.Text, "+"),
            new HelpRun(HelpRunKind.Key, "↑"),
            new HelpRun(HelpRunKind.Text, " 순서 · "),
            new HelpRun(HelpRunKind.Bold, "손댄 칸만"),
            new HelpRun(HelpRunKind.Text, " 적용"),
        }, runs);
    }

    [Theory]
    [InlineData("[구성 저장] 뒤 확정", "[구성 저장] 뒤 확정")]      // 대괄호는 단추 이름 — 표기가 아니다
    [InlineData("짝 없는 { 괄호", "짝 없는 { 괄호")]
    [InlineData("빈 {} 칩", "빈 {} 칩")]
    [InlineData("짝 없는 ** 별", "짝 없는 ** 별")]
    [InlineData("{Shift}+끌기 = 망 선택", "Shift+끌기 = 망 선택")]
    [InlineData("", "")]
    public void should_keep_plain_text_readable_when_the_markup_is_stripped(string source, string plain)
        => Assert.Equal(plain, HelpText.Plain(source));
    #endregion

    #region - Entry · catalog -
    [Fact]
    public void should_append_keyboard_items_once_when_merging_into_an_entry()
    {
        var entry = HelpEntry.Create("Test.Model.Merge", "값 편집", "한 줄을 고르면 그 줄을 고칩니다.")
            .With("키보드로", "{Delete} 빼기");

        var merged = entry.Merge("키보드로", new[] { "Delete 빼기", "Alt+↑/↓ 순서", "Alt+↑/↓ 순서", " " });

        var keyboard = Assert.Single(merged.Sections, s => s.Heading == "키보드로");
        Assert.Equal(new[] { "{Delete} 빼기", "Alt+↑/↓ 순서" }, keyboard.Items);   // 표기를 걷은 글로 중복을 가린다
        Assert.Same(entry, entry.Merge("키보드로", Array.Empty<string>()));          // 더할 것이 없으면 그대로
    }

    [Fact]
    public void should_create_the_keyboard_section_at_the_end_when_the_entry_has_none()
    {
        var entry = HelpEntry.Create("Test.Model.NewSection", "목록", "항목");

        var merged = entry.Merge(HelpKeyboardFallbacks.Heading, new[] { "Alt+↑/↓" });

        Assert.Equal(2, merged.Sections.Count);
        Assert.Equal("키보드로", merged.Sections[1].Heading);
    }

    [Fact]
    public void should_read_title_headings_and_items_as_plain_text_when_automation_asks_for_the_name()
    {
        var entry = HelpEntry.Create("Test.Model.Plain", "펜스 보기", "{Shift}+끌기 = 망 선택").With("키보드로", "{Esc} = 취소");

        Assert.Equal("펜스 보기\nShift+끌기 = 망 선택\n키보드로\nEsc = 취소", entry.ToPlainText());
    }

    [Fact]
    public void should_find_registered_entries_and_return_null_when_the_key_is_missing()
    {
        // Arrange
        var raised = 0;
        EventHandler handler = (_, _) => raised++;
        HelpCatalog.Changed += handler;
        try
        {
            // Act
            HelpCatalog.Register(new[] { HelpEntry.Create("Test.Model.Catalog.A", "가") });
            HelpCatalog.Register(new[] { HelpEntry.Create("Test.Model.Catalog.A", "가 — 고침") });

            // Assert
            Assert.Equal("가 — 고침", HelpCatalog.Find("Test.Model.Catalog.A")!.Title);   // 나중 것이 이긴다
            Assert.Null(HelpCatalog.Find("Test.Model.Catalog.Nope"));
            Assert.False(HelpCatalog.TryGet(null, out _));
            Assert.Contains("Test.Model.Catalog.A", HelpCatalog.Keys);
            Assert.True(raised >= 2);
        }
        finally
        {
            HelpCatalog.Changed -= handler;
        }
    }

    [Fact]
    public void should_refuse_an_empty_key_when_creating_an_entry()
        => Assert.Throws<ArgumentException>(() => HelpEntry.Create(" ", "제목"));
    #endregion
}
