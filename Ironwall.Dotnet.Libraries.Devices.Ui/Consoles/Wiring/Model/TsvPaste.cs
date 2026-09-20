using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>붙여넣기 한 줄의 판정 결과.</summary>
/// <param name="LineNumber">붙여넣은 글자에서의 줄 번호(1부터) — 사람이 엑셀에서 찾아갈 수 있게.</param>
public sealed record PasteRow(int LineNumber, SensorFacts Facts, string? Error)
{
    public bool IsAccepted => Error is null;
}

/// <summary>붙여넣은 글자의 열 하나가 무엇인가(W7).</summary>
public enum PasteColumn
{
    /// <summary>읽지 않는다.</summary>
    Ignore = 0,
    Number = 1,
    Name = 2,
    Type = 3,
    Zone = 4,
}

/// <summary>열 매핑 — 붙여넣은 글자의 <b>열 순서대로</b> 무엇으로 읽을지(W7).</summary>
public sealed record PasteColumnMap(IReadOnlyList<PasteColumn> Columns)
{
    public int IndexOf(PasteColumn column)
    {
        for (var i = 0; i < Columns.Count; i++) if (Columns[i] == column) return i;
        return -1;
    }

    public PasteColumn At(int index) => index >= 0 && index < Columns.Count ? Columns[index] : PasteColumn.Ignore;

    /// <summary>번호 열이 없으면 아무 줄도 만들 수 없다.</summary>
    public bool HasNumber => IndexOf(PasteColumn.Number) >= 0;

    /// <summary>한 열의 뜻을 바꾼 새 매핑 — 같은 뜻을 두 열에 둘 수 없다.</summary>
    public PasteColumnMap With(int index, PasteColumn column)
    {
        var next = Columns.ToList();
        while (next.Count <= index) next.Add(PasteColumn.Ignore);
        if (column != PasteColumn.Ignore)
            for (var i = 0; i < next.Count; i++) if (i != index && next[i] == column) next[i] = PasteColumn.Ignore;
        next[index] = column;
        return new PasteColumnMap(next);
    }
}

/// <summary>붙여넣기 결과 보고 — 받은 줄 · 만들 줄 · 버린 줄과 까닭(WS L368, L499, L527).</summary>
public sealed record PasteReport(
    int TotalLines,
    bool HeaderDetected,
    IReadOnlyList<string> ColumnOrder,
    IReadOnlyList<PasteRow> Rows,
    string? FatalError,
    PasteColumnMap? Mapping = null,
    string? SourceText = null)
{
    public IReadOnlyList<PasteRow> Accepted { get; } = Rows.Where(r => r.IsAccepted).ToList();
    public IReadOnlyList<PasteRow> Rejected { get; } = Rows.Where(r => !r.IsAccepted).ToList();
    public bool HasRows => Accepted.Count > 0;

    /// <summary>다시 읽을 때 쓰는 원본 — 열 매핑을 바꾸면 이 글자를 다시 읽는다.</summary>
    public string? Text { get; } = SourceText;

    /// <summary>사람이 고칠 수 있는 열 매핑.</summary>
    public PasteColumnMap Columns { get; } = Mapping ?? new PasteColumnMap(Array.Empty<PasteColumn>());

    public string Summary => FatalError is not null
        ? FatalError
        : $"{TotalLines}줄을 읽어 {Accepted.Count}줄을 만듭니다" + (Rejected.Count > 0 ? $" · {Rejected.Count}줄은 건너뜁니다" : string.Empty)
          + (HeaderDetected ? " · 첫 줄은 머리글로 보고 건너뛰었습니다" : string.Empty);
}

/// <summary>
/// 엑셀에서 붙여넣기 — 탭·줄바꿈 분해 → 열 인식 → 검증 보고(WS L350, L368, L659-664).
/// </summary>
/// <remarks>
/// <para><b>클립보드는 믿지 않는다</b>(경계 검증) — 글자 수 · 줄 수 · 칸 길이에 상한을 두고 제어문자를 지운다.
/// 붙여넣기는 사용자가 어디서 복사해 왔는지 알 수 없는 유일한 입력 경로다.</para>
/// <para>여기서는 <b>만들 줄만 계산</b>한다 — 실제 생성은 미리보기를 사람이 확인한 뒤다.</para>
/// </remarks>
public static class TsvPaste
{
    /// <summary>받아들이는 글자 수 상한(약 1 MB).</summary>
    public const int MAX_TEXT_LENGTH = 1_000_000;

