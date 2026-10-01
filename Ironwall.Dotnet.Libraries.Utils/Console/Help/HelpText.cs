using System.Text;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>말풍선 글 조각의 종류.</summary>
public enum HelpRunKind
{
    /// <summary>보통 글.</summary>
    Text,
    /// <summary>굵게 — <c>**…**</c>.</summary>
    Bold,
    /// <summary>단축키 칩 — <c>{…}</c>(고정폭 글꼴 · 테두리 상자).</summary>
    Key,
}

/// <summary>말풍선 글의 한 조각.</summary>
public readonly record struct HelpRun(HelpRunKind Kind, string Text);

/// <summary>
/// 설명 목록의 작은 표기를 조각으로 나눈다 — <c>{Alt}+{↑}</c> 는 단축키 칩, <c>**손댄 칸만**</c> 은 굵게. 순수 함수.
/// </summary>
/// <remarks>
/// 대괄호(<c>[구성 저장]</c>)는 표기가 아니다 — 이 저장소의 화면 글은 단추 이름을 대괄호로 적는다. 짝이 없는 <c>{</c> · <c>**</c> 는 글자 그대로 둔다.
/// </remarks>
public static class HelpText
{
    /// <summary>조각으로 나눈다. 빈 글이면 빈 목록.</summary>
    public static IReadOnlyList<HelpRun> Parse(string? text)
    {
        var runs = new List<HelpRun>();
        if (string.IsNullOrEmpty(text)) return runs;

        var buffer = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '{')
            {
                var close = text.IndexOf('}', i + 1);
                if (close > i + 1 && text.IndexOf('{', i + 1, close - i - 1) < 0)
                {
                    Flush(runs, buffer);
                    runs.Add(new HelpRun(HelpRunKind.Key, text.Substring(i + 1, close - i - 1)));
                    i = close + 1;
                    continue;
                }
            }
            else if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var close = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (close > i + 2)
                {
                    Flush(runs, buffer);
                    runs.Add(new HelpRun(HelpRunKind.Bold, text.Substring(i + 2, close - i - 2)));
                    i = close + 2;
                    continue;
                }
            }

            buffer.Append(text[i]);
            i++;
        }

        Flush(runs, buffer);
        return runs;
    }

    /// <summary>표기를 걷은 평문(자동화 이름 · 중복 판정용).</summary>
    public static string Plain(string? text) => string.Concat(Parse(text).Select(r => r.Text));

    private static void Flush(List<HelpRun> runs, StringBuilder buffer)
    {
        if (buffer.Length == 0) return;
        runs.Add(new HelpRun(HelpRunKind.Text, buffer.ToString()));
        buffer.Clear();
    }
}
