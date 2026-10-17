// D-34 action-report template round trip. Wired into Steps.RunAll (Steps.Core.cs) just before the
// cleanup sweep; the sweep (Steps.Cleanup.cs) lists events/action-report-templates and matches on "content".
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    /// <summary>
    /// Action-report template round trip against the REAL loopback server: create 3 (LRT- prefixed,
    /// so the cleanup sweep can find them), reorder via the single <c>POST /reorder</c> call (never
    /// per-row PATCH), re-GET and assert the new order stuck, undo via a <b>second</b> <c>/reorder</c>
    /// call with the previous order, re-GET and assert it's back, delete all three in <c>finally</c>.
    /// </summary>
    public static async Task Item_ActionReportTemplates(Bootstrap boot, Recorder rec, Raw raw)
    {
        const string TAG = "action-report-template";
        var createdIds = new List<int>();
        try
        {
            var api = new ActionReportTemplateApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);

            // ---------- create 3, LRT- prefixed ----------
            boot.Wire.CurrentTag = TAG + "/create";
            var before = rec.LastSeq();
            string[] contents = { "LRT-조치문구-1", "LRT-조치문구-2", "LRT-조치문구-3" };
            foreach (var content in contents)
            {
                var res = await api.CreateTemplateAsync(new ActionReportTemplateCreateDto { Content = content, DisplayOrder = createdIds.Count }).ConfigureAwait(false);
                if (!res.Success || res.Data is null || res.StatusCode != 201)
                {
                    rec.Add("art.0", TAG, "POST 로 문구를 등록(201 확인 — status==200 판정이면 성공을 실패로 표시하는 결함 E4 회귀)",
                        Verdict.FAIL, $"content='{content}' success={res.Success} status={res.StatusCode} msg='{res.Message}'",
                        defectAt: "Reports.Api/Services/ActionReportTemplateApiService.cs CreateTemplateAsync",
                        seqs: SeqsSince(rec, before));
                    return;
                }
                createdIds.Add(res.Data.Id);
                rec.Created("action-report-template", res.Data.Id);
            }
            rec.Add("art.0", TAG, "POST 로 문구 3건을 등록(201)", Verdict.PASS, $"ids={string.Join(",", createdIds)}", seqs: SeqsSince(rec, before));

            // ---------- reorder: single call, full new order (never per-row PATCH) ----------
            boot.Wire.CurrentTag = TAG + "/reorder";
            before = rec.LastSeq();
            var reversed = ((IEnumerable<int>)createdIds).Reverse().ToList();
            var reorderPayload = reversed.Select((id, i) => new ActionReportTemplateReorderItemDto { Id = id, DisplayOrder = i }).ToList();
            var reordered = await api.ReorderTemplatesAsync(reorderPayload).ConfigureAwait(false);
            var (rstatus, rjo) = await raw.Get("events/action-report-templates").ConfigureAwait(false);
            var rarr = rjo["data"] as JArray ?? new JArray();
            var serverOrderAfterReorder = rarr.Select(x => (int)x["id"]!).Where(id => createdIds.Contains(id)).ToList();
            var reorderOk = reordered.Success && rstatus == 200 && serverOrderAfterReorder.SequenceEqual(reversed);
            rec.Add("art.1", TAG, "POST /reorder 단일 호출로 순서가 그대로 뒤집힌다(행마다 PATCH 반복 금지)",
                reorderOk ? Verdict.PASS : Verdict.FAIL,
                $"reorder success={reordered.Success} HTTP re-GET {rstatus}; sent={string.Join(",", reversed)}; server={string.Join(",", serverOrderAfterReorder)}",
                defectAt: reorderOk ? "" : "Reports.Api/Services/ActionReportTemplateApiService.cs ReorderTemplatesAsync",
                seqs: SeqsSince(rec, before));

            // ---------- undo: second /reorder call with the previous order ----------
            boot.Wire.CurrentTag = TAG + "/undo";
            before = rec.LastSeq();
            var undoPayload = createdIds.Select((id, i) => new ActionReportTemplateReorderItemDto { Id = id, DisplayOrder = i }).ToList();
            var undone = await api.ReorderTemplatesAsync(undoPayload).ConfigureAwait(false);
            var (ustatus, ujo) = await raw.Get("events/action-report-templates").ConfigureAwait(false);
            var uarr = ujo["data"] as JArray ?? new JArray();
            var serverOrderAfterUndo = uarr.Select(x => (int)x["id"]!).Where(id => createdIds.Contains(id)).ToList();
            var undoOk = undone.Success && ustatus == 200 && serverOrderAfterUndo.SequenceEqual(createdIds);
            rec.Add("art.2", TAG, "되돌리기 = 두 번째 POST /reorder 호출(직전 순서를 그대로 다시 보낸다)",
                undoOk ? Verdict.PASS : Verdict.FAIL,
                $"undo success={undone.Success} HTTP re-GET {ustatus}; expected={string.Join(",", createdIds)}; server={string.Join(",", serverOrderAfterUndo)}",
                defectAt: undoOk ? "" : "Reports.Api/Services/ActionReportTemplateApiService.cs ReorderTemplatesAsync",
                seqs: SeqsSince(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("art!", TAG, "action report template round trip", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
        finally
        {
            foreach (var id in createdIds)
            {
                var (s, _) = await raw.Delete($"events/action-report-templates/{id}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("action-report-template", id);
                else rec.Leftover("action-report-template", id, $"DELETE {s}");
            }
        }
    }
}
