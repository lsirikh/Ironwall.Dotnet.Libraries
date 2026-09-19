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

/// <summary>붙여넣기 결과 보고 — 받은 줄 · 만들 줄 · 버린 줄과 까닭(WS L368, L499, L527).</summary>
public sealed record PasteReport(
    int TotalLines,
    bool HeaderDetected,
    IReadOnlyList<string> ColumnOrder,
    IReadOnlyList<PasteRow> Rows,
    string? FatalError)
{
    public IReadOnlyList<PasteRow> Accepted { get; } = Rows.Where(r => r.IsAccepted).ToList();
    public IReadOnlyList<PasteRow> Rejected { get; } = Rows.Where(r => !r.IsAccepted).ToList();
    public bool HasRows => Accepted.Count > 0;

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
    public static PasteReport Parse(string? text, IEnumerable<int>? existingNumbers, string defaultType = "", string defaultZone = "")
    {
        if (string.IsNullOrWhiteSpace(text))
            return new PasteReport(0, false, Array.Empty<string>(), Array.Empty<PasteRow>(), "붙여넣을 내용이 없습니다.");

        if (text.Length > MAX_TEXT_LENGTH)
            return new PasteReport(0, false, Array.Empty<string>(), Array.Empty<PasteRow>(),
                $"붙여넣은 내용이 너무 큽니다({text.Length:N0}자) — 한 번에 {MAX_TEXT_LENGTH:N0}자까지 받습니다.");

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                        .Replace('\r', '\n')
                        .Split('\n')
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();

        if (lines.Count == 0)
            return new PasteReport(0, false, Array.Empty<string>(), Array.Empty<PasteRow>(), "붙여넣을 내용이 없습니다.");

        var cells = lines.Select(l => l.Split('\t').Select(Sanitize).ToArray()).ToList();

        var map = DetectHeader(cells[0]);
        var headerDetected = map is not null;
        map ??= new ColumnMap(0, 1, 2, 3);
        var start = headerDetected ? 1 : 0;

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

        var order = new[] { "번호", "이름", "종류", "구역" };
        return new PasteReport(lines.Count - start, headerDetected, order, rows, null);
    }

    private sealed record ColumnMap(int Number, int Name, int Type, int Zone);

    /// <summary>첫 줄이 머리글이면 열 자리를, 아니면 <c>null</c>.</summary>
    private static ColumnMap? DetectHeader(IReadOnlyList<string> cells)
    {
        int number = -1, name = -1, type = -1, zone = -1;
        for (var i = 0; i < cells.Count; i++)
        {
            var key = cells[i].Trim().ToLowerInvariant();
            if (number < 0 && NumberHeaders.Contains(key)) number = i;
            else if (name < 0 && NameHeaders.Contains(key)) name = i;
            else if (type < 0 && TypeHeaders.Contains(key)) type = i;
            else if (zone < 0 && ZoneHeaders.Contains(key)) zone = i;
        }

        // 번호 + 하나만 더 알아봐도 머리글로 본다 — 못 찾은 열은 자리를 비운다(-1 = 읽지 않음).
        if (number < 0 || (name < 0 && type < 0 && zone < 0)) return null;
        return new ColumnMap(number, name, type, zone);
    }

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
