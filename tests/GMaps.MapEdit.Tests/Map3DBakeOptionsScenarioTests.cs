using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 만들기 판(영역 B 전수, 2026-10-19) — 실제 MapViewModel 경로: 영역(TakeMap3DArea · 「다시 만들기」) → 판(방식 라디오 · 줌) → [만들기] · [취소]
/// → 가짜 map-3d 가 받은 POST 본문(overlay_mode · overlays[].mask · id). 영역 안 이미지는 지도에 놓은 실제 GMapImageMarker(임시 PNG).
/// 「마지막 굽기 방식」 저장(MapSettingsHelper.SaveMap3DAsync)은 실행 폴더 설정 파일에 쓰므로 — 설정의 마지막 값을 미리 고를 방식과 같게 둬 저장 경로를 타지 않는다.
/// </summary>
public class Map3DBakeOptionsScenarioTests
{
    private const int ImageId = 77;

    private sealed class Rig
    {
        public Map3DEntryScenarioTests.Rig R { get; }
        public MapViewModel Vm => R.Vm;
        public Map3DFakeServer Server { get; } = new();
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), $"map3d-bake-{Guid.NewGuid():N}");

        public Rig()
        {
            R = new Map3DEntryScenarioTests.Rig(Map3DModel.EntryManual);
            R.Settings.PreloadZoom = 16; R.SetZoom(15.5);   // 다 만든 뒤 게이트가 3D 화면(WebView2)을 미리 열지 않게
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Server));
            Directory.CreateDirectory(Dir);
        }

        public double[] Bbox => R.AreaAround("a").Bbox;

        /// <summary>지도에 오버레이 이미지 마커를 놓는다(가로 · 세로 경도 · 위도 범위, 실제 PNG 파일).</summary>
        public GMapImageMarker PlaceImage(double left, double top, double right, double bottom, double rotation = 0)
        {
            string file = Path.Combine(Dir, $"img-{Guid.NewGuid():N}.png");
            var bmp = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 4 * 4], 16);
            using (var fs = File.Create(file)) { var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); enc.Save(fs); }
            var model = new ImageModel
            {
                Id = ImageId, Title = "발전소", FilePath = file, Rotation = rotation, Width = 100, Height = 100, Opacity = 0.8, Visibility = true,
                Left = left, Top = top, Right = right, Bottom = bottom,
            };
            var marker = new GMapImageMarker(Mock.Of<Ironwall.Dotnet.Libraries.Base.Services.ILogService>(), model);
            R.W.Map.Markers.Add(marker);
            return marker;
        }

        public bool Baking => Scenario.GetField<bool>(Vm, "_isMap3DBaking");

        public void Done()
        {
            R.Done();
            try { Directory.Delete(Dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void Run(Action<Rig> body) => Scenario.Sta(() => { var rig = new Rig(); body(rig); rig.Done(); });
    private static void RunAsync(Func<Rig, Task> body) => Scenario.StaAsync(async () => { var rig = new Rig(); await body(rig); rig.Done(); });

    private static async Task Until(Func<bool> condition, string what, double seconds = 6)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end && !condition()) await Task.Delay(20);
        Assert.True(condition(), what);
    }

    private static void Choose(MapViewModel vm, string mode)
    {
        switch (mode)
        {
            case Map3DBake.ModeTiles: vm.IsMap3DBakeTiles = true; break;
            case Map3DBake.ModePhoto: vm.IsMap3DBakePhoto = true; break;
            case Map3DBake.ModeMask: vm.IsMap3DBakeMask = true; break;
            case Map3DBake.ModeExtract: vm.IsMap3DBakeExtract = true; break;
        }
    }

    // ── B-32 · B-33 영역 안 이미지 있음 → 방식 넷 각각의 본문 ──

    [Theory]
    [InlineData(Map3DBake.ModeTiles, null, null)]
    [InlineData(Map3DBake.ModePhoto, "composite", false)]
    [InlineData(Map3DBake.ModeMask, "composite", true)]
    [InlineData(Map3DBake.ModeExtract, "extract", true)]
    public void should_post_overlay_mode_and_mask_per_chosen_mode_when_images_in_area(string mode, string? serverMode, bool? mask)
        => RunAsync(async rig =>
        {
            rig.R.Settings.OverlayMode = mode;   // 마지막 고른 값 = 이번 값(판이 그것을 골라 두는지도 본다 · 저장 경로를 타지 않게)
            var b = rig.Bbox;
            var image = rig.PlaceImage(b[0] + 0.0005, b[3] - 0.0005, b[0] + 0.0015, b[3] - 0.0015, rotation: -30);
            rig.Server.PostStatus = "done";

            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(b, null));

            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            Assert.True(rig.Vm.IsMap3DBakeModeVisible, "영역 안 이미지가 있으면 방식 라디오");
            Assert.Equal("영역 안 오버레이 이미지 1개", rig.Vm.Map3DBakeOptionsSummary);
            Assert.Equal(("3D 만들기", "만들기"), (rig.Vm.Map3DBakeOptionsTitle, rig.Vm.Map3DBakeOptionsConfirmText));
            Assert.Equal(new[] { mode == Map3DBake.ModeTiles, mode == Map3DBake.ModePhoto, mode == Map3DBake.ModeMask, mode == Map3DBake.ModeExtract },
                         new[] { rig.Vm.IsMap3DBakeTiles, rig.Vm.IsMap3DBakePhoto, rig.Vm.IsMap3DBakeMask, rig.Vm.IsMap3DBakeExtract });
            Choose(rig.Vm, Map3DBake.ModeTiles); Choose(rig.Vm, mode);   // 다른 것을 눌렀다 되돌림 — 라디오는 하나만
            rig.Vm.Map3DBakeEnterZoom = 18;
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            Assert.False(rig.Vm.IsMap3DBakeOptionsOpen);
            await Until(() => !rig.Baking, "만들기 끝");

            var post = Assert.Single(rig.Server.Posts());
            Assert.Equal(b, post["bbox"]!.ToObject<double[]>());
            Assert.Equal(18d, post.Value<double>("enter_zoom"));
            Assert.Null(post["rect"]);
            if (serverMode == null)
            {
                Assert.Null(post["overlay_mode"]);
                Assert.Null(post["overlays"]);
                return;
            }
            Assert.Equal(serverMode, post.Value<string>("overlay_mode"));
            var o = (JObject)Assert.Single((JArray)post["overlays"]!);
            Assert.Equal(ImageId, o.Value<int>("id"));                         // 3D 스냅샷과 같은 id(모델 Id)
            Assert.Equal(image.FilePath, o.Value<string>("image_path"));       // PNG 원본 절대 경로 그대로
            Assert.Equal(mask, o.Value<bool>("mask"));
            Assert.Equal(330d, o.Value<double>("rotation"), 9);
            Assert.Equal(0.8, o.Value<double>("opacity"), 9);
            Assert.Equal(image.Left, o["bounds"]!.Value<double>("left"));
        });

    // ── B-33 이미지 없음 → 라디오 숨김 · overlay_mode 없음 ──

    [Fact]
    public void should_hide_mode_radios_and_post_tiles_body_when_no_image_in_area()
        => RunAsync(async rig =>
        {
            rig.R.Settings.OverlayMode = Map3DBake.ModeTiles;
            var b = rig.Bbox;
            rig.PlaceImage(b[2] + 0.01, b[3] + 0.01, b[2] + 0.02, b[3] + 0.005);   // 영역 밖 이미지
            var hidden = rig.PlaceImage(b[0] + 0.0005, b[3] - 0.0005, b[0] + 0.0015, b[3] - 0.0015);
            hidden.Visible = false;                                                   // 숨긴 이미지는 넣지 않는다
            rig.Server.PostStatus = "done";

            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(b, null));

            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen, "줌을 고르게 판은 늘 띄운다");
            Assert.False(rig.Vm.IsMap3DBakeModeVisible);
            Assert.True(rig.Vm.IsMap3DBakeTiles);
            Assert.Equal("영역 안 오버레이 이미지 0개", rig.Vm.Map3DBakeOptionsSummary);
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            await Until(() => !rig.Baking, "만들기 끝");

            var post = Assert.Single(rig.Server.Posts());
            Assert.Null(post["overlay_mode"]);
            Assert.Null(post["overlays"]);
            Assert.Equal(17d, post.Value<double>("enter_zoom"));   // 기본 = 세팅의 3D 최소 줌
        });

    // ── B-43 원본 파일이 없는 이미지는 뺀다 ──

    [Fact]
    public void should_leave_out_image_whose_file_is_gone_when_bake_panel_opens()
        => Run(rig =>
        {
            rig.R.Settings.OverlayMode = Map3DBake.ModeTiles;
            var b = rig.Bbox;
            var gone = rig.PlaceImage(b[0] + 0.0005, b[3] - 0.0005, b[0] + 0.0015, b[3] - 0.0015);
            File.Delete(gone.FilePath!);   // 놓은 뒤 원본 파일이 지워졌다

            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(b, null));

            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            Assert.False(rig.Vm.IsMap3DBakeModeVisible);
            Assert.Equal("영역 안 오버레이 이미지 0개", rig.Vm.Map3DBakeOptionsSummary);
            rig.Vm.CancelMap3DBakeOptionsCommand.Execute(null);
        });

    // ── B-44 「3D 줌 바꾸기…」 — 같은 판을 줌만 ──

    [Fact]
    public void should_open_zoom_only_panel_and_patch_zoom_when_change_zoom_chosen()
        => RunAsync(async rig =>
        {
            var b = rig.Bbox;
            rig.PlaceImage(b[0] + 0.0005, b[3] - 0.0005, b[0] + 0.0015, b[3] - 0.0015);   // 영역 안 이미지가 있어도 방식은 묻지 않는다
            rig.R.Areas(rig.R.AreaAround("x", enterZoom: 17.5));
            var change = rig.Vm.Map3DAreas.Single().Actions.Single(a => a.Header == "3D 줌 바꾸기…");

            change.Command.Execute(null);
            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            Assert.Equal(("3D 줌 바꾸기", "바꾸기", "영역: 시험 x"), (rig.Vm.Map3DBakeOptionsTitle, rig.Vm.Map3DBakeOptionsConfirmText, rig.Vm.Map3DBakeOptionsSummary));
            Assert.False(rig.Vm.IsMap3DBakeModeVisible);
            Assert.Equal(17.5, rig.Vm.Map3DBakeEnterZoom);
            rig.Vm.CancelMap3DBakeOptionsCommand.Execute(null);
            await Task.Delay(50);
            Assert.Empty(rig.Server.Requests);

            change.Command.Execute(null);
            rig.Vm.Map3DBakeEnterZoom = 18.5;
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            await Until(() => (rig.Vm.AimStatusMessage ?? "").Contains("바꿨습니다"), "줌 바꿈 안내");
            var patch = Assert.Single(rig.Server.Requests, r => r.Method.Method == "PATCH");
            Assert.Equal("/api/jobs/x", patch.Path);
            Assert.Equal(18.5, JObject.Parse(patch.Body!).Value<double>("enter_zoom"));
            Assert.Empty(rig.Server.Posts());   // 다시 굽지 않는다
            Assert.Equal(18.5, Scenario.GetField<List<Map3DScene>>(rig.Vm, "_map3DScenes").Single().EnterZoom);
        });

    // ── B-34 [취소] → POST 0 · 다시 만들기 표지 새지 않음 ──

    [Fact]
    public void should_post_nothing_and_clear_rebake_marks_when_bake_panel_cancelled()
        => RunAsync(async rig =>
        {
            rig.R.Settings.OverlayMode = Map3DBake.ModeTiles;
            rig.R.Areas(rig.R.AreaAround("x"));
            rig.Vm.Map3DAreas.Single().Actions.Single(a => a.Header == "다시 만들기").Command.Execute(null);
            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            Assert.Equal("x", Scenario.GetField<string?>(rig.Vm, "_map3DBakeReplaces"));

            rig.Vm.CancelMap3DBakeOptionsCommand.Execute(null);

            Assert.False(rig.Vm.IsMap3DBakeOptionsOpen);
            await Task.Delay(100);
            Assert.Empty(rig.Server.Posts());
            Assert.Null(Scenario.GetField<string?>(rig.Vm, "_map3DBakeReplaces"));
            Assert.Null(Scenario.GetField<string?>(rig.Vm, "_map3DBakeOptionsSourceId"));
            Assert.Null(Scenario.GetField<Map3DArea?>(rig.Vm, "_map3DBakeOptionsArea"));

            // 그 뒤 새 영역을 만들어도 옛 영역을 지우거나 「만들기 취소」를 달지 않는다
            rig.Server.PostStatus = "done";
            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(rig.R.AreaAround("n", dLon: 0.01).Bbox, null));
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            await Until(() => !rig.Baking, "만들기 끝");
            Assert.Equal(0, rig.Server.Count(HttpMethod.Delete, "/api/jobs/x"));
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == "x");
        });

    // ── B-35 회전 영역 — 감싸는 bbox 모서리(사각형 밖)의 이미지 ──

    /// <summary>
    /// 결정(의도된 근사): 회전 영역의 겹치는 이미지는 감싸는 bbox 로 고른다(RequestMap3DBake 주석 — map-3d 가 회전 사각형 밖 이미지는 굽지 않는다).
    /// 그래서 사각형 밖 bbox 모서리에만 있는 이미지도 판이 센다(라디오가 보인다). 지금 동작을 기록한다.
    /// </summary>
    [Fact]
    public void should_count_image_in_bbox_corner_outside_rotated_rect_when_bake_panel_opens()
        => Run(rig =>
        {
            rig.R.Settings.OverlayMode = Map3DBake.ModeExtract;
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng, c.Lat, 300, 160, 45);
            var b = rect.Bbox();
            var corner = new PointLatLng(b[3] - 0.00005, b[2] - 0.00005);
            Assert.False(rect.Contains(corner.Lng, corner.Lat));
            rig.PlaceImage(corner.Lng - 0.00003, corner.Lat + 0.00003, corner.Lng + 0.00003, corner.Lat - 0.00003);

            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(b, rect));

            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            Assert.True(rig.Vm.IsMap3DBakeModeVisible);
            Assert.Equal("영역 안 오버레이 이미지 1개", rig.Vm.Map3DBakeOptionsSummary);
            rig.Vm.CancelMap3DBakeOptionsCommand.Execute(null);
        });
}
