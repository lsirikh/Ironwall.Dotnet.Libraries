using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;

/// <summary>
/// 반복 펼치기의 입력 — 유형 · 개수 · 시작 채널 · key 규칙(FR-08).
/// </summary>
/// <remarks><paramref name="KeyFormat"/> 은 <c>ci_{02d}</c>(앞을 0 으로 채워 두 자리) · <c>ci_{d}</c>(그대로) 꼴.</remarks>
public sealed record RepeatExpandSpec(string TypeCode, int Count, int StartChannel, string KeyFormat);

/// <summary>미리보기 한 줄. <paramref name="IsConflict"/> 인 줄은 붉게 — 하나라도 있으면 펼치지 않는다.</summary>
public sealed record RepeatPreviewRow(string Key, int Channel, bool IsConflict, string? Error)
{
    /// <summary>
    /// 미리보기 줄에 붙일 까닭. 까닭이 없으면 <b>빈 글자</b>다 — <c>StringFormat</c> 은 null 에도 틀을 씌워
    /// 멀쩡한 줄마다 ⚠ 를 하나씩 찍는다(실측).
    /// </summary>
    public string ErrorSuffix => string.IsNullOrEmpty(Error) ? string.Empty : $"   ⚠ {Error}";
}

/// <summary>
/// 16채널을 16번 끌게 하지 않는다(AS §3). 위치가 의미를 갖는 조작은 드래그가 1순위지만,
/// <b>규칙적 대량 생성</b>은 드래그가 애초에 잘하는 일이 아니다 — 결과는 똑같은 슬롯이라 이후 편집 · 정렬은 드래그로 잇는다.
/// </summary>
public static class RepeatExpand
{
    public const int MaxCount = 64;

    /// <summary>
    /// <c>{0Nd}</c> 의 자리 수 상한. 최대 <see cref="MaxCount"/> 개(두 자리)를 펼치는 자리에
    /// 여섯 자리면 충분하고도 남는다.
    /// </summary>
    /// <remarks>
    /// 상한이 없으면 <c>{099999999999d}</c> 한 줄이 <see cref="int.Parse(string)"/> 에서
    /// <see cref="OverflowException"/> 을 던지거나(자리 수가 <see cref="int"/> 를 넘을 때),
    /// 넘지 않더라도 <see cref="string.PadLeft(int, char)"/> 가 <b>수억 글자짜리 key</b> 를 만들어
    /// 미리보기 한 번에 메모리를 통째로 먹는다. 둘 다 사용자가 글자 몇 개를 더 친 결과여서는 안 된다.
    /// </remarks>
    public const int MaxKeyPadWidth = 6;

