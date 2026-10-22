using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 2026-10-22 사용자 — "외관 디자인 최소 10종(공장 · 발전소 · 군사시설 우선)" · "3D 지도를 익스포트해 다른 GIS 에 임포트(한 개의 파일 .3d 같은)" ·
/// "3D 로 나오는 모습을 다른 곳에서 주입". facade-package-contract A · B + 엔진 8cacc6e 넣은 모델을 실제 MapViewModel 경로 + 가짜 엔진으로:
/// A 엔진 외벽 목록(GET /api/facades) → 두 판 콤보에 분류 머리글 · 옛 엔진 폴백 · 목록을 늦게 받아도 고른 값 유지 · 저장 본문의 새 키,
/// B 영역 메뉴 [이 영역 내보내기…](저장 대화상자 · 진행 · 반쪽 파일 없음 · 옛 엔진) · 지도 메뉴 [3D 영역 가져오기…](GIS 쪽 거름 · multipart · 충돌 · 거절 · 권한),
/// 넣은 모델 — [모델 넣기…] · 목록 · 3D 고름(model.pick) · 손잡이 저장(edit.commit scale) · 판 크기 단추 · 지우기 · 되돌리기/다시 하기(되살린 uid 따라가기) · 옛 엔진 · 틀린 파일.
/// </summary>
public partial class TwinContractScenarioTests
{
    /// <summary>facade-package-contract 쪽 가짜 엔진 — FakeEngine.Extra 로 붙인다.</summary>
    private sealed class PackageEngine
    {
        private readonly Rig _rig;
        private int _next;
        public PackageEngine(Rig rig) { _rig = rig; rig.Server.Extra = HandleAsync; }

        public List<(HttpMethod Method, string Path, Dictionary<string, string> Form, byte[]? Body)> Requests { get; } = new();
        /// <summary>GET /api/facades 응답 — null 이면 404(옛 엔진).</summary>
        public JArray? Facades { get; set; }
        public byte[] Package { get; set; } = Array.Empty<byte>();
        /// <summary>package · import · models 주소가 없는 옛 엔진(FastAPI 기본 404 "Not Found").</summary>
        public bool OldEngine { get; set; }
        public string? ImportReject { get; set; }
        public JToken ImportConflict { get; set; } = JValue.CreateNull();
        public List<JObject> Models { get; } = new();
        public Dictionary<string, byte[]> Files { get; } = new();

