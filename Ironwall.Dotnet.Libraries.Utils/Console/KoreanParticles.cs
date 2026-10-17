using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 조사 병기("은(는)" · "을(를)" · "이(가)" · "과(와)" · "(으)로")를 앞 낱말의 받침에 맞는 하나로 고른다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: 이름이 실행 중에 정해지는 문구("'{이름}'을(를) 저장했습니다")가 장비 · 부대 · 서버 콘솔에 40곳 가까이 있어
/// 운영자 화면에 병기가 그대로 보였다(2026-09-27 실창 육안 검토 #15 계열). 문구마다 고치는 대신 화면에 그릴 때 한 곳에서 고른다
/// (<see cref="KoreanWordWrap"/> 이 TextBlock 에 걸 때 함께 부른다).</para>
/// <para>받침 판정은 앞 글자를 본다 — 따옴표 · 괄호 · 공백은 건너뛴다. 숫자와 영문은 읽는 소리로 판정한다
/// (1 일 · 3 삼 · 6 육 · 7 칠 · 8 팔 · 0 영 = 받침 있음, L · M · N · R = 엘 · 엠 · 엔 · 알). 모르면 병기를 그대로 둔다.</para>
/// </remarks>
public static class KoreanParticles
{
    private static readonly Regex Dual = new(
        @"(은\(는\)|는\(은\)|을\(를\)|를\(을\)|이\(가\)|가\(이\)|과\(와\)|와\(과\)|\(으\)로|으로\(로\)|로\(으로\))",
        RegexOptions.Compiled);

    /// <summary>문구 안의 병기를 모두 고른다. 병기가 없으면 원문 그대로.</summary>
    public static string Resolve(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('(') < 0) return text ?? "";
        // 단어 결합자(KoreanWordWrap)가 "을(를)" 사이에 끼면 패턴이 끊긴다 — 걷고 고른다(부른 쪽이 다시 잇는다).
        if (text.IndexOf(KoreanWordWrap.WordJoiner) >= 0)
        {
            var plain = KoreanWordWrap.Strip(text);
            var resolved = Resolve(plain);
            return string.Equals(plain, resolved, StringComparison.Ordinal) ? text : resolved;
        }
        return Dual.Replace(text, m =>
        {
            var batchim = FinalOf(text, m.Index);
            if (batchim is null) return m.Value;
            var (has, isRieul) = batchim.Value;
            return m.Value switch
            {
                "은(는)" or "는(은)" => has ? "은" : "는",
                "을(를)" or "를(을)" => has ? "을" : "를",
                "이(가)" or "가(이)" => has ? "이" : "가",
                "과(와)" or "와(과)" => has ? "과" : "와",
                _ => has && !isRieul ? "으로" : "로",      // (으)로: ㄹ 받침은 '로'(서울로)
            };
        });
    }

    /// <summary>
    /// <paramref name="index"/> 앞 낱말 끝 글자의 받침 — (받침 있음, 그 받침이 ㄹ). 판정할 수 없으면 null.
    /// </summary>
    internal static (bool Has, bool IsRieul)? FinalOf(string text, int index)
    {
        var i = index - 1;
        while (i >= 0 && IsSkippable(text[i])) i--;
        if (i < 0) return null;
        var c = text[i];

        if (c >= '가' && c <= '힣')
        {
            var jong = (c - 0xAC00) % 28;
            return (jong != 0, jong == 8);   // 8 = ㄹ
        }
        if (c >= '0' && c <= '9')
            return c switch
            {
                '0' or '1' or '3' or '6' or '7' or '8' => (true, c is '1' or '7' or '8'),   // 영 · 일 · 삼 · 육 · 칠 · 팔 (일 · 칠 · 팔 = ㄹ)
                _ => (false, false),                                                          // 이 · 사 · 오 · 구
            };
        if (char.IsLetter(c) && c < 128)
        {
            var u = char.ToUpperInvariant(c);
            return u switch
            {
                'L' => (true, true),                   // 엘
                'M' or 'N' => (true, false),           // 엠 · 엔
                'R' => (true, true),                   // 알
                _ => (false, false),
            };
        }
        return null;
    }

    private static bool IsSkippable(char c)
        => c is '\'' or '"' or ')' or ']' or '}' or '’' or '”' or '」' or '』' or '>' or KoreanWordWrap.WordJoiner;
}
