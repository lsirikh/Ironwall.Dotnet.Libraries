using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>센서 여러 개 만들기의 입력(WS L157, L446, L649-653).</summary>
/// <param name="Count">몇 개.</param>
/// <param name="StartNumber">시작 번호.</param>
/// <param name="Step">번호 간격(기본 1 — WS W-D5 권고).</param>
/// <param name="NameRule">이름 규칙 — <c>{번호}</c> 자리에 번호가 들어간다.</param>
/// <param name="TypeText">센서 종류(전부 같은 값).</param>
/// <param name="Zone">구역(전부 같은 값).</param>
public sealed record SensorBulkCreateSpec(int Count, int StartNumber, int Step, string NameRule, string TypeText, string Zone);

/// <summary>미리보기 한 줄. <paramref name="IsConflict"/> 인 줄이 있으면 <b>하나도 만들지 않는다</b>.</summary>
public sealed record SensorPreviewRow(int Number, string Name, bool IsConflict, string? Error);

/// <summary>
/// "1번부터 24번까지" 를 한 번에(WS L157). 규칙은 <b>한 번만</b> 정하면 전부에 들어간다.
/// </summary>
/// <remarks>
/// 24줄을 24번 만들게 하지 않는다 — 규칙적 대량 생성은 드래그가 잘하는 일이 아니다(조립기 <c>RepeatExpand</c> 와 같은 판단).
/// 결과는 보통 Draft 줄이라 이후 편집 · 결선은 드래그로 잇는다.
/// </remarks>
public static class SensorBulkCreate
{
    /// <summary>한 번에 만들 수 있는 상한 — 저장이 대당 호출 1회라 폭발반경을 여기서 묶는다.</summary>
    public const int MAX_COUNT = 200;

    public const int MAX_STEP = 1000;

    /// <summary>null = 통과.</summary>
    public static string? Validate(SensorBulkCreateSpec spec)
    {
        if (spec is null) return "만들 내용이 없습니다.";
        if (spec.Count < 1 || spec.Count > MAX_COUNT) return $"개수는 1 과 {MAX_COUNT} 사이여야 합니다.";
        if (spec.StartNumber < 1) return "시작 번호는 1 이상이어야 합니다.";
        if (spec.Step < 1 || spec.Step > MAX_STEP) return $"번호 간격은 1 과 {MAX_STEP} 사이여야 합니다.";
        if (string.IsNullOrWhiteSpace(spec.NameRule)) return "이름 규칙이 비어 있습니다.";

        var last = (long)spec.StartNumber + (long)spec.Step * (spec.Count - 1);
        if (last > SensorTableEdit.MAX_NUMBER) return WiringValidation.Particles($"마지막 번호가 {SensorTableEdit.MAX_NUMBER}을(를) 넘습니다 — 개수나 시작 번호를 줄여 주세요.");
        return null;
    }

    /// <summary>
    /// 만들면 생길 줄들. 이미 있는 번호는 <b>충돌</b>로 표시한다(WS L446 "3·7번은 이미 있어요").
    /// </summary>
    public static IReadOnlyList<SensorPreviewRow> Preview(SensorBulkCreateSpec spec, IEnumerable<int>? existingNumbers)
    {
        if (Validate(spec) is not null) return Array.Empty<SensorPreviewRow>();

        var taken = new HashSet<int>(existingNumbers ?? Enumerable.Empty<int>());
        var inPreview = new HashSet<int>();
        var rows = new List<SensorPreviewRow>(spec.Count);

        for (var i = 0; i < spec.Count; i++)
        {
            var number = spec.StartNumber + spec.Step * i;
            string? error = null;
            if (taken.Contains(number)) error = "이미 있는 번호입니다";
            else if (!inPreview.Add(number)) error = "만들 목록 안에서 번호가 겹칩니다";

            rows.Add(new SensorPreviewRow(number, SensorTableEdit.FormatName(spec.NameRule, number), error is not null, error));
        }

        return rows;
    }

    public static bool HasConflict(IEnumerable<SensorPreviewRow> preview) => preview?.Any(r => r.IsConflict) == true;

    /// <summary>
    /// 실제로 만들 줄들. 충돌이 하나라도 있으면 <b>빈 목록</b>(전부 만들거나 하나도 만들지 않는다).
    /// </summary>
    public static IReadOnlyList<SensorFacts> Expand(SensorBulkCreateSpec spec, IEnumerable<int>? existingNumbers)
    {
        var preview = Preview(spec, existingNumbers);
        if (preview.Count == 0 || HasConflict(preview)) return Array.Empty<SensorFacts>();

        return preview
            .Select(r => new SensorFacts(r.Number, r.Name, spec.TypeText ?? string.Empty, spec.Zone ?? string.Empty))
            .ToList();
    }

    /// <summary>충돌한 번호만 건너뛰고 만든다 — 사용자가 "건너뛸까요?" 에 예라고 답했을 때(WS L446).</summary>
    public static IReadOnlyList<SensorFacts> ExpandSkippingConflicts(SensorBulkCreateSpec spec, IEnumerable<int>? existingNumbers)
    {
        var preview = Preview(spec, existingNumbers);
        return preview
            .Where(r => !r.IsConflict)
            .Select(r => new SensorFacts(r.Number, r.Name, spec.TypeText ?? string.Empty, spec.Zone ?? string.Empty))
            .ToList();
    }
}
