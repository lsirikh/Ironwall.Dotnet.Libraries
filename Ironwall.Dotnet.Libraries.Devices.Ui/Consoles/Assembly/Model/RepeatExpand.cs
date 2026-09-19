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
public sealed record RepeatPreviewRow(string Key, int Channel, bool IsConflict, string? Error);

/// <summary>
/// 16채널을 16번 끌게 하지 않는다(AS §3). 위치가 의미를 갖는 조작은 드래그가 1순위지만,
/// <b>규칙적 대량 생성</b>은 드래그가 애초에 잘하는 일이 아니다 — 결과는 똑같은 슬롯이라 이후 편집 · 정렬은 드래그로 잇는다.
/// </summary>
public static class RepeatExpand
{
    public const int MaxCount = 64;

    /// <summary><c>{d}</c> · <c>{02d}</c> · <c>{03d}</c> … 자리.</summary>
    private static readonly Regex _placeholder = new(@"\{(?:0(\d+))?d\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>null = 통과.</summary>
    public static string? Validate(RepeatExpandSpec spec)
    {
        if (spec is null) return "펼칠 내용이 없다";
        if (string.IsNullOrWhiteSpace(spec.TypeCode)) return "유형을 고르지 않았다";
        if (spec.Count < 1 || spec.Count > MaxCount) return $"개수는 1 과 {MaxCount} 사이여야 한다";
        if (string.IsNullOrWhiteSpace(spec.KeyFormat)) return "key 규칙이 비어 있다";
        if (!_placeholder.IsMatch(spec.KeyFormat)) return "key 규칙에 {d} 또는 {02d} 같은 번호 자리가 있어야 한다";
        if (spec.StartChannel < 0) return "시작 채널은 0 이상이어야 한다";
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
            if (error is null && onBoard.Contains(key)) error = "이미 보드에 있는 key 다";
            if (error is null && !inPreview.Add(key)) error = "펼칠 목록 안에서 겹친다";

            rows.Add(new RepeatPreviewRow(key, number, error is not null, error));
        }

        return rows;
    }

    /// <summary><c>ci_{02d}</c> + 7 → <c>ci_07</c>. 자리가 여럿이면 모두 같은 번호로 채운다.</summary>
    public static string FormatKey(string keyFormat, int number)
    {
        if (string.IsNullOrEmpty(keyFormat)) return string.Empty;

        return _placeholder.Replace(keyFormat, m =>
        {
            var text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!m.Groups[1].Success) return text;

            var width = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var negative = text.StartsWith('-');
            var digits = negative ? text[1..] : text;
            return (negative ? "-" : string.Empty) + digits.PadLeft(width, '0');
        });
    }
}
