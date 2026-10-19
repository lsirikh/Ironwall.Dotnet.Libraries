using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 오버레이 이미지 다각형 자르기(2026-10-19) — 창 없이 순수 함수(ImageCropMath)와 판 VM(ImageCropViewModel):
/// 다각형 → 알파 마스크(오목 · 경계 픽셀 · 1px) · 상자 · 원본 알파 교집합 · 검사(3점 미만 · 자기 교차) · 경계 위경도 조정(회전 0/30/90°) ·
/// 꼭짓점 추가 · 닫기 · 끌기(데드존 8 DIU) · 선분 가운데 추가 · 지우기 · Undo/Redo · Esc 두 단계 · 확대 상태 클릭 좌표.
/// </summary>
public class ImageCropMathTests
{
    // ─────────────── 장치 ───────────────

    private static Point P(double x, double y) => new(x, y);

    /// <summary>w×h BGRA 픽셀(모두 불투명 빨강, 알파 지정 가능).</summary>
    private static byte[] Solid(int w, int h, byte alpha = 255)
    {
        var px = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++) { px[i * 4 + 2] = 255; px[i * 4 + 3] = alpha; }
        return px;
    }

    private static byte Alpha(byte[] px, int w, int x, int y) => px[(y * w + x) * 4 + 3];

    /// <summary>
    /// 그림 정규 좌표(u,v: 0~1, 회전 전)의 위경도 — 시험용 독립 구현. 지도 화면처럼 경계 귀퉁이를 메르카토르 화면으로 옮겨 그 사이에 늘이고
    /// 중심 기준 시계 방향으로 돌린 자리(GMapMarkerImageControl 과 같은 모양).
    /// </summary>
    private static PointLatLng Geo(RectLatLng b, double bearing, double u, double v)
    {
        static double Y(double lat) => Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360)) * 180 / Math.PI;
        static double Lat(double y) => (2 * Math.Atan(Math.Exp(y * Math.PI / 180)) - Math.PI / 2) * 180 / Math.PI;
        double yT = Y(b.Lat), yB = Y(b.Lat - b.HeightLat);
        double cx = b.Lng + b.WidthLng / 2, cy = (yT + yB) / 2;
        double ex = (u - 0.5) * b.WidthLng, sy = (v - 0.5) * (yT - yB);
        double t = bearing * Math.PI / 180;
        double rx = ex * Math.Cos(t) - sy * Math.Sin(t), ry = ex * Math.Sin(t) + sy * Math.Cos(t);
        return new PointLatLng(Lat(cy - ry), cx + rx);
    }

    private static void Near(PointLatLng expected, PointLatLng actual, string what)
    {
        Assert.True(Math.Abs(expected.Lat - actual.Lat) < 1e-9 && Math.Abs(expected.Lng - actual.Lng) < 1e-9,
            $"{what}: 기대 ({expected.Lat:F9},{expected.Lng:F9}) 실제 ({actual.Lat:F9},{actual.Lng:F9})");
    }

    // ─────────────── 1. 마스크 · 상자 ───────────────

    [Fact]
    public void should_make_notch_transparent_when_polygon_is_concave()
    {
        // ㄴ 자(오목) — 오른쪽 아래 5×5 가 빠진다
        var poly = new[] { P(0, 0), P(10, 0), P(10, 5), P(5, 5), P(5, 10), P(0, 10) };
        var box = ImageCropMath.BoundingBox(poly, 10, 10);
        Assert.Equal(new Int32Rect(0, 0, 10, 10), box);
        var px = ImageCropMath.MaskAndCrop(Solid(10, 10), 10, 10, poly, box);
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
                Assert.Equal(x >= 5 && y >= 5 ? 0 : 255, Alpha(px, 10, x, y));
        // 바깥 픽셀은 색도 0(투명 검정) — 3D 가 알파만 보더라도 남은 색이 새지 않게
        Assert.Equal(0, px[(7 * 10 + 7) * 4 + 2]);
    }

    [Fact]
    public void should_decide_each_boundary_pixel_by_its_center_when_edge_is_diagonal()
    {
        // 직각삼각형 (0,0)-(8,0)-(0,8): 픽셀 (x,y) 중심 (x+.5, y+.5) 이 안 ⇔ x+y+1 < 8 ⇔ x+y ≤ 6 — 1px 단위로 정확히
        var poly = new[] { P(0, 0), P(8, 0), P(0, 8) };
        var box = ImageCropMath.BoundingBox(poly, 8, 8);
        var px = ImageCropMath.MaskAndCrop(Solid(8, 8), 8, 8, poly, box);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                Assert.True((x + y <= 6) == (Alpha(px, 8, x, y) == 255), $"({x},{y})");
    }

    [Fact]
    public void should_crop_to_bounding_box_with_one_pixel_accuracy_when_polygon_has_fractional_edges()
    {
        // 사각형 x 2.4~6.6, y 3~8 → 상자 = floor/ceil = (2,3) 5×5. 중심 2.5 · 6.5 는 안, 열 전부 남는다
        var poly = new[] { P(2.4, 3), P(6.6, 3), P(6.6, 8), P(2.4, 8) };
        var box = ImageCropMath.BoundingBox(poly, 20, 20);
        Assert.Equal(new Int32Rect(2, 3, 5, 5), box);
        var px = ImageCropMath.MaskAndCrop(Solid(20, 20), 20, 20, poly, box);
        Assert.All(Enumerable.Range(0, 25), i => Assert.Equal(255, px[i * 4 + 3]));

        // 6.4 로 줄이면 중심 6.5 가 바깥 → 오른쪽 열만 투명(상자는 ceil 6.4 = 7 그대로)
        var narrower = new[] { P(2.4, 3), P(6.4, 3), P(6.4, 8), P(2.4, 8) };
        var px2 = ImageCropMath.MaskAndCrop(Solid(20, 20), 20, 20, narrower, ImageCropMath.BoundingBox(narrower, 20, 20));
        for (int y = 0; y < 5; y++) { Assert.Equal(255, Alpha(px2, 5, 3, y)); Assert.Equal(0, Alpha(px2, 5, 4, y)); }
    }

    [Fact]
    public void should_clamp_box_to_image_when_points_lie_on_the_image_border()
    {
        var poly = new[] { P(0, 0), P(30, 0), P(30, 20), P(0, 20) };
        Assert.Equal(new Int32Rect(0, 0, 30, 20), ImageCropMath.BoundingBox(poly, 30, 20));
        var px = ImageCropMath.MaskAndCrop(Solid(30, 20), 30, 20, poly, new Int32Rect(0, 0, 30, 20));
        Assert.Equal(255, Alpha(px, 30, 29, 19));   // 오른쪽 아래 끝 픽셀도 남는다
    }

    [Fact]
    public void should_intersect_with_source_alpha_when_original_png_is_already_transparent()
    {
        var src = Solid(4, 1);
        src[0 * 4 + 3] = 0;     // (0,0) 원래 투명
        src[1 * 4 + 3] = 128;   // (1,0) 반투명
        var poly = new[] { P(0, 0), P(3, 0), P(3, 1), P(0, 1) };   // x 0~3 → (3,0) 은 바깥
        var px = ImageCropMath.MaskAndCrop(src, 4, 1, poly, new Int32Rect(0, 0, 4, 1));
        Assert.Equal(new byte[] { 0, 128, 255, 0 }, new[] { Alpha(px, 4, 0, 0), Alpha(px, 4, 1, 0), Alpha(px, 4, 2, 0), Alpha(px, 4, 3, 0) });
    }

    [Fact]
    public void should_keep_full_resolution_and_bgra_png_when_cropping_a_bitmap() => Scenario.Sta(() =>
    {
        var bmp = BitmapSource.Create(400, 300, 72, 72, PixelFormats.Bgra32, null, Solid(400, 300), 400 * 4);
        var (image, box) = ImageCropMath.Crop(bmp, new[] { P(100, 50), P(350, 60), P(300, 280) });
        Assert.Equal(new Int32Rect(100, 50, 250, 230), box);
        Assert.Equal((250, 230), (image.PixelWidth, image.PixelHeight));   // 줄이지 않는다
        var decoded = BitmapDecoder.Create(new MemoryStream(ImageCropMath.EncodePng(image)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal((250, 230), (decoded.PixelWidth, decoded.PixelHeight));
        Assert.Equal(PixelFormats.Bgra32, decoded.Format);
    });

    // ─────────────── 2. 다각형 검사 ───────────────

    [Fact]
    public void should_block_apply_with_reason_when_polygon_has_fewer_than_three_points_or_crosses_itself()
    {
        Assert.Contains("3개", ImageCropMath.Validate(new[] { P(0, 0), P(5, 5) }));
        Assert.Contains("겹칩", ImageCropMath.Validate(new[] { P(0, 0), P(10, 10), P(10, 0), P(0, 10) }));   // 나비 모양
        Assert.Contains("겹칩", ImageCropMath.Validate(new[] { P(0, 0), P(10, 0), P(10, 10), P(5, 0), P(0, 10) }));   // 꼭짓점이 다른 변에 닿음
        Assert.Contains("넓이", ImageCropMath.Validate(new[] { P(0, 0), P(5, 5), P(10, 10) }));   // 한 줄
        Assert.Null(ImageCropMath.Validate(new[] { P(0, 0), P(10, 0), P(10, 5), P(5, 5), P(5, 10), P(0, 10) }));   // 오목은 된다
    }

    // ─────────────── 3. 지도 경계 조정 ───────────────

    private static readonly RectLatLng Bounds = new(37.5010, 127.0000, 0.0040, 0.0020);   // 위 37.5010 · 왼쪽 127.0000 · 폭 0.004° · 높이 0.002°

    [Theory]
    [InlineData(0d)]
    [InlineData(30d)]
    [InlineData(90d)]
    public void should_keep_remaining_part_in_place_when_placed_image_is_cropped(double bearing)
    {
        const int w = 400, h = 200;
        var box = new Int32Rect(220, 30, 120, 90);
        var cropped = ImageCropMath.CropBounds(Bounds, bearing, w, h, box);
        // 자른 그림의 네 귀퉁이 · 가운데 = 원본에서 같은 픽셀의 위경도
        foreach (var (cu, cv) in new[] { (0d, 0d), (1d, 0d), (0d, 1d), (1d, 1d), (0.5, 0.5) })
        {
            var expected = Geo(Bounds, bearing, (box.X + cu * box.Width) / w, (box.Y + cv * box.Height) / h);
            Near(expected, Geo(cropped, bearing, cu, cv), $"회전 {bearing}° ({cu},{cv})");
        }
        Assert.Equal(Bounds.WidthLng * 120 / 400, cropped.WidthLng, 12);
        Assert.Equal((ImageCropMath.MercatorY(Bounds.Lat) - ImageCropMath.MercatorY(Bounds.Lat - Bounds.HeightLat)) * 90 / 200,
                     ImageCropMath.MercatorY(cropped.Lat) - ImageCropMath.MercatorY(cropped.Lat - cropped.HeightLat), 12);
    }

    [Fact]
    public void should_move_center_south_when_right_half_is_kept_at_90_degrees()
    {
        // 시계 방향 90° — 그림 오른쪽이 남쪽을 본다 → 오른쪽 절반의 중심은 원래 중심보다 남쪽(경도는 그대로)
        var c = ImageCropMath.CropBounds(Bounds, 90, 400, 200, new Int32Rect(200, 0, 200, 200));
        double oldLat = Bounds.Lat - Bounds.HeightLat / 2, newLat = c.Lat - c.HeightLat / 2;
        Assert.True(newLat < oldLat);
        Assert.Equal(Bounds.Lng + Bounds.WidthLng / 2, c.Lng + c.WidthLng / 2, 12);
        // 0° 면 동쪽으로 폭의 1/4
        var c0 = ImageCropMath.CropBounds(Bounds, 0, 400, 200, new Int32Rect(200, 0, 200, 200));
        Assert.Equal(Bounds.Lng + Bounds.WidthLng * 0.75, c0.Lng + c0.WidthLng / 2, 12);
        Assert.Equal(oldLat, c0.Lat - c0.HeightLat / 2, 10);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(30d)]
    [InlineData(90d)]
    public void should_restore_original_bounds_when_crop_is_reverted(double bearing)
    {
        var box = new Int32Rect(13, 41, 177, 99);
        var back = ImageCropMath.UncropBounds(ImageCropMath.CropBounds(Bounds, bearing, 400, 200, box), bearing, 400, 200, box);
        Assert.Equal(Bounds.Lat, back.Lat, 10);
        Assert.Equal(Bounds.Lng, back.Lng, 10);
        Assert.Equal(Bounds.WidthLng, back.WidthLng, 12);
        Assert.Equal(Bounds.HeightLat, back.HeightLat, 12);
    }

    // ─────────────── 4. 판 VM ───────────────

    /// <summary>400×200 그림, 판 400×200(Fit → 배율 0.96 · 여백 (8,4)).</summary>
    private static ImageCropViewModel NewVm(bool allowSkip = true)
    {
        var bmp = BitmapSource.Create(400, 200, 96, 96, PixelFormats.Bgra32, null, Solid(400, 200), 400 * 4);
        bmp.Freeze();
        var vm = new ImageCropViewModel(bmp, "이미지 자르기", allowSkip);
        vm.Fit(400, 200);
        return vm;
    }

    private static void Same(Point e, Point a) { Assert.Equal(e.X, a.X, 9); Assert.Equal(e.Y, a.Y, 9); }
    private static bool Has(IEnumerable<Point> pts, Point p) => pts.Any(q => (q - p).Length < 1e-9);

    private static void Click(ImageCropViewModel vm, Point screen) { vm.PointerDown(screen); vm.PointerUp(screen); }

    private static void Drag(ImageCropViewModel vm, Point from, Point to)
    {
        vm.PointerDown(from);
        vm.PointerMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        vm.PointerMove(to);
        vm.PointerUp(to);
    }

    /// <summary>그림 좌표 사각형(50,50)-(150,50)-(150,150)-(50,150)을 클릭으로 찍고 첫 점을 눌러 닫는다.</summary>
    private static void DrawSquare(ImageCropViewModel vm)
    {
        foreach (var p in new[] { P(50, 50), P(150, 50), P(150, 150), P(50, 150) }) Click(vm, vm.ImageToScreen(p));
        Click(vm, vm.ImageToScreen(P(50, 50)));
    }

    [Fact]
    public void should_add_points_and_close_when_first_point_is_clicked() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        DrawSquare(vm);
        Assert.True(vm.IsClosed);
        Assert.Equal(4, vm.PointCount);
        Assert.Equal("꼭짓점 4개 · 닫힘", vm.PointCountText);
        for (int i = 0; i < 4; i++) Same(new[] { P(50, 50), P(150, 50), P(150, 150), P(50, 150) }[i], vm.Points[i]);
        // 닫힌 뒤 빈 곳 클릭은 점을 더하지 않는다
        Click(vm, vm.ImageToScreen(P(300, 100)));
        Assert.Equal(4, vm.PointCount);
    });

    [Fact]
    public void should_close_by_double_click_or_enter_only_when_three_points_exist() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        Click(vm, vm.ImageToScreen(P(10, 10)));
        Click(vm, vm.ImageToScreen(P(100, 10)));
        Assert.True(vm.HandleKey(Key.Enter, ModifierKeys.None));
        Assert.False(vm.IsClosed);                        // 2점 — 닫지 않는다
        Click(vm, vm.ImageToScreen(P(100, 100)));
        vm.DoubleClick();
        Assert.True(vm.IsClosed);

        var vm2 = NewVm();
        foreach (var p in new[] { P(10, 10), P(100, 10), P(100, 100) }) Click(vm2, vm2.ImageToScreen(p));
        vm2.HandleKey(Key.Enter, ModifierKeys.None);
        Assert.True(vm2.IsClosed);
    });

    [Fact]
    public void should_treat_small_move_as_click_and_larger_move_as_vertex_drag_when_pressing_a_vertex() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        DrawSquare(vm);
        var v1 = vm.ImageToScreen(P(150, 50));
        // 데드존 이내(8 DIU) — 꼭짓점이 움직이지 않고 Undo 칸도 늘지 않는다
        vm.PointerDown(v1);
        vm.PointerMove(new Point(v1.X + 8, v1.Y));
        Assert.False(vm.IsDragging);
        vm.PointerUp(new Point(v1.X + 8, v1.Y));
        Same(P(150, 50), vm.Points[1]);
        // 넘으면 끌기 — 놓은 자리(그림 좌표)로
        var target = new Point(v1.X + 48, v1.Y + 24);
        Drag(vm, v1, target);
        Same(vm.ScreenToImage(target), vm.Points[1]);
        Assert.Equal(4, vm.PointCount);
        // 끌기 한 번 = Undo 한 칸
        vm.Undo();
        Same(P(150, 50), vm.Points[1]);
        vm.Redo();
        Same(vm.ScreenToImage(target), vm.Points[1]);
    });

    [Fact]
    public void should_insert_vertex_when_edge_midpoint_handle_is_dragged() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        DrawSquare(vm);
        var mid = vm.ImageToScreen(P(100, 50));             // 선분 0(위쪽) 가운데
        var to = vm.ImageToScreen(P(100, 20));
        Drag(vm, mid, to);
        Assert.Equal(5, vm.PointCount);
        Assert.Equal(P(100, 20).X, vm.Points[1].X, 6);
        Assert.Equal(P(100, 20).Y, vm.Points[1].Y, 6);
        Assert.True(vm.IsClosed);
        // 닫힌 다각형은 마지막 → 첫 선분에도 가운데 손잡이가 있다
        Assert.Contains(vm.Midpoints(), m => m.EdgeIndex == 4 && (m.Point - P(50, 100)).Length < 1e-9);
        vm.Undo();
        Assert.Equal(4, vm.PointCount);
    });

    [Fact]
    public void should_remove_vertex_by_right_click_delete_and_backspace() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        foreach (var p in new[] { P(10, 10), P(100, 10), P(100, 100), P(10, 100), P(5, 50) }) Click(vm, vm.ImageToScreen(p));
        vm.RightClick(vm.ImageToScreen(P(100, 10)));
        Assert.Equal(4, vm.PointCount);
        Assert.False(Has(vm.Points, P(100, 10)));
        vm.HandleKey(Key.Back, ModifierKeys.None);       // 마지막 점
        Assert.Equal(3, vm.PointCount);
        Assert.False(Has(vm.Points, P(5, 50)));
        // 누른 꼭짓점(고른 점) → Delete
        var target = vm.ImageToScreen(P(100, 100));
        vm.PointerDown(target);
        vm.PointerUp(target);
        Assert.Equal(1, vm.SelectedVertex);
        vm.HandleKey(Key.Delete, ModifierKeys.None);
        Assert.Equal(2, vm.PointCount); Same(P(10, 10), vm.Points[0]); Same(P(10, 100), vm.Points[1]);
        // 우클릭이 빈 곳이면 아무것도
        vm.RightClick(new Point(390, 190));
        Assert.Equal(2, vm.PointCount);
    });

    [Fact]
    public void should_select_without_adding_point_when_existing_vertex_is_clicked_while_drawing() => Scenario.Sta(() =>
    {
        // 결함(2026-10-19 개발 중 발견): 그리는 중 첫 점이 아닌 꼭짓점을 누르면 같은 자리에 점이 하나 더 찍혔다 → 고르기만
        var vm = NewVm();
        foreach (var p in new[] { P(10, 10), P(100, 10), P(100, 100) }) Click(vm, vm.ImageToScreen(p));
        Click(vm, vm.ImageToScreen(P(100, 10)));
        Assert.Equal(3, vm.PointCount);
        Assert.Equal(1, vm.SelectedVertex);
        Assert.False(vm.IsClosed);
    });

    [Fact]
    public void should_reopen_polygon_when_vertex_removal_leaves_fewer_than_three_points() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        foreach (var p in new[] { P(10, 10), P(100, 10), P(100, 100) }) Click(vm, vm.ImageToScreen(p));
        vm.Close();
        vm.RemoveVertex(0);
        Assert.False(vm.IsClosed);
    });

    [Fact]
    public void should_undo_and_redo_with_ctrl_keys_inside_panel() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        Click(vm, vm.ImageToScreen(P(10, 10)));
        Click(vm, vm.ImageToScreen(P(20, 10)));
        Assert.True(vm.HandleKey(Key.Z, ModifierKeys.Control));
        Assert.Equal(1, vm.PointCount);
        Assert.True(vm.CanRedo);
        Assert.True(vm.HandleKey(Key.Y, ModifierKeys.Control));
        Assert.Equal(2, vm.PointCount);
        // 새 편집은 Redo 를 비운다
        vm.Undo();
        Click(vm, vm.ImageToScreen(P(30, 30)));
        Assert.False(vm.CanRedo);
        Same(P(30, 30), vm.Points[1]);
    });

    [Fact]
    public void should_cancel_drawing_then_ask_then_close_when_escape_is_pressed_repeatedly() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        Click(vm, vm.ImageToScreen(P(10, 10)));
        Click(vm, vm.ImageToScreen(P(100, 10)));
        vm.HandleKey(Key.Escape, ModifierKeys.None);      // 1단계 — 그리던 점 취소
        Assert.Equal(0, vm.PointCount);
        Assert.False(vm.IsCloseConfirmVisible);
        Assert.False(vm.IsCompleted);
        vm.Undo();                                        // 취소도 Undo 된다
        Assert.Equal(2, vm.PointCount);
        vm.Escape();
        vm.HandleKey(Key.Escape, ModifierKeys.None);      // 2단계 — 그릴 것이 없으면 닫기 확인
        Assert.True(vm.IsCloseConfirmVisible);
        Assert.False(vm.IsCompleted);
        vm.KeepEditingCommand.Execute(null);              // 「계속 자르기」
        Assert.False(vm.IsCloseConfirmVisible);
        vm.Escape();
        vm.Escape();                                      // 확인이 떠 있을 때 한 번 더 = 닫기(취소)
        Assert.True(vm.IsCompleted);
        Assert.Equal(ImageCropOutcome.Cancel, vm.Completion.Result);
    });

    [Fact]
    public void should_ask_before_closing_when_escape_is_pressed_on_a_closed_polygon() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        DrawSquare(vm);
        vm.Escape();
        Assert.Equal(4, vm.PointCount);                   // 닫힌 다각형은 지우지 않는다
        Assert.True(vm.IsCloseConfirmVisible);
        vm.ConfirmCloseCommand.Execute(null);
        Assert.Equal(ImageCropOutcome.Cancel, vm.Completion.Result);
    });

    [Fact]
    public void should_cancel_vertex_drag_when_pointer_is_cancelled() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        DrawSquare(vm);
        var v = vm.ImageToScreen(P(150, 150));
        vm.PointerDown(v);
        vm.PointerMove(new Point(v.X + 40, v.Y + 40));
        Assert.True(vm.IsDragging);
        Assert.True(vm.PointerCancel());
        Same(P(150, 150), vm.Points[2]);
        Assert.False(vm.CanRedo);
        Assert.False(vm.PointerCancel());                 // 끄는 중이 아니면 처리 안 함
    });

    [Fact]
    public void should_map_click_to_image_pixel_when_view_is_zoomed_and_panned() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        Assert.Equal(0.96, vm.Scale, 10);
        Assert.Equal((8d, 4d), (vm.OffsetX, vm.OffsetY));
        var anchor = new Point(56, 52);                   // 그림 (50, 50)
        vm.ZoomAt(anchor, 2);
        Assert.Equal(1.92, vm.Scale, 10);
        var under = vm.ScreenToImage(anchor);             // 커서 기준 — 커서 아래 그림 점이 그대로
        Assert.Equal(50, under.X, 9);
        Assert.Equal(50, under.Y, 9);
        vm.PanBy(-30, 10);
        // 화면 (56+1.92*10-30, 52+10) = 그림 (60, 50)
        Click(vm, new Point(56 + 19.2 - 30, 62));
        Assert.Equal(60, vm.Points[0].X, 9);
        Assert.Equal(50, vm.Points[0].Y, 9);
        // 그림 밖 클릭은 그림 가장자리로
        Click(vm, new Point(-500, -500));
        Assert.Equal(P(0, 0), vm.Points[1]);
        // 화면에 맞추기
        vm.ViewportWidth = 400; vm.ViewportHeight = 200;
        vm.FitCommand.Execute(null);
        Assert.Equal(0.96, vm.Scale, 10);
    });

    [Fact]
    public void should_block_apply_with_reason_and_complete_only_for_a_valid_closed_polygon() => Scenario.Sta(() =>
    {
        var vm = NewVm();
        vm.ApplyCommand.Execute(null);
        Assert.Contains("3개", vm.Error);
        Assert.True(vm.HasError);
        Assert.False(vm.IsCompleted);
        foreach (var p in new[] { P(10, 10), P(100, 100), P(100, 10), P(10, 100) }) Click(vm, vm.ImageToScreen(p));
        vm.ApplyCommand.Execute(null);
        Assert.Contains("닫아", vm.Error);                 // 열린 다각형
        vm.Close();
        Assert.False(vm.HasError);                        // 편집하면 이유 줄을 지운다
        vm.ApplyCommand.Execute(null);
        Assert.Contains("겹칩", vm.Error);                 // 나비 모양
        Assert.False(vm.IsCompleted);
        vm.MoveVertex(1, P(100, 10));
        vm.MoveVertex(2, P(100, 100));
        vm.ApplyCommand.Execute(null);
        Assert.Null(vm.Error);
        Assert.Equal(ImageCropOutcome.Apply, vm.Completion.Result);
    });

    [Fact]
    public void should_offer_skip_only_on_first_load() => Scenario.Sta(() =>
    {
        Assert.True(NewVm(allowSkip: true).SkipCommand.CanExecute(null));
        var recrop = NewVm(allowSkip: false);
        Assert.False(recrop.SkipCommand.CanExecute(null));
        var first = NewVm(allowSkip: true);
        first.SkipCommand.Execute(null);
        Assert.Equal(ImageCropOutcome.Skip, first.Completion.Result);
    });
}
