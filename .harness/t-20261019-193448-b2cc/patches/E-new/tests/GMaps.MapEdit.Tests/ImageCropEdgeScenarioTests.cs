using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 오버레이 이미지 자르기 — 경계 · 오류 · 취소 · 순서 · 연타 · 조합 시나리오(t-20261019-193448-b2cc 영역 E).
/// ImageCropScenarioTests 의 정상 경로 옆에서, 판이 떠 있는 동안의 다른 조작(지도 키 · 편집 모드 끄기 · 그림 바뀜 · 또 불러오기) ·
/// 저장 실패(DB · 저장 폴더) · 기록 깨짐 · 여러 번 자르기/되돌리기를 창 없이 실제 MapViewModel 경로로 본다.
/// </summary>
public class ImageCropEdgeScenarioTests
{
    // ─────────────── 장치(ImageCropScenarioTests 와 같은 규칙 + 실패 주입) ───────────────

    private sealed class TempImageFiles : IImageFileService
    {
        public string Base { get; } = Path.Combine(Path.GetTempPath(), $"imgcrop-edge-{Guid.NewGuid():N}");
        public int Copies { get; private set; }
        /// <summary>있으면 복사 때 던진다(저장 폴더에 쓸 수 없음).</summary>
        public Exception? CopyFailure { get; set; }
        public void EnsureImageDirectory() => Directory.CreateDirectory(Path.Combine(Base, "Images/Overlays"));
        public string CopyImageToLocal(string sourcePath)
        {
            if (CopyFailure != null) throw CopyFailure;
            EnsureImageDirectory();
            var relative = $"Images/Overlays/{Path.GetFileNameWithoutExtension(sourcePath)}_{DateTime.Now:yyyyMMdd_HHmmss}_{Copies}{Path.GetExtension(sourcePath)}";
            File.Copy(sourcePath, GetAbsolutePath(relative));
            Copies++;
            return relative;
        }
        public string GetAbsolutePath(string relativePath) => IsRelativePath(relativePath) ? Path.Combine(Base, relativePath) : relativePath;
        public void DeleteLocalImage(string relativePath) { }
        public bool IsRelativePath(string path) => !string.IsNullOrEmpty(path) && !Path.IsPathRooted(path);
    }

    private sealed class Rig
    {
        public WiredMapRig W { get; } = WiredMapRig.Create();
        public MapViewModel Vm => W.Vm;
        public TempImageFiles Files { get; } = new();
        public Mock<IGMapDbService> MapDb { get; } = new();
        public List<IMapLayerModel> Layers { get; } = new();
        public List<string> Updated { get; } = new();

        public Rig()
        {
            Scenario.SetField(Vm, "_imageFileService", Files);
            Scenario.SetField(Vm, "_gMapDbService", MapDb.Object);
            MapDb.Setup(d => d.FetchMapLayersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Layers);
            MapDb.Setup(d => d.UpdateMapLayerAsync(It.IsAny<IMapLayerModel>(), It.IsAny<CancellationToken>()))
                 .Callback<IMapLayerModel, CancellationToken>((l, _) => Updated.Add(l.FilePath ?? ""))
                 .ReturnsAsync(true);
        }