    /// <summary>한 번에 만들 수 있는 줄 수 상한.</summary>
    public const int MAX_ROWS = 500;

    /// <summary>한 칸의 글자 수 상한 — 넘으면 자른다(줄을 버리지는 않는다).</summary>
    public const int MAX_CELL_LENGTH = 200;

    private static readonly string[] NumberHeaders = { "번호", "장비번호", "센서번호", "number", "no", "number_device" };
    private static readonly string[] NameHeaders = { "이름", "명칭", "name", "name_device" };
    private static readonly string[] TypeHeaders = { "종류", "타입", "type", "type_device", "type_sensor" };
    private static readonly string[] ZoneHeaders = { "구역", "위치", "zone", "location" };

    /// <summary>
    /// 붙여넣은 글자를 줄로 만든다.
    /// </summary>
    /// <param name="text">클립보드 글자(탭 구분).</param>
    /// <param name="existingNumbers">이미 있는 장비 번호 — 겹치면 그 줄은 버린다.</param>
    /// <param name="defaultType">종류 칸이 비었을 때 넣을 값.</param>
    /// <param name="defaultZone">구역 칸이 비었을 때 넣을 값.</param>
    public static PasteReport Parse(string? text, IEnumerable<int>? existingNumbers, string defaultType = "", string defaultZone = "",
                                    PasteColumnMap? mapping = null, bool? treatFirstLineAsHeader = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new PasteReport(0, false, Array.Empty<string>(), Array.Empty<PasteRow>(), "붙여넣을 내용이 없습니다.");

        if (text.Length > MAX_TEXT_LENGTH)
            return new PasteReport(0, false, Array.Empty<string>(), Array.Empty<PasteRow>(),
                $"붙여넣은 내용이 너무 큽니다({text.Length:N0}자) — 한 번에 {MAX_TEXT_LENGTH:N0}자까지 받습니다.");

        var lines = Lines(text).ToList();

        if (lines.Count == 0)
            return new PasteReport(0, false, Array.Empty<string>(), Array.Empty<PasteRow>(), "붙여넣을 내용이 없습니다.");

        var cells = lines.Select(l => l.Split('\t').Select(Sanitize).ToArray()).ToList();

        var detected = DetectHeader(cells[0]);
        var headerDetected = treatFirstLineAsHeader ?? detected is not null;
        var columns = mapping ?? detected ?? DefaultMap(cells.Max(c => c.Length));
        var start = headerDetected ? 1 : 0;

        if (!columns.HasNumber)
            return new PasteReport(Math.Max(lines.Count - start, 0), headerDetected, ColumnNames, Array.Empty<PasteRow>(),
                "번호 열을 골라 주세요 — 번호가 없으면 어느 센서인지 알 수 없습니다.", columns, text);

        var map = new ColumnMap(columns.IndexOf(PasteColumn.Number), columns.IndexOf(PasteColumn.Name),
                                columns.IndexOf(PasteColumn.Type), columns.IndexOf(PasteColumn.Zone));

        var taken = new HashSet<int>(existingNumbers ?? Enumerable.Empty<int>());
        var rows = new List<PasteRow>();
        var made = 0;

        for (var i = start; i < cells.Count; i++)
        {
            var line = i + 1;
            var cell = cells[i];
            var numberText = At(cell, map.Number);

            if (cell.Length < 1 || string.IsNullOrWhiteSpace(numberText))
            {
                rows.Add(new PasteRow(line, SensorFacts.Empty, "번호 칸이 비어 있습니다"));
                continue;
            }

            if (!int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                rows.Add(new PasteRow(line, SensorFacts.Empty, $"번호 '{Shorten(numberText)}' 를 숫자로 읽지 못했습니다"));
                continue;
            }

            if (number < 1 || number > SensorTableEdit.MAX_NUMBER)
            {
                rows.Add(new PasteRow(line, SensorFacts.Empty, $"번호 {number} 가 범위(1 ~ {SensorTableEdit.MAX_NUMBER}) 밖입니다"));
                continue;
            }

            if (!taken.Add(number))
            {
                rows.Add(new PasteRow(line, SensorFacts.Empty, $"번호 {number} 는 이미 있습니다"));
                continue;
            }

            if (made >= MAX_ROWS)
            {
                rows.Add(new PasteRow(line, SensorFacts.Empty, $"한 번에 만들 수 있는 줄 수({MAX_ROWS})를 넘었습니다"));
                continue;
            }

            var name = At(cell, map.Name);
            if (string.IsNullOrWhiteSpace(name)) name = $"센서 {number}";
            var type = At(cell, map.Type);
            if (string.IsNullOrWhiteSpace(type)) type = defaultType;
            var zone = At(cell, map.Zone);
            if (string.IsNullOrWhiteSpace(zone)) zone = defaultZone;

            rows.Add(new PasteRow(line, new SensorFacts(number, name, type, zone), null));
            made++;
        }

        return new PasteReport(lines.Count - start, headerDetected, ColumnNames, rows, null, columns, text);
    }

