using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 한글을 <b>띄어쓰기에서만</b> 줄바꿈한다 — WPF 는 한글 음절 사이를 모두 줄바꿈 자리로 본다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: 실창 캡처에서 "바 / 꿀 수 없습니다", "다른 카 / 테고리로", "보 / 고 없음" 처럼 낱말 가운데서 끊긴 곳이
/// 10곳 넘게 나왔다(2026-09-27 GIS 실창 육안 검토). WPF 는 유니코드 줄바꿈 규칙대로 한글 음절을 한자처럼 다뤄
/// 어느 음절 사이에서나 끊는다. <c>Language="ko-KR"</c> 도 <c>WrapWithOverflow</c> 도 바꾸지 못한다(실측 — 네 조합 모두 같은 줄).</para>
/// <para><b>어떻게</b>: 줄바꿈이 켜진(<see cref="TextWrapping.NoWrap"/> 아닌) <see cref="TextBlock"/> 의 글에서, 띄어쓰기가 아닌
/// 두 글자 사이에 한글이 끼어 있으면 보이지 않는 단어 결합자(U+2060)를 넣는다. 결합자는 폭이 0 이고 그 자리의 줄바꿈만 막는다.
/// 한 낱말이 칸보다 길면 WPF 가 그래도 끊는다(<see cref="TextWrapping.Wrap"/>).</para>
/// <para>값은 <see cref="DependencyObject.SetCurrentValue"/> 로 넣어 바인딩을 끊지 않는다. 글이 인라인(Run 여럿)으로 만들어진
/// TextBlock 은 건드리지 않는다(Text 를 쓰면 인라인이 사라진다). UIA 이름에는 결합자가 섞이므로 자동화는 비교 전에
/// <see cref="Strip"/> 으로 걷는다.</para>
/// </remarks>
public static class KoreanWordWrap
{
    /// <summary>단어 결합자 — 폭 0, 줄바꿈 금지.</summary>
    public const char WordJoiner = '⁠';

    // ⚠ 이름을 바꾸지 않는다 — 호스트 시험(Dotnet.Monitoring.Solution.Tests KoreanWrapGuard)이 반사로 읽고 쓴다.
    //   false 면 처리기가 걸려 있어도 아무것도 하지 않는다(Uninstall).
    private static volatile bool _installed;
    private static readonly DependencyPropertyDescriptor TextDescriptor =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    /// <summary>지금 켜져 있는가(시험용).</summary>
    internal static bool IsInstalled => _installed;

    /// <summary>
    /// 시험용 해제 — 클래스 처리기는 WPF 가 떼는 길을 주지 않으므로 걸린 채 두고 <b>일을 멈춘다</b>: 이후 새 TextBlock 은 붙잡지 않고,
    /// 이미 붙잡은 TextBlock 도 글이 바뀌거나 다시 Loaded 돼도 손대지 않는다. 이미 넣은 결합자는 걷지 않는다(<see cref="Strip"/>).
    /// 다시 <see cref="Install"/> 하면 원래대로 돈다. 앱은 부르지 않는다(호스트 보고 R10 — 같은 프로세스 시험 글자 비교가 순서에 따라 깨졌다).
    /// </summary>
    internal static void Uninstall() => _installed = false;

