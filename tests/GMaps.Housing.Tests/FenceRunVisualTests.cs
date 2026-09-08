using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>PRD FR-05/06/07/08 — FenceRunVisual 버킷·예산·펄스, 그룹 3D 컨트롤 프레임 계산·템플릿 파트, PIDS DoorState DP.</summary>
public class FenceRunVisualTests
{
    private static FenceLayoutResult Line(double lengthPx, double spacingPx, EnumFenceMode mode)
        => FenceLayout.Compute(new[] { new FencePoint(0, 0), new FencePoint(lengthPx, 0) }, spacingPx, mode, false);

    [Fact]
    public void should_build_fence_run_under_triangle_budget() => HousingTests.Sta(() =>
    {
        // 1 km / 3 m = 333 구간을 실제 px 프레임(mpp 0.2 → 5000 px, 간격 15 px)에서 — 예산 20k 삼각형(FR-05, R-01). 길이는 MetersPerPixel 로 m 환산(C17)
        const double mpp = 0.2;
        var view = new FenceRunVisual { HeightPx = 12, Mode = EnumFenceMode.Posts, MetersPerPixel = mpp, Layout = Line(1000 / mpp, 3 / mpp, EnumFenceMode.Posts) };
        Assert.Equal(333, view.Layout!.Panels.Count);
        Assert.True(view.TriangleCount > 0);
        Assert.True(view.TriangleCount <= FenceDefaults.TriangleBudgetPerKm, $"triangles={view.TriangleCount}");
        Assert.False(view.IsBudgetDegraded);
        Assert.Equal(0, view.PartCounts.Nodes + view.PartCounts.Active);   // Posts 모드는 노드 없음
    });

    [Fact]
    public void should_degrade_rails_when_budget_exceeded() => HousingTests.Sta(() =>
    {
        // 100m 에 0.1m 간격 = 1000 구간 → 예산 초과 → 레일 생략, 패널·기둥은 유지 (MetersPerPixel 기본 1 = m 프레임)
        var view = new FenceRunVisual { HeightPx = 24, Layout = Line(100, .1, EnumFenceMode.Posts) };
        Assert.True(view.IsBudgetDegraded);
        Assert.True(view.TriangleCount > 0);
    });

    [Theory]
    [InlineData(1.0, 0.2, true)]    // 1 km / 1.0 m(슬라이더 최소) @ mpp 0.2: 1000 구간·38k > 20k → 레일 생략(R-01)
    [InlineData(1.5, 0.2, true)]    // 667 구간·25k > 20k
    [InlineData(3.0, 0.2, false)]   // 기본 3.0 m: 333 구간·12.6k ≤ 20k — 두 도메인 모두 미초과
    [InlineData(1.0, 0.1, true)]    // 같은 1 km 를 z 한 단계 더 확대(10,000 px)해도 판정은 m 기준이라 불변
    public void should_judge_triangle_budget_in_meters_when_layout_is_px_frame(double spacingM, double mpp, bool expectedDegraded) => HousingTests.Sta(() =>
    {
        // C17: 레이아웃은 px 프레임이라 TotalLengthM 이 px — 종전엔 px 를 m 로 읽어 예산이 1/mpp 배 부풀어 Full3D 에서 절대 초과하지 않았다
        double lengthPx = 1000 / mpp, spacingPx = spacingM / mpp;
        var layout = Line(lengthPx, spacingPx, EnumFenceMode.Posts);
        Assert.Equal((int)Math.Round(1000 / spacingM), layout.Panels.Count);
        Assert.Equal(lengthPx, layout.TotalLengthM, 6);   // 입력 단위(px) 그대로

        var view = new FenceRunVisual { HeightPx = 12, Mode = EnumFenceMode.Posts, MetersPerPixel = mpp, Layout = layout };
        Assert.Equal(expectedDegraded, view.IsBudgetDegraded);
        Assert.Equal(expectedDegraded, FenceMath.ExceedsBudget(layout.Panels.Count, false, layout.TotalLengthM * mpp));
        if (expectedDegraded)
        {
            // 회귀 고정: px 를 m 로 넘기던 종전 판정은 같은 입력에서 미초과(사문) — 이 단언이 깨지면 단위 환산이 사라진 것
            Assert.False(FenceMath.ExceedsBudget(layout.Panels.Count, false, layout.TotalLengthM));
            var stale = new FenceRunVisual { HeightPx = 12, Mode = EnumFenceMode.Posts, Layout = layout };   // MetersPerPixel 기본 1
            Assert.False(stale.IsBudgetDegraded);
            Assert.True(view.TriangleCount < stale.TriangleCount, $"레일 생략이 삼각형을 줄여야 한다 ({view.TriangleCount} vs {stale.TriangleCount})");
        }
    });