    /// <summary><c>{d}</c> · <c>{02d}</c> · <c>{03d}</c> … 자리.</summary>
    private static readonly Regex _placeholder = new(@"\{(?:0(\d+))?d\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>null = 통과.</summary>
    public static string? Validate(RepeatExpandSpec spec)
    {
        if (spec is null) return "펼칠 내용이 없습니다.";
        if (string.IsNullOrWhiteSpace(spec.TypeCode)) return "유형을 고르세요.";
        if (spec.Count < 1 || spec.Count > MaxCount) return $"개수는 1에서 {MaxCount} 사이로 입력하세요.";
        if (string.IsNullOrWhiteSpace(spec.KeyFormat)) return "식별 이름 규칙을 입력하세요.";
        if (!_placeholder.IsMatch(spec.KeyFormat)) return "식별 이름 규칙에 {d} 또는 {02d} 같은 번호 자리를 넣으세요.";

        foreach (Match match in _placeholder.Matches(spec.KeyFormat))
        {
            if (!TryReadWidth(match, out _))
                return $"식별 이름 규칙의 자릿수는 1에서 {MaxKeyPadWidth} 사이로 정하세요.";
        }

        if (spec.StartChannel < 0) return "시작 채널은 0 이상으로 입력하세요.";
        return null;
    }

    /// <summary>
    /// 펼쳤을 때 생길 줄들. 번호는 <see cref="RepeatExpandSpec.StartChannel"/> 부터 1씩 — key 번호와 채널 번호가 같다(목업과 동일).
    /// </summary>
    public static IReadOnlyList<RepeatPreviewRow> Preview(RepeatExpandSpec spec, IEnumerable<string> existingKeys)
    {
        if (Validate(spec) is not null) return Array.Empty<RepeatPreviewRow>();

        var onBoard = new HashSet<string>(existingKeys?.Where(x => x is not null) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        var inPreview = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<RepeatPreviewRow>(spec.Count);

        for (var i = 0; i < spec.Count; i++)
        {
            var number = spec.StartChannel + i;
            var key = FormatKey(spec.KeyFormat, number);

            var error = AssemblyKeyRules.ValidateFormat(key);
            if (error is null && onBoard.Contains(key)) error = "이미 보드에 있는 식별 이름입니다";
            if (error is null && !inPreview.Add(key)) error = "펼칠 목록 안에서 겹칩니다";

            rows.Add(new RepeatPreviewRow(key, number, error is not null, error));
        }

        return rows;
    }

    /// <summary><c>ci_{02d}</c> + 7 → <c>ci_07</c>. 자리가 여럿이면 모두 같은 번호로 채운다.</summary>
    /// <remarks>
    /// <b>절대 던지지 않는다</b> — 미리보기는 사용자가 글자를 칠 때마다 돌고, 그 도중의 반쯤 쓴 규칙
    /// (<c>{0999999999999d}</c> 처럼)에서 예외가 나면 입력 창이 통째로 죽는다. 쓸 수 없는 자릿수는
    /// <b>채우지 않은 번호</b>로 떨어뜨리고, 거절은 <see cref="Validate"/> 가 문장으로 한다.
    /// </remarks>
    public static string FormatKey(string keyFormat, int number)
    {
        if (string.IsNullOrEmpty(keyFormat)) return string.Empty;

        return _placeholder.Replace(keyFormat, m =>
        {
            var text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!TryReadWidth(m, out var width) || width is null) return text;

            var negative = text.StartsWith('-');
            var digits = negative ? text[1..] : text;
            return (negative ? "-" : string.Empty) + digits.PadLeft(width.Value, '0');
        });
    }

    /// <summary>
    /// 자리 하나의 자릿수를 읽는다 — <c>{d}</c>(채우지 않음) 는 <c>true</c> + <c>null</c>,
    /// 1..<see cref="MaxKeyPadWidth"/> 는 <c>true</c> + 값, 그 밖(0 · 상한 초과 · <see cref="int"/> 초과)은 <c>false</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="int.TryParse(string, out int)"/> 를 쓴다 — 자리 수가 <see cref="int"/> 를 넘으면
    /// <see cref="int.Parse(string)"/> 는 <see cref="OverflowException"/> 을 던지지만 TryParse 는
    /// <c>false</c> 를 돌려준다. 글자 수부터 먼저 잘라 아주 긴 숫자에서 파싱 자체를 건너뛴다.
    /// </remarks>
    private static bool TryReadWidth(Match match, out int? width)
    {
        width = null;
        if (!match.Groups[1].Success) return true;      // {d} — 그대로

        var text = match.Groups[1].Value;
        if (text.Length > MAX_WIDTH_DIGITS) return false;   // int 를 넘길 만큼 길다 — 파싱할 것도 없다

        if (!int.TryParse(text, System.Globalization.NumberStyles.None,
                          System.Globalization.CultureInfo.InvariantCulture, out var value))
            return false;

        if (value < 1 || value > MaxKeyPadWidth) return false;

        width = value;
        return true;
    }

    /// <summary><see cref="int"/> 가 담을 수 있는 자리 수 — 이보다 길면 파싱조차 하지 않는다.</summary>
    private const int MAX_WIDTH_DIGITS = 9;
}
