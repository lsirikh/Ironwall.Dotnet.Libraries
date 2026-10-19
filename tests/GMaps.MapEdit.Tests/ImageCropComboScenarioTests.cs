using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;
using K = System.Windows.Input.Key;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 오버레이 이미지 자르기 × 다른 편집 기능 복합 시나리오 — 자르기 판이 열린 채 묶음 회전 · 레이어 선택 · 편집 모드 끄기,
/// 다시 자르기 뒤 Undo/Redo 반복, 묶음 회전 뒤 되돌리기, Ctrl+I(편집 모드만) · 레이어 재구성 뒤 선택. 실제 MapViewModel 경로 · 창 없음.
/// </summary>
public class ImageCropComboScenarioTests
{
    private sealed class TempImageFiles : IImageFileService
    {
        public string Base { get; } = Path.Combine(Path.GetTempPath(), $"imgcrop-combo-{Guid.NewGuid():N}");
        public int Copies { get; private set; }
        public void EnsureImageDirectory() => Directory.CreateDirectory(Path.Combine(Base, "Images/Overlays"));
        public string CopyImageToLocal(string sourcePath)
        {
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
        public List<string> LayerPaths { get; } = new();
        public List<int> Saves { get; } = new();
        public Dictionary<int, IEditableMarker> M { get; } = new();

        public Rig()
        {
            Scenario.SetField(Vm, "_imageFileService", Files);
            Scenario.SetField(Vm, "_gMapDbService", MapDb.Object);
            MapDb.Setup(d => d.FetchMapLayersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Layers);
            MapDb.Setup(d => d.UpdateMapLayerAsync(It.IsAny<IMapLayerModel>(), It.IsAny<CancellationToken>()))
                 .Callback<IMapLayerModel, CancellationToken>((l, _) => LayerPaths.Add(l.FilePath ?? ""))
                 .ReturnsAsync(true);
            W.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
                .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => Saves.Add(m.Id)).ReturnsAsync((IGeometricSymbolModel?)null);
            W.Db.Setup(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()))
                .Callback<IImageModel, CancellationToken>((m, _) => Saves.Add(m.Id)).ReturnsAsync((IImageModel?)null);
        }

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

        /// <summary>지도 가운데 오른쪽에 200×100 그림을 놓는다(화면 400×200).</summary>
        public GMapImageMarker PlaceImage(double bearing = 0)
        {
            var relative = Files.CopyImageToLocal(UserPng(200, 100));
            var c = W.Map.FromLatLngToLocal(W.Map.Position);
            var tl = W.Map.FromLocalToLatLng((int)c.X + 50, (int)c.Y - 100);
            var br = W.Map.FromLocalToLatLng((int)c.X + 450, (int)c.Y + 100);
            var marker = new GMapImageMarker(Mock.Of<ILogService>(), new ImageModel
            {
                Id = 77, Title = "발전소", FilePath = relative, Rotation = bearing, Width = 400, Height = 200, Opacity = 1, Visibility = true,
                Top = tl.Lat, Left = tl.Lng, Right = br.Lng, Bottom = br.Lat,
            }) { IsLayerEnabled = true };
            W.Map.Markers.Add(marker);
            Layers.Add(new MapLayerModel { Id = 5, Name = "발전소", LayerType = "OverlayImage", Category = "OverlayImage", FilePath = relative, IsVisible = true, Opacity = 1 });
            return marker;
        }

