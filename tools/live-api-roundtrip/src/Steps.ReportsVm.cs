// 보고서 · 조치보고 문구 창의 뷰모델 경로를 실제 루프백 서버에 붙여 왕복한다(U-15, 확장 점검 "reports-vm").
//
// 원칙: 단언은 "창이 실제로 부르는 뷰모델 메서드 → 실제 서비스 → 와이어 → 재조회" 로 잡는다.
//   arrange(고정물 만들기)와 재조회만 Raw 로 한다 — 서비스 자리를 Raw 가 대신하지 않는다.
// 이름 접두: LRT-RPT- / lrt_rpt_ — 정리 스윕(item 9)이 LRT-/lrt_ 로 잡는다. 생성 이력은 스윕 대상에 아직 없다
//   (finally 에서 지우고, 남으면 Leftover 로 적는다).
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;
using Newtonsoft.Json.Linq;
using VmMsg = Ironwall.Dotnet.Libraries.ViewModel.Models;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    const string RPT_USER_ID = "lrt_rpt_editonly";
    const string RPT_USER_PW = "LrtRptEdit1!x";

    // ======================================================================================
    // 조치보고 문구 콘솔 — 뷰모델 경로(MoveSelected · UndoReorderAsync · ApplyAsync)
    // ======================================================================================
    [ExtraStep("reports-vm", 10)]
    static async Task ReportsVm_ActionReportTemplates(ExtraContext ctx)
    {
        const string TAG = "rpt-vm/art";
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        var created = new List<int>();
        List<(int Id, int Order)> snapshot = null;
        try
        {
            // ---- 전 행 순서 스냅샷(콘솔이 목록 전체를 다시 번호 매기므로 끝나면 되돌린다) ----
            var (ss, sj) = await raw.Get("events/action-report-templates").ConfigureAwait(false);
            if (ss != 200)
            {
                rec.Add("rv.art.0", TAG, "arrange: 문구 전 행 스냅샷", Verdict.BLOCKED, $"HTTP {ss} {Short(sj)}", blocked: "list endpoint unavailable");
                return;
            }
            snapshot = (sj["data"] as JArray ?? new JArray()).Select(x => ((int)x["id"], (int)x["display_order"])).ToList();
            var baseOrder = snapshot.Count == 0 ? 0 : snapshot.Max(s => s.Order) + 1;
            for (var i = 1; i <= 3; i++)
            {
                var (cs, cj) = await raw.Post("events/action-report-templates", new { content = $"LRT-RPT-ART-{i}", display_order = baseOrder + i - 1 }).ConfigureAwait(false);
                if (cs != 201 && cs != 200) { rec.Add("rv.art.0", TAG, "arrange: 문구 3건", Verdict.BLOCKED, $"HTTP {cs} {Short(cj)}", blocked: "fixture setup failed"); return; }
                var id = (int)cj["data"]["id"];
                created.Add(id); rec.Created("action-report-template", id);
            }

            // ---- 창과 똑같이 세운다: 실제 서비스 + 실제 권한 서비스(관리자) + 활성화(=LoadAsync) ----
            var api = new ActionReportTemplateApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var vm = new ActionReportTemplateConsoleViewModel(new EventAggregator(), boot.Log, AdminPermission(), api);
            boot.Wire.CurrentTag = TAG + "/load";
            await ((IActivate)vm).ActivateAsync().ConfigureAwait(false);
            var loadedOk = created.All(id => vm.Items.Any(i => i.Id == id)) && vm.Items.Count == snapshot.Count + created.Count;
            if (!loadedOk)
            {
                rec.Add("rv.art.0", TAG, "콘솔 활성화가 서버 목록 전체를 적재", Verdict.FAIL,
                    $"vm items={vm.Items.Count} expected={snapshot.Count + created.Count}; LoadError='{vm.LoadError}'",
                    defectAt: "Reports.Ui/ViewModels/Panels/ActionReportTemplateConsoleViewModel.cs LoadAsync");
                return;
            }

            // ---- 2a: ▲ 단추 경로(MoveSelected) = 드래그와 같은 담당 → /reorder 1건 · 전체 id · 0..n-1 ----
            var target = vm.Items.First(i => i.Id == created[2]);
            vm.OnRowSelected(target);
            var beforeOrder = vm.Board.OrderedIds.ToList();
            boot.Wire.CurrentTag = TAG + "/move";
            var before = rec.LastSeq();
            vm.MoveSelected(target, -1);
            await WaitUntil(() => rec.Since(before).Any(w => w.Uri.Contains("/reorder")) && !vm.IsReordering).ConfigureAwait(false);
            var moveWire = rec.Since(before).ToList();
            var reorderCalls = moveWire.Where(w => w.Method == "POST" && w.Uri.Contains("/reorder")).ToList();
            var patchCalls = moveWire.Count(w => w.Method == "PATCH");
            var sent = reorderCalls.Count == 1 ? ReorderItems(reorderCalls[0].RequestBody) : new List<(int Id, int Order)>();
            var boardAfterMove = vm.Board.OrderedIds.ToList();
            var serverAfterMove = await ArtServerOrder(raw).ConfigureAwait(false);
            var moved = !boardAfterMove.SequenceEqual(beforeOrder);
            var sentIds = sent.Select(s => s.Id).ToList();
            var renumbered = sent.Select(s => s.Order).SequenceEqual(Enumerable.Range(0, sent.Count));
            var ok2a = moved && reorderCalls.Count == 1 && patchCalls == 0
                       && sentIds.Count == boardAfterMove.Count && sentIds.SequenceEqual(boardAfterMove) && renumbered
                       && serverAfterMove.SequenceEqual(boardAfterMove);
            rec.Add("rv.art.1", TAG, "MoveSelected(▲) → POST /reorder 정확히 1건(전체 id · 0..n-1 재번호) · PATCH 0건 · 재조회 순서 = 보드",
                ok2a ? Verdict.PASS : Verdict.FAIL,
                $"reorder calls={reorderCalls.Count} (HTTP {string.Join(",", reorderCalls.Select(w => w.Status))}) PATCH={patchCalls}; " +
                $"sent {sent.Count}건 ids=[{string.Join(",", sentIds)}] orders 0..n-1={renumbered}; board=[{string.Join(",", boardAfterMove)}]; server=[{string.Join(",", serverAfterMove)}]; before=[{string.Join(",", beforeOrder)}]",
                defectAt: ok2a ? "" : "Reports.Ui/ViewModels/Panels/ActionReportTemplateConsoleViewModel.cs MoveSelected/CommitReorderAsync",
                seqs: SeqsSince(rec, before));

            // ---- 2a': 되돌리기 = 두 번째 /reorder, 재조회가 직전 순서로 ----
            boot.Wire.CurrentTag = TAG + "/undo";
            var canUndo = vm.CanUndoReorder;
            before = rec.LastSeq();
            await vm.UndoReorderAsync().ConfigureAwait(false);
            var undoWire = rec.Since(before).ToList();
            var undoReorders = undoWire.Count(w => w.Method == "POST" && w.Uri.Contains("/reorder"));
            var serverAfterUndo = await ArtServerOrder(raw).ConfigureAwait(false);
            var ok2u = canUndo && undoReorders == 1 && undoWire.Count(w => w.Method == "PATCH") == 0
                       && serverAfterUndo.SequenceEqual(beforeOrder) && vm.Board.OrderedIds.SequenceEqual(beforeOrder) && !vm.CanUndoReorder;
            rec.Add("rv.art.2", TAG, "UndoReorderAsync → 두 번째 POST /reorder 1건, 재조회 순서가 옮기기 전으로",
                ok2u ? Verdict.PASS : Verdict.FAIL,
                $"CanUndoReorder(before)={canUndo} reorder calls={undoReorders}; server=[{string.Join(",", serverAfterUndo)}] expected=[{string.Join(",", beforeOrder)}]; CanUndoReorder(after)={vm.CanUndoReorder}",
                defectAt: ok2u ? "" : "Reports.Ui/ViewModels/Panels/ActionReportTemplateConsoleViewModel.cs UndoReorderAsync",
                seqs: SeqsSince(rec, before));

            // ---- 2b: 내용 수정 → PATCH 본문이 정확히 {"content": "..."} ----
            boot.Wire.CurrentTag = TAG + "/edit";
            const string EDITED = "LRT-RPT-ART-1-edited";
            vm.OnRowSelected(vm.Items.First(i => i.Id == created[0]));
            vm.DraftContent = EDITED;
            before = rec.LastSeq();
            await vm.ApplyAsync().ConfigureAwait(false);
            var editWire = rec.Since(before).Where(w => w.Method == "PATCH").ToList();
            var bodyKeys = editWire.Count == 1 ? BodyKeys(editWire[0].RequestBody) : new List<string>();
            var bodyContent = editWire.Count == 1 ? (string)JObject.Parse(editWire[0].RequestBody)["content"] : null;
            var (gs, gj) = await raw.Get($"events/action-report-templates/{created[0]}").ConfigureAwait(false);
            var serverContent = Str(gj["data"]?["content"]);
            var ok2b = editWire.Count == 1 && editWire[0].Status == 200 && bodyKeys.SequenceEqual(new[] { "content" })
                       && bodyContent == EDITED && gs == 200 && serverContent == EDITED;
            rec.Add("rv.art.3", TAG, "ApplyAsync(수정) → PATCH 1건, 본문이 정확히 {\"content\"} · 재조회 반영",
                ok2b ? Verdict.PASS : Verdict.FAIL,
                $"PATCH calls={editWire.Count} HTTP {string.Join(",", editWire.Select(w => w.Status))}; body={Recorder.Trunc(editWire.FirstOrDefault()?.RequestBody ?? "(none)", 200)}; " +
                $"server content='{serverContent}'; footer='{vm.Detail.LastMessage}'",
                defectAt: ok2b ? "" : "Reports.Ui/ViewModels/Panels/ActionReportTemplateConsoleViewModel.cs ApplyAsync / ActionReportTemplateUpdateDto",
                seqs: SeqsSince(rec, before));

            // ---- 2b': 다른 곳에서 같은 문구가 막 생겼다 → 서버 409 → 창이 "이미 등록된 문구입니다." ----
            boot.Wire.CurrentTag = TAG + "/dup";
            const string DUP = "LRT-RPT-ART-DUP";
            var (ds, dj) = await raw.Post("events/action-report-templates", new { content = DUP, display_order = baseOrder + 10 }).ConfigureAwait(false);
            if (ds == 201 || ds == 200) { var did = (int)dj["data"]["id"]; created.Add(did); rec.Created("action-report-template", did); }
            vm.OnRowSelected(vm.Items.First(i => i.Id == created[1]));
            vm.DraftContent = DUP;
            var clientBlocked = vm.DraftValidationError;   // 창은 아직 그 문구를 모른다 — 서버가 판정해야 한다
            before = rec.LastSeq();
            await vm.ApplyAsync().ConfigureAwait(false);
            var dupWire = rec.Since(before).Where(w => w.Method == "PATCH").ToList();
            var (g2s, g2j) = await raw.Get($"events/action-report-templates/{created[1]}").ConfigureAwait(false);
            var unchanged = Str(g2j["data"]?["content"]) == "LRT-RPT-ART-2";
            var ok409 = (ds == 201 || ds == 200) && clientBlocked == null && dupWire.Count == 1 && dupWire[0].Status == 409
                        && vm.Detail.LastMessage == "이미 등록된 문구입니다." && unchanged;
            rec.Add("rv.art.4", TAG, "중복 문구 PATCH → 서버 409 → 창 문구 '이미 등록된 문구입니다.' · 서버 값 그대로",
                ok409 ? Verdict.PASS : Verdict.FAIL,
                $"arrange dup HTTP {ds}; client pre-check='{clientBlocked}'; PATCH HTTP {string.Join(",", dupWire.Select(w => w.Status))}; footer='{vm.Detail.LastMessage}'; server unchanged={unchanged}",
                defectAt: ok409 ? "" : "Reports.Ui/ViewModels/Panels/ActionReportTemplateConsoleViewModel.cs MapError / Reports.Api ToWriteResponseAsync",
                seqs: SeqsSince(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("rv.art!", TAG, "action-report template VM round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            // 원래 display_order 를 그대로 되돌린다(서버 /reorder 는 부분 집합을 받는다).
            if (snapshot is { Count: > 0 })
            {
                var (rs, rj) = await raw.Post("events/action-report-templates/reorder",
                    new { items = snapshot.Select(s => new { id = s.Id, display_order = s.Order }).ToList() }).ConfigureAwait(false);
                var (vs, vj) = await raw.Get("events/action-report-templates").ConfigureAwait(false);
                var now = (vj["data"] as JArray ?? new JArray()).ToDictionary(x => (int)x["id"], x => (int)x["display_order"]);
                var drift = snapshot.Where(s => !now.TryGetValue(s.Id, out var o) || o != s.Order).ToList();
                rec.Add("rv.art.9", TAG, "기존 문구 display_order 원복(스냅샷 그대로)",
                    rs == 200 && vs == 200 && drift.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                    $"restore HTTP {rs}; drift {drift.Count}건 [{string.Join(",", drift.Select(d => $"{d.Id}:{d.Order}"))}] {(rs == 200 ? "" : Short(rj))}",
                    defectAt: drift.Count == 0 ? "" : "harness restore (not a product defect)");
            }
            foreach (var id in created)
            {
                var (s, _) = await raw.Delete($"events/action-report-templates/{id}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("action-report-template", id); else rec.Leftover("action-report-template", id, $"DELETE {s}");
            }
        }
    }

    // ======================================================================================
    // 보고서 템플릿 — default_period null 이 서비스 · 창까지 그대로 오는가 + 목록 역직렬화
    // ======================================================================================
    [ExtraStep("reports-vm", 20)]
    static async Task ReportsVm_Templates(ExtraContext ctx)
    {
        const string TAG = "rpt-vm/tpl";
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        int tplId = 0;
        try
        {
            // arrange: default_period 없이 만든다(서버는 "7d" 로 채운다) → PATCH 로 명시적 null(서버 DB 는 nullable)
            var (cs, cj) = await raw.Post("reports/templates", new
            {
                name = "LRT-RPT-NULLPERIOD",
                description = "reports-vm null period",
                report_type = "CUSTOM",
                components = new object[] { new { id = "SUMMARY_CARD", order = 0, enabled = true } },
            }).ConfigureAwait(false);
            if (cs != 201 && cs != 200) { rec.Add("rv.tpl.0", TAG, "arrange template", Verdict.BLOCKED, $"HTTP {cs} {Short(cj)}", blocked: "fixture setup failed"); return; }
            tplId = (int)cj["data"]["id"];
            rec.Created("report-template", tplId);
            var createdPeriod = J(cj["data"]["default_period"]);
            var (ps, pj) = await raw.Patch($"reports/templates/{tplId}", new JObject { ["default_period"] = JValue.CreateNull() }).ConfigureAwait(false);
            var (rs, rj) = await raw.Get($"reports/templates/{tplId}").ConfigureAwait(false);
            var rawPeriod = rj["data"]?["default_period"];
            if (ps != 200 || rawPeriod == null || rawPeriod.Type != JTokenType.Null)
            {
                rec.Add("rv.tpl.0", TAG, "arrange: default_period null 인 템플릿", Verdict.BLOCKED,
                    $"create default_period={createdPeriod}; PATCH null HTTP {ps}; re-GET default_period={J(rawPeriod)}", blocked: "server did not store null");
                return;
            }

            // ---- 3a: 서비스 GET(단건 · 목록)이 null 을 null 로 준다 ----
            boot.Wire.CurrentTag = TAG + "/get";
            var api = new ReportApiService(boot.Log, boot.Api, boot.Setup);
            var before = rec.LastSeq();
            var one = await api.GetTemplateByIdAsync(tplId).ConfigureAwait(false);
            var list = await api.GetTemplatesAsync(1, 100).ConfigureAwait(false);
            var listed = list.Data?.FirstOrDefault(t => t.Id == tplId);
            var ok3a = one.Success && one.Data != null && one.Data.DefaultPeriod == null && listed != null && listed.DefaultPeriod == null;
            rec.Add("rv.tpl.1", TAG, "default_period=null 이 서비스 GET(단건 · 목록)에서 null 로 남는다(\"7d\" 로 둔갑하지 않는다)",
                ok3a ? Verdict.PASS : Verdict.FAIL,
                $"server(raw)={J(rawPeriod)} (created with omitted key → {createdPeriod}); GetTemplateByIdAsync.DefaultPeriod={Q(one.Data?.DefaultPeriod)}; GetTemplatesAsync row.DefaultPeriod={Q(listed?.DefaultPeriod)}",
                defectAt: ok3a ? "" : "Messages/Dto/Reports/ReportTemplateDto.cs:52 DefaultPeriod 기본값 \"7d\" + ApiMessageHelper NullValueHandling.Ignore(역직렬화가 JSON null 을 건너뛴다)",
                seqs: SeqsSince(rec, before));

            // ---- 4(템플릿): 목록 역직렬화 — pagination.total · component_count · default_period 전 행 ----
            var (ls, lj) = await raw.Get("reports/templates?page=1&limit=100").ConfigureAwait(false);
            var rawRows = (lj["data"] as JArray ?? new JArray()).OfType<JObject>().ToDictionary(x => (int)x["id"], x => x);
            var rawTotal = (int?)(lj["pagination"] as JObject)?["total"];
            var mism = new List<string>();
            foreach (var t in list.Data ?? new List<ReportTemplateDto>())
            {
                if (!rawRows.TryGetValue(t.Id, out var r)) { mism.Add($"{t.Id}:not-in-raw"); continue; }
                if (t.ComponentCount != (int?)r["component_count"]) mism.Add($"{t.Id}:component_count {t.ComponentCount}!={J(r["component_count"])}");
                if (t.EffectiveComponentCount != ((int?)r["component_count"] ?? -1)) mism.Add($"{t.Id}:EffectiveComponentCount {t.EffectiveComponentCount}");
                var rp = r["default_period"];
                var rawP = rp == null || rp.Type == JTokenType.Null ? null : (string)rp;
                if (t.DefaultPeriod != rawP) mism.Add($"{t.Id}:default_period {Q(t.DefaultPeriod)}!={Q(rawP)}");
                if (t.Name != Str(r["name"])) mism.Add($"{t.Id}:name");
            }
            var total = list.Pagination?.Total ?? list.Total;
            var ok4t = ls == 200 && list.Success && total == rawTotal && (list.Data?.Count ?? -1) == rawRows.Count && mism.Count == 0;
            rec.Add("rv.tpl.2", TAG, "GetTemplatesAsync 역직렬화 — pagination.total · 행 수 · component_count · default_period 가 서버와 일치",
                ok4t ? Verdict.PASS : Verdict.FAIL,
                $"service total={total} rows={list.Data?.Count}; raw total={rawTotal} rows={rawRows.Count}; mismatches=[{string.Join("; ", mism)}]",
                defectAt: ok4t ? "" : "Messages/Dto/Reports/ReportTemplateDto.cs / Reports.Api ReportApiService.GetTemplatesAsync");

            // ---- 3a': 창 — 템플릿 레일 목록 칸 표시 + 편집 칸 + 손대지 않은 null 은 PATCH 에 안 실린다 ----
            boot.Wire.CurrentTag = TAG + "/console";
            var console = BuildReportConsole(new EventAggregator(), boot.Log, AdminPermission(), api);
            await ((IActivate)console).ActivateAsync().ConfigureAwait(false);
            await console.SelectRailAsync(ReportConsoleRails.Template).ConfigureAwait(false);
            var row = console.TemplateViewModel.Rows.FirstOrDefault(t => t.Id == tplId);
            var shown = row == null ? "(row missing)" : (string)new PeriodCodeConverter().Convert(row.DefaultPeriod, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
            if (row != null) console.OnRowSelected(row);
            await WaitUntil(() => console.EditViewModel.TemplateId == tplId && console.EditViewModel.CanEdit).ConfigureAwait(false);
            var selectedPeriod = console.EditViewModel.SelectedPeriod?.Value;
            var honest = shown == "지정 안 함" && selectedPeriod == null;
            rec.Add("rv.tpl.3", TAG, "창: 기본기간 null 을 '지정 안 함' 으로 보이고 편집 칸도 임의 기간을 고르지 않는다",
                honest ? Verdict.PASS : Verdict.FAIL,
                $"list cell='{shown}'; edit SelectedPeriod={Q(selectedPeriod)}",
                defectAt: honest ? "" : "Reports.Ui/Views/Panels/ReportConsoleView.xaml.cs PeriodCodeConverter · ViewModels/Panels/ReportTemplateEditViewModel.cs LoadAsync(_originalPeriod ?? \"7d\")");

            console.EditViewModel.Name = "LRT-RPT-NULLPERIOD-renamed";
            before = rec.LastSeq();
            await console.ApplyAsync().ConfigureAwait(false);
            var patch = rec.Since(before).Where(w => w.Method == "PATCH").ToList();
            var keys = patch.Count == 1 ? BodyKeys(patch[0].RequestBody) : new List<string>();
            var (as_, aj) = await raw.Get($"reports/templates/{tplId}").ConfigureAwait(false);
            var afterPeriod = aj["data"]?["default_period"];
            var okPatch = patch.Count == 1 && patch[0].Status == 200 && keys.SequenceEqual(new[] { "name" })
                          && afterPeriod != null && afterPeriod.Type == JTokenType.Null
                          && Str(aj["data"]?["name"]) == "LRT-RPT-NULLPERIOD-renamed";
            rec.Add("rv.tpl.4", TAG, "창 [적용](이름만 고침) → PATCH 본문 {\"name\"} 뿐 · default_period 는 서버에서 여전히 null",
                okPatch ? Verdict.PASS : Verdict.FAIL,
                $"PATCH calls={patch.Count} HTTP {string.Join(",", patch.Select(w => w.Status))} body={Recorder.Trunc(patch.FirstOrDefault()?.RequestBody ?? "(none)", 200)}; server default_period={J(afterPeriod)} name='{Str(aj["data"]?["name"])}'",
                defectAt: okPatch ? "" : "Reports.Ui/ViewModels/Panels/ReportTemplateEditViewModel.cs ApplyAsync(손댄 칸만)",
                seqs: SeqsSince(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("rv.tpl!", TAG, "report template VM round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            if (tplId > 0)
            {
                var (s, _) = await raw.Delete($"reports/templates/{tplId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("report-template", tplId); else rec.Leftover("report-template", tplId, $"DELETE {s}");
            }
        }
    }

    // ======================================================================================
    // 생성 이력 — 편집만 가진 사용자의 삭제 · 취소 게이트(서버 403 대조) · 취소 사유 표시 · 목록 역직렬화
    // ======================================================================================
    [ExtraStep("reports-vm", 30)]
    static async Task ReportsVm_GenerationsAndPermissions(ExtraContext ctx)
    {
        const string TAG = "rpt-vm/gen";
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        int genId = 0, groupId = 0, userId = 0, tplId = 0;
        try
        {
            // ---------- arrange: reports view+edit 만 있는(delete 없음) 그룹 · 사용자 · 템플릿 ----------
            var (gs, gj) = await raw.Post("user-groups", new { name = "LRT-RPT-EDITONLY-GRP", description = "reports edit without delete" }).ConfigureAwait(false);
            if (gs != 200 && gs != 201) { rec.Add("rv.gen.0", TAG, "arrange user-group", Verdict.BLOCKED, $"HTTP {gs} {Short(gj)}", blocked: "fixture setup failed"); return; }
            groupId = (int)gj["data"]["id"]; rec.Created("user-group", groupId);

            var mods = new JObject();
            foreach (var m in ALL_MODULES)
                mods[m] = new JObject { ["view"] = m == "reports", ["edit"] = m == "reports", ["delete"] = false, ["control"] = false };
            var (ms, mj) = await raw.Post($"user-groups/{groupId}/permissions", new JObject { ["modules"] = mods }).ConfigureAwait(false);
            if (ms != 200 && ms != 201) { rec.Add("rv.gen.0", TAG, "arrange group permissions", Verdict.BLOCKED, $"HTTP {ms} {Short(mj)}", blocked: "fixture setup failed"); return; }

            var (us, uj) = await raw.Post("users", new { login_id = RPT_USER_ID, password = RPT_USER_PW, name = "LRT 보고서 편집 전용", role = "USER" }).ConfigureAwait(false);
            if (us != 200 && us != 201) { rec.Add("rv.gen.0", TAG, "arrange user", Verdict.BLOCKED, $"HTTP {us} {Short(uj)}", blocked: "fixture setup failed"); return; }
            userId = (int)uj["data"]["id"]; rec.Created("user", userId);
            var assign = await boot.AccountApi.AssignUserGroupAsync(userId, groupId).ConfigureAwait(false);
            if (!assign.Success) { rec.Add("rv.gen.0", TAG, "arrange assign group", Verdict.BLOCKED, assign.Message, blocked: "fixture setup failed"); return; }

            var (ts, tj) = await raw.Post("reports/templates", new
            {
                name = "LRT-RPT-PERM-TPL",
                report_type = "CUSTOM",
                default_period = "7d",
                components = new object[] { new { id = "SUMMARY_CARD", order = 0, enabled = true } },
            }).ConfigureAwait(false);
            if (ts != 200 && ts != 201) { rec.Add("rv.gen.0", TAG, "arrange template", Verdict.BLOCKED, $"HTTP {ts} {Short(tj)}", blocked: "fixture setup failed"); return; }
            tplId = (int)tj["data"]["id"]; rec.Created("report-template", tplId);

            // 두 번째 세션 파이프라인(자기 토큰 · 자기 와이어 큐). 서버의 세션 축출은 user_id 단위라
            // 다른 계정으로 로그인해도 하네스 관리자 세션은 그대로다(auth.py login: _active_sessions 는 같은 user_id 만).
            var pipe = UserPipe.Create(boot.Log);
            var login = await pipe.Account.LoginAsync(RPT_USER_ID, RPT_USER_PW).ConfigureAwait(false);
            if (!login.Success || login.Data?.User == null)
            {
                rec.Add("rv.gen.0", TAG, "arrange: 편집 전용 사용자 로그인", Verdict.BLOCKED, $"success={login.Success} msg='{login.Message}'", blocked: "second-user login failed");
                return;
            }
            pipe.Tokens.SetTokens(login.Data.AccessToken, login.Data.RefreshToken, login.Data.SessionId);
            var userPerm = new PermissionService();
            userPerm.Apply(login.Data.User);
            if (!userPerm.CanEdit("reports") || userPerm.CanDelete("reports"))
            {
                rec.Add("rv.gen.0", TAG, "arrange: 로그인 권한 = reports edit O / delete X", Verdict.BLOCKED,
                    $"CanEdit={userPerm.CanEdit("reports")} CanDelete={userPerm.CanDelete("reports")}", blocked: "fixture permissions not as intended");
                return;
            }

            // 진행 중 행이 있어야 [취소] 게이트를 볼 수 있다 — 사용자 준비를 끝낸 뒤 바로 요청한다.
            var (ns, nj) = await raw.Post("reports/generate", new { report_type = "STANDARD", title = "LRT-RPT-GEN", period_type = "7d" }).ConfigureAwait(false);
            if (ns != 202 && ns != 200 && ns != 201) { rec.Add("rv.gen.0", TAG, "arrange generation", Verdict.BLOCKED, $"HTTP {ns} {Short(nj)}", blocked: "generate endpoint refused"); return; }
            genId = (int)nj["data"]["id"]; rec.Created("report-generation", genId);

            // ---------- 1: 편집 전용 사용자의 창 — 삭제 · 취소를 내밀지 않고, 눌러도 와이어에 파괴 요청 0 ----------
            var uApi = new ReportApiService(boot.Log, pipe.Api, pipe.Setup);
            var uEvents = new EventAggregator();
            var uSpy = new ConfirmSpy();
            uEvents.SubscribeOnPublishedThread(uSpy);
            var uConsole = BuildReportConsole(uEvents, boot.Log, userPerm, uApi);
            await ((IActivate)uConsole).ActivateAsync().ConfigureAwait(false);
            var uRow = uConsole.ListViewModel.Rows.FirstOrDefault(r => r.Id == genId);
            var wasInProgress = uRow?.IsInProgress == true;
            if (uRow != null) uConsole.OnRowSelected(uRow);
            var gateCancel = uConsole.CanCancelGeneration;
            var gateDeleteGen = uConsole.CanDeleteGeneration;
            var uWireBefore = pipe.Wire.Count;
            var confirmsBefore = uSpy.Count;
            if (wasInProgress)
            {
                await uConsole.CancelGenerationAsync().ConfigureAwait(false);
                if (uSpy.Count > confirmsBefore)   // 확인 창이 떴다면 사용자가 [확인] 을 누른 것으로 친다
                    await uConsole.ListViewModel.HandleAsync(new VmMsg.CallCancelReportGenerationProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);
            }
            confirmsBefore = uSpy.Count;
            await uConsole.DeleteGenerationAsync().ConfigureAwait(false);
            if (uSpy.Count > confirmsBefore)
                await uConsole.ListViewModel.HandleAsync(new VmMsg.CallDeleteReportGenerationProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);

            await uConsole.SelectRailAsync(ReportConsoleRails.Template).ConfigureAwait(false);
            var uTpl = uConsole.TemplateViewModel.Rows.FirstOrDefault(t => t.Id == tplId);
            if (uTpl != null) uConsole.OnRowSelected(uTpl);
            await WaitUntil(() => uConsole.EditViewModel.TemplateId == tplId && uConsole.EditViewModel.CanEdit).ConfigureAwait(false);
            var gateDeleteTpl = uConsole.CanDelete;
            confirmsBefore = uSpy.Count;
            await uConsole.DeleteAsync().ConfigureAwait(false);
            if (uSpy.Count > confirmsBefore)
                await uConsole.TemplateViewModel.HandleAsync(new VmMsg.CallDeleteReportTemplateProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);

            var destructive = pipe.Wire.Skip(uWireBefore)
                .Where(w => w.Method == "DELETE" || (w.Method == "POST" && w.Uri.Contains("/cancel"))).ToList();
            var ok1 = !gateDeleteGen && !gateDeleteTpl && (!wasInProgress || !gateCancel) && destructive.Count == 0;
            rec.Add("rv.gen.1", TAG, "편집 전용(reports edit O · delete X) 사용자의 창: [삭제] · [취소] 가 꺼지고 눌러도 파괴 요청 0건",
                ok1 ? Verdict.PASS : Verdict.FAIL,
                $"CanDeleteGeneration={gateDeleteGen} CanCancelGeneration={gateCancel} (row in progress={wasInProgress}) CanDelete(template)={gateDeleteTpl}; " +
                $"confirm popups={uSpy.Count}; destructive requests from this user's window: " +
                (destructive.Count == 0 ? "none" : string.Join(" | ", destructive.Select(w => $"{w.Method} {ShortUri(w.Uri)} -> HTTP {w.Status}"))),
                defectAt: ok1 ? "" : "Reports.Ui/ViewModels/Panels/ReportConsoleViewModel.cs:298 CanDelete · :471 CanCancelGeneration · :472 CanDeleteGeneration (reports:edit 로 게이트 — 서버는 reports:delete)");

            // ---------- 1': 서버 대조 — 같은 사용자의 실제 서비스 호출: edit 는 200, delete/cancel 은 403 ----------
            var upd = await uApi.UpdateTemplateAsync(tplId, new ReportTemplateUpdateDto { Description = "edit-only user edit" }).ConfigureAwait(false);
            var delTpl = await uApi.DeleteTemplateAsync(tplId).ConfigureAwait(false);
            var delGen = await uApi.DeleteGenerationAsync(genId).ConfigureAwait(false);
            var canGen = await uApi.CancelGenerationAsync(genId).ConfigureAwait(false);
            var (t2s, _) = await raw.Get($"reports/templates/{tplId}").ConfigureAwait(false);
            var (g2s, _) = await raw.Get($"reports/generations/{genId}").ConfigureAwait(false);
            var ok1s = upd.Success && delTpl.StatusCode == 403 && delGen.StatusCode == 403 && canGen.StatusCode == 403 && t2s == 200 && g2s == 200;
            rec.Add("rv.gen.2", TAG, "서버 대조: 편집 전용 사용자 — PATCH 템플릿 200 · DELETE 템플릿/생성 · POST cancel 은 403(reports:delete)",
                ok1s ? Verdict.PASS : Verdict.FAIL,
                $"PATCH template HTTP {upd.StatusCode}; DELETE template HTTP {delTpl.StatusCode}; DELETE generation HTTP {delGen.StatusCode}; POST cancel HTTP {canGen.StatusCode}; " +
                $"admin re-GET template {t2s} generation {g2s}",
                defectAt: ok1s ? "" : "server contract changed (api-test-server routers/reports.py:470/1043/1090) — re-check the gate verb");

            // ---------- 3b: 관리자 창에서 취소 → 서버가 준 한국어 사유를 상세 칸이 그대로 보인다 ----------
            boot.Wire.CurrentTag = TAG + "/cancel";
            var api = new ReportApiService(boot.Log, boot.Api, boot.Setup);
            var aEvents = new EventAggregator();
            var aSpy = new ConfirmSpy();
            aEvents.SubscribeOnPublishedThread(aSpy);
            var aConsole = BuildReportConsole(aEvents, boot.Log, AdminPermission(), api);
            await ((IActivate)aConsole).ActivateAsync().ConfigureAwait(false);
            var aRow = aConsole.ListViewModel.Rows.FirstOrDefault(r => r.Id == genId);
            if (aRow?.IsInProgress == true)
            {
                aConsole.OnRowSelected(aRow);
                var before = rec.LastSeq();
                var c0 = aSpy.Count;
                await aConsole.CancelGenerationAsync().ConfigureAwait(false);
                if (aSpy.Count > c0)
                    await aConsole.ListViewModel.HandleAsync(new VmMsg.CallCancelReportGenerationProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);
                var cancelWire = rec.Since(before).FirstOrDefault(w => w.Method == "POST" && w.Uri.Contains("/cancel"));
                var (rs, rj) = await raw.Get($"reports/generations/{genId}").ConfigureAwait(false);
                var rawErr = Str(rj["data"]?["error_message"]);
                var cancelled = aConsole.ListViewModel.Rows.FirstOrDefault(r => r.Id == genId);
                if (cancelled != null) aConsole.OnRowSelected(cancelled);
                var shown = aConsole.PreviewViewModel.FailureText;
                var ok3b = cancelWire?.Status == 200 && cancelled?.Status == "CANCELLED" && rawErr.Length > 0
                           && cancelled.Dto.ErrorMessage == rawErr && shown.Contains(rawErr, StringComparison.Ordinal);
                rec.Add("rv.gen.3", TAG, "관리자 창 [취소] → CANCELLED · 서버 error_message(한국어 사유)를 상세 칸이 보인다",
                    ok3b ? Verdict.PASS : Verdict.FAIL,
                    $"cancel HTTP {cancelWire?.Status}; row status={cancelled?.Status}; server error_message='{rawErr}'; dto.ErrorMessage='{cancelled?.Dto.ErrorMessage}'; detail FailureText='{shown}' HasFailure={aConsole.PreviewViewModel.HasFailure}",
                    defectAt: ok3b ? "" : "Reports.Ui/Consoles/Lists/ReportGenerationRow.cs:136 FailureText (CANCELLED 은 사유를 버린다)",
                    seqs: SeqsSince(rec, before));

                // 서버 쪽 관찰 — 창은 받은 글을 그대로 보일 뿐이다. 사용자 취소가 "내부 오류" 로 적히면 서버 결함이다.
                if (cancelled?.Status == "CANCELLED")
                    rec.Add("rv.gen.3i", TAG, "관찰(서버): 사용자 취소에 서버가 붙인 사유 문구", Verdict.INFO,
                        rawErr.Contains("취소", StringComparison.Ordinal)
                            ? $"server reason='{rawErr}'"
                            : $"server labels a USER cancel as '{rawErr}'. The cancel endpoint writes 'Cancelled by user <login>' " +
                              "(api-test-server app/routers/reports.py:1141), then the worker's CancelledError handler overwrites it with " +
                              "'Cancelled by user' (reports.py:800, no trailing login), which misses _CANCELLED_PREFIX 'Cancelled by user ' " +
                              "(reports.py:535) and falls through to the internal-error fallback. Server defect - the window shows it verbatim.");
            }
            else
            {
                rec.Add("rv.gen.3", TAG, "관리자 창 [취소] → CANCELLED · 사유 표시", Verdict.BLOCKED,
                    $"generation {genId} was already {aRow?.Status ?? "(missing)"} before the window could cancel it",
                    blocked: "server finished the generation before cancel - timing, not a product defect");
            }

            // ---------- 4(생성 이력): 서비스 역직렬화 — total · 상태 어휘 · error_message · pdf_download_url ----------
            boot.Wire.CurrentTag = TAG + "/list";
            var gl = await api.GetGenerationsAsync(1, 100).ConfigureAwait(false);
            var (ls, lj) = await raw.Get("reports/generations?page=1&limit=100").ConfigureAwait(false);
            var rawRows = (lj["data"] as JArray ?? new JArray()).OfType<JObject>().ToDictionary(x => (int)x["id"], x => x);
            var rawTotal = (int?)(lj["pagination"] as JObject)?["total"];
            var mism = new List<string>();
            foreach (var g in gl.Data ?? new List<ReportGenerationDto>())
            {
                if (!ReportGenerationStatus.IsValid(g.Status)) mism.Add($"{g.Id}:status '{g.Status}' outside vocabulary");
                if (g.HasPdf && !g.IsCompleted) mism.Add($"{g.Id}:pdf url on {g.Status}");
                if (!rawRows.TryGetValue(g.Id, out var r)) { mism.Add($"{g.Id}:not-in-raw"); continue; }
                if (g.Status != Str(r["status"])) mism.Add($"{g.Id}:status {g.Status}!={Str(r["status"])}");
                var rawPdf = r["pdf_download_url"];
                var rawHasPdf = rawPdf != null && rawPdf.Type != JTokenType.Null;
                if (g.HasPdf != rawHasPdf) mism.Add($"{g.Id}:HasPdf {g.HasPdf}!=raw {rawHasPdf}");
                if ((g.ErrorMessage ?? "") != Str(r["error_message"])) mism.Add($"{g.Id}:error_message");
            }
            var gTotal = gl.Pagination?.Total ?? gl.Total;
            var statuses = string.Join(",", (gl.Data ?? new List<ReportGenerationDto>()).GroupBy(g => g.Status).Select(x => $"{x.Key}={x.Count()}"));
            var ok4g = ls == 200 && gl.Success && gTotal == rawTotal && (gl.Data?.Count ?? -1) == rawRows.Count && mism.Count == 0;
            rec.Add("rv.gen.4", TAG, "GetGenerationsAsync 역직렬화 — pagination.total · 상태 5어휘 · error_message · pdf_download_url(COMPLETED 만)",
                ok4g ? Verdict.PASS : Verdict.FAIL,
                $"service total={gTotal} rows={gl.Data?.Count} statuses[{statuses}]; raw total={rawTotal} rows={rawRows.Count}; mismatches=[{string.Join("; ", mism)}]",
                defectAt: ok4g ? "" : "Messages/Dto/Reports/ReportGenerationDto.cs / Reports.Api ReportApiService.GetGenerationsAsync");

            // ---------- 정리도 창 경로로: 관리자 [삭제] → DELETE 200 → 재조회 404 ----------
            boot.Wire.CurrentTag = TAG + "/delete";
            await aConsole.ListViewModel.LoadAsync().ConfigureAwait(false);
            var dRow = aConsole.ListViewModel.Rows.FirstOrDefault(r => r.Id == genId);
            if (dRow != null)
            {
                aConsole.OnRowSelected(dRow);
                var before = rec.LastSeq();
                var c0 = aSpy.Count;
                await aConsole.DeleteGenerationAsync().ConfigureAwait(false);
                if (aSpy.Count > c0)
                    await aConsole.ListViewModel.HandleAsync(new VmMsg.CallDeleteReportGenerationProcessMessageModel(), CancellationToken.None).ConfigureAwait(false);
                var delWire = rec.Since(before).FirstOrDefault(w => w.Method == "DELETE");
                var (xs, _) = await raw.Get($"reports/generations/{genId}").ConfigureAwait(false);
                var deletedId = genId;
                var stillListed = aConsole.ListViewModel.Rows.Any(r => r.Id == deletedId);
                var gone = xs == 404 && !stillListed;
                if (xs == 404) { rec.Deleted("report-generation", genId); genId = 0; }
                rec.Add("rv.gen.5", TAG, "관리자 창 [삭제] → DELETE 200 · 재조회 404 · 목록에서 빠짐",
                    delWire?.Status == 200 && gone ? Verdict.PASS : Verdict.FAIL,
                    $"DELETE HTTP {delWire?.Status}; re-GET {xs}; still in rows={stillListed}",
                    defectAt: delWire?.Status == 200 && gone ? "" : "Reports.Ui/ViewModels/Panels/ReportListViewModel.cs HandleAsync(CallDeleteReportGeneration…)",
                    seqs: SeqsSince(rec, before));
            }
        }
        catch (Exception ex)
        {
            rec.Add("rv.gen!", TAG, "report generation / permission VM round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            if (genId > 0)
            {
                var (gs0, _) = await raw.Get($"reports/generations/{genId}").ConfigureAwait(false);
                if (gs0 == 404) rec.Deleted("report-generation", genId);
                else
                {
                    // 진행 중이면 먼저 멈춘다(진행 중 행 삭제는 서버가 받아도 작업이 남는다).
                    await raw.Post($"reports/generations/{genId}/cancel", new { }).ConfigureAwait(false);
                    var (s, _) = await raw.Delete($"reports/generations/{genId}").ConfigureAwait(false);
                    if (s == 200 || s == 204) rec.Deleted("report-generation", genId); else rec.Leftover("report-generation", genId, $"DELETE {s}");
                }
            }
            if (tplId > 0)
            {
                var (s, _) = await raw.Delete($"reports/templates/{tplId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("report-template", tplId); else rec.Leftover("report-template", tplId, $"DELETE {s}");
            }
            if (userId > 0)
            {
                var (s, _) = await raw.Delete($"users/{userId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("user", userId); else rec.Leftover("user", userId, $"DELETE {s}");
            }
            if (groupId > 0)
            {
                var (s, _) = await raw.Delete($"user-groups/{groupId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("user-group", groupId); else rec.Leftover("user-group", groupId, $"DELETE {s}");
            }
        }
    }

    // ======================================================================================
    // 도우미
    // ======================================================================================

    /// <summary>창을 조립하는 그대로 — 다섯 뷰모델을 한 서비스 위에 세운다(ReportUiModule 과 같은 조합).</summary>
    static ReportConsoleViewModel BuildReportConsole(IEventAggregator events, ILogService log, IPermissionService permission, IReportApiService api)
    {
        var console = new ReportConsoleViewModel(
            events, log, permission,
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));
        // 네이티브 런타임을 건드리지 않는다(창을 띄우지 않는 하네스) — HTML 조회는 그대로 HTTP 로 간다.
        console.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        return console;
    }

    /// <summary>관리자 권한(실제 PermissionService — ADMIN 은 모든 모듈 허용).</summary>
    static IPermissionService AdminPermission()
    {
        var p = new PermissionService();
        p.Apply(new AuthUserDto { LoginId = Bootstrap.LOGIN_ID, Role = "ADMIN" });
        return p;
    }

    static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 15000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            await Task.Delay(25).ConfigureAwait(false);
        }
        return condition();
    }

    static async Task<List<int>> ArtServerOrder(Raw raw)
    {
        var (_, jo) = await raw.Get("events/action-report-templates").ConfigureAwait(false);
        return (jo["data"] as JArray ?? new JArray()).Select(x => (int)x["id"]).ToList();
    }

    static List<(int Id, int Order)> ReorderItems(string body)
    {
        try
        {
            return (JObject.Parse(body)["items"] as JArray ?? new JArray())
                .Select(x => ((int)x["id"], (int)x["display_order"])).ToList();
        }
        catch { return new List<(int, int)>(); }
    }

    static List<string> BodyKeys(string body)
    {
        try { return JObject.Parse(body).Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList(); }
        catch { return new List<string>(); }
    }

    static string Q(string s) => s == null ? "null" : $"\"{s}\"";

    static string ShortUri(string uri) => uri.Replace(Bootstrap.BASE_URL, "", StringComparison.Ordinal);

    /// <summary>확인 팝업을 가로챈다 — 떴으면 사용자가 [확인] 을 누른 것으로 이어 간다.</summary>
    sealed class ConfirmSpy : IHandle<VmMsg.OpenConfirmPopupMessageModel>
    {
        public int Count;
        public Task HandleAsync(VmMsg.OpenConfirmPopupMessageModel message, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 두 번째 계정의 요청 파이프라인 — Bootstrap 과 같은 조립(Bearer → 루프백 전용 TLS), 토큰 · 와이어 큐는 따로.
    /// 와이어를 공용 Recorder 큐에 섞지 않는다(순번 카운터가 핸들러마다 따로라 섞으면 Since() 가 어긋난다).
    /// </summary>
    sealed class UserPipe
    {
        public ApiSetupModel Setup;
        public ApiService Api;
        public TokenStorageService Tokens;
        public AccountApiService Account;
        public ConcurrentQueue<WireExchange> Wire = new();

        public static UserPipe Create(ILogService log)
        {
            LoopbackGuard.Assert(Bootstrap.BASE_URL);
            var p = new UserPipe { Setup = new ApiSetupModel { Url = Bootstrap.BASE_URL, Timeout = 30 }, Tokens = new TokenStorageService() };
            var tls = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
                {
                    if (req.RequestUri is not null) LoopbackGuard.Assert(req.RequestUri);
                    return true;
                }
            };
            IAccountApiService accountRef = null;
            var bearer = new BearerAuthHandler(p.Tokens, () => accountRef, log) { InnerHandler = tls };
            var capture = new WireCaptureHandler(bearer, p.Wire) { CurrentTag = "rpt-vm/edit-only-user" };
            p.Api = new ApiService(log, p.Setup, capture);
            p.Api.Initialize();
            p.Account = new AccountApiService(p.Api, log);
            accountRef = p.Account;
            return p;
        }
    }
}