        /// <summary>사용자 폴더에 w×h 불투명 PNG.</summary>
        public string UserPng(int w, int h, string name = "발전소.png")
        {
            var px = new byte[w * h * 4];
            for (int i = 0; i < px.Length; i += 4) { px[i] = 200; px[i + 1] = 120; px[i + 2] = 40; px[i + 3] = 255; }
            var dir = Path.Combine(Files.Base, "user");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name);
            File.WriteAllBytes(path, ImageCropMath.EncodePng(BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4)));
            return path;
        }

        public ImageCropViewModel WaitPanel()
        {
            for (int i = 0; i < 50 && Vm.ImageCrop == null; i++) Scenario.Pump();
            Assert.True(Vm.IsImageCropPanelOpen);
            return Vm.ImageCrop!;
        }

        /// <summary>판이 닫힐 때까지(완료 이어짐은 비동기) 디스패처를 돌린다.</summary>
        public void WaitClosed()
        {
            for (int i = 0; i < 50 && Vm.IsImageCropPanelOpen; i++) Scenario.Pump();
        }

        public static void Draw(ImageCropViewModel crop, params Point[] points)
        {
            crop.Fit(800, 600);
            foreach (var p in points) { var s = crop.ImageToScreen(p); crop.PointerDown(s); crop.PointerUp(s); }
            Assert.True(crop.Close());
        }

        public GMapImageMarker PlaceImage(string relative, RectLatLng bounds, double bearing, double width, double height)
        {
            var model = new ImageModel
            {
                Id = 77, Title = "발전소", FilePath = relative, Rotation = bearing, Width = width, Height = height, Opacity = 1, Visibility = true,
                Top = bounds.Lat, Left = bounds.Lng, Right = bounds.Lng + bounds.WidthLng, Bottom = bounds.Lat - bounds.HeightLat,
            };
            var marker = new GMapImageMarker(Mock.Of<Ironwall.Dotnet.Libraries.Base.Services.ILogService>(), model);
            W.Map.Markers.Add(marker);
            Layers.Add(new MapLayerModel { Id = 5, Name = "발전소", LayerType = "OverlayImage", Category = "OverlayImage", FilePath = relative });
            return marker;
        }

        /// <summary>다시 자르기 한 번 — 판을 열고 다각형을 그려 적용까지.</summary>
        public async Task RecropAsync(GMapImageMarker marker, params Point[] polygon)
        {
            var task = Vm.RecropImageAsync(marker);
            Draw(WaitPanel(), polygon);
            Vm.ImageCrop!.ApplyCommand.Execute(null);
            await task;
        }

        public List<string> Logged(string level)
            => W.Log.Invocations.Where(i => i.Method.Name == level).Select(i => (string)i.Arguments[0]).ToList();

        /// <summary>끝 — 판이 닫혔고, Error 로그는 expectedErrorPrefix 로 시작하는 것만 허용.</summary>
        public void Done(string? expectedErrorPrefix = null)
        {
            Assert.False(Vm.IsImageCropPanelOpen);
            Assert.Null(Vm.ImageCropPanel);
            Assert.DoesNotContain(W.Errors, e => expectedErrorPrefix == null || !e.StartsWith(expectedErrorPrefix));
            try { Directory.Delete(Files.Base, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static readonly RectLatLng Placed = new(37.5010, 127.0000, 0.0040, 0.0020);

    private static void SameBounds(RectLatLng e, RectLatLng a, double tolerance = 1e-10)
    {
        Assert.Equal(e.Lat, a.Lat, tolerance);
        Assert.Equal(e.Lng, a.Lng, tolerance);
        Assert.Equal(e.WidthLng, a.WidthLng, tolerance);
        Assert.Equal(e.HeightLat, a.HeightLat, tolerance);
    }

    private static readonly Point[] RightHalf = { new(100, 0), new(200, 0), new(200, 100), new(100, 100) };   // 200×100 의 오른쪽 절반

    private static List<string> CroppedFiles(Rig rig)
    {
        var dir = Path.Combine(rig.Files.Base, "Images", "Overlays");
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*_crop_*.png").ToList() : new List<string>();
    }

    private static KeyEventArgs KeyFrom(object source, Key key)
    {
        var e = EditRig.Key(key);
        e.Source = source;   // 키 원본 = 판(실제로는 판 안 캔버스 · 단추) — OriginalSource 도 이것
        return e;
    }

    // ─────────────── D-1 / E-18 판 안 키를 지도 처리기가 가로챔 ───────────────

    [Fact]
    [Trait("Scenario", "E-18")]
    [Trait("Defect", "D-1")]
    public void should_leave_delete_and_arrow_keys_to_panel_when_key_comes_from_image_crop_panel() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        rig.Vm.SelectedMarker = marker;                                        // 지도에서 그 그림이 선택된 채 우클릭 「다각형으로 자르기」
        var task = rig.Vm.RecropImageAsync(marker);
        var crop = rig.WaitPanel();
        var panel = rig.Vm.ImageCropPanel!;

        var del = KeyFrom(panel, Key.Delete);
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.W.Map, del);
        await Task.Delay(20);
        Assert.False(del.Handled, "판에서 온 Delete 를 지도 처리기가 소비했다");
        Assert.DoesNotContain(rig.W.Published, m => m.GetType().Name == "OpenConfirmPopupMessageModel");   // 선택 그림 삭제 확인창 없음

        var bounds = marker.ImageBounds;
        var right = KeyFrom(panel, Key.Right);
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.W.Map, right);
        Assert.False(right.Handled, "판에서 온 방향키를 지도 처리기가 소비했다");
        SameBounds(bounds, marker.ImageBounds);                                // 그림이 움직이지 않는다

        // 판 밖(지도)에서 온 Delete 는 예전처럼 지도 몫 — 가드는 판 안 키만
        var mapDel = KeyFrom(rig.W.Map, Key.Delete);
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.W.Map, mapDel);
        await Task.Delay(20);
        Assert.True(mapDel.Handled);

        crop.CancelCommand.Execute(null);
        await task;
        Assert.Equal(relative, marker.FilePath);
        rig.Done();
    });

    // ─────────────── D-2 / E-17 판이 떠 있는데 편집 모드 끄기 ───────────────

    [Fact]
    [Trait("Scenario", "E-17")]
    [Trait("Defect", "D-2")]
    public void should_cancel_open_crop_panel_and_add_nothing_when_edit_mode_is_turned_off() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var user = rig.UserPng(120, 80);
        var first = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        var crop = rig.WaitPanel();
        Rig.Draw(crop, new Point(10, 10), new Point(110, 10), new Point(60, 70));   // 그려 두고 적용 전

        rig.Vm.IsEditModeEnabled = false;
        await Task.WhenAny(first, Task.Delay(2000));
        try
        {
            Assert.True(first.IsCompleted, "편집 모드를 껐는데 자르기 판이 남아 있다");
            Assert.Null(await first);                                          // 처음 불러오기 = 취소(추가 안 함), 「그대로」가 아님
            Assert.Equal(0, rig.Files.Copies);
            rig.WaitClosed();
            Assert.False(rig.Vm.IsImageCropPanelOpen);
        }
        finally { if (!first.IsCompleted) crop.CancelCommand.Execute(null); }

        // 다시 자르기 판도 — 편집 모드를 끄면 그림 · DB · Undo 그대로
        rig.Vm.IsEditModeEnabled = true;
        var relative = rig.Files.CopyImageToLocal(user);
        var marker = rig.PlaceImage(relative, Placed, 0, 240, 160);
        rig.W.Undo.Clear();
        var recrop = rig.Vm.RecropImageAsync(marker);
        var crop2 = rig.WaitPanel();
        rig.Vm.IsEditModeEnabled = false;
        await Task.WhenAny(recrop, Task.Delay(2000));
        bool closedByEditOff = recrop.IsCompleted;
        if (!closedByEditOff) crop2.CancelCommand.Execute(null);
        await recrop;
        Assert.True(closedByEditOff, "편집 모드를 껐는데 다시 자르기 판이 남아 있다");
        Assert.Equal(relative, marker.FilePath);
        Assert.False(rig.W.Undo.CanUndo);
        rig.W.Db.Verify(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()), Times.Never());
        rig.Done();
    });

    // ─────────────── D-3 / E-33 판이 떠 있는 동안 그림이 바뀜 ───────────────

    [Fact]
    [Trait("Scenario", "E-33")]
    [Trait("Defect", "D-3")]
    public void should_not_apply_and_tell_when_image_changes_while_recrop_panel_is_open() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 30, 400, 200);
        rig.W.Undo.Clear();
        await rig.RecropAsync(marker, RightHalf);                              // 한 번 자름(100×100)
        Assert.Contains("_crop_", marker.FilePath);

        var task = rig.Vm.RecropImageAsync(marker);                            // 자른 그림을 다시 자르려고 판을 엶
        var crop = rig.WaitPanel();
        Assert.True(await rig.W.Undo.UndoAsync());                             // 판이 떠 있는 동안 툴바 Undo — 그림이 자르기 전으로
        Assert.Equal(relative, marker.FilePath);
        Rig.Draw(crop, new Point(0, 0), new Point(100, 0), new Point(100, 50), new Point(0, 50));
        crop.ApplyCommand.Execute(null);
        await task;

        Assert.Equal(relative, marker.FilePath);                               // 섞여 적용되지 않는다
        SameBounds(Placed, marker.ImageBounds, 1e-9);
        Assert.Equal((400d, 200d), (marker.Width, marker.Height));
        Assert.Equal("그림이 바뀌어 자르기를 취소했습니다", rig.Vm.AimStatusMessage);
        Assert.False(rig.W.Undo.CanUndo);                                       // 새 Undo 없음
        rig.Done();
    });

    // ─────────────── D-4 / E-07 · E-08 남는 픽셀 0개 ───────────────

    [Fact]
    [Trait("Scenario", "E-07")]
    [Trait("Defect", "D-4")]
    public void should_block_apply_with_reason_when_polygon_keeps_no_pixel()
    {
        // 넓이 0.5(검사 통과) — 1×1 그림의 삼각형: 픽셀 중심 (0.5,0.5) 가 빗변 위(바깥)
        var triangle = new[] { new Point(0, 0), new Point(1, 0), new Point(0, 1) };
        // 높이 0.8 px 띠(넓이 80) — 픽셀 중심 y=0.5 · 1.5 둘 다 밖
        var band = new[] { new Point(0, 0.6), new Point(100, 0.6), new Point(100, 1.4), new Point(0, 1.4) };
        foreach (var polygon in new[] { triangle, band })
        {
            var px = Enumerable.Repeat((byte)255, 100 * 2 * 4).ToArray();
            var masked = ImageCropMath.MaskAndCrop(px, 100, 2, polygon, ImageCropMath.BoundingBox(polygon, 100, 2));
            Assert.All(masked, b => Assert.Equal(0, b));                        // 정말로 남는 픽셀이 없다(전제)
            Assert.Contains("픽셀", ImageCropMath.Validate(polygon) ?? "(통과)");
        }
        // 작아도 픽셀이 남으면 된다
        Assert.Null(ImageCropMath.Validate(new[] { new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1) }));
        Assert.Null(ImageCropMath.Validate(new[] { new Point(0, 0.4), new Point(100, 0.4), new Point(100, 0.6), new Point(0, 0.6) }));   // 0.2 px 띠라도 중심 줄을 지남
    }

    [Fact]
    [Trait("Scenario", "E-08")]
    [Trait("Defect", "D-4")]
    public void should_agree_with_mask_when_pixel_check_runs_on_random_polygons()
    {
        // 남는 픽셀 판정이 실제 자르기(MaskAndCrop)와 같은가 — 작은 무작위 삼각형 · 사각형 600개
        var random = new Random(20261019);
        const int w = 6, h = 6;
        var px = Enumerable.Repeat((byte)255, w * h * 4).ToArray();
        for (int n = 0; n < 600; n++)
        {
            int count = n % 2 == 0 ? 3 : 4;
            var polygon = Enumerable.Range(0, count).Select(_ => new Point(Math.Round(random.NextDouble() * w, 1), Math.Round(random.NextDouble() * h, 1))).ToArray();
            var masked = ImageCropMath.MaskAndCrop(px, w, h, polygon, ImageCropMath.BoundingBox(polygon, w, h));
            bool any = false;
            for (int i = 3; i < masked.Length; i += 4) any |= masked[i] != 0;
            Assert.True(any == ImageCropMath.HasAnyPixel(polygon), $"다각형 {string.Join(" ", polygon)} — 자르기 결과 {any}, 판정 {!any}");
        }
    }

    [Fact]
    [Trait("Scenario", "E-07")]
    [Trait("Defect", "D-4")]
    public void should_keep_panel_open_with_reason_when_first_load_polygon_keeps_no_pixel() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var task = rig.Vm.CopyOverlayImageWithCropAsync(rig.UserPng(1, 1), "점");
        var crop = rig.WaitPanel();
        Rig.Draw(crop, new Point(0, 0), new Point(1, 0), new Point(0, 1));
        crop.ApplyCommand.Execute(null);
        try
        {
            Assert.False(crop.IsCompleted, "남는 픽셀이 없는데 적용됐다(완전 투명 PNG)");
            Assert.Contains("픽셀", crop.Error);
        }
        finally { if (!crop.IsCompleted) crop.CancelCommand.Execute(null); }
        Assert.Null(await task);
        Assert.Empty(CroppedFiles(rig));
        rig.Done();
    });

    // ─────────────── D-5 / E-34 DB 저장 실패 ───────────────

    [Fact]
    [Trait("Scenario", "E-34")]
    [Trait("Defect", "D-5")]
    public void should_keep_image_and_add_no_undo_when_db_save_fails_on_crop_or_revert() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 30, 400, 200);
        rig.W.Undo.Clear();
        rig.W.Db.Setup(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("DB 끊김"));

        await rig.RecropAsync(marker, RightHalf);
        Assert.Equal(relative, marker.FilePath);                               // 화면 · 모델 = 이전(DB 와 같음)
        Assert.Equal(relative, marker.ImageModel.FilePath);
        SameBounds(Placed, marker.ImageBounds);
        Assert.Equal((400d, 200d), (marker.Width, marker.Height));
        Assert.False(rig.W.Undo.CanUndo);                                       // 저장 안 된 일을 Undo 로 쌓지 않는다
        Assert.Empty(rig.Updated);                                             // MapLayers 경로도 그대로
        Assert.Equal("저장하지 못해 자르지 않았습니다", rig.Vm.AimStatusMessage);
        Assert.Contains(rig.W.AllErrors, e => e.StartsWith("[ImageCrop] 저장 실패 id=77"));

        // 자르기 되돌리기 저장 실패 — 자른 그림 그대로
        rig.W.Db.Setup(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>())).ReturnsAsync((IImageModel m, CancellationToken _) => m);
        await rig.RecropAsync(marker, RightHalf);
        var cropped = marker.FilePath;
        var croppedBounds = marker.ImageBounds;
        rig.W.Undo.Clear();
        rig.W.Db.Setup(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("DB 끊김"));
        await rig.Vm.UncropImageAsync(marker);
        Assert.Equal(cropped, marker.FilePath);
        SameBounds(croppedBounds, marker.ImageBounds);
        Assert.False(rig.W.Undo.CanUndo);
        Assert.Equal("저장하지 못해 되돌리지 않았습니다", rig.Vm.AimStatusMessage);
        rig.Done("[ImageCrop] 저장 실패");
    });

    // ─────────────── D-6 / E-16 판이 떠 있는데 또 불러오기 ───────────────

    [Fact]
    [Trait("Scenario", "E-16")]
    [Trait("Defect", "D-6")]
    public void should_tell_to_close_panel_first_and_keep_first_panel_when_image_load_is_requested_again() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var user = rig.UserPng(60, 40);
        var first = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        var crop = rig.WaitPanel();

        var second = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");   // 판이 떠 있는 동안 또 불러오기(파일까지 고른 뒤)
        Assert.True(second.IsCompleted);
        Assert.Null(await second);
        Assert.Equal("자르기 판을 먼저 닫으세요", rig.Vm.AimStatusMessage);
        Assert.True(rig.Vm.IsAimStatusVisible);
        Assert.Same(crop, rig.Vm.ImageCrop);                                   // 첫 판은 그대로
        Assert.Equal(0, rig.Files.Copies);
        Assert.True(rig.Vm.LoadImageOverlayCommand?.CanExecute(null) ?? true); // CanExecute 는 바꾸지 않는다(단축키 무반응 대신 안내)

        crop.SkipCommand.Execute(null);
        Assert.NotNull(await first);
        Assert.Equal(1, rig.Files.Copies);
        rig.Done();
    });

    // ─────────────── E-10 · E-11 처음 불러올 때 오류 ───────────────

    [Fact]
    [Trait("Scenario", "E-10")]
    public void should_copy_unchanged_without_panel_and_warn_when_picked_file_is_not_a_readable_image() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var dir = Path.Combine(rig.Files.Base, "user");
        Directory.CreateDirectory(dir);
        var broken = Path.Combine(dir, "깨짐.png");
        File.WriteAllBytes(broken, new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5 });   // PNG 머리만 있고 깨짐

        var task = rig.Vm.CopyOverlayImageWithCropAsync(broken, "깨짐");
        Assert.False(rig.Vm.IsImageCropPanelOpen);                             // 판 없이
        var relative = await task;
        Assert.NotNull(relative);
        Assert.Equal(File.ReadAllBytes(broken), File.ReadAllBytes(rig.Files.GetAbsolutePath(relative!)));   // 그대로 복사(예전 동작)
        Assert.Contains(rig.Logged("Warning"), m => m.StartsWith("[ImageCrop] 그림을 읽지 못해 자르기 없이 추가"));
        rig.Done();
    });

    [Fact]
    [Trait("Scenario", "E-11")]
    public void should_tell_and_add_nothing_without_throwing_when_overlay_folder_cannot_be_written_on_first_load() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var user = rig.UserPng(80, 60);

        // ① 복사부터 실패(「그대로」)
        rig.Files.CopyFailure = new UnauthorizedAccessException("쓰기 금지");
        var skip = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        rig.WaitPanel().SkipCommand.Execute(null);
        Assert.Null(await skip);
        Assert.Equal("그림을 저장하지 못해 추가하지 않았습니다 — 저장 폴더를 확인하세요", rig.Vm.AimStatusMessage);

        // ② 복사는 됐고 자른 PNG 쓰기 실패(「적용」) — 저장 폴더 자리에 같은 이름 파일이 있어 폴더를 못 만든다
        rig.Files.CopyFailure = null;
        rig.Vm.IsAimStatusVisible = false;
        var blocker = Path.Combine(rig.Files.Base, "blocker");
        File.WriteAllText(blocker, "폴더 아님");
        Scenario.SetField(rig.Vm, "_imageCropResolve", (Func<string, string>)(p => p.Contains("_crop_") ? Path.Combine(blocker, p) : rig.Files.GetAbsolutePath(p)));
        var apply = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        Rig.Draw(rig.WaitPanel(), new Point(5, 5), new Point(70, 5), new Point(40, 50));
        rig.Vm.ImageCrop!.ApplyCommand.Execute(null);
        Assert.Null(await apply);
        Assert.True(rig.Vm.IsAimStatusVisible);
        Assert.StartsWith("그림을 저장하지 못해 추가하지 않았습니다", rig.Vm.AimStatusMessage);
        rig.Done("[ImageCrop] 저장 폴더에 쓰지 못해");
    });

    [Fact]
    [Trait("Scenario", "E-11")]
    public void should_tell_and_leave_placed_image_when_cropped_png_cannot_be_written_on_recrop() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        rig.W.Undo.Clear();
        var blocker = Path.Combine(rig.Files.Base, "blocker");
        File.WriteAllText(blocker, "폴더 아님");
        Scenario.SetField(rig.Vm, "_imageCropResolve", (Func<string, string>)(p => p.Contains("_crop_") ? Path.Combine(blocker, p) : rig.Files.GetAbsolutePath(p)));

        await rig.RecropAsync(marker, RightHalf);                              // 예외가 위로 새지 않는다
        Assert.Equal(relative, marker.FilePath);
        SameBounds(Placed, marker.ImageBounds);
        Assert.False(rig.W.Undo.CanUndo);
        Assert.Equal("자른 그림을 저장하지 못했습니다 — 저장 폴더를 확인하세요", rig.Vm.AimStatusMessage);
        rig.W.Db.Verify(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()), Times.Never());
        rig.Done("[ImageCrop] 자른 그림 저장 실패");
    });

    // ─────────────── E-15 적용 연타 · 적용 뒤 취소 ───────────────

    [Fact]
    [Trait("Scenario", "E-15")]
    public void should_produce_one_result_when_apply_is_pressed_twice_or_cancel_follows_apply() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var task = rig.Vm.CopyOverlayImageWithCropAsync(rig.UserPng(100, 80), "발전소");
        var crop = rig.WaitPanel();
        Rig.Draw(crop, new Point(10, 10), new Point(90, 10), new Point(50, 70));
        crop.ApplyCommand.Execute(null);
        crop.ApplyCommand.Execute(null);
        crop.CancelCommand.Execute(null);
        crop.SkipCommand.Execute(null);
        var relative = await task;
        Assert.Contains("_crop_", relative);                                   // 첫 결과(적용) 하나
        Assert.Equal(ImageCropOutcome.Apply, crop.Completion.Result);
        Assert.Equal(1, rig.Files.Copies);
        Assert.Single(CroppedFiles(rig));
        rig.Done();
    });

    // ─────────────── E-25 ~ E-28 되돌리기 · 다시 자르기 오류 ───────────────

    [Fact]
    [Trait("Scenario", "E-25")]
    public void should_tell_and_keep_cropped_image_when_pre_crop_file_is_missing_on_revert() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        await rig.RecropAsync(marker, RightHalf);
        var cropped = marker.FilePath;
        File.Delete(rig.Files.GetAbsolutePath(relative));
        rig.W.Undo.Clear();

        await rig.Vm.UncropImageAsync(marker);
        Assert.Equal(cropped, marker.FilePath);
        Assert.Equal("자르기 전 그림 파일이 없어 되돌릴 수 없습니다", rig.Vm.AimStatusMessage);
        Assert.False(rig.W.Undo.CanUndo);
        Assert.True(rig.Vm.IsCroppedImage(marker));                            // 메뉴는 보이되(기록은 있다) 누르면 안내
        rig.Done();
    });

    [Fact]
    [Trait("Scenario", "E-26")]
    public void should_treat_image_as_not_cropped_when_crop_record_is_broken() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        await rig.RecropAsync(marker, RightHalf);
        Assert.True(rig.Vm.IsCroppedImage(marker));
        File.WriteAllText(rig.Files.GetAbsolutePath(marker.FilePath!) + ImageCropStore.RecordSuffix, "{ 깨진 기록");
        var cropped = marker.FilePath;
        rig.W.Undo.Clear();

        Assert.False(rig.Vm.IsCroppedImage(marker));                           // 「자르기 되돌리기」 안 보임
        await rig.Vm.UncropImageAsync(marker);
        Assert.Equal(cropped, marker.FilePath);
        Assert.Equal("자른 그림이 아니라 되돌릴 것이 없습니다", rig.Vm.AimStatusMessage);
        Assert.False(rig.W.Undo.CanUndo);
        rig.Done();
    });

    [Fact]
    [Trait("Scenario", "E-27")]
    public void should_tell_no_permission_and_keep_cropped_image_when_revert_is_requested_without_edit_permission() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        await rig.RecropAsync(marker, RightHalf);
        var cropped = marker.FilePath;
        rig.W.Undo.Clear();
        rig.W.Permission.Setup(p => p.CanEdit("map")).Returns(false);

        await rig.Vm.UncropImageAsync(marker);
        Assert.Equal(cropped, marker.FilePath);
        Assert.False(rig.W.Undo.CanUndo);
        Assert.Contains(rig.W.Published, m => m.GetType().Name == "OpenInfoPopupMessageModel");   // 「권한 없음」 안내
        rig.Done();
    });

    [Fact]
    [Trait("Scenario", "E-28")]
    public void should_tell_and_not_open_panel_when_placed_image_file_is_missing_on_recrop() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        File.Delete(rig.Files.GetAbsolutePath(relative));

        await rig.Vm.RecropImageAsync(marker);
        Assert.False(rig.Vm.IsImageCropPanelOpen);
        Assert.Equal("그림 파일을 읽을 수 없어 자를 수 없습니다", rig.Vm.AimStatusMessage);
        Assert.Contains(rig.Logged("Warning"), m => m.StartsWith($"[ImageCrop] 그림 읽기 실패 {relative}"));
        Assert.Equal(relative, marker.FilePath);
        rig.Done();
    });

    // ─────────────── E-29 · E-30 여러 번 · 회전과 조합 ───────────────

    [Fact]
    [Trait("Scenario", "E-29")]
    public void should_revert_one_step_at_a_time_when_image_was_cropped_twice() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 30, 400, 200);
        await rig.RecropAsync(marker, RightHalf);                              // 1차: 200×100 → 오른쪽 절반 100×100
        var once = marker.FilePath;
        var onceBounds = marker.ImageBounds;
        await rig.RecropAsync(marker, new Point(0, 50), new Point(100, 50), new Point(100, 100), new Point(0, 100));   // 2차: 아래 절반 100×50
        Assert.NotEqual(once, marker.FilePath);
        Assert.Equal((200d, 100d), (marker.Width, marker.Height));
        Assert.Equal(once, ImageCropStore.Read(rig.Files.GetAbsolutePath, marker.FilePath)!.Source);

        await rig.Vm.UncropImageAsync(marker);                                  // 한 단계 — 1차 결과로
        Assert.Equal(once, marker.FilePath);
        SameBounds(onceBounds, marker.ImageBounds, 1e-9);
        Assert.Equal(200, marker.Width, 9);
        Assert.Equal(200, marker.Height, 9);
        await rig.Vm.UncropImageAsync(marker);                                  // 또 한 단계 — 원본으로
        Assert.Equal(relative, marker.FilePath);
        SameBounds(Placed, marker.ImageBounds, 1e-9);
        Assert.Equal(400, marker.Width, 9);
        Assert.Equal(200, marker.Height, 9);
        Assert.False(rig.Vm.IsCroppedImage(marker));
        rig.Done();
    });

    [Fact]
    [Trait("Scenario", "E-30")]
    public void should_restore_around_cropped_part_with_current_rotation_when_rotation_changed_after_crop() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        await rig.RecropAsync(marker, RightHalf);
        marker.Bearing = 45;                                                   // 자른 뒤 사용자가 돌렸다
        var cropped = marker.ImageBounds;

        await rig.Vm.UncropImageAsync(marker);
        Assert.Equal(relative, marker.FilePath);
        Assert.Equal(45, marker.Bearing, 9);                                   // 회전은 그대로
        var box = new Int32Rect(100, 0, 100, 100);
        SameBounds(ImageCropMath.UncropBounds(cropped, 45, 200, 100, box), marker.ImageBounds);
        // 남은 부분(오른쪽 절반)은 제자리 — 되돌린 경계를 지금 회전으로 다시 자르면 자른 경계와 같다
        SameBounds(cropped, ImageCropMath.CropBounds(marker.ImageBounds, 45, 200, 100, box), 1e-9);
        rig.Done();
    });

    // ─────────────── E-31 · E-32 · E-35 연타 · 삭제와 조합 ───────────────

    [Fact]
    [Trait("Scenario", "E-31")]
    public void should_keep_single_panel_when_recrop_menu_is_clicked_repeatedly() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        var first = rig.Vm.RecropImageAsync(marker);
        var crop = rig.WaitPanel();
        var again = rig.Vm.RecropImageAsync(marker);
        var third = rig.Vm.RecropImageAsync(marker);
        Assert.True(again.IsCompleted && third.IsCompleted);                    // 두 번째부터는 아무것도 하지 않는다
        Assert.Same(crop, rig.Vm.ImageCrop);
        Assert.Same(crop, rig.Vm.ImageCropPanel!.DataContext);
        crop.CancelCommand.Execute(null);
        await first;
        rig.Done();
    });

    [Fact]
    [Trait("Scenario", "E-32")]
    public void should_ignore_apply_when_image_was_deleted_while_recrop_panel_is_open() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        rig.W.Undo.Clear();
        var task = rig.Vm.RecropImageAsync(marker);
        var crop = rig.WaitPanel();
        rig.W.Map.Markers.Remove(marker);                                      // 판이 떠 있는 동안 그 그림이 지워짐
        marker.Dispose();
        Rig.Draw(crop, RightHalf);
        crop.ApplyCommand.Execute(null);
        await task;

        Assert.False(rig.W.Undo.CanUndo);
        Assert.Empty(CroppedFiles(rig));                                       // 파일도 쓰지 않는다
        rig.W.Db.Verify(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Empty(rig.Updated);
        rig.Done();
    });

    [Fact]
    [Trait("Scenario", "E-35")]
    public void should_do_nothing_when_crop_is_undone_after_cropped_image_was_deleted() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        rig.W.Undo.Clear();
        await rig.RecropAsync(marker, RightHalf);
        rig.W.Map.Markers.Remove(marker);                                      // 지도에서 지움(삭제 Undo 없이 — 자르기 Undo 만 남은 상태)
        marker.Dispose();
        rig.W.Db.Invocations.Clear();
        rig.Updated.Clear();

        Assert.Equal("다각형으로 자르기", rig.W.Undo.NextUndoDescription);
        await rig.W.Undo.UndoAsync();                                          // 예외 없이
        rig.W.Db.Verify(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Empty(rig.Updated);
        Assert.DoesNotContain(marker, rig.W.Map.Markers);                      // 되살아나지 않는다
        rig.Done();
    });

    // ─────────────── E-37 MapLayers 행 없음 ───────────────

    [Fact]
    [Trait("Scenario", "E-37")]
    public void should_crop_quietly_without_layer_update_when_map_layers_has_no_row_for_image() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 0, 400, 200);
        rig.Layers.Clear();                                                    // 예전 데이터 — 레이어 행이 없다
        await rig.RecropAsync(marker, RightHalf);
        Assert.Contains("_crop_", marker.FilePath);
        rig.MapDb.Verify(d => d.UpdateMapLayerAsync(It.IsAny<IMapLayerModel>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal("잘랐습니다 — 되돌리려면 Ctrl+Z 또는 우클릭 「자르기 되돌리기」", rig.Vm.AimStatusMessage);
        rig.Done();
    });
}