    [Fact]
    public void should_repartition_status_bucket_without_rebuilding_normal_mesh() => HousingTests.Sta(() =>
    {
        var view = new FenceRunVisual { HeightPx = 24, Mode = EnumFenceMode.SensorMount, Layout = Line(90, 9, EnumFenceMode.SensorMount) };
        var before = view.StaticMeshes; int rebuilds = view.RebuildCount;
        Assert.True(view.PartCounts.Nodes > 0); Assert.Equal(0, view.PartCounts.Active);

        view.ActiveNodes = new HashSet<int> { 1, 3 };

        Assert.True(view.PartCounts.Active > 0);
        Assert.Equal(before.Count, view.StaticMeshes.Count);
        for (int i = 0; i < before.Count; i++) Assert.Same(before[i], view.StaticMeshes[i]);   // 정적 버킷 메시 참조 불변
        Assert.Equal(rebuilds + 1, view.RebuildCount);
    });

    [Fact]
    public void should_change_only_active_brush_opacity_when_pulse_animates() => HousingTests.Sta(() =>
    {
        var view = new FenceRunVisual { HeightPx = 24, Mode = EnumFenceMode.SensorMount, Layout = Line(90, 9, EnumFenceMode.SensorMount), ActiveNodes = new HashSet<int> { 0 }, StatusBrush = Brushes.Red };
        int rebuilds = view.RebuildCount;
        var material = view.Children.OfType<Viewport3D>().Single().Children.OfType<ModelVisual3D>()
            .Select(v => v.Content).OfType<Model3DGroup>().SelectMany(Flatten).OfType<GeometryModel3D>()
            .Select(m => m.Material).OfType<MaterialGroup>().SelectMany(g => g.Children).OfType<DiffuseMaterial>()
            .First(d => d.Brush is SolidColorBrush s && s.Color == Colors.Red);
        double o1 = material.Brush.Opacity;
        view.StatusPulse = 0;
        double o0 = material.Brush.Opacity;
        Assert.True(o1 > o0, $"펄스 1→0 이면 불투명도가 줄어야 한다 ({o1} → {o0})");
        Assert.Equal(rebuilds, view.RebuildCount);   // 펄스는 재생성을 일으키지 않는다
    });

    private static IEnumerable<Model3D> Flatten(Model3D m) => m is Model3DGroup g ? g.Children.SelectMany(Flatten) : new[] { m };

    [Fact]
    public void should_project_ground_identically_and_lift_height_by_cos_pitch()
    {
        // FenceRunVisual 카메라 계약 = FenceMath.Project (테스트된 순수 함수). 컨트롤 로컬 (X, y_px) ↔ 모델 (X−W/2, y, −y_px+H/2)
        double w = 200, h = 100; var p = new Point(30, 70);
        var g = FenceMath.Project(p.X - w / 2, 0, -p.Y + h / 2, w / 2, h / 2);
        Assert.Equal(p.X, g.X, 9); Assert.Equal(p.Y, g.Y, 9);
        var top = FenceMath.Project(p.X - w / 2, 10, -p.Y + h / 2, w / 2, h / 2);
        Assert.Equal(p.Y - 10 * FenceMath.CosPitch, top.Y, 9);
    }

