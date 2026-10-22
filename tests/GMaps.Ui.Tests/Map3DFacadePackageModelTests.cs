using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// facade-package-contract(2026-10-22) GIS 쪽 순수 함수 · HTTP 창구 — A 외벽 무늬 22개 · GET /api/facades(받기 · 옛 엔진 폴백),
/// B .iw3d 내보내기(GET …/package 받아 쓰기 · 진행) · 가져오기(GIS 쪽 검사 · multipart · 응답 · 거절), 넣은 모델(엔진 8cacc6e — 목록 · 올리기 · 자리 · 지우기 ·
/// 3D 메시지 · edit.commit scale). 가짜 HttpMessageHandler(실서버 무관).
/// </summary>
public class Map3DFacadePackageModelTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, byte[]?, HttpResponseMessage> _respond;
        public List<(HttpMethod Method, string Path, byte[]? Body, string? ContentType)> Requests { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, byte[]?, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            byte[]? body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync(ct);
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body, request.Content?.Headers.ContentType?.MediaType));
            return _respond(request, body);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage NotFound() => Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");

    /// <summary>manifest.json 이 맨 앞인 zip(엔진 패키지 모양) — format 을 바꿔 엉뚱한 zip 도 만든다.</summary>
    private static byte[] Package(string format = "ironwall-3d", bool manifest = true)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (manifest)
                using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
                    w.Write(new JObject { ["format"] = format, ["format_version"] = 1, ["name"] = "발전소 A", ["files"] = new JArray() }.ToString());
            using (var w = new StreamWriter(zip.CreateEntry("scene.json").Open())) w.Write("{}");
        }
        return ms.ToArray();
    }

    // ── A. 외벽 무늬 ──

    [Fact]
    public void should_list_22_facades_in_contract_order_with_group_headers()
    {
        var keys = Map3DLook.FacadeCatalog.Select(f => f.Key).ToList();
        Assert.Equal(22, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        foreach (var k in new[] { "plain_wall", "corrugated_metal", "sandwich_panel", "warehouse_shutter", "factory_clerestory", "plant_concrete", "turbine_hall",
                                  "chimney_banded", "chimney_concrete", "tank_steel", "military_bunker", "military_barracks", "camouflage", "office_ribbon", "retail_store",
                                  "auto", "residential", "commercial", "industrial", "glass", "brick", "concrete" })
            Assert.Contains(k, keys);
        // 차례 = 자동 → 공장 → 발전소 → 군사 → 일반(옛 무늬 포함) — 분류가 한 번씩만 이어서 나온다
        var groups = Map3DLook.FacadeCatalog.Select(f => f.Group).ToList();
        Assert.Equal(new[] { "auto", "factory", "plant", "military", "general" }, groups.Distinct());
        Assert.Equal(groups.OrderBy(g => Array.IndexOf(new[] { "auto", "factory", "plant", "military", "general" }, g)), groups);
        Assert.Equal("auto", keys[0]);
        Assert.Equal(("공장", "발전소", "군사", "일반", ""), (Map3DLook.GroupText("factory"), Map3DLook.GroupText("plant"), Map3DLook.GroupText("military"),
                                                    Map3DLook.GroupText("general"), Map3DLook.GroupText("auto")));
        Assert.Equal("굴뚝 · 적백 띠", Map3DLook.Facades.Single(p => p.Key == "chimney_banded").Value);
        Assert.Equal("chimney_banded", Map3DLook.NormalizeFacade("chimney_banded"));
        Assert.Equal("military_bunker", Map3DLook.PutBody(null, new Map3DLookState(false, null, null, "military_bunker")).Value<string>("facade"));
    }

    [Fact]
    public void should_parse_engine_facade_list_and_skip_bad_rows()
    {
        var list = Map3DLook.ParseFacades("[{\"key\":\"auto\",\"label\":\"자동\",\"group\":\"auto\"},{\"key\":\"tank_steel\",\"label\":\"탱크\",\"group\":\"plant\"},"
                                          + "{\"key\":\"Bad Key\",\"label\":\"x\"},{\"label\":\"키 없음\"},{\"key\":\"tank_steel\",\"label\":\"겹침\"},{\"key\":\"brick\"}]")!;
        Assert.Equal(new[] { "auto", "tank_steel", "brick" }, list.Select(f => f.Key));
        Assert.Equal(("탱크", "plant"), (list[1].Label, list[1].Group));
        Assert.Equal(("벽돌", "general"), (list[2].Label, list[2].Group));                 // 이름 · 분류가 빠지면 내장 이름 · 일반
        Assert.Equal("auto", Map3DLook.ParseFacades("[{\"key\":\"glass\",\"label\":\"유리\",\"group\":\"general\"}]")![0].Key);   // auto 가 없으면 맨 앞에
        Assert.Null(Map3DLook.ParseFacades("{\"detail\":\"x\"}"));
        Assert.Null(Map3DLook.ParseFacades("[]"));
        Assert.Null(Map3DLook.ParseFacades("<html>"));
    }

    [Fact]
    public void should_keep_new_engine_key_after_engine_list_is_used()
    {
        const string key = "x_contract_test_only";   // 다른 시험이 쓰지 않는 키(정규화 표는 프로그램 전체 공용)
        Assert.Equal("auto", Map3DLook.NormalizeFacade(key));
        Map3DLook.UseEngineFacades(Map3DLook.FacadeCatalog.Append(new Map3DFacade(key, "새 무늬", "factory")));
        Assert.Equal(key, Map3DLook.NormalizeFacade(key));
        Assert.Equal("auto", Map3DLook.NormalizeFacade("marble"));
    }

    [Fact]
    public async Task should_get_facades_from_engine_and_fall_back_on_old_engine()
    {
        var h = new FakeHandler((_, _) => Json(HttpStatusCode.OK, "[{\"key\":\"auto\",\"label\":\"자동\",\"group\":\"auto\"},{\"key\":\"camouflage\",\"label\":\"군 · 위장 무늬\",\"group\":\"military\"}]"));
        using var c = new Map3DClient("http://h", h);
        var list = await c.ListFacadesAsync();
        Assert.Equal((HttpMethod.Get, "/api/facades"), (h.Requests[0].Method, h.Requests[0].Path));
        Assert.Equal(new[] { "auto", "camouflage" }, list!.Select(f => f.Key));

        using var old = new Map3DClient("http://h", new FakeHandler((_, _) => NotFound()));
        Assert.Null(await old.ListFacadesAsync());                                          // 옛 엔진 → 호출자가 내장 22개

        using var down = new Map3DClient("http://h", new FakeHandler((_, _) => throw new HttpRequestException("거부")));
        await Assert.ThrowsAsync<Map3DServerUnavailableException>(() => down.ListFacadesAsync());
    }

    // ── B. .iw3d 패키지 ──

    [Fact]
    public void should_make_safe_export_file_name()
    {
        Assert.Equal("발전소 A.iw3d", Map3DPackage.ExportFileName("발전소 A"));
        Assert.Equal("a_b_c.iw3d", Map3DPackage.ExportFileName("a/b:c"));
        Assert.Equal("3d-area.iw3d", Map3DPackage.ExportFileName("  ..  "));
        Assert.Equal(80 + 5, Map3DPackage.ExportFileName(new string('가', 200)).Length);
    }

    [Fact]
    public void should_inspect_package_by_manifest_not_extension()
    {
        using (var ok = new MemoryStream(Package()))
        {
            var r = Map3DPackage.Inspect(ok);
            Assert.Equal((null, "발전소 A"), (r.Error, r.Name));
            Assert.True(ok.CanRead);                                                         // 스트림은 닫지 않는다(처음부터 다시 올린다)
        }
        Assert.Equal(Map3DPackage.NotPackageText, Map3DPackage.Inspect(new MemoryStream(Package(format: "something-else"))).Error);
        Assert.Equal(Map3DPackage.NotPackageText, Map3DPackage.Inspect(new MemoryStream(Package(manifest: false))).Error);
        Assert.Equal(Map3DPackage.NotPackageText, Map3DPackage.Inspect(new MemoryStream(Encoding.UTF8.GetBytes("PNG 아님 zip 아님"))).Error);
        Assert.Equal(Map3DPackage.TooBigText, Map3DPackage.Inspect(new LongStream(Map3DPackage.MaxBytes + 1)).Error);
    }

    /// <summary>길이만 큰 빈 스트림(2 GB 넘는 파일을 디스크에 만들지 않고).</summary>
    private sealed class LongStream : MemoryStream
    {
        private readonly long _length;
        public LongStream(long length) => _length = length;
        public override long Length => _length;
    }

    [Fact]
    public void should_parse_import_reply_and_conflict_forms()
    {
        Assert.Equal(new Map3DImportResult("j2", "발전소 A", null), Map3DPackage.ParseImport("{\"id\":\"j2\",\"name\":\"발전소 A\",\"conflict\":null}"));
        Assert.Equal("이미 같은 영역(j1)이 있어 j1-2 로 들였습니다", Map3DPackage.ParseImport("{\"id\":\"j1-2\",\"conflict\":\"이미 같은 영역(j1)이 있어 j1-2 로 들였습니다\"}")!.Conflict);
        Assert.NotNull(Map3DPackage.ParseImport("{\"id\":\"j1-2\",\"conflict\":true}")!.Conflict);
        Assert.Null(Map3DPackage.ParseImport("{\"id\":\"j1\",\"conflict\":false}")!.Conflict);
        Assert.Equal("원래 j1", Map3DPackage.ParseImport("{\"id\":\"j1-2\",\"conflict\":{\"message\":\"원래 j1\"}}")!.Conflict);
        Assert.Null(Map3DPackage.ParseImport("{\"name\":\"id 없음\"}"));
        Assert.Equal("3D 영역 '발전소 A' 을(를) 가져왔습니다 — 겹침", Map3DPackage.ImportedText(new Map3DImportResult("j", "발전소 A", "겹침")));
        Assert.Equal("3D 영역 내보내는 중… 50%", Map3DPackage.ProgressText("내보내는", 50, 100));
        Assert.Equal("3D 영역 가져오는 중… 2.0 MB", Map3DPackage.ProgressText("가져오는", 2 * 1024 * 1024, null));
    }

    [Fact]
    public async Task should_download_package_to_stream_with_progress()
    {
        var bytes = Package();
        var h = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        using var c = new Map3DClient("http://h", h);
        using var dest = new MemoryStream();
        var seen = new List<(long, long?)>();
        await c.DownloadPackageAsync("j1", dest, new SyncProgress(seen.Add));
        Assert.Equal((HttpMethod.Get, "/api/jobs/j1/package"), (h.Requests[0].Method, h.Requests[0].Path));
        Assert.Equal(bytes, dest.ToArray());
        Assert.Equal(((long)bytes.Length, (long?)bytes.Length), seen.Last());

        using var old = new Map3DClient("http://h", new FakeHandler((_, _) => NotFound()));
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => old.DownloadPackageAsync("j1", new MemoryStream()));
        Assert.Equal("Not Found", ex.Message);                                             // 옛 엔진 — 호출자가 쉬운 말로
        using var down = new Map3DClient("http://h", new FakeHandler((_, _) => throw new HttpRequestException("거부")));
        await Assert.ThrowsAsync<Map3DServerUnavailableException>(() => down.DownloadPackageAsync("j1", new MemoryStream()));
    }

    /// <summary>바로 부르는 진행(Progress&lt;T&gt; 는 동기화 문맥으로 늦게 부른다).</summary>
    private sealed class SyncProgress : IProgress<(long Done, long? Total)>
    {
        private readonly Action<(long, long?)> _on;
        public SyncProgress(Action<(long, long?)> on) => _on = on;
        public void Report((long Done, long? Total) value) => _on(value);
    }

    [Fact]
    public async Task should_upload_package_as_multipart_file_and_read_reply()
    {
        var bytes = Package();
        var h = new FakeHandler((_, _) => Json(HttpStatusCode.OK, "{\"id\":\"j1-2\",\"name\":\"발전소 A\",\"conflict\":\"같은 영역이 있어 새 id 로\"}"));
        using var c = new Map3DClient("http://h", h);
        var seen = new List<(long, long?)>();
        var r = await c.ImportPackageAsync(new MemoryStream(bytes), "발전소 A.iw3d", new SyncProgress(seen.Add));
        var req = h.Requests.Single();
        Assert.Equal((HttpMethod.Post, "/api/jobs/import", "multipart/form-data"), (req.Method, req.Path, req.ContentType));
        var text = Encoding.UTF8.GetString(req.Body!);
        Assert.Contains("name=file", text);
        Assert.Contains("application/zip", text);
        Assert.Contains("manifest.json", text);                                             // zip 본문이 그대로 실렸다
        Assert.Equal(new Map3DImportResult("j1-2", "발전소 A", "같은 영역이 있어 새 id 로"), r);
        Assert.Equal(((long)bytes.Length, (long?)bytes.Length), seen.Last());

        using var rej = new Map3DClient("http://h", new FakeHandler((_, _) => Json(HttpStatusCode.BadRequest, "{\"detail\":\"파일 해시가 맞지 않습니다(손상)\"}")));
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => rej.ImportPackageAsync(new MemoryStream(bytes), "a.iw3d"));
        Assert.Equal((HttpStatusCode.BadRequest, "파일 해시가 맞지 않습니다(손상)"), (ex.StatusCode, ex.Message));
    }

    // ── 넣은 모델 ──

    private const string ModelJson = "{\"uid\":\"0123456789ab\",\"name\":\"변압기\",\"file\":\"0123456789ab.glb\",\"lon\":127.1,\"lat\":37.5,\"alt_m\":null,"
                                     + "\"heading_deg\":90,\"scale\":1.5,\"tris\":1200,\"size_m\":[2,3,4],\"source\":\"fbx\"}";

    [Fact]
    public void should_parse_model_items_and_build_place_body()
    {
        var it = Map3DModels.ParseItem(JObject.Parse(ModelJson))!;
        Assert.Equal(new Map3DModelItem("0123456789ab", "변압기", 127.1, 37.5, null, 90, 1.5), it with { Tris = null, Source = null });
        Assert.Equal((1200, "fbx"), (it.Tris, it.Source));
        Assert.Equal("변압기 · 1.5배 · 90°", it.Text);
        Assert.Null(Map3DModels.ParseItem(JObject.Parse("{\"uid\":\"../etc\",\"lon\":1,\"lat\":1}")));
        Assert.Null(Map3DModels.ParseItem(JObject.Parse("{\"uid\":\"0123456789ab\",\"lon\":200,\"lat\":1}")));
        Assert.Single(Map3DModels.ParseList("{\"items\":[" + ModelJson + ",{\"uid\":1}]}"));

        var body = Map3DModels.PlaceBody(it with { AltM = 12.5, Name = null });
        Assert.Equal(new[] { "lon", "lat", "alt_m", "heading_deg", "scale", "name" }, body.Properties().Select(p => p.Name));   // 엔진 ModelPlaceIn(extra=forbid) 키만
        Assert.Equal((12.5, JTokenType.Null), (body.Value<double>("alt_m"), body["name"]!.Type));
        Assert.Equal(JTokenType.Null, Map3DModels.PlaceBody(it)["alt_m"]!.Type);          // null = 지형에 붙임

        Assert.Equal("{\"type\":\"models.reload\"}", Map3DModels.ReloadMessageJson());
        Assert.Equal("{\"type\":\"model.select\",\"uid\":null}", Map3DModels.SelectMessageJson(null));
        Assert.Equal("넣은 모델 2개", Map3DModels.CountText(2));
        Assert.StartsWith(Map3DDecoration.UndoPrefix, Map3DModels.UndoPrefix);               // 꾸미기 판 Ctrl+Z 가 같이 되돌린다
    }

    [Fact]
    public void should_check_model_file_before_upload()
    {
        Assert.Null(Map3DModels.CheckFile(@"C:\m\tank.GLB", 1000));
        Assert.Null(Map3DModels.CheckFile("a.obj", 1));
        Assert.Null(Map3DModels.CheckFile("a.fbx", Map3DModels.MaxBytes));
        Assert.Equal(Map3DModels.WrongFileText, Map3DModels.CheckFile("a.stl", 10));
        Assert.Equal(Map3DModels.TooBigText, Map3DModels.CheckFile("a.glb", Map3DModels.MaxBytes + 1));
        Assert.NotNull(Map3DModels.CheckFile("a.glb", 0));
    }

    [Fact]
    public async Task should_call_model_endpoints_with_contract_shapes()
    {
        var h = new FakeHandler((r, _) =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path.EndsWith("/file")) return path.Contains("ffffffffffff") ? NotFound() : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };
            if (r.Method == HttpMethod.Get) return Json(HttpStatusCode.OK, "{\"items\":[" + ModelJson + "],\"count\":1}");
            if (r.Method == HttpMethod.Delete) return path.Contains("ffffffffffff") ? Json(HttpStatusCode.NotFound, "{\"detail\":\"그 모델이 없습니다\"}") : Json(HttpStatusCode.OK, "{\"deleted\":1,\"count\":0}");
            return Json(HttpStatusCode.OK, "{\"item\":" + ModelJson + ",\"count\":1}");
        });
        using var c = new Map3DClient("http://h", h);

        Assert.Single((await c.ListModelsAsync("j1"))!);
        var made = await c.UploadModelAsync("j1", new byte[] { 9, 9 }, "탱크.fbx", lon: 127.1, lat: 37.5, name: "탱크");
        Assert.Equal("0123456789ab", made.Uid);
        var post = h.Requests[1];
        Assert.Equal((HttpMethod.Post, "/api/jobs/j1/models", "multipart/form-data"), (post.Method, post.Path, post.ContentType));
        var form = Encoding.UTF8.GetString(post.Body!);
        Assert.Contains("name=file", form);
        Assert.Contains("name=lon", form);
        Assert.Contains("127.1", form);
        Assert.Contains("name=lat", form);
        Assert.DoesNotContain("name=scale", form);                                          // 빠진 칸 = 엔진 기본(1배)
        Assert.DoesNotContain("name=heading_deg", form);

        var moved = await c.PutModelAsync("j1", made with { Lon = 127.2, Scale = 2 });
        var put = h.Requests[2];
        Assert.Equal((HttpMethod.Put, "/api/jobs/j1/models/0123456789ab"), (put.Method, put.Path));
        var body = JObject.Parse(Encoding.UTF8.GetString(put.Body!));
        Assert.Equal((127.2, 2.0), (body.Value<double>("lon"), body.Value<double>("scale")));
        Assert.Equal("0123456789ab", moved.Uid);

        Assert.Equal(new byte[] { 1, 2, 3 }, await c.GetModelFileAsync("j1", "0123456789ab"));
        Assert.Null(await c.GetModelFileAsync("j1", "ffffffffffff"));
        Assert.True(await c.DeleteModelAsync("j1", "0123456789ab"));
        Assert.False(await c.DeleteModelAsync("j1", "ffffffffffff"));

        using var old = new Map3DClient("http://h", new FakeHandler((_, _) => NotFound()));
        Assert.Null(await old.ListModelsAsync("j1"));                                       // 옛 엔진
        using var rej = new Map3DClient("http://h", new FakeHandler((_, _) => Json(HttpStatusCode.BadRequest, "{\"detail\":\"GLB · OBJ · FBX 파일만 넣을 수 있습니다\"}")));
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => rej.UploadModelAsync("j1", new byte[] { 1 }, "a.stl"));
        Assert.Equal("GLB · OBJ · FBX 파일만 넣을 수 있습니다", ex.Message);
    }

    [Fact]
    public void should_parse_model_pick_and_model_edit_commit_with_scale()
    {
        Assert.Equal("0123456789ab", Map3DMath.ParseMessage("{\"type\":\"model.pick\",\"uid\":\"0123456789ab\"}")!.ModelUid);
        Assert.Null(Map3DMath.ParseMessage("{\"type\":\"model.pick\",\"uid\":\"../x\"}"));

        var c = Map3DMath.ParseMessage("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"model\",\"uid\":\"0123456789ab\"},"
                                       + "\"geometry\":{\"type\":\"Point\",\"coordinates\":[127.1,37.5]},\"heading_deg\":370,\"scale\":2.5}")!.EditCommit!;
        Assert.Equal(("model", "0123456789ab", "Point", 10.0, 2.5), (c.Kind, c.Uid, c.Geometry, c.HeadingDeg!.Value, c.Scale!.Value));
        Assert.Equal(100, Map3DMath.ParseMessage("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"model\",\"uid\":\"0123456789ab\"},"
                                                 + "\"geometry\":{\"type\":\"Point\",\"coordinates\":[127.1,37.5]},\"scale\":1000}")!.EditCommit!.Scale);
        Assert.Null(Map3DMath.ParseMessage("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"model\",\"uid\":\"0123456789ab\"},"
                                           + "\"geometry\":{\"type\":\"LineString\",\"coordinates\":[[127.1,37.5],[127.2,37.5]]}}"));   // 모델은 한 점만
        Assert.Null(Map3DMath.ParseMessage("{\"type\":\"edit.commit\",\"target\":{\"kind\":\"decoration\",\"uid\":\"0123456789ab\"},"
                                           + "\"geometry\":{\"type\":\"Point\",\"coordinates\":[127.1,37.5]}}")!.EditCommit!.Scale);
        Assert.Equal("{\"type\":\"edit.key\",\"key\":\"+\",\"shift\":false}", Map3DEdit.EditKeyJson("+", false));
        Assert.Equal("{\"type\":\"edit.key\",\"key\":\"-\",\"shift\":true}", Map3DEdit.EditKeyJson("-", true));
        Assert.Equal("{\"type\":\"edit.start\",\"target\":{\"kind\":\"model\",\"uid\":\"0123456789ab\"}}", Map3DEdit.EditStartJson(Map3DEdit.TargetModel, "0123456789ab"));
    }
}
