using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 오버레이 이미지 다각형 자르기(2026-10-19) — 창 없이 실제 MapViewModel 경로로:
/// 처음 불러올 때(CopyOverlayImageWithCropAsync — 파일 고르기 직후 판 → 적용 · 그대로 · 취소) · 저장 경로 · 원본 불변 · 기록(.crop.json) ·
/// 이미 놓인 그림 다시 자르기(RecropImageAsync — 경계 위경도 조정 · DB · MapLayers 경로 · Undo/Redo 1단위) · 자르기 되돌리기 ·
/// 3D 로 보낼 때 PNG(투명) 유지 · 판 템플릿(AutomationId · 토큰 · 판 안 키).
/// </summary>
public class ImageCropScenarioTests
{
    // ─────────────── 장치 ───────────────

    /// <summary>오버레이 저장 폴더를 임시 폴더로 — 제품 ImageFileService 와 같은 이름 · 상대 경로 규칙.</summary>
    private sealed class TempImageFiles : IImageFileService
    {
        public string Base { get; } = Path.Combine(Path.GetTempPath(), $"imgcrop-{Guid.NewGuid():N}");
        public int Copies { get; private set; }
        public void EnsureImageDirectory() => Directory.CreateDirectory(Path.Combine(Base, "Images/Overlays"));
        public string CopyImageToLocal(string sourcePath)
        {
            EnsureImageDirectory();
            var relative = $"Images/Overlays/{Path.GetFileNameWithoutExtension(sourcePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(sourcePath)}";
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

        /// <summary>사용자 폴더(저장 폴더 밖)에 w×h 불투명 PNG — 왼쪽 절반 빨강 · 오른쪽 절반 파랑.</summary>
        public string UserPng(int w, int h, bool transparentCorner = false)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    px[i + (x < w / 2 ? 2 : 0)] = 255;
                    px[i + 3] = (byte)(transparentCorner && x < 4 && y < 4 ? 0 : 255);
                }
            var dir = Path.Combine(Files.Base, "user");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "발전소.png");
            File.WriteAllBytes(path, ImageCropMath.EncodePng(BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4)));
            return path;
        }

        /// <summary>판이 뜰 때까지 디스패처를 돌린다.</summary>
        public ImageCropViewModel WaitPanel()
        {
            for (int i = 0; i < 50 && Vm.ImageCrop == null; i++) Scenario.Pump();
            Assert.True(Vm.IsImageCropPanelOpen);
            Assert.NotNull(Vm.ImageCropPanel);
            Assert.Same(Vm.ImageCrop, Vm.ImageCropPanel!.DataContext);
            return Vm.ImageCrop!;
        }

        /// <summary>그림 좌표 다각형을 찍고 닫는다(판 VM 의 실제 클릭 경로).</summary>
        public static void Draw(ImageCropViewModel crop, params Point[] points)
        {
            crop.Fit(800, 600);
            foreach (var p in points) { var s = crop.ImageToScreen(p); crop.PointerDown(s); crop.PointerUp(s); }
            Assert.True(crop.Close());
        }

        /// <summary>지도에 놓인 오버레이 이미지(저장 폴더의 파일, 회전 bearing°).</summary>
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

        public void Done()
        {
            Assert.False(Vm.IsImageCropPanelOpen);
            Assert.Null(Vm.ImageCropPanel);
            Assert.Empty(W.Errors);
            try { Directory.Delete(Files.Base, recursive: true); } catch (IOException) { }
        }
    }

    private static void RunAsync(Func<Rig, Task> body) => Scenario.StaAsync(async () => { var rig = new Rig(); await body(rig); rig.Done(); });

    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

    private static byte AlphaAt(BitmapSource image, int x, int y)
    {
        var px = new byte[4];
        new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0).CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return px[3];
    }

    // ─────────────── 1. 처음 불러올 때 ───────────────

    [Fact]
    public void should_save_cropped_png_next_to_untouched_copy_when_crop_is_applied_on_first_load() => RunAsync(async rig =>
    {
        var user = rig.UserPng(200, 100);
        var userHash = Hash(user);
        var task = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        var crop = rig.WaitPanel();
        Assert.True(crop.AllowSkip);
        Assert.Equal((200, 100), (crop.PixelWidth, crop.PixelHeight));
        Assert.Equal(0, rig.Files.Copies);                                     // 판이 떠 있는 동안은 아직 복사하지 않는다
        Rig.Draw(crop, new Point(40, 10), new Point(180, 20), new Point(120, 90));
        crop.ApplyCommand.Execute(null);
        var relative = await task;

        Assert.NotNull(relative);
        Assert.StartsWith("Images/Overlays/발전소_", relative);
        Assert.Contains("_crop_", relative);
        Assert.EndsWith(".png", relative);
        var absolute = rig.Files.GetAbsolutePath(relative!);
        var cropped = ImageCropStore.Load(absolute);
        Assert.Equal((140, 80), (cropped.PixelWidth, cropped.PixelHeight));    // 경계 상자 · 원본 해상도
        Assert.Equal(0, AlphaAt(cropped, 0, 79));                              // 다각형 바깥 = 투명
        Assert.Equal(255, AlphaAt(cropped, 80, 20));                           // 안 = 그대로

        // 원본(사용자 파일)은 그대로, 저장 폴더에 복사본이 남고 기록이 그 복사본 · 상자를 가리킨다
        Assert.Equal(userHash, Hash(user));
        Assert.Equal(1, rig.Files.Copies);
        var record = ImageCropStore.Read(rig.Files.GetAbsolutePath, relative);
        Assert.NotNull(record);
        Assert.Equal(userHash, Hash(rig.Files.GetAbsolutePath(record!.Source)));
        Assert.Equal((200, 100), (record.SourceWidth, record.SourceHeight));
        Assert.Equal(new Int32Rect(40, 10, 140, 80), record.Box);
    });

    [Fact]
    public void should_copy_unchanged_when_skip_is_chosen_and_add_nothing_when_cancelled() => RunAsync(async rig =>
    {
        var user = rig.UserPng(60, 40);
        var task = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        rig.WaitPanel().SkipCommand.Execute(null);
        var relative = await task;
        Assert.DoesNotContain("_crop_", relative);
        Assert.Equal(Hash(user), Hash(rig.Files.GetAbsolutePath(relative!)));
        Assert.Null(ImageCropStore.Read(rig.Files.GetAbsolutePath, relative));

        var cancelled = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        rig.WaitPanel().CancelCommand.Execute(null);
        Assert.Null(await cancelled);
        Assert.Equal(1, rig.Files.Copies);                                     // 취소는 복사도 하지 않는다

        // 머리 띠 ✕ = 취소
        var closed = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        rig.WaitPanel();
        rig.Vm.ImageCropPanel!.CloseCommand!.Execute(null);
        Assert.Null(await closed);
    });

    [Fact]
    public void should_keep_original_transparency_and_polygon_together_when_source_png_has_alpha() => RunAsync(async rig =>
    {
        var user = rig.UserPng(40, 40, transparentCorner: true);              // 왼쪽 위 4×4 원래 투명
        var task = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        // 왼쪽이 (20,20) 까지 파인 오목 오각형 — 상자는 그림 전체(40×40)라 좌표가 그대로다
        Rig.Draw(rig.WaitPanel(), new Point(0, 0), new Point(40, 0), new Point(40, 40), new Point(0, 40), new Point(20, 20));
        var crop = rig.Vm.ImageCrop!;
        crop.ApplyCommand.Execute(null);
        Assert.Null(crop.Error);
        var cropped = ImageCropStore.Load(rig.Files.GetAbsolutePath((await task)!));
        Assert.Equal((40, 40), (cropped.PixelWidth, cropped.PixelHeight));
        Assert.Equal(0, AlphaAt(cropped, 2, 1));                               // 원래 투명 + 다각형 안 = 투명(교집합)
        Assert.Equal(255, AlphaAt(cropped, 6, 1));                             // 원래 불투명 + 다각형 안
        Assert.Equal(255, AlphaAt(cropped, 30, 30));                           // 다각형 안 · 불투명
        Assert.Equal(0, AlphaAt(cropped, 2, 20));                              // 오목 쐐기(다각형 바깥)
    });

    [Fact]
    public void should_send_cropped_image_to_3d_as_png_when_baking() => RunAsync(async rig =>
    {
        var user = rig.UserPng(120, 80);
        // 자르기 전(불투명) 그림은 3D 로 JPEG — 자른 그림은 투명 픽셀이 생겨 PNG 로 간다
        Assert.Equal("image/jpeg", Map3DImageCache.Encode(ImageCropStore.Load(user)).Mime);
        var task = rig.Vm.CopyOverlayImageWithCropAsync(user, "발전소");
        Rig.Draw(rig.WaitPanel(), new Point(10, 10), new Point(110, 10), new Point(60, 70));
        rig.Vm.ImageCrop!.ApplyCommand.Execute(null);
        var absolute = rig.Files.GetAbsolutePath((await task)!);
        var (bytes, mime) = Map3DImageCache.Encode(ImageCropStore.Load(absolute));
        Assert.Equal("image/png", mime);
        var sent = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal(0, AlphaAt(sent, 0, sent.PixelHeight - 1));               // 투명 그대로
        // 캐시 경로(GetOrEncodeFile — 굽기 요청 파일)도 PNG
        var cacheDir = Path.Combine(rig.Files.Base, "map3d-cache");
        var file = Map3DImageCache.GetOrEncodeFile(ImageCropStore.Load(absolute), cacheDir);
        Assert.EndsWith(".png", file);
    });

    // ─────────────── 2. 이미 놓인 그림 다시 자르기 · 되돌리기 ───────────────

    private static readonly RectLatLng Placed = new(37.5010, 127.0000, 0.0040, 0.0020);

    /// <summary>경계 비교 — 기본 1e-10°, 옮긴 뒤 비교처럼 위도 · 메르카토르 차이가 섞이면 tolerance(°)로(1e-7° ≈ 1 cm).</summary>
    private static void SameBounds(RectLatLng e, RectLatLng a, double tolerance = 1e-10)
    {
        Assert.Equal(e.Lat, a.Lat, tolerance);
        Assert.Equal(e.Lng, a.Lng, tolerance);
        Assert.Equal(e.WidthLng, a.WidthLng, tolerance);
        Assert.Equal(e.HeightLat, a.HeightLat, tolerance);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(30d)]
    [InlineData(90d)]
    public void should_crop_placed_image_in_place_save_and_undo_as_one_step(double bearing) => RunAsync(async rig =>
    {
        var user = rig.UserPng(200, 100);
        var relative = rig.Files.CopyImageToLocal(user);
        var marker = rig.PlaceImage(relative, Placed, bearing, 400, 200);
        rig.W.Undo.Clear();

        var task = rig.Vm.RecropImageAsync(marker);
        var crop = rig.WaitPanel();
        Assert.False(crop.AllowSkip);                                          // 다시 자르기에는 「그대로」 없음
        Rig.Draw(crop, new Point(100, 0), new Point(200, 0), new Point(200, 100), new Point(100, 100));   // 오른쪽 절반
        crop.ApplyCommand.Execute(null);
        await task;

        var box = new Int32Rect(100, 0, 100, 100);
        Assert.Contains("_crop_", marker.FilePath);
        SameBounds(ImageCropMath.CropBounds(Placed, bearing, 200, 100, box), marker.ImageBounds);
        Assert.Equal((200d, 200d), (marker.Width, marker.Height));            // 표시 크기도 상자 비율만큼
        Assert.Equal(bearing, marker.Bearing, 9);                              // 회전은 그대로
        Assert.Equal(marker.FilePath, marker.ImageModel.FilePath);             // 모델(DB 저장 값)도
        rig.W.Db.Verify(d => d.UpdateImageAsync(It.Is<IImageModel>(m => m.FilePath == marker.FilePath), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        Assert.Equal(marker.FilePath, rig.Updated.Last());                     // MapLayers 경로도 새 그림으로
        Assert.Equal(relative, ImageCropStore.Read(rig.Files.GetAbsolutePath, marker.FilePath)!.Source);
        Assert.True(File.Exists(rig.Files.GetAbsolutePath(relative)));         // 자르기 전 그림은 지우지 않는다

        // Undo 1단위 — 그림 · 경계 · 크기가 함께 돌아온다, Redo 로 다시
        var croppedPath = marker.FilePath;
        Assert.Equal("다각형으로 자르기", rig.W.Undo.NextUndoDescription);
        Assert.True(await rig.W.Undo.UndoAsync());
        Assert.False(rig.W.Undo.CanUndo);
        Assert.Equal(relative, marker.FilePath);
        SameBounds(Placed, marker.ImageBounds);
        Assert.Equal((400d, 200d), (marker.Width, marker.Height));
        Assert.Equal(relative, rig.Updated.Last());
        Assert.True(await rig.W.Undo.RedoAsync());
        Assert.Equal(croppedPath, marker.FilePath);
        SameBounds(ImageCropMath.CropBounds(Placed, bearing, 200, 100, box), marker.ImageBounds);
    });

    [Fact]
    public void should_restore_pre_crop_image_in_place_when_crop_is_reverted() => RunAsync(async rig =>
    {
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(200, 100));
        var marker = rig.PlaceImage(relative, Placed, 30, 400, 200);
        Assert.False(rig.Vm.IsCroppedImage(marker));
        await rig.Vm.UncropImageAsync(marker);                                  // 자른 그림이 아니면 바꾸지 않는다
        Assert.Equal(relative, marker.FilePath);

        var task = rig.Vm.RecropImageAsync(marker);
        Rig.Draw(rig.WaitPanel(), new Point(20, 10), new Point(150, 30), new Point(90, 95));
        rig.Vm.ImageCrop!.ApplyCommand.Execute(null);
        await task;
        Assert.True(rig.Vm.IsCroppedImage(marker));
        // 자른 뒤 사용자가 옮겼다 — 되돌리기는 지금 자리 기준(옮긴 만큼 원본도 옮겨져 있다)
        var moved = marker.ImageBounds;
        marker.ImageBounds = new RectLatLng(moved.Lat + 0.001, moved.Lng + 0.002, moved.WidthLng, moved.HeightLat);

        rig.W.Undo.Clear();
        await rig.Vm.UncropImageAsync(marker);
        Assert.Equal(relative, marker.FilePath);
        // 위도로 옮겼으므로(화면은 메르카토르) 1 cm 이내로 같은 자리
        SameBounds(new RectLatLng(Placed.Lat + 0.001, Placed.Lng + 0.002, Placed.WidthLng, Placed.HeightLat), marker.ImageBounds, 1e-7);
        Assert.Equal(400, marker.Width, 9);
        Assert.Equal(200, marker.Height, 9);
        Assert.Equal("자르기 되돌리기", rig.W.Undo.NextUndoDescription);
        Assert.True(await rig.W.Undo.UndoAsync());
        Assert.Contains("_crop_", marker.FilePath);
    });

    [Fact]
    public void should_leave_image_unchanged_when_recrop_is_cancelled_or_permission_is_missing() => RunAsync(async rig =>
    {
        var relative = rig.Files.CopyImageToLocal(rig.UserPng(50, 50));
        var marker = rig.PlaceImage(relative, Placed, 0, 100, 100);
        rig.W.Undo.Clear();
        var task = rig.Vm.RecropImageAsync(marker);
        var crop = rig.WaitPanel();
        crop.Escape();
        crop.Escape();                                                         // 그릴 것 없음 → 확인 → 닫기
        await task;
        Assert.Equal(relative, marker.FilePath);
        SameBounds(Placed, marker.ImageBounds);
        Assert.False(rig.W.Undo.CanUndo);

        rig.W.Permission.Setup(p => p.CanEdit("map")).Returns(false);
        await rig.Vm.RecropImageAsync(marker);
        Assert.False(rig.Vm.IsImageCropPanelOpen);                             // 권한 없으면 판을 열지 않는다
    });

    // ─────────────── 3. 판 템플릿 ───────────────

    [Fact]
    public void should_expose_automation_ids_tokens_and_keys_when_panel_template_is_applied() => Scenario.Sta(() =>
    {
        var platform = Caliburn.Micro.PlatformProvider.Current;
        Caliburn.Micro.PlatformProvider.Current = new Caliburn.Micro.XamlPlatformProvider();
        try { PanelTemplateScenario(); }
        finally { Caliburn.Micro.PlatformProvider.Current = platform; }
    });

    private static void PanelTemplateScenario()
    {
        var bmp = BitmapSource.Create(300, 200, 96, 96, PixelFormats.Bgra32, null, new byte[300 * 200 * 4], 300 * 4);
        var vm = new ImageCropViewModel(bmp, "이미지 자르기", allowSkip: true);
        var panel = new ImageCropPanelControl { DataContext = vm, CloseCommand = vm.CancelCommand };
        Assert.Equal("Map.ImageCrop.Panel", AutomationProperties.GetAutomationId(panel));
        Assert.NotNull(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(panel));
        var dict = PropertyStyleLoader.Load("ImageCropPanelStyle.xaml");
        panel.Resources.MergedDictionaries.Add(dict);
        panel.Style = (Style)dict[typeof(ImageCropPanelControl)];
        Scenario.Layout(panel, 760, 600);

        var ids = Descendants(panel).Select(AutomationProperties.GetAutomationId).Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
        foreach (var id in new[] { "Map.ImageCrop.Canvas", "Map.ImageCrop.Apply", "Map.ImageCrop.Skip", "Map.ImageCrop.Cancel", "Map.ImageCrop.Fit",
                     "Map.ImageCrop.Undo", "Map.ImageCrop.Redo", "Map.ImageCrop.PointCount", "Map.ImageCrop.Error", "Map.ImageCrop.Close",
                     "Help.Map.ImageCrop.Panel" })
            Assert.Contains(id, ids);
        Assert.DoesNotContain(Descendants(panel).OfType<FrameworkElement>(), e => e.Name.Length > 0 && e.Name.StartsWith("ImageCrop"));

        // 캔버스가 판 크기를 받아 그림을 맞춘다 · 꼭짓점 수가 보인다
        var canvas = Descendants(panel).OfType<ImageCropCanvas>().Single();
        Assert.True(canvas.ActualWidth > 0);
        Assert.Equal(canvas.ActualWidth, vm.ViewportWidth);
        Assert.Equal(Math.Min(vm.ViewportWidth / 300, vm.ViewportHeight / 200) * 0.96, vm.Scale, 9);   // 처음 열면 화면에 맞춘다
        foreach (var p in new[] { new Point(10, 10), new Point(200, 20), new Point(100, 150) }) { var s = vm.ImageToScreen(p); vm.PointerDown(s); vm.PointerUp(s); }
        Scenario.Layout(panel, 760, 600);
        Assert.Equal("꼭짓점 3개", ById<TextBlock>(panel, "Map.ImageCrop.PointCount").Text);

        // 판 안 키 — Enter = 닫기, Ctrl+Z = 판 안 되돌리기, Esc 두 번 = 닫기 확인 → 닫기
        var source = new HeadlessKeySource(panel);
        void Key_(Key key) => canvas.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Key_(Key.Enter);
        Assert.True(vm.IsClosed);
        vm.ApplyCommand.Execute(null);                                        // 유효 — 끝난다(아래 Esc 확인용으로 새 VM)
        Assert.True(vm.IsCompleted);

        // 이유 줄 — 막히면 보인다
        var vm2 = new ImageCropViewModel(bmp, "이미지 자르기", allowSkip: false);
        panel.DataContext = vm2;
        vm2.ApplyCommand.Execute(null);
        Scenario.Layout(panel, 760, 600);
        var error = ById<TextBlock>(panel, "Map.ImageCrop.Error");
        Assert.Equal(Visibility.Visible, error.Visibility);
        Assert.Contains("3개", error.Text);
        Assert.Equal(Visibility.Collapsed, ById<Button>(panel, "Map.ImageCrop.Skip").Visibility);
        Key_(Key.Escape);
        Assert.True(vm2.IsCloseConfirmVisible);
        Key_(Key.Escape);
        Assert.Equal(ImageCropOutcome.Cancel, vm2.Completion.Result);

        // 머리 띠 색 = 토큰(PrimaryBrush) — 라이트 → 다크로 바꾸면 따라 바뀐다
        var header = Descendants(panel).OfType<Border>().First(bd => bd.Cursor == Cursors.SizeAll);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#0C6B89"), ((SolidColorBrush)header.Background).Color);
        var light = dict.MergedDictionaries.First(d => d.Source?.OriginalString.Contains("Tokens.Light") == true);
        dict.MergedDictionaries[dict.MergedDictionaries.IndexOf(light)] = new ResourceDictionary
        { Source = new Uri("pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml") };
        Scenario.Layout(panel, 760, 600);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#22B8D9"), ((SolidColorBrush)header.Background).Color);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    private static T ById<T>(DependencyObject root, string id) where T : DependencyObject
        => Descendants(root).OfType<T>().First(e => AutomationProperties.GetAutomationId(e) == id);

    private sealed class HeadlessKeySource : PresentationSource
    {
        private Visual? _root;
        public HeadlessKeySource(Visual root) { RootVisual = root; }
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