        public IEditableMarker AddSymbol(int id, int dx, int dy = 0)
        {
            var c = W.Map.FromLatLngToLocal(W.Map.Position);
            var geo = W.Map.FromLocalToLatLng((int)c.X + dx, (int)c.Y + dy);
            var model = new GeometricSymbolModel { Id = id, Title = $"도형-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, ShowShape = true, Visible = true };
            var m = new MarkerFactory(Mock.Of<ILogService>(), W.Devices).CreateMarker(model);
            W.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)m);
            Scenario.GetField<SymbolProvider>(Vm, "_symbolProvider").Add(model);
            M[id] = m;
            return m;
        }

        public void ShowLayers()
        {
            Scenario.Layout(W.Deco, 1200, 800);
            Scenario.SetField(Vm, "_layerTreeNodes", LayerTreeBuilder.Build(Layers, Scenario.GetField<SymbolProvider>(Vm, "_symbolProvider")));
            Vm.ShowLayerPanel();
            LayerSelection.ExpandToShow(LayerTreeBuilder.Flatten(Roots).Where(n => n.Symbol != null || n.Model?.LayerType == "OverlayImage").ToList());
        }

        public ObservableCollection<LayerTreeNode> Roots => Scenario.GetField<ObservableCollection<LayerTreeNode>>(Vm, "_layerTreeNodes");
        public LayerTreeNode Leaf(int id) => LayerTreeBuilder.Flatten(Roots).Single(n => n.Symbol?.Id == id);
        public LayerTreeNode ImageLeaf => LayerTreeBuilder.Flatten(Roots).Single(n => n.Model?.LayerType == "OverlayImage");
        public LayerPanelControl Panel => Vm.LayerPanel!;
        public GroupSelectionService Group => Scenario.GetField<GroupSelectionService>(Vm, "_groupSelection");

        public System.Windows.Input.KeyEventArgs Key(K key)
        {
            var e = EditRig.Key(key);
            Scenario.Call(Vm, "OnMapPreviewKeyDownForGroup", W.Map, e);
            return e;
        }

        public ImageCropViewModel WaitPanel()
        {
            for (int i = 0; i < 50 && Vm.ImageCrop == null; i++) Scenario.Pump();
            Assert.True(Vm.IsImageCropPanelOpen);
            return Vm.ImageCrop!;
        }

        public static void Draw(ImageCropViewModel crop, params Point[] points)
        {
            crop.Fit(800, 600);
            foreach (var p in points) { var s = crop.ImageToScreen(p); crop.PointerDown(s); crop.PointerUp(s); }
            Assert.True(crop.Close());
        }

        public async Task RecropAsync(GMapImageMarker marker, params Point[] polygon)
        {
            var task = Vm.RecropImageAsync(marker);
            Draw(WaitPanel(), polygon);
            Vm.ImageCrop!.ApplyCommand.Execute(null);
            await task;
        }

        public Task Undo() { Vm.UndoCommand!.Execute(null); return Task.Delay(80); }
        public Task Redo() { Vm.RedoCommand!.Execute(null); return Task.Delay(80); }

        public void Done()
        {
            Assert.False(Vm.IsImageCropPanelOpen);
            // 3D 상태는 이 장치에 없다 — 편집 모드 전환이 부르는 3D 게이트의 격리 로그("[Map3D] … (격리)")는 시험 범위 밖
            var errors = W.Errors.Where(e => !(e.StartsWith("[Map3D]") && e.Contains("(격리)"))).ToList();
            Assert.True(errors.Count == 0, string.Join(" | ", errors));
            try { Directory.Delete(Files.Base, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static readonly Point[] RightHalf = { new(100, 0), new(200, 0), new(200, 100), new(100, 100) };
    private static readonly Point[] LeftHalf = { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };

    private static void SameBounds(RectLatLng e, RectLatLng a, double tolerance = 1e-10)
    {
        Assert.Equal(e.Lat, a.Lat, tolerance);
        Assert.Equal(e.Lng, a.Lng, tolerance);
        Assert.Equal(e.WidthLng, a.WidthLng, tolerance);
        Assert.Equal(e.HeightLat, a.HeightLat, tolerance);
    }

    // ───────────── X-01 자르기 판 열린 채 묶음 회전(그림 포함) → 적용 → Undo 2회 ─────────────

    [Fact]
    [Trait("Scenario", "X-01")]
    public void should_crop_with_rotated_bearing_and_undo_crop_then_rotation_when_group_is_rotated_while_recrop_panel_is_open() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var img = rig.PlaceImage();
        var s = rig.AddSymbol(301, -150);
        Scenario.Layout(rig.W.Deco, 1200, 800);
        var original = img.FilePath;
        var task = rig.Vm.RecropImageAsync(img);
        var crop = rig.WaitPanel();

        rig.Group.SetSelection(new IEditableMarker[] { img, s });   // 판은 그대로 두고 지도에서 묶음 회전
        for (int i = 0; i < 30; i++) rig.Key(K.OemCloseBrackets);
        rig.Key(K.Enter);
        await Task.Delay(80);
        Assert.Equal(30, img.Bearing, 6);
        Assert.True(rig.Vm.IsImageCropPanelOpen);                    // 판은 남는다
        var rotatedBounds = img.ImageBounds;

        Rig.Draw(crop, RightHalf);
        crop.ApplyCommand.Execute(null);
        await task;
        Assert.Contains("_crop_", img.FilePath);
        Assert.Equal(30, img.Bearing, 6);                             // 자르기는 회전을 바꾸지 않는다
        SameBounds(ImageCropMath.CropBounds(rotatedBounds, 30, 200, 100, new Int32Rect(100, 0, 100, 100)), img.ImageBounds);
        Assert.Equal("다각형으로 자르기", rig.W.Undo.NextUndoDescription);

        await rig.Undo();                                             // 1 = 자르기
        Assert.Equal(original, img.FilePath);
        SameBounds(rotatedBounds, img.ImageBounds);
        Assert.Equal(30, img.Bearing, 6);
        await rig.Undo();                                             // 2 = 회전
        Assert.Equal(0, img.Bearing, 6);
        Assert.Equal(0, s.Bearing, 9);
        rig.Done();
    });

    // ───────────── X-02 자르기 판 열린 채 레이어 패널로 다른 행 · 묶음 고르기 ─────────────

    [Fact]
    [Trait("Scenario", "X-02")]
    public void should_keep_recrop_panel_and_still_apply_when_layer_rows_are_picked_while_it_is_open() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var img = rig.PlaceImage();
        rig.AddSymbol(311, -150);
        rig.AddSymbol(312, -60);
        rig.ShowLayers();
        var task = rig.Vm.RecropImageAsync(img);
        var crop = rig.WaitPanel();

        rig.Panel.SelectNode(rig.Leaf(311), ModifierKeys.None);
        rig.Panel.SelectNode(rig.Leaf(312), ModifierKeys.Control);
        rig.Panel.SelectNode(rig.ImageLeaf, ModifierKeys.Control);
        await Task.Delay(30);
        Assert.Equal(3, rig.Group.Selection.Count);
        Assert.Same(crop, rig.Vm.ImageCrop);                          // 판은 그대로

        Rig.Draw(crop, LeftHalf);
        crop.ApplyCommand.Execute(null);
        await task;
        Assert.Contains("_crop_", img.FilePath);
        Assert.Equal(3, rig.Group.Selection.Count);                  // 묶음 선택도 그대로
        Assert.Contains(img, rig.Group.Selection);
        rig.Done();
    });

    // ───────────── X-03 자르기 판 + 회전 미리보기가 둘 다 열린 채 편집 모드 끄기 ─────────────

    [Fact]
    [Trait("Scenario", "X-03")]
    public void should_confirm_rotation_and_cancel_crop_without_image_change_when_edit_mode_turns_off_with_both_open() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var img = rig.PlaceImage();
        var a = rig.AddSymbol(321, -150);
        var b = rig.AddSymbol(322, -60);
        Scenario.Layout(rig.W.Deco, 1200, 800);
        var original = img.FilePath;
        var task = rig.Vm.RecropImageAsync(img);
        var crop = rig.WaitPanel();
        Rig.Draw(crop, RightHalf);                                    // 그려 두고 적용 전
        rig.Group.SetSelection(new[] { a, b });
        for (int i = 0; i < 7; i++) rig.Key(K.OemCloseBrackets);

        rig.Vm.IsEditModeEnabled = false;
        await Task.WhenAny(task, Task.Delay(2000));
        Assert.True(task.IsCompleted, "편집 모드를 껐는데 자르기 판이 남아 있다");
        await Task.Delay(60);
        Assert.False(rig.Group.IsRotating);
        Assert.Equal(7, a.Bearing, 9);                                // 회전은 확정 · 저장
        Assert.Contains(321, rig.Saves);
        Assert.Equal(original, img.FilePath);                         // 자르기는 취소
        Assert.DoesNotContain(77, rig.Saves);
        Assert.Equal("그룹 회전 2개", rig.W.Undo.NextUndoDescription);
        rig.Done();
    });

    // ───────────── X-04 다시 자르기 → Undo/Redo 3회 반복 ─────────────

    [Fact]
    [Trait("Scenario", "X-04")]
    public void should_toggle_same_file_bounds_and_layer_path_each_time_when_recrop_is_undone_and_redone_three_times() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var img = rig.PlaceImage(bearing: 20);
        Scenario.Layout(rig.W.Deco, 1200, 800);
        var original = img.FilePath!;
        var before = img.ImageBounds;
        await rig.RecropAsync(img, RightHalf);
        var cropped = img.FilePath!;
        var after = img.ImageBounds;
        Assert.NotEqual(original, cropped);

        for (int round = 0; round < 3; round++)
        {
            await rig.Undo();
            Assert.Equal(original, img.FilePath);
            SameBounds(before, img.ImageBounds);
            Assert.Equal(400, img.Width, 6);
            Assert.Equal(original, rig.Layers[0].FilePath);           // 레이어 경로도 함께
            await rig.Redo();
            Assert.Equal(cropped, img.FilePath);
            SameBounds(after, img.ImageBounds);
            Assert.Equal(200, img.Width, 6);
            Assert.Equal(cropped, rig.Layers[0].FilePath);
        }
        Assert.Equal(20, img.Bearing, 6);
        Assert.Equal(1 + 3 * 2, rig.LayerPaths.Count);                // 자르기 1 + Undo/Redo 6번 = 레이어 저장 7번
        Assert.False(rig.W.Undo.CanRedo);
        Assert.Single(Directory.GetFiles(Path.Combine(rig.Files.Base, "Images", "Overlays"), "*_crop_*.png"));   // 반복해도 자른 파일은 하나
        rig.Done();
    });

    // ───────────── X-05 자르기 → Undo → 새로 자르기(Redo 무효) → 되돌리기 → Undo ─────────────

    [Fact]
    [Trait("Scenario", "X-05")]
    public void should_drop_old_redo_and_revert_new_crop_then_undo_back_when_image_is_recropped_after_undo() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var img = rig.PlaceImage();
        Scenario.Layout(rig.W.Deco, 1200, 800);
        var original = img.FilePath!;
        await rig.RecropAsync(img, RightHalf);
        await rig.Undo();
        Assert.True(rig.W.Undo.CanRedo);

        await rig.RecropAsync(img, LeftHalf);                        // 새 자르기 = 옛 Redo 무효
        Assert.False(rig.W.Undo.CanRedo);
        var second = img.FilePath!;
        var secondBounds = img.ImageBounds;
        Assert.True(rig.Vm.IsCroppedImage(img));

        await rig.Vm.UncropImageAsync(img);                           // 우클릭 「자르기 되돌리기」
        Assert.Equal(original, img.FilePath);
        Assert.Equal("자르기 되돌리기", rig.W.Undo.NextUndoDescription);
        await rig.Undo();
        Assert.Equal(second, img.FilePath);
        SameBounds(secondBounds, img.ImageBounds);
        await rig.Undo();
        Assert.Equal(original, img.FilePath);
        Assert.False(rig.W.Undo.CanUndo);
        rig.Done();
    });

    // ───────────── X-06 자르기 → 묶음 회전 → 되돌리기 → Undo 3회 ─────────────

    [Fact]
    [Trait("Scenario", "X-06")]
    public void should_revert_around_remaining_part_with_group_rotated_bearing_and_undo_three_steps_in_order() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var img = rig.PlaceImage();
        var s = rig.AddSymbol(331, -150);
        Scenario.Layout(rig.W.Deco, 1200, 800);
        var original = img.FilePath!;
        await rig.RecropAsync(img, RightHalf);
        var cropped = img.FilePath!;

        rig.Group.SetSelection(new IEditableMarker[] { img, s });
        rig.Vm.StartGroupRotationCommand.Execute(null);
        rig.Vm.GroupRotationAngleText = "40";
        rig.Vm.ConfirmGroupRotationCommand.Execute(null);
        await Task.Delay(80);
        Assert.Equal(40, img.Bearing, 6);
        var croppedRotated = img.ImageBounds;

        await rig.Vm.UncropImageAsync(img);
        Assert.Equal(original, img.FilePath);
        Assert.Equal(40, img.Bearing, 6);                             // 회전 유지
        var box = new Int32Rect(100, 0, 100, 100);
        SameBounds(croppedRotated, ImageCropMath.CropBounds(img.ImageBounds, 40, 200, 100, box), 1e-9);   // 남은 부분 제자리

        await rig.Undo();                                             // 되돌리기
        Assert.Equal(cropped, img.FilePath);
        SameBounds(croppedRotated, img.ImageBounds);
        await rig.Undo();                                             // 회전
        Assert.Equal(0, img.Bearing, 6);
        await rig.Undo();                                             // 자르기
        Assert.Equal(original, img.FilePath);
        Assert.False(rig.W.Undo.CanUndo);
        rig.Done();
    });

    // ───────────── X-07 Ctrl+I = 편집 모드에서만 · 판 열린 채면 막힘 ─────────────

    [Fact]
    [Trait("Scenario", "X-07")]
    public void should_tell_edit_mode_only_then_close_panel_first_when_image_load_is_pressed_off_edit_and_with_recrop_open() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var img = rig.PlaceImage();
        Scenario.Layout(rig.W.Deco, 1200, 800);
        Scenario.Call(rig.Vm, "InitializeFileCommands");                // 도구줄 「이미지 불러오기」 · Ctrl+I 가 묶인 명령(생성자 없이 만든 VM)
        rig.Vm.IsEditModeEnabled = false;
        Assert.True(rig.Vm.LoadImageOverlayCommand!.CanExecute(null));   // 단추 · Ctrl+I 는 켜져 있고 누르면 안내
        rig.Vm.LoadImageOverlayCommand.Execute(null);
        await Task.Delay(30);
        Assert.Equal("편집 모드에서 쓸 수 있습니다", rig.Vm.AimStatusMessage);
        Assert.False(rig.Vm.IsImageCropPanelOpen);

        rig.Vm.IsEditModeEnabled = true;
        var task = rig.Vm.RecropImageAsync(img);
        var crop = rig.WaitPanel();
        rig.Vm.LoadImageOverlayCommand.Execute(null);                 // 판이 떠 있는데 Ctrl+I
        await Task.Delay(30);
        Assert.Equal("자르기 판을 먼저 닫으세요", rig.Vm.AimStatusMessage);
        Assert.Same(crop, rig.Vm.ImageCrop);

        rig.Vm.IsEditModeEnabled = false;                             // 편집 모드 끄기 = 판 취소, 그림 그대로
        await task;
        Assert.Equal(1, rig.Files.Copies);
        Assert.False(rig.W.Undo.CanUndo);
        rig.Done();
    });

    // ───────────── X-08 레이어로 고른 그림 + 심볼 → 다시 자르기 → 트리 재구성 뒤 선택 유지 ─────────────

    [Fact]
    [Trait("Scenario", "X-08")]
    public void should_keep_image_row_and_group_selection_after_layer_tree_reload_when_layer_picked_image_is_recropped() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        var img = rig.PlaceImage();
        rig.AddSymbol(341, -150);
        rig.ShowLayers();
        rig.Panel.SelectNode(rig.Leaf(341), ModifierKeys.None);
        rig.Panel.SelectNode(rig.ImageLeaf, ModifierKeys.Control);
        await Task.Delay(30);
        Assert.Equal(2, rig.Group.Selection.Count);
        var treeBefore = rig.Roots;

        await rig.RecropAsync(img, RightHalf);
        for (int i = 0; i < 50 && ReferenceEquals(treeBefore, rig.Roots); i++) await Task.Delay(10);
        Assert.NotSame(treeBefore, rig.Roots);                        // 경로가 바뀌어 트리를 다시 만들었다
        Assert.Equal(img.FilePath, rig.ImageLeaf.Model!.FilePath);
        Assert.True(rig.ImageLeaf.IsSelected);                        // 같은 레이어(Id) 행이 여전히 선택
        Assert.True(rig.Leaf(341).IsSelected);
        Assert.Equal(2, rig.Group.Selection.Count);
        Assert.Contains(img, rig.Group.Selection);
        rig.Done();
    });
}
