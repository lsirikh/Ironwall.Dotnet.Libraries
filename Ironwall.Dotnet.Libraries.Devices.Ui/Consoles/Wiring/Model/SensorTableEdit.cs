using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>
/// 한꺼번에 채우기의 입력 — <b>손댄 칸만</b> 값이 들어 있다(<c>null</c> = 손대지 않음, WS L596-601).
/// </summary>
public sealed record SensorBulkEdit(string? Number = null, string? Name = null, string? TypeText = null, string? Zone = null)
{
    public bool IsEmpty => Number is null && Name is null && TypeText is null && Zone is null;

    public int TouchedCount => (Number is null ? 0 : 1) + (Name is null ? 0 : 1) + (TypeText is null ? 0 : 1) + (Zone is null ? 0 : 1);
}

/// <summary>
/// 표 편집의 순수 함수 — 공통값("여러 값") · 손댄 칸만 적용 · 연속 번호 · 통일 · 이름 규칙(WS L595-646).
/// </summary>
/// <remarks>
/// <b>덮어쓰지 않는다</b> — 값이 줄마다 다른 칸은 빈 칸으로 보이지 않고 "여러 값" 으로 쓰고, 적용은 손댄 칸만 한다.
/// 이 규칙이 없으면 여러 줄을 고르는 순간 줄마다 달랐던 값이 조용히 같아진다(지금 앱의 결함, WS L328).
/// </remarks>
public static class SensorTableEdit
{
    /// <summary>값이 줄마다 다를 때 칸에 쓰는 글자(WS L597).</summary>
    public const string MULTI_VALUE_TEXT = "— 여러 값 —";

    /// <summary>장비 번호 상한 — 서버 <c>number_device</c> 는 정수다. 사람이 칠 수 있는 자리로 묶는다.</summary>
    public const int MAX_NUMBER = 999_999;

    /// <summary>이름 규칙의 번호 자리(WS L635).</summary>
    public const string NUMBER_TOKEN = "{번호}";

    /// <summary>고른 줄의 공통값. 줄마다 다르면 <c>null</c>(= "여러 값").</summary>
    public static string? CommonText(IEnumerable<SensorFacts> rows, Func<SensorFacts, string> pick)
    {
        ArgumentNullException.ThrowIfNull(pick);
        var list = rows?.ToList() ?? new List<SensorFacts>();
        if (list.Count == 0) return null;

        var first = pick(list[0]) ?? string.Empty;
        return list.All(r => string.Equals(pick(r) ?? string.Empty, first, StringComparison.Ordinal)) ? first : null;
    }

    /// <summary>번호 칸의 공통값(글자). 줄마다 다르면 <c>null</c>.</summary>
    public static string? CommonNumberText(IEnumerable<SensorFacts> rows)
        => CommonText(rows, r => r.Number.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// 적용해도 되는가 — 막는 까닭(<c>null</c> = 통과). 번호는 <b>정수 · 1 이상</b>이어야 한다.
    /// </summary>
    public static string? Validate(SensorBulkEdit edit, int rowCount)
    {
        if (edit is null || edit.IsEmpty) return "고칠 칸을 먼저 건드리세요.";
        if (rowCount <= 0) return "고른 줄이 없습니다.";

        if (edit.Number is { } number)
        {
            if (!TryNumber(number, out var value)) return $"번호는 숫자로 적어 주세요(1 ~ {MAX_NUMBER}).";
            if (value < 1 || value > MAX_NUMBER) return $"번호는 1 과 {MAX_NUMBER} 사이여야 합니다.";
        }

        if (edit.Name is { } name && name.Length > 100) return "이름이 너무 깁니다(100자까지).";
        return null;
    }

    /// <summary>막지는 않지만 알려야 하는 것(<c>null</c> = 없음).</summary>
    public static string? Advice(SensorBulkEdit edit, int rowCount)
    {
        if (edit?.Number is not null && rowCount > 1)
            return "여러 줄에 같은 번호가 들어갑니다 — [연속 번호 채우기] 를 쓰면 한 줄씩 늘어납니다.";
        return null;
    }

    /// <summary>손댄 칸만 얹은 새 값. 손대지 않은 칸은 줄마다 원래 값 그대로다(WS L640-646).</summary>
    public static SensorFacts Apply(SensorFacts facts, SensorBulkEdit edit)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (edit is null || edit.IsEmpty) return facts;

        var number = edit.Number is { } text && TryNumber(text, out var value) ? value : facts.Number;
        return facts with
        {
            Number = number,
            Name = edit.Name ?? facts.Name,
            TypeText = edit.TypeText ?? facts.TypeText,
            Zone = edit.Zone ?? facts.Zone,
        };
    }

    /// <summary>연속 번호 채우기 — 고른 순서대로 <paramref name="start"/> 부터 <paramref name="step"/> 씩(WS L611, L631-632).</summary>
    public static IReadOnlyList<SensorFacts> FillSequential(IEnumerable<SensorFacts> rows, int start, int step = 1)
    {
        var list = rows?.ToList() ?? new List<SensorFacts>();
        var result = new List<SensorFacts>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var number = start + (long)step * i;
            result.Add(list[i] with { Number = number < 1 ? 1 : number > MAX_NUMBER ? MAX_NUMBER : (int)number });
        }
        return result;
    }

    /// <summary>첫 줄 값으로 통일 — 종류 · 구역만(WS L612, L633-634).</summary>
    public static IReadOnlyList<SensorFacts> UnifyWithFirst(IEnumerable<SensorFacts> rows)
    {
        var list = rows?.ToList() ?? new List<SensorFacts>();
        if (list.Count == 0) return list;

        var first = list[0];
        return list.Select(r => r with { TypeText = first.TypeText, Zone = first.Zone }).ToList();
    }

    /// <summary>이름 규칙 — <c>{번호}</c> 자리에 그 줄의 번호(WS L613, L635-636).</summary>
    public static IReadOnlyList<SensorFacts> ApplyNameRule(IEnumerable<SensorFacts> rows, string rule)
    {
        var list = rows?.ToList() ?? new List<SensorFacts>();
        if (string.IsNullOrWhiteSpace(rule)) return list;
        return list.Select(r => r with { Name = FormatName(rule, r.Number) }).ToList();
    }

    /// <summary><c>북측 {번호}구간 펜스</c> + 1203 → <c>북측 1203구간 펜스</c>.</summary>
    public static string FormatName(string? rule, int number)
    {
        if (string.IsNullOrEmpty(rule)) return string.Empty;
        return rule.Replace(NUMBER_TOKEN, number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    /// <summary>적용 전에 말로 보여 주는 한 줄 — "2줄의 종류를 스마트센서로"(WS L447, L619-621).</summary>
    public static string PreviewSentence(SensorBulkEdit edit, int rowCount)
    {
        if (edit is null || edit.IsEmpty) return "고친 칸이 없습니다.";

        var parts = new List<string>();
        if (edit.Number is { } n) parts.Add($"번호={n}");
        if (edit.Name is { } name) parts.Add($"이름={name}");
        if (edit.TypeText is { } type) parts.Add($"종류={type}");
        if (edit.Zone is { } zone) parts.Add($"구역={zone}");
        return $"{rowCount}줄에 적용: {string.Join(" · ", parts)} — 손대지 않은 칸은 줄마다 원래 값을 그대로 둡니다.";
    }

    private static bool TryNumber(string text, out int value)
        => int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
