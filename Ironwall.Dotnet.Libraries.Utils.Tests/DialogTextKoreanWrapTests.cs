using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 다이얼로그 글(<see cref="ConsoleDialogText"/> — 안내 문장 · 갈래 줄 · 버튼 줄 안내)은 한글을 <b>어절에서만</b> 끊는다.
/// 확인 창 렌더에서 "…변경이 사 / 라집니다" 처럼 낱말 가운데서 끊겼다(2026-09-30). 전역 설치(<see cref="KoreanWordWrap.Install"/>)에
/// 기대지 않고 틀 글 스스로 결합자를 넣는다 — 창 · 호스트 팝업층 어디서 떠도 같다.
/// </summary>
/// <remarks>전역 설치(<see cref="KoreanWordWrap.Install"/>)를 부르지 않는다. 같은 프로세스에서 다른 반이 설치했으면 그 길로도 통과할 수 있어,
/// 적색 확인은 이 반만 따로 돌려 했다(2026-09-30: 5건 모두 실패 → 수정 뒤 통과).</remarks>
[Collection(WpfFocusCollection.Name)]
public class DialogTextKoreanWrapTests
{
    private const string Sentence = "저장하지 않은 편제 변경 3건이 있습니다. 창을 닫으면 변경이 사라집니다. 닫을까요?";

    [Fact]
    public void should_break_only_between_words_when_a_wrapping_dialog_text_is_laid_out_at_any_dialog_width()
    {
        // 406 = S 확인 창 몸통(창 448 − 테두리 − 좌우 20) · 360 = 팝업층 카드 S 400 − 좌우 20 · 그 사이와 아래를 7 간격으로 훑는다.
        var widths = new[] { 406d, 360d }.Concat(Enumerable.Range(0, 30).Select(i => 200d + (i * 7))).ToArray();
        var broken = OnSta(() => widths
            .Select(w => (Width: w, Lines: Lines(new ConsoleDialogText { Text = Sentence, TextWrapping = TextWrapping.Wrap, FontSize = 13 }, w)))
            .SelectMany(r => r.Lines.Zip(r.Lines.Skip(1), (a, b) => (r.Width, a, b))
                .Where(p => !p.a.EndsWith(" ", StringComparison.Ordinal) && !p.b.StartsWith(" ", StringComparison.Ordinal))
                .Select(p => $"{p.Width}: '{p.a.TrimEnd()}' / '{p.b}'"))
            .ToList());

        Assert.True(broken.Count == 0, "낱말 가운데서 끊겼다: " + string.Join(" | ", broken));
    }

    [Fact]
    public void should_keep_the_raw_text_for_automation_when_the_display_text_is_joined()
    {
        var result = OnSta(() =>
        {
            var text = new ConsoleDialogText { Text = Sentence, TextWrapping = TextWrapping.Wrap };
            Lines(text, 406);
            return (Name: UIElementAutomationPeer.CreatePeerForElement(text).GetName(), text.Text);
        });

        Assert.Equal(Sentence, result.Name);    // UIA 이름에 결합자가 섞이지 않는다
        Assert.Equal(Sentence, result.Text);    // 바인딩된 원문도 그대로
    }

    [Fact]
    public void should_leave_a_single_line_title_alone_when_wrapping_is_off()
    {
        var shown = OnSta(() =>
        {
            var title = new ConsoleDialogText { Text = "부대 편제 닫기", TextWrapping = TextWrapping.NoWrap };
            Lines(title, 400);
            return Inner(title).Text;
        });

        Assert.Equal("부대 편제 닫기", shown);
    }

    #region - Fixtures -
    /// <summary>화면 밖 창에 얹어 배치한다 — 틀 글의 기본 스타일(Generic.xaml → Dialogs.xaml)이 실제 앱처럼 풀린다.</summary>
    private static List<string> Lines(ConsoleDialogText text, double width)
    {
        text.Width = width;
        text.HorizontalAlignment = HorizontalAlignment.Left;
        text.VerticalAlignment = VerticalAlignment.Top;
        var window = new Window
        {
            Content = new Grid { Children = { text } },
            Width = width + 100, Height = 400, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            var tb = Inner(text);
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
        finally { window.Close(); }
    }

    private static TextBlock Inner(ConsoleDialogText text)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(text);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is TextBlock tb) return tb;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        throw new InvalidOperationException("틀 글의 템플릿에 TextBlock 이 없다");
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
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
        return result;
    }
    #endregion
}