    private sealed record ColumnMap(int Number, int Name, int Type, int Zone);

    /// <summary>열 이름 — 매핑 콤보에 그대로 쓴다.</summary>
    public static readonly IReadOnlyList<string> ColumnNames = new[] { "번호", "이름", "종류", "구역" };

    /// <summary>머리글이 없을 때의 기본 순서: 번호 · 이름 · 종류 · 구역.</summary>
    public static PasteColumnMap DefaultMap(int columnCount)
    {
        var order = new[] { PasteColumn.Number, PasteColumn.Name, PasteColumn.Type, PasteColumn.Zone };
        var count = Math.Max(columnCount, 1);
        var columns = new List<PasteColumn>(count);
        for (var i = 0; i < count; i++) columns.Add(i < order.Length ? order[i] : PasteColumn.Ignore);
        return new PasteColumnMap(columns);
    }

    /// <summary>첫 줄이 머리글이면 그 자리로 읽은 매핑을, 아니면 <c>null</c>(W7 — 사람이 고칠 수 있는 시작값).</summary>
    public static PasteColumnMap? DetectHeader(IReadOnlyList<string> cells)
    {
        var columns = new List<PasteColumn>(cells.Count);
        var found = 0;
        foreach (var raw in cells)
        {
            var key = raw.Trim().ToLowerInvariant();
            var column = NumberHeaders.Contains(key) ? PasteColumn.Number
                : NameHeaders.Contains(key) ? PasteColumn.Name
                : TypeHeaders.Contains(key) ? PasteColumn.Type
                : ZoneHeaders.Contains(key) ? PasteColumn.Zone
                : PasteColumn.Ignore;
            if (column != PasteColumn.Ignore && columns.Contains(column)) column = PasteColumn.Ignore;
            if (column != PasteColumn.Ignore) found++;
            columns.Add(column);
        }

        var map = new PasteColumnMap(columns);
        return map.HasNumber && found >= 2 ? map : null;
    }

    /// <summary>글자를 줄 · 칸으로만 쪼갠다 — 매핑 화면이 첫 줄을 보여 줄 때 쓴다.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<IReadOnlyList<string>>();
        return Lines(text).Select(l => (IReadOnlyList<string>)l.Split('\t').Select(Sanitize).ToArray()).ToList();
    }

    private static IEnumerable<string> Lines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
               .Replace('\r', '\n')
               .Split('\n')
               .Where(l => !string.IsNullOrWhiteSpace(l));

    private static string At(IReadOnlyList<string> cells, int index)
        => index >= 0 && index < cells.Count ? cells[index] : string.Empty;

    /// <summary>제어문자를 지우고 길이를 묶는다 — 어디서 복사해 온 글자인지 알 수 없다.</summary>
    private static string Sanitize(string? cell)
    {
        if (string.IsNullOrEmpty(cell)) return string.Empty;

        var builder = new StringBuilder(Math.Min(cell.Length, MAX_CELL_LENGTH));
        foreach (var ch in cell)
        {
            if (builder.Length >= MAX_CELL_LENGTH) break;
            if (char.IsControl(ch)) continue;                       // 줄바꿈 · 탭은 이미 분해됐다
            if (ch is '​' or '﻿' or ' ') { builder.Append(' '); continue; }
            builder.Append(ch);
        }
        return builder.ToString().Trim();
    }

    private static string Shorten(string text) => text.Length <= 20 ? text : text[..20] + "…";
}
