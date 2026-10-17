using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 한글을 띄어쓰기에서만 줄바꿈 — 실창에서 "바 / 꿀 수 없습니다" · "다른 카 / 테고리로" 처럼 낱말 가운데서 끊기던 결함(2026-09-27).
/// </summary>
[Collection(WpfFocusCollection.Name)]
public class KoreanWordWrapTests
{
    private const string Hint = "카테고리는 바꿀 수 없습니다. 다른 카테고리로 옮기려면 삭제한 뒤 다시 등록하세요.";

    [Fact]
    public void should_join_hangul_syllables_but_keep_spaces_breakable_when_text_is_korean()
    {
        var joined = KoreanWordWrap.Join("바꿀 수");

        Assert.Equal("바⁠꿀 수", joined);
    }

    [Fact]
    public void should_glue_punctuation_to_the_korean_word_when_it_follows_directly()
    {
        Assert.Equal("없⁠다⁠.", KoreanWordWrap.Join("없다."));
    }

    [Fact]
    public void should_leave_text_untouched_when_it_has_no_hangul()
    {
        Assert.Equal("192.168.1.100:8100 PIR", KoreanWordWrap.Join("192.168.1.100:8100 PIR"));
    }

    [Fact]
    public void should_give_the_same_result_when_applied_twice()
    {
        var once = KoreanWordWrap.Join(Hint);

        Assert.Equal(once, KoreanWordWrap.Join(once));
        Assert.Equal(Hint, KoreanWordWrap.Strip(once));
    }

    [Fact]
    public void should_break_lines_only_at_spaces_when_a_wrapping_textblock_is_rendered()
    {
        var lines = OnSta(() => RenderLines(KoreanWordWrap.Join(Hint), 170));

        // 줄 끝마다 낱말이 온전해야 한다 — 끝 글자 뒤 원문 글자가 띄어쓰기이거나 문장 끝.
        var plain = Hint;
        var consumed = 0;
        foreach (var line in lines)
        {
            consumed += line.Length;
            var atEnd = consumed >= plain.Length;
            Assert.True(atEnd || char.IsWhiteSpace(plain[consumed - 1]) || char.IsWhiteSpace(plain[consumed]),
                $"낱말 가운데서 끊겼다: '{line}' | 전체 [{string.Join(" | ", lines)}]");
        }
    }

    [Fact]
    public void should_break_inside_words_when_the_text_is_not_joined()
    {
        // 전제(결함의 원인): 결합자 없이 그리면 WPF 는 음절 사이에서 끊는다.
        // 인라인(Run)으로 넣는다 — 전역 처리기(KoreanWordWrap)는 인라인 글을 건드리지 않으므로 WPF 날것의 줄바꿈을 잰다.
        var lines = OnSta(() => RenderLines(Hint, 170, asInline: true));
        var plain = Hint;
        var consumed = 0;
        var midWord = false;
        foreach (var line in lines)
        {
            consumed += line.Length;
            if (consumed < plain.Length && !char.IsWhiteSpace(plain[consumed - 1]) && !char.IsWhiteSpace(plain[consumed])) midWord = true;
        }
        Assert.True(midWord, $"WPF 가 이제 낱말 단위로 끊는다면 KoreanWordWrap 이 필요 없다 — [{string.Join(" | ", lines)}]");
    }

    [Fact]
    public void should_join_the_text_of_a_wrapping_textblock_when_it_is_loaded_in_a_window()
    {
        // 실창 확인(2026-09-27 3회차): 설치했는데도 실제 창에서 음절 가운데 줄바꿈이 남았다 — 설치 경로(Loaded 클래스 처리기)를 잠근다.
        var (bound, direct) = OnSta(() =>
        {
            KoreanWordWrap.Install();
            var source = new System.Windows.Controls.TextBlock();   // 바인딩 원본 흉내 없이 두 경우: 직접 값 · 바인딩
            var holder = new Holder { Text = Hint };
            var tbBound = new TextBlock { TextWrapping = TextWrapping.Wrap, DataContext = holder };
            tbBound.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Holder.Text)));
            var tbDirect = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = Hint };
            var panel = new StackPanel();
            panel.Children.Add(tbBound);
            panel.Children.Add(tbDirect);
            var window = new Window
            {
                Left = -20000, Top = -20000, Width = 300, Height = 200,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
                Content = panel,
            };
            window.Show();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var r = (tbBound.Text, tbDirect.Text);
            window.Close();
            return r;
        });

        Assert.Contains(KoreanWordWrap.WordJoiner, direct);
        Assert.Contains(KoreanWordWrap.WordJoiner, bound);
    }

    private sealed class Holder { public string Text { get; set; } = ""; }

    private static List<string> RenderLines(string text, double width, bool asInline = false)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Width = width };
        if (asInline) tb.Inlines.Add(new Run(text));
        else tb.Text = text;
        tb.Measure(new Size(width, 2000));
        tb.Arrange(new Rect(0, 0, width, tb.DesiredSize.Height));
        var lines = new List<string>();
        var pos = tb.ContentStart.GetLineStartPosition(0);
        while (pos is not null && lines.Count < 50)
        {
            var next = pos.GetLineStartPosition(1);
            lines.Add(KoreanWordWrap.Strip(new TextRange(pos, next ?? tb.ContentEnd).Text));
            pos = next;
        }
        return lines;
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }
}