        private async Task<HttpResponseMessage?> HandleAsync(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            var m = request.Method;
            bool mine = path == "/api/facades" || path == "/api/jobs/import" || path.EndsWith("/package") || path.Contains("/models");
            if (!mine) return null;
            var form = new Dictionary<string, string>();
            byte[]? body = null;
            if (request.Content is MultipartFormDataContent multi)
                foreach (var part in multi)
                {
                    var name = part.Headers.ContentDisposition!.Name!.Trim('"');
                    if (name == "file") { body = await part.ReadAsByteArrayAsync(); form["filename"] = part.Headers.ContentDisposition.FileNameStar ?? part.Headers.ContentDisposition.FileName!.Trim('"'); }
                    else form[name] = await part.ReadAsStringAsync();
                }
            else if (request.Content != null) body = await request.Content.ReadAsByteArrayAsync();
            Requests.Add((m, path, form, body));

            if (path == "/api/facades")
                return Facades == null ? Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}") : Json(HttpStatusCode.OK, Facades.ToString());
            if (OldEngine) return Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");
            if (path == "/api/jobs/import")
                return ImportReject != null ? Json(HttpStatusCode.BadRequest, new JObject { ["detail"] = ImportReject }.ToString())
                                            : Json(HttpStatusCode.OK, new JObject { ["id"] = "j-imported", ["name"] = "발전소 A", ["conflict"] = ImportConflict }.ToString());
            if (path.EndsWith("/package")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package) };
            var seg = path.Split('/');   // "", api, jobs, {job}, models, {uid}, file
            if (seg[3] != Job) return Json(HttpStatusCode.NotFound, "{\"detail\":\"작업이 없습니다\"}");
            string? uid = seg.Length > 5 ? seg[5] : null;
            var it = Models.FirstOrDefault(o => o.Value<string>("uid") == uid);
            if (seg.Length == 7) return it == null ? Json(HttpStatusCode.NotFound, "{\"detail\":\"그 모델이 없습니다\"}") : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Files[uid!]) };
            if (m == HttpMethod.Get) return Json(HttpStatusCode.OK, new JObject { ["items"] = new JArray(Models), ["count"] = Models.Count }.ToString());
            if (m == HttpMethod.Post)
            {
                if (!form["filename"].EndsWith(".glb") && !form["filename"].EndsWith(".fbx")) return Json(HttpStatusCode.BadRequest, "{\"detail\":\"GLB · OBJ · FBX 파일만 넣을 수 있습니다\"}");
                string id = $"a{++_next:00000000000}";
                double D(string k, double d) => form.TryGetValue(k, out var v) ? double.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : d;
                var item = new JObject
                {
                    ["uid"] = id, ["name"] = form.TryGetValue("name", out var n) ? n : Path.GetFileNameWithoutExtension(form["filename"]), ["file"] = id + ".glb",
                    ["lon"] = D("lon", _rig.Center.Lng), ["lat"] = D("lat", _rig.Center.Lat), ["alt_m"] = form.ContainsKey("alt_m") ? D("alt_m", 0) : null,
                    ["heading_deg"] = D("heading_deg", 0), ["scale"] = D("scale", 1), ["tris"] = 12, ["source"] = "glb",
                };
                Models.Add(item);
                Files[id] = body!;
                return Json(HttpStatusCode.OK, new JObject { ["item"] = item, ["count"] = Models.Count }.ToString());
            }
            if (it == null) return Json(HttpStatusCode.NotFound, "{\"detail\":\"그 모델이 없습니다\"}");
            if (m == HttpMethod.Delete) { Models.Remove(it); return Json(HttpStatusCode.OK, $"{{\"deleted\":1,\"count\":{Models.Count}}}"); }
            it.Merge(JObject.Parse(Encoding.UTF8.GetString(body!)), new JsonMergeSettings { MergeNullValueHandling = MergeNullValueHandling.Merge });
            return Json(HttpStatusCode.OK, new JObject { ["item"] = it }.ToString());
        }

        public List<(HttpMethod Method, string Path, Dictionary<string, string> Form, byte[]? Body)> To(string tail) => Requests.Where(r => r.Path.EndsWith(tail)).ToList();

        private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static byte[] Iw3d(string format = "ironwall-3d")
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
                w.Write(new JObject { ["format"] = format, ["format_version"] = 1, ["name"] = "발전소 A" }.ToString());
            using (var w = new StreamWriter(zip.CreateEntry("scene.glb").Open())) w.Write("glTF");
        }
        return ms.ToArray();
    }

    /// <summary>이 작업 임시 폴더(TEMP)의 새 파일 경로 — 시험 끝에 지운다.</summary>
    private static string TempFile(string ext) => Path.Combine(Path.GetTempPath(), $"fpm-{Guid.NewGuid():N}{ext}");

    private static async Task RefreshAreas(Rig rig) => await (Task)Scenario.Call(rig.Vm, "RefreshMap3DAreasAsync")!;

    // ─────────────── A. 외벽 무늬 목록 ───────────────

    [Fact]
    public void should_load_engine_facades_into_both_panels_with_group_headers_and_keep_choice() => RunAsync(async rig =>
    {
        var eng = new PackageEngine(rig)
        {
            Facades = JArray.FromObject(Map3DLook.FacadeCatalog.Select(f => new { key = f.Key, label = f.Label, group = f.Group })
                                        .Append(new { key = "x_scenario_radar", label = "군 · 레이더 돔", group = "military" })),
        };
        rig.Vm.UserBuildingFacade = "tank_steel";                                            // 목록을 받기 전에 고른 값(내장 목록에 있다)
        await RefreshAreas(rig);
        await Rig.Until(() => eng.To("/api/facades").Count == 1 && ((ICollectionView)rig.Vm.UserBuildingFacades).Cast<object>().Count() == 23, "엔진 외벽 목록");
        Assert.Equal("tank_steel", rig.Vm.UserBuildingFacade);                              // 항목을 갈아도 고른 값은 그대로

        AssertRendered(rig.Vm.UserBuildingPanel, "UserBuildingPanelStyle.xaml", "Map.UserBuilding.Facade");
        var combo = Find<ComboBox>(rig.Vm.UserBuildingPanel, "Map.UserBuilding.Facade");
        Assert.Equal("tank_steel", combo.SelectedValue);
        var groups = combo.Items.Groups!.Cast<CollectionViewGroup>().Select(g => (string)g.Name).ToList();
        Assert.Equal(new[] { "", "공장", "발전소", "군사", "일반" }, groups);                 // 자동(머리글 없음) → 공장 → 발전소 → 군사 → 일반
        Assert.Contains("x_scenario_radar", combo.Items.Cast<KeyValuePair<string, string>>().Select(p => p.Key));
        Choose(rig.Vm.UserBuildingPanel, "Map.UserBuilding.Facade", "x_scenario_radar");   // 엔진이 더한 새 키도 정규화에서 살아남는다
        Assert.Equal("x_scenario_radar", rig.Vm.UserBuildingFacade);
        Choose(rig.Vm.UserBuildingPanel, "Map.UserBuilding.Facade", "chimney_banded");
        Assert.Equal("chimney_banded", rig.Vm.UserBuildingFacade);

        // 「건물 높이」 판 콤보도 같은 목록 · 머리글(보기는 판마다 따로 — 한쪽 고름이 다른 쪽을 끌지 않는다)
        AssertRendered(rig.Vm.BuildingHeightPanel, "BuildingHeightPanelStyle.xaml", "Map.BuildingHeight.Facade");
        var bh = Find<ComboBox>(rig.Vm.BuildingHeightPanel, "Map.BuildingHeight.Facade");
        Assert.Equal(23, bh.Items.Count);
        Assert.Equal(5, bh.Items.Groups!.Count);
        Assert.NotSame(rig.Vm.UserBuildingFacades, rig.Vm.BuildingFacades);

        await RefreshAreas(rig);
        Assert.Single(eng.To("/api/facades"));                                              // 받은 뒤엔 다시 묻지 않는다
    });

    [Fact]
    public void should_fall_back_to_builtin_22_facades_on_old_engine_and_save_new_key() => RunAsync(async rig =>
    {
        var eng = new PackageEngine(rig) { Facades = null };
        rig.Enter3D();
        await RefreshAreas(rig);
        await Rig.Until(() => eng.To("/api/facades").Count == 1, "외벽 목록 요청");
        await Task.Delay(50);
        var items = ((ICollectionView)rig.Vm.BuildingFacades).Cast<KeyValuePair<string, string>>().Select(p => p.Key).ToList();
        Assert.Equal(Map3DLook.FacadeCatalog.Select(f => f.Key), items);                    // 계약 A 표 그대로 22개(옛 엔진)
        await RefreshAreas(rig);
        Assert.Single(eng.To("/api/facades"));                                              // 옛 엔진도 한 번이면 끝(폴백 확정)

        // 공장 · 발전소 · 군사 새 키가 저장 본문 · 3D 메시지에 그대로 실린다
        rig.PickBuilding(7);
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/looks"), "외관 목록");
        rig.Vm.BuildingFacade = "military_bunker";
        rig.Vm.ApplyBuildingLookCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Any(w => w.Path.EndsWith("/look")), "외관 저장");
        Assert.Equal("military_bunker", rig.Writes.Last(w => w.Path.EndsWith("/look")).Body!.Value<string>("facade"));
        Assert.Equal("military_bunker", rig.Sent("building.look").Last().Value<string>("facade"));
    });

    // ─────────────── B. .iw3d 내보내기 · 가져오기 ───────────────

    [Fact]
    public void should_export_area_to_one_iw3d_file_and_leave_no_partial_file_on_failure() => RunAsync(async rig =>
    {
        var eng = new PackageEngine(rig) { Package = Iw3d() };
        string path = TempFile(".iw3d"), path2 = TempFile(".iw3d");
        try
        {
            string? offered = null;
            Scenario.SetField(rig.Vm, "_map3DPackageSavePicker", (Func<string, string?>)(name => { offered = name; return path; }));
            var item = rig.Vm.Map3DAreas.Single();
            var export = item.Actions.Single(a => a.Header == Map3DAreaItem.ExportHeader);
            Assert.Equal($"GMaps.Map3D.Export.{Job}", export.AutomationId);
            Assert.True(item.Actions.ToList().FindIndex(a => a.Header == "다시 만들기") > item.Actions.ToList().IndexOf(export));
            export.Command.Execute(null);
            await Rig.Until(() => rig.Vm.AimStatusMessage.StartsWith("3D 영역을 내보냈습니다"), "내보내기 끝");
            Assert.Equal("시험 영역.iw3d", offered);                                         // 기본 이름 = 영역 이름
            Assert.Equal(eng.Package, File.ReadAllBytes(path));                                  // 엔진이 준 한 파일 그대로
            Assert.False(File.Exists(path + ".part"));
            Assert.Equal((HttpMethod.Get, $"/api/jobs/{Job}/package"), (eng.Requests.Single().Method, eng.Requests.Single().Path));

            // 옛 엔진 — 쉬운 말 · 파일 · 반쪽 파일 없음
            eng.OldEngine = true;
            Scenario.SetField(rig.Vm, "_map3DPackageSavePicker", (Func<string, string?>)(_ => path2));
            export.Command.Execute(null);
            await Rig.Until(() => rig.Vm.AimStatusMessage == Map3DPackage.OldEngineText, "옛 엔진 안내");
            Assert.False(File.Exists(path2));
            Assert.False(File.Exists(path2 + ".part"));

            // 대화상자 취소 = 아무것도 하지 않는다
            int before = eng.Requests.Count;
            Scenario.SetField(rig.Vm, "_map3DPackageSavePicker", (Func<string, string?>)(_ => null));
            export.Command.Execute(null);
            await Task.Delay(50);
            Assert.Equal(before, eng.Requests.Count);
        }
        finally { foreach (var f in new[] { path, path2, path + ".part", path2 + ".part" }) if (File.Exists(f)) File.Delete(f); }
    });

    [Fact]
    public void should_import_iw3d_after_checking_it_and_report_conflict_rejection_in_plain_words() => RunAsync(async rig =>
    {
        var eng = new PackageEngine(rig) { ImportConflict = "같은 영역(j-twin)이 있어 새 id 로 들였습니다" };
        string good = TempFile(".iw3d"), other = TempFile(".iw3d");
        File.WriteAllBytes(good, Iw3d());
        File.WriteAllBytes(other, Iw3d(format: "some-other-zip"));
        try
        {
            var view = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));
            Assert.Contains("AutomationProperties.AutomationId=\"GMaps.Map3D.Import\"", view);
            Assert.Contains("{Binding ImportMap3DAreaCommand}", view);

            // 다른 zip — 올리기 전에 거른다
            Scenario.SetField(rig.Vm, "_map3DPackageOpenPicker", (Func<string?>)(() => other));
            rig.Vm.ImportMap3DAreaCommand.Execute(null);
            await Rig.Until(() => rig.Vm.AimStatusMessage == Map3DPackage.NotPackageText, "거름 안내");
            Assert.Empty(eng.To("/import"));

            // 제대로 된 패키지 — multipart file 로 올리고 결과 · 충돌을 쉬운 말로, 영역 목록 다시 받기
            int lists = rig.Server.Requests.Count(r => r.Path == "/api/jobs" && r.Method == HttpMethod.Get);
            Scenario.SetField(rig.Vm, "_map3DPackageOpenPicker", (Func<string?>)(() => good));
            rig.Vm.ImportMap3DAreaCommand.Execute(null);
            await Rig.Until(() => rig.Vm.AimStatusMessage.StartsWith("3D 영역 '발전소 A' 을(를) 가져왔습니다"), "가져오기 끝");
            Assert.EndsWith("같은 영역(j-twin)이 있어 새 id 로 들였습니다", rig.Vm.AimStatusMessage);
            var up = eng.To("/import").Single();
            Assert.Equal(HttpMethod.Post, up.Method);
            Assert.Equal(Path.GetFileName(good), up.Form["filename"]);
            Assert.Equal(File.ReadAllBytes(good), up.Body);
            await Rig.Until(() => rig.Server.Requests.Count(r => r.Path == "/api/jobs" && r.Method == HttpMethod.Get) > lists, "영역 목록 갱신");

            // 엔진 거절(해시 · 크기) — 엔진 문구 그대로
            eng.ImportReject = "파일 해시가 맞지 않습니다 — 옮기는 중 망가졌을 수 있습니다";
            rig.Vm.ImportMap3DAreaCommand.Execute(null);
            await Rig.Until(() => rig.Vm.AimStatusMessage == "3D 영역을 가져오지 못했습니다: 파일 해시가 맞지 않습니다 — 옮기는 중 망가졌을 수 있습니다", "거절 안내");

            // 옛 엔진
            eng.OldEngine = true;
            rig.Vm.ImportMap3DAreaCommand.Execute(null);
            await Rig.Until(() => rig.Vm.AimStatusMessage == Map3DPackage.OldEngineText, "옛 엔진 안내");
            Assert.True(rig.Vm.ImportMap3DAreaCommand.CanExecute(null));                       // 끝나면 다시 누를 수 있다
        }
        finally { File.Delete(good); File.Delete(other); }
    });

    [Fact]
    public void should_not_offer_export_or_import_without_map_edit_permission() => RunAsync(async rig =>
    {
        var eng = new PackageEngine(rig) { Package = Iw3d() };
        Assert.False(rig.Vm.CanMapEdit);
        Assert.DoesNotContain(rig.Vm.Map3DAreas.Single().Actions, a => a.Header == Map3DAreaItem.ExportHeader);
        Scenario.SetField(rig.Vm, "_map3DPackageOpenPicker", (Func<string?>)(() => throw new InvalidOperationException("대화상자를 열면 안 된다")));
        rig.Vm.ImportMap3DAreaCommand.Execute(null);
        await Rig.Until(() => rig.W.Published.OfType<OpenInfoPopupMessageModel>().Any(), "권한 없음 안내");
        Assert.Empty(eng.Requests);
    }, canEdit: false);

    // ─────────────── 넣은 모델 ───────────────

    [Fact]
    public void should_add_list_select_and_undo_redo_inserted_model_from_decoration_panel() => RunAsync(async rig =>
    {
        var eng = new PackageEngine(rig);
        string glb = TempFile(".glb"), stl = TempFile(".stl");
        File.WriteAllBytes(glb, Encoding.ASCII.GetBytes("glTF-fake-binary"));
        File.WriteAllBytes(stl, new byte[] { 1, 2 });
        try
        {
            rig.Vm.DecorateCommand.Execute(null);
            await Rig.Until(() => eng.To("/models").Any(r => r.Method == HttpMethod.Get), "모델 목록");
            Assert.Equal("넣은 모델 없음", rig.Vm.ModelCountText);
            AssertRendered(rig.Vm.DecorationPanel, "DecorationPanelStyle.xaml", "Map.Model.Add", "Map.Model.List", "Map.Model.Count");

            // 틀린 파일 — 올리지 않고 쉬운 말
            Scenario.SetField(rig.Vm, "_modelFilePicker", (Func<string?>)(() => stl));
            rig.Vm.AddModelCommand.Execute(null);
            await Rig.Until(() => rig.Vm.ModelStatusText == Map3DModels.WrongFileText, "틀린 파일 안내");
            Assert.DoesNotContain(eng.Requests, r => r.Method == HttpMethod.Post);

            // 넣기 — 화면 가운데(영역 안) 자리로 multipart, 3D 다시 그리기 · 고름
            Scenario.SetField(rig.Vm, "_modelFilePicker", (Func<string?>)(() => glb));
            Press(rig.Vm.DecorationPanel, "Map.Model.Add");
            await Rig.Until(() => rig.Vm.ModelItems.Count == 1 && rig.Vm.HasModelSelection, "넣은 모델");
            var post = eng.Requests.Single(r => r.Method == HttpMethod.Post);
            Assert.Equal($"/api/jobs/{Job}/models", post.Path);
            Assert.Equal(Path.GetFileName(glb), post.Form["filename"]);
            Assert.Equal(File.ReadAllBytes(glb), post.Body);
            Assert.Equal(rig.Center.Lng, double.Parse(post.Form["lon"], System.Globalization.CultureInfo.InvariantCulture), 6);
            Assert.Equal(rig.Center.Lat, double.Parse(post.Form["lat"], System.Globalization.CultureInfo.InvariantCulture), 6);
            string uid = rig.Vm.ModelItems[0].Uid;
            Assert.Equal(uid, rig.Vm.ModelSelectedUid);
            Assert.NotEmpty(rig.Sent("models.reload"));
            Assert.Equal(uid, rig.Sent("model.select").Last().Value<string>("uid"));
            Assert.Equal("넣은 모델 1개", rig.Vm.ModelCountText);

            // 이름
            rig.Vm.ModelName = "변압기";
            rig.Vm.RenameModelCommand.Execute(null);
            await Rig.Until(() => rig.Vm.ModelItems.Single().Name == "변압기", "이름 저장");
            Assert.Equal("변압기", JObject.Parse(Encoding.UTF8.GetString(eng.Requests.Last(r => r.Method == HttpMethod.Put).Body!)).Value<string>("name"));

            // 되돌리기 2번 = 이름 → 넣기 취소(지움), 다시 하기 = 같은 파일을 다시 올림(새 uid)
            rig.Vm.UndoDecorationCommand.Execute(null);
            await Rig.Until(() => rig.Vm.ModelItems.SingleOrDefault()?.Name != "변압기", "이름 되돌리기");
            rig.Vm.UndoDecorationCommand.Execute(null);
            await Rig.Until(() => rig.Vm.ModelItems.Count == 0, "넣기 되돌리기");
            Assert.Equal($"/api/jobs/{Job}/models/{uid}", eng.Requests.Last(r => r.Method == HttpMethod.Delete).Path);
            Assert.False(rig.Vm.HasModelSelection);
            rig.Vm.RedoDecorationCommand.Execute(null);
            await Rig.Until(() => rig.Vm.ModelItems.Count == 1, "넣기 다시 하기");
            Assert.Equal(2, eng.Requests.Count(r => r.Method == HttpMethod.Post));
            Assert.Equal(File.ReadAllBytes(glb), eng.Requests.Last(r => r.Method == HttpMethod.Post).Body);
            string uid2 = rig.Vm.ModelItems[0].Uid;
            Assert.NotEqual(uid, uid2);
            rig.Vm.RedoDecorationCommand.Execute(null);                                        // 이름 다시 하기 — 옛 uid 기록이 새 모델을 가리킨다
            await Rig.Until(() => rig.Vm.ModelItems.Single().Name == "변압기", "이름 다시 하기(되살린 uid)");
            Assert.EndsWith($"/models/{uid2}", eng.Requests.Last(r => r.Method == HttpMethod.Put).Path);
        }
        finally { File.Delete(glb); File.Delete(stl); }
    });

    [Fact]
    public void should_pick_model_in_3d_move_rotate_scale_with_handles_then_delete_and_restore() => RunAsync(async rig =>
    {
        var eng = new PackageEngine(rig);
        eng.Models.Add(new JObject
        {
            ["uid"] = Uid, ["name"] = "탱크", ["lon"] = rig.Center.Lng, ["lat"] = rig.Center.Lat, ["alt_m"] = null, ["heading_deg"] = 0.0, ["scale"] = 1.0, ["tris"] = 10,
        });
        eng.Files[Uid] = new byte[] { 0x67, 0x6c, 0x54, 0x46 };
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "model.pick", ["uid"] = Uid });
        await Rig.Until(() => rig.Vm.ModelSelectedUid == Uid, "3D 에서 고른 모델");
        Assert.True(rig.Vm.IsDecorating);
        Assert.True(rig.Vm.IsEdit3DMode);                                                      // 3D 에 머문 채 판
        var start = rig.Sent("edit.start").Last()["target"]!;
        Assert.Equal(("model", Uid), (start.Value<string>("kind"), start.Value<string>("uid")));
        Assert.True(rig.Vm.IsEdit3DRotateVisible);
        Assert.True(rig.Vm.IsEdit3DScaleVisible);
        Assert.False(rig.Vm.IsEdit3DVertexVisible);

        // 판 크기 단추 = 뷰어 키(+) — 실제 판 템플릿의 단추를 누른다
        AssertRendered(rig.Vm.DecorationPanel, "DecorationPanelStyle.xaml", "Map.Edit3D.ScaleUp", "Map.Edit3D.ScaleDown", "Map.Model.Delete", "Map.Model.Name");
        Press(rig.Vm.DecorationPanel, "Map.Edit3D.ScaleUp");
        Assert.Equal(new[] { "+", "Enter" }, rig.Sent("edit.key").TakeLast(2).Select(k => k.Value<string>("key")));   // 누를 때마다 확정(2026-10-22)

        // 손잡이로 옮기기 · 돌리기 · 키우기 → PUT, 되돌리기 = 앞 자리로 PUT
        double lon = rig.Center.Lng + 0.0005, lat = rig.Center.Lat + 0.0003;
        rig.From3D(new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "model", ["uid"] = Uid },
            ["geometry"] = new JObject { ["type"] = "Point", ["coordinates"] = new JArray(lon, lat) }, ["heading_deg"] = 45.0, ["scale"] = 2.5,
        });
        await Rig.Until(() => rig.Vm.ModelItems.SingleOrDefault()?.Scale == 2.5, "손잡이 저장");
        var put = JObject.Parse(Encoding.UTF8.GetString(eng.Requests.Last(r => r.Method == HttpMethod.Put).Body!));
        Assert.Equal((Math.Round(lon, 7), Math.Round(lat, 7), 45.0, 2.5, "탱크"), (put.Value<double>("lon"), put.Value<double>("lat"), put.Value<double>("heading_deg"),
                                                                               put.Value<double>("scale"), put.Value<string>("name")));
        Assert.Equal(JTokenType.Null, put["alt_m"]!.Type);                                     // 지형에 붙인 채
        Assert.Equal("edit.start", rig.Posts.Last(p => p.Value<string>("type") is "edit.start" or "edit.revert").Value<string>("type"));   // 새 자리로 손잡이 다시

        // 영역 밖으로 끌면 저장하지 않고 원위치(edit.revert)
        int puts = eng.Requests.Count(r => r.Method == HttpMethod.Put);
        rig.From3D(new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "model", ["uid"] = Uid },
            ["geometry"] = new JObject { ["type"] = "Point", ["coordinates"] = new JArray(rig.Center.Lng + 0.01, lat) },
        });
        await Rig.Until(() => rig.Sent("edit.revert").Any(), "영역 밖 원위치");
        Assert.Equal(puts, eng.Requests.Count(r => r.Method == HttpMethod.Put));
        Assert.Equal(Map3DUserBuilding.OutsideText, rig.Vm.ModelStatusText);

        // 지우기 — 원본을 먼저 받아 두고 DELETE, 되돌리기 = 같은 자리 · 이름 · 크기로 다시 올림
        rig.Vm.DeleteModelCommand.Execute(null);
        await Rig.Until(() => rig.Vm.ModelItems.Count == 0, "지우기");
        var tail = eng.Requests.Where(r => r.Path.Contains(Uid)).TakeLast(2).Select(r => (r.Method, r.Path)).ToList();
        Assert.Equal(new[] { (HttpMethod.Get, $"/api/jobs/{Job}/models/{Uid}/file"), (HttpMethod.Delete, $"/api/jobs/{Job}/models/{Uid}") }, tail);
        rig.Vm.UndoDecorationCommand.Execute(null);
        await Rig.Until(() => rig.Vm.ModelItems.Count == 1, "지우기 되돌리기");
        var re = eng.Requests.Last(r => r.Method == HttpMethod.Post);
        Assert.Equal(new byte[] { 0x67, 0x6c, 0x54, 0x46 }, re.Body);
        Assert.Equal(("탱크", "2.5", "45"), (re.Form["name"], re.Form["scale"], re.Form["heading_deg"]));
        string again = rig.Vm.ModelItems.Single().Uid;

        // 그 앞 기록(옮기기) 되돌리기 — 옛 uid 기록이 되살린 모델(새 uid)을 고친다
        rig.Vm.UndoDecorationCommand.Execute(null);
        await Rig.Until(() => rig.Vm.ModelItems.Single().Scale == 1.0, "옮기기 되돌리기");
        Assert.EndsWith($"/models/{again}", eng.Requests.Last(r => r.Method == HttpMethod.Put).Path);

        // 3D 에서 Esc 로 편집 끝(edit.end) → 판의 고름도 풀린다
        rig.Vm.ModelSelectedUid = again;
        rig.From3D(new JObject { ["type"] = "edit.end", ["reason"] = "escape" });
        Assert.False(rig.Vm.HasModelSelection);
    });

    [Fact]
    public void should_keep_model_and_decoration_selection_exclusive_and_handle_old_engine() => RunAsync(async rig =>
    {
        var eng = new PackageEngine(rig);
        eng.Models.Add(new JObject { ["uid"] = Uid, ["lon"] = rig.Center.Lng, ["lat"] = rig.Center.Lat, ["heading_deg"] = 0.0, ["scale"] = 1.0 });
        rig.Vm.DecorateCommand.Execute(null);
        await Rig.Until(() => rig.Vm.ModelItems.Count == 1, "모델 목록");
        rig.Vm.ModelSelectedUid = Uid;
        Assert.True(rig.Vm.HasModelSelection);
        rig.Vm.SelectDecorationToolCommand.Execute("TreePoint");                               // 도구를 고르면 모델 고름은 풀린다
        Assert.False(rig.Vm.HasModelSelection);
        Assert.Equal(JTokenType.Null, rig.Sent("model.select").Last()["uid"]!.Type);
        rig.Vm.ModelSelectedUid = Uid;                                                         // 모델을 고르면 도구는 내려놓는다
        Assert.Null(rig.Vm.DecorationTool);
        Assert.True(rig.DecoKey(System.Windows.Input.Key.Escape));                             // Esc 단계 — 모델 고름 풀기
        Assert.False(rig.Vm.HasModelSelection);
        Assert.True(rig.Vm.IsDecorating);
        rig.Vm.ExitDecorationCommand.Execute(null);

        // 옛 엔진 — 모델 칸만 꺼지고 이유 한 줄
        eng.OldEngine = true;
        rig.Vm.DecorateCommand.Execute(null);
        await Rig.Until(() => rig.Vm.ModelStatusText == Map3DModels.OldEngineText, "옛 엔진 안내");
        Assert.False(rig.Vm.AddModelCommand.CanExecute(null));
        Assert.True(rig.Vm.IsDecorationToolsEnabled);                                          // 꾸미기는 그대로
    });
}
