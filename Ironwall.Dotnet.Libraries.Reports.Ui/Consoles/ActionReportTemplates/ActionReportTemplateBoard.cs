using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;

/// <summary>
/// 조치보고 문구 목록의 순서를 쥔 <b>순수</b> 모델(WPF 를 모른다) — <c>Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates.TemplateComponentBoard</c>
/// 와 같은 자리(드래그 판정은 순수 함수로 분리한다 — UIA 에 드래그 패턴이 없어 드래그 자체는 자동화로 단언할 수 없다).
/// </summary>
/// <remarks>
/// <para><b>서버 왕복 0</b> — 로컬에서 옮기는 동안은 아무것도 보내지 않는다. 커밋은
/// <see cref="ActionReportTemplateDropHandler"/> 를 구독하는 뷰모델이 <c>ReorderTemplatesAsync</c> 단일 호출로 한다
/// (드래그 규칙: 행마다 순차 PATCH 반복 금지 — 서버가 배치 엔드포인트를 이미 제공한다).</para>
/// </remarks>
public sealed class ActionReportTemplateBoard
{
    /// <summary>화면에 보이는 순서 그대로.</summary>
    public ObservableCollection<ActionReportTemplateItem> Items { get; } = new();

    /// <summary>순서 · 내용 중 무엇이든 바뀌었다.</summary>
    public event EventHandler? Changed;

    public int Count => Items.Count;

    /// <summary>서버 응답(<c>display_order</c> 오름차순)으로 목록을 다시 세운다.</summary>
    public void Load(IEnumerable<ActionReportTemplateDto> templates)
    {
        Items.Clear();
        foreach (var t in (templates ?? Enumerable.Empty<ActionReportTemplateDto>()).OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id))
            Items.Add(new ActionReportTemplateItem(t.Id, t.Content, t.DisplayOrder));
        Renumber();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>화면 순번(1부터)을 지금 순서대로 다시 매긴다 — 서버의 0부터 세는 <c>display_order</c> 를 화면에 내지 않는다(A1).</summary>
    private void Renumber()
    {
        for (var i = 0; i < Items.Count; i++) Items[i].Position = i + 1;
    }

    /// <summary>서버가 확인해 준 순서로 다시 맞춘다(재정렬 커밋 성공 · 실패 후 재조회 양쪽에서 쓴다).</summary>
    public void ApplyServerOrder(IEnumerable<ActionReportTemplateDto> ordered) => Load(ordered);

    /// <summary>지금 화면 순서대로의 id 목록 — 커밋 실패 시 되돌리기(Undo) 페이로드의 재료.</summary>
    public IReadOnlyList<int> OrderedIds => Items.Select(i => i.Id).ToList();

    /// <summary>
    /// 지금 화면 순서를 서버 <c>POST /reorder</c> 본문으로. <b>화면에 보이는 전체 목록</b>을 싣는다
    /// (부분 목록을 보내면 나머지 행의 <c>display_order</c> 가 무엇으로 남는지 서버 계약이 보장하지 않는다).
    /// </summary>
    public List<ActionReportTemplateReorderItemDto> ToReorderPayload()
        => Items.Select((item, index) => new ActionReportTemplateReorderItemDto { Id = item.Id, DisplayOrder = index }).ToList();

    /// <summary>id 목록(이전 순서)을 <c>POST /reorder</c> 본문으로 — 되돌리기(Undo) 전용.</summary>
    public static List<ActionReportTemplateReorderItemDto> ToReorderPayload(IReadOnlyList<int> orderedIds)
        => orderedIds.Select((id, index) => new ActionReportTemplateReorderItemDto { Id = id, DisplayOrder = index }).ToList();

    /// <summary>
    /// 고른 줄들을 <paramref name="insertionIndex"/> 자리로 옮긴다(옮기기 <b>전</b> 목록 기준 0..Count).
    /// 실제로 자리가 바뀌었으면 참.
    /// </summary>
    public bool Move(IReadOnlyList<int> fromIndexes, int insertionIndex)
    {
        if (fromIndexes is null || fromIndexes.Count == 0) return false;
        if (insertionIndex < 0 || insertionIndex > Items.Count) return false;
        if (fromIndexes.Any(i => i < 0 || i >= Items.Count)) return false;

        var before = Items.Select(i => i.Id).ToList();
        DragMath.MoveMany(Items, fromIndexes, insertionIndex);
        var after = Items.Select(i => i.Id).ToList();
        if (before.SequenceEqual(after)) return false;

        Renumber();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