    /// <summary>전역 설치 — 앱(또는 미리보기 도구)마다 한 번. 여러 번 불러도 한 번만 건다. UI 스레드에서 부른다.</summary>
    /// <remarks>
    /// <see cref="Uninstall"/> 뒤에 다시 부르면 처리기를 한 번 더 건다 — 호스트 시험이 반사로 처리기를 떼고 표지를 끄는 길과도 맞물린다.
    /// 겹쳐 걸려도 <c>IsTracked</c> 가 한 TextBlock 을 한 번만 붙잡으므로 결과는 같다(시험에서만 생긴다).
    /// </remarks>
    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        // ⚠ Loaded 클래스 처리기로는 안 된다 — WPF 는 Loaded/Unloaded 를 '제 처리기(인스턴스 · 스타일 EventSetter)가 있는 요소'에만
        //   방송한다(FrameworkElement.ThisHasLoadedChangeEventHandler 에 클래스 처리기는 들지 않는다). 그래서 실창에서 한 번도 불리지 않았다
        //   (2026-09-27 3회차 실창: "바 / 꿀 수 없습니다" 가 그대로). SizeChanged 는 모든 요소에서 올라오므로 그것으로 처음 한 번 붙잡고,
        //   그 요소에 인스턴스 Loaded/Unloaded 와 Text 변경 구독을 단다.
        EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnFirstSized));
    }

    private static readonly DependencyProperty IsTrackedProperty = DependencyProperty.RegisterAttached(
        "IsTracked", typeof(bool), typeof(KoreanWordWrap), new PropertyMetadata(false));

    private static void OnFirstSized(object sender, SizeChangedEventArgs e)
    {
        if (!_installed || sender is not TextBlock tb || (bool)tb.GetValue(IsTrackedProperty)) return;
        tb.SetValue(IsTrackedProperty, true);
        tb.Loaded += OnLoaded;
        tb.Unloaded += OnUnloaded;
        TextDescriptor.AddValueChanged(tb, OnTextChanged);
        Apply(tb);
    }

    /// <summary>글에서 결합자를 걷는다(자동화 · 복사용).</summary>
    public static string Strip(string? text) => string.IsNullOrEmpty(text) ? text ?? "" : text.Replace(WordJoiner.ToString(), "");

    /// <summary>
    /// 띄어쓰기가 아닌 두 글자 사이 가운데 한쪽이라도 한글이면 결합자를 넣는다. 이미 든 결합자는 먼저 걷는다(여러 번 불러도 같다).
    /// 한글이 없으면 원문을 그대로 돌려준다.
    /// </summary>
    public static string Join(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var plain = Strip(text);
        if (!HasHangul(plain)) return plain;

        var sb = new StringBuilder(plain.Length * 2);
        for (var i = 0; i < plain.Length; i++)
        {
            var c = plain[i];
            sb.Append(c);
            if (i + 1 >= plain.Length) break;
            var n = plain[i + 1];
            if (char.IsWhiteSpace(c) || char.IsWhiteSpace(n)) continue;
            if (IsHangul(c) || IsHangul(n)) sb.Append(WordJoiner);
        }
        return sb.ToString();
    }

    private static bool HasHangul(string s)
    {
        foreach (var c in s) if (IsHangul(c)) return true;
        return false;
    }

    private static bool IsHangul(char c)
        => (c >= '가' && c <= '힣')      // 완성형 음절
           || (c >= 'ᄀ' && c <= 'ᇿ')   // 자모
           || (c >= '㄰' && c <= '㆏');  // 호환 자모

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_installed || sender is not TextBlock tb) return;
        TextDescriptor.RemoveValueChanged(tb, OnTextChanged);   // 다시 붙어도 한 번만
        TextDescriptor.AddValueChanged(tb, OnTextChanged);
        Apply(tb);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock tb) TextDescriptor.RemoveValueChanged(tb, OnTextChanged);
    }

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        if (_installed && sender is TextBlock tb) Apply(tb);
    }

    private static void Apply(TextBlock tb)
    {
        // 인라인으로 만든 글(<Run> 여럿 · Hyperlink 등)은 Text 를 쓰면 인라인이 사라진다 — 건드리지 않는다.
        if (DependencyPropertyHelper.GetValueSource(tb, TextBlock.TextProperty).BaseValueSource == BaseValueSource.Default) return;
        var current = tb.Text;
        // 조사 병기("을(를)")는 줄바꿈과 무관하게 모든 글에서 고른다(KoreanParticles).
        var next = KoreanParticles.Resolve(current);
        if (tb.TextWrapping != TextWrapping.NoWrap) next = Join(next);
        if (!string.Equals(current, next, StringComparison.Ordinal))
            tb.SetCurrentValue(TextBlock.TextProperty, next);
    }
}