    [Theory]
    [InlineData(3.0, 0.2, 1.0, FenceLodLevel.Full3D)]           // 15 px/간격
    [InlineData(3.0, 0.6, 1.0, FenceLodLevel.PostsAndThickLine)] // 5 px
    [InlineData(3.0, 2.0, 1.0, FenceLodLevel.Line2D)]           // 1.5 px
    [InlineData(3.0, 2.0, 8.0, FenceLodLevel.Full3D)]           // 디지털 줌 ×8 → 12 px
    public void should_compute_frame_with_lod_and_px_layout(double spacingM, double mpp, double digital, FenceLodLevel expected)
    {
        var pts = new[] { new Point(10, 10), new Point(70, 10), new Point(70, 40) };
        var (layout, lod, heightPx, hash) = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, spacingM, 2.4, EnumFenceMode.Posts, false, mpp, digital);
        Assert.Equal(expected, lod);
        Assert.Equal(Math.Max(2.4 / mpp, FenceDefaults.MinVisualHeightPx), heightPx, 9);   // 가독 하한 12 px
        Assert.Equal(2, layout.EdgeCount);
        Assert.True(layout.Posts.Count >= 3);
        Assert.NotEqual(0, hash);
        var same = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, spacingM, 2.4, EnumFenceMode.Posts, false, mpp, digital);
        Assert.Equal(hash, same.Hash);   // 동일 입력 = 동일 해시(재생성 스킵 근거)
        var moved = GMapMarkerPidsGroup3DControl.ComputeFrame(new[] { new Point(10, 10), new Point(71, 10), new Point(70, 40) }, spacingM, 2.4, EnumFenceMode.Posts, false, mpp, digital);
        Assert.NotEqual(hash, moved.Hash);
    }

    [Fact]
    public void should_return_empty_frame_when_fewer_than_two_points()
    {
        var (layout, _, heightPx, hash) = GMapMarkerPidsGroup3DControl.ComputeFrame(new[] { new Point(1, 1) }, 3, 2.4, EnumFenceMode.Posts, false, .2, 1);
        Assert.Empty(layout.Panels); Assert.Equal(0, heightPx); Assert.Equal(0, hash);
    }

    // ---- C2/C4: 재생성 해시는 평행이동에 불변, ±1 px 위상 흔들림은 메시 재사용 ----

    private static FenceFrame Frame(IReadOnlyList<Point> pts, double spacingM = 3.0, double mpp = 0.2, EnumFenceMode mode = EnumFenceMode.Posts, bool closed = false)
        => GMapMarkerPidsGroup3DControl.ComputeFrame(pts, spacingM, 2.4, mode, closed, mpp, 1.0);

    // 해시 불변식 고정 테스트(결함 재현 아님): 실제 훅 입력은 bbox 원점 기준 정수 GPoint 라 분수 px 평행이동은 운영에서 발생하지 않는다(리뷰어 정정).
    // 실기전(정수 투영 위상 ±1 px 흔들림) 재현은 should_reuse_mesh_when_relative_points_jitter_within_one_pixel 이 담당한다.
    [Theory]
    [InlineData(0.4, 0.6)]      // 분수 px 평행이동(후보 fixSketch 케이스)
    [InlineData(37, -12)]       // 정수 px 평행이동(팬·Move 정상 케이스)
    [InlineData(-1000, 2500)]   // 원점이 크게 달라져도 상대 기하는 같다
    public void should_keep_frame_hash_when_points_are_translated(double dx, double dy)
    {
        var pts = new[] { new Point(10, 10), new Point(70, 10), new Point(70, 40), new Point(130, 55) };
        var moved = pts.Select(p => new Point(p.X + dx, p.Y + dy)).ToArray();

        var a = Frame(pts); var b = Frame(moved);

        Assert.Equal(a.Hash, b.Hash);
        Assert.Equal(a.SettingsHash, b.SettingsHash);
        Assert.Equal(a.RelativePoints, b.RelativePoints);
        Assert.Equal(new Point(0, 0), a.RelativePoints[0]);   // 첫 정점 기준
        Assert.Equal(a.Layout.Panels.Count, b.Layout.Panels.Count);
        Assert.Equal(FenceFrameAction.Skip, GMapMarkerPidsGroup3DControl.DecideFrame(b, true, false, a, true, false));
    }

    [Fact]
    public void should_change_frame_hash_when_settings_change_but_not_points()
    {
        var pts = new[] { new Point(10, 10), new Point(70, 10), new Point(70, 40) };
        var a = Frame(pts);
        Assert.NotEqual(a.Hash, Frame(pts, spacingM: 4.0).Hash);
        Assert.NotEqual(a.Hash, Frame(pts, mode: EnumFenceMode.SensorMount).Hash);
        Assert.NotEqual(a.Hash, Frame(pts, closed: true).Hash);
        Assert.NotEqual(a.Hash, GMapMarkerPidsGroup3DControl.ComputeFrame(pts, 3.0, 4.0, EnumFenceMode.Posts, false, 0.1, 1.0).Hash);   // 높이 px 변경(4.0 m @ 0.1 → 40 px)
        Assert.NotEqual(a.SettingsHash, Frame(pts, spacingM: 4.0).SettingsHash);
    }

    [Fact]
    public void should_reuse_mesh_when_relative_points_jitter_within_one_pixel()
    {
        // 리뷰 실측 기전: 정수 GPoint 투영 + DB 8dp 잔차 부호 혼재 + 디지털 줌 2.0× 0.5 px 스텝 → 정점 일부만 반올림 방향이 갈려 상대좌표가 정확히 ±1 px 흔들린다.
        var pts = new[] { new Point(5, 5), new Point(265, 82), new Point(5, 82), new Point(265, 82), new Point(265, 5) };
        var jitter = new[] { new Point(5, 5), new Point(265, 83), new Point(5, 83), new Point(265, 83), new Point(265, 4) };   // Height +1(bbox 흔들림)
        var built = Frame(pts); var next = Frame(jitter);

        Assert.NotEqual(built.Hash, next.Hash);   // 기하는 실제로 1 px 달라졌다 — 1 px 반올림 해시로는 못 막는다(리뷰어 정정)
        Assert.True(GMapMarkerPidsGroup3DControl.IsJitterEquivalent(built.RelativePoints, next.RelativePoints));
        Assert.Equal(FenceFrameAction.Reuse, GMapMarkerPidsGroup3DControl.DecideFrame(next, true, false, built, true, false));
        // idle 정확 재계산(exact) 에서는 재사용하지 않고 다시 만든다 — 드래그 종료 후 2D 선과 정확히 재정합
        Assert.Equal(FenceFrameAction.Rebuild, GMapMarkerPidsGroup3DControl.DecideFrame(next, true, false, built, true, false, exact: true));
        // 다음 프레임이 원래 위상으로 돌아오면(해시 동일) 스킵
        Assert.Equal(FenceFrameAction.Skip, GMapMarkerPidsGroup3DControl.DecideFrame(Frame(pts), true, false, built, true, false));
    }

    [Fact]
    public void should_rebuild_when_geometry_moves_beyond_jitter_or_topology_changes()
    {
        var pts = new[] { new Point(5, 5), new Point(265, 82), new Point(5, 82) };
        var built = Frame(pts);
        var twoPx = Frame(new[] { new Point(5, 5), new Point(265, 84), new Point(5, 82) });
        var extra = Frame(new[] { new Point(5, 5), new Point(265, 82), new Point(5, 82), new Point(5, 120) });
        var oneMeter = Frame(new[] { new Point(5, 5), new Point(265, 83), new Point(5, 82) }, spacingM: 4.0);   // 1 px 지터 + 간격 변경 → 설정이 달라 재사용 불가

        Assert.False(GMapMarkerPidsGroup3DControl.IsJitterEquivalent(built.RelativePoints, twoPx.RelativePoints));
        Assert.Equal(FenceFrameAction.Rebuild, GMapMarkerPidsGroup3DControl.DecideFrame(twoPx, true, false, built, true, false));
        Assert.Equal(FenceFrameAction.Rebuild, GMapMarkerPidsGroup3DControl.DecideFrame(extra, true, false, built, true, false));
        Assert.Equal(FenceFrameAction.Rebuild, GMapMarkerPidsGroup3DControl.DecideFrame(oneMeter, true, false, built, true, false));
        Assert.Equal(FenceFrameAction.Rebuild, GMapMarkerPidsGroup3DControl.DecideFrame(built, true, false, null, false, false));   // 첫 프레임
    }

    // ---- C16: 디지털 줌으로 표시 단계(LOD)만 바뀌면 해시가 같아도 Layout/캔버스를 다시 채운다 ----

    [Theory]
    [InlineData(false, true, true, false)]    // Posts → Full3D: 이전 빌드는 Layout 을 세팅한 적이 없다
    [InlineData(true, false, false, true)]    // Full3D → Posts: 캔버스가 비어 있다
    [InlineData(true, false, true, true)]     // Full3D 상태에서 Posts 캔버스가 같이 켜지는 조합 변화
    public void should_rebuild_when_only_lod_stage_changes_at_same_geometry(bool lastShow3D, bool lastShowPosts, bool show3D, bool showPosts)
    {
        // 디지털 줌은 RenderTransform 뿐이라 로컬 px·mpp 가 불변 → ComputeFrame 해시 동일(digital 1.0 vs 2.0)
        var pts = new[] { new Point(10, 10), new Point(70, 10), new Point(70, 40) };
        var posts = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, 3.0, 2.4, EnumFenceMode.Posts, false, 0.4744, 1.0);
        var full = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, 3.0, 2.4, EnumFenceMode.Posts, false, 0.4744, 2.0);
        Assert.Equal(FenceLodLevel.PostsAndThickLine, posts.Lod);
        Assert.Equal(FenceLodLevel.Full3D, full.Lod);
        Assert.Equal(posts.Hash, full.Hash);   // 해시만으로는 전이를 못 본다(결함 원인)

        Assert.Equal(FenceFrameAction.Rebuild, GMapMarkerPidsGroup3DControl.DecideFrame(full, show3D, showPosts, posts, lastShow3D, lastShowPosts));
        Assert.Equal(FenceFrameAction.Skip, GMapMarkerPidsGroup3DControl.DecideFrame(full, show3D, showPosts, posts, show3D, showPosts));   // 같은 단계면 스킵
    }

    [Fact]
    public void should_assign_layout_to_fence_when_lod_flips_to_full3d_by_digital_zoom() => HousingTests.Sta(() =>
    {
        // 실제 배선 검증(리뷰 지적: 훅 본문을 손으로 재현하면 Layout 대입 누락을 못 잡는다) — 템플릿만 적용한 컨트롤에서 ApplyFrame 을 직접 구동
        var (control, fence, canvas) = TemplatedControl();
        var pts = new[] { new Point(10, 10), new Point(70, 10), new Point(70, 40) };
        var posts = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, 3.0, 2.4, EnumFenceMode.Posts, false, 0.4744, 1.0);
        control.ApplyFrame(posts, show3D: false, showPosts: true, mpp: 0.4744);    // Posts 단계: 캔버스만
        Assert.Null(fence.Layout); Assert.True(canvas.Children.Count > 0, "Posts 단계에서 2D 기둥 점이 있어야 한다");

        var full = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, 3.0, 2.4, EnumFenceMode.Posts, false, 0.4744, 2.0);   // 디지털 줌 2× → Full3D, 기하 해시 동일
        Assert.Equal(posts.Hash, full.Hash);
        control.ApplyFrame(full, show3D: true, showPosts: false, mpp: 0.4744);
        Assert.Same(full.Layout, fence.Layout);
        Assert.True(fence.PartCounts.Static > 0, "Full3D 전이 후 정적 메시가 있어야 한다");
        Assert.Equal(2, control.FrameCounters.Rebuilds);

        control.ApplyFrame(posts, show3D: false, showPosts: true, mpp: 0.4744);   // Full3D → Posts 복귀: 캔버스를 다시 채운다
        Assert.True(canvas.Children.Count > 0, "Posts 복귀 후 캔버스가 비면 안 된다");
        Assert.Equal(3, control.FrameCounters.Rebuilds);
    });

    // ---- C2: 지터 재사용 뒤 정착은 디바운스 1회(ContextIdle 은 매 프레임 실행돼 스로틀이 아니었다 — 리뷰 지적) ----

    [Fact]
    public void should_settle_once_after_jitter_burst_when_move_drag_ends() => HousingTests.Sta(() =>
    {
        var (control, fence, _) = TemplatedControl();
        var a = new[] { new Point(5, 5), new Point(265, 82), new Point(5, 82), new Point(265, 82), new Point(265, 5) };
        var b = new[] { new Point(5, 5), new Point(265, 83), new Point(5, 83), new Point(265, 83), new Point(265, 4) };   // bbox Height ±1 위상 흔들림
        var fa = GMapMarkerPidsGroup3DControl.ComputeFrame(a, 20, 30, EnumFenceMode.Posts, false, 1.0, 1.0);
        var fb = GMapMarkerPidsGroup3DControl.ComputeFrame(b, 20, 30, EnumFenceMode.Posts, false, 1.0, 1.0);
        control.ApplyFrame(fa, true, false, 1.0);
        Assert.Equal(1, control.FrameCounters.Rebuilds); Assert.Same(fa.Layout, fence.Layout);

        for (int i = 0; i < 10; i++) control.ApplyFrame(i % 2 == 0 ? fb : fa, true, false, 1.0);   // 드래그 중 A↔B 교대(62회/초 실측 기전)
        Assert.Equal(1, control.FrameCounters.Rebuilds);                       // 버스트 동안 재생성 0
        Assert.True(control.IsSettlePending); Assert.Equal(0, control.SettleCounters.Fired);
        Assert.True(control.SettleCounters.Requests >= 5);

        Pump(FenceDefaults.SliderCommitDelayMs * 3);                           // 손을 뗀 뒤 디바운스 경과
        Assert.Equal(1, control.SettleCounters.Fired);                         // 정착 정확히 1회
        Assert.False(control.IsSettlePending);
    });

    private static (GMapMarkerPidsGroup3DControl control, FenceRunVisual fence, Canvas canvas) TemplatedControl()
    {
        var model = new PidsGroupSymbolModel { Latitude = 37.5, Longitude = 127.0, Render3D = true };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0), new(37.501, 127.001, 0) };
        var marker = new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
        var control = new GMapMarkerPidsGroup3DControl(marker);
        control.Resources.MergedDictionaries.Add(GroupStyles());
        control.Style = (Style)control.Resources[typeof(GMapMarkerPidsGroup3DControl)];
        HousingTests.Layout(control, 300, 100);
        Assert.True(control.ApplyTemplate() || control.Template != null);
        var fence = (FenceRunVisual)control.Template.FindName("PART_Fence3D", control)!;
        var canvas = (Canvas)control.Template.FindName("PART_PostCanvas", control)!;
        return (control, fence, canvas);
    }

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    // ---- C18: 카메라 거리를 프레임 높이에 비례시켜 남쪽 구간 근평면 클리핑 방지 ----

    [Theory]
    [InlineData(100, 12)]
    [InlineData(4000, 24)]
    [InlineData(5800, 12)]     // 종전 고정 4096/근평면 1 에서 잘리기 시작하던 높이(≈5.7k px)
    [InlineData(8000, 40)]
    [InlineData(10800, 24)]    // 상한(8192) 직전
    public void should_keep_south_end_in_front_of_near_plane_when_frame_is_tall(double frameHeightPx, double fenceHeightPx)
    {
        double distance = FenceMath.CameraDistance(frameHeightPx, fenceHeightPx);
        Assert.InRange(distance, FenceMath.MinCameraDistance, FenceMath.MaxCameraDistance);
        double depth = FenceMath.MinSceneDepth(distance, frameHeightPx, fenceHeightPx);
        Assert.True(depth > FenceMath.CameraNearPlane, $"H={frameHeightPx} D={distance:F0} 최소 깊이 {depth:F2} ≤ 근평면");
        Assert.True(distance + frameHeightPx * FenceMath.CosPitch / (2 * FenceMath.SinPitch) < distance * 4, "최대 깊이는 원평면(4·D) 안");
        if (frameHeightPx >= 5800)
            Assert.True(FenceMath.MinSceneDepth(FenceMath.MinCameraDistance, frameHeightPx, fenceHeightPx) < 1, "종전 고정 4096/근평면 1 이면 잘렸어야 하는 케이스");   // 회귀 고정
    }

    [Fact]
    public void should_scale_camera_distance_with_frame_height_and_cap_at_software_limit() => HousingTests.Sta(() =>
    {
        // 남북으로 긴 라인: 프레임 300×8000 px
        var pts = new[] { new Point(150, 10), new Point(150, 7990) };
        var frame = GMapMarkerPidsGroup3DControl.ComputeFrame(pts, 3.0, 2.4, EnumFenceMode.Posts, false, 0.2, 1.0);
        var view = new FenceRunVisual { HeightPx = frame.HeightPx, Mode = EnumFenceMode.Posts, MetersPerPixel = 0.2, Layout = frame.Layout };
        HousingTests.Layout(view, 300, 8000);
        var camera = Assert.IsType<OrthographicCamera>(view.Children.OfType<Viewport3D>().Single().Camera);

        double expected = FenceMath.CameraDistance(8000, frame.HeightPx);
        Assert.Equal(expected, view.CameraDistancePx, 6);
        Assert.True(expected > FenceMath.MinCameraDistance, "8000 px 프레임은 고정 4096 보다 멀리 두어야 한다");
        Assert.Equal(expected, ((Vector3D)(camera.Position - new Point3D())).Length, 6);
        Assert.Equal(FenceMath.CameraNearPlane, camera.NearPlaneDistance);
        Assert.Equal(expected * 4, camera.FarPlaneDistance, 6);
        Assert.True(FenceMath.MinSceneDepth(expected, 8000, frame.HeightPx) > camera.NearPlaneDistance);

        // 작은 프레임은 종전 값 그대로, 초대형 프레임은 소프트웨어 래스터라이저 한계(D ≥ 16384 전체 소실 실측) 아래로 고정
        HousingTests.Layout(view, 300, 200);
        Assert.Equal(FenceMath.MinCameraDistance, view.CameraDistancePx, 6);
        HousingTests.Layout(view, 300, 30000);
        Assert.Equal(FenceMath.MaxCameraDistance, view.CameraDistancePx, 6);
    });

    private static ResourceDictionary GroupStyles() => new() { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsGroupMarkerStyle.xaml", UriKind.Relative) };

    [Fact]
    public void should_expose_fence_and_post_parts_in_group_3d_template() => HousingTests.Sta(() =>
    {
        var model = new PidsGroupSymbolModel { Latitude = 37.5, Longitude = 127.0, PostSpacingM = 2.5, FenceMode = EnumFenceMode.SensorMount, Render3D = false };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0), new(37.501, 127.001, 0) };
        using var marker = new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
        var control = new GMapMarkerPidsGroup3DControl(marker);
        control.Resources.MergedDictionaries.Add(GroupStyles());
        control.Style = (Style)control.Resources[typeof(GMapMarkerPidsGroup3DControl)];
        HousingTests.Layout(control, 120, 80);
        Assert.True(control.ApplyTemplate() || control.Template != null);
        Assert.NotNull(control.Template.FindName("PART_Fence3D", control));
        Assert.NotNull(control.Template.FindName("PART_PostCanvas", control));
        Assert.NotNull(control.Template.FindName("PART_MainPolyline", control));
        Assert.Null(control.Template.FindName("PART_HitBackplate", control));   // 스트로크 히트 계약
        // 마커 → 컨트롤 DP 바인딩(OneWay)
        Assert.Equal(2.5, control.PostSpacingM); Assert.Equal(EnumFenceMode.SensorMount, control.FenceMode); Assert.False(control.Render3D);
        marker.PostSpacingM = 4; Assert.Equal(4, control.PostSpacingM);
        Assert.Equal(FenceLodLevel.Line2D, control.FenceLod);   // 지도 없음 → 기하 미계산
    });

    [Fact]
    public void should_bind_door_state_to_3d_pids_control() => HousingTests.Sta(() =>
    {
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.Gate });
        var control = new GMapMarker3DHousingControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[typeof(GMapMarker3DHousingControl)];
        HousingTests.Layout(control, 83, 41);
        Assert.Equal(EnumDoorState.Unknown, control.DoorState);
        marker.DoorState = EnumDoorState.Open;
        Assert.Equal(EnumDoorState.Open, control.DoorState);
        var housing = Assert.IsType<HousingVisual>(control.Template.FindName("PART_Housing3D", control));
        Assert.Equal("fencegate", housing.ModelKey);
    });
}
