using System;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 관성 회전 중 카메라 상자 떨림 재현 하네스(창 · WebView2 없음).
/// 3D 쪽 = view.js 프레임 순서를 옮긴 것: controls.update(감쇠 0.05) → camera 메시지(150 ms 에 한 번 · 놓을 때 "user") → 그리기 → overlay.screen(0.1 px 반올림 · 0.5 px 넘게 바뀐 것만, 새 3D 는 그 프레임 view 를 싣고 view 가 바뀌면 전부).
/// GIS 쪽 = MapViewModel 경로를 옮긴 것: 메시지는 실코드 Map3DMath.ParseMessage 로 풀고, camera 면 2D 를 맞추며 2D 통지 3번(줌 · 위치 · 회전)이 상자를 다시 놓고,
/// overlay.screen 이면 좌표를 기록하고 놓는다. 놓는 셈 · 출처 판정은 실코드(Map3DScreen.OffsetView3D · PlaceOn2DChange · PopupTopLeft3D · DraggedAnchor2D).
/// 2D 지도는 GMap 처럼 정수 화면 좌표, 2D 줌은 0.5 칸으로 맞춘다고 본다.
/// 수정 전 = 옛 3D(view 없음) + 끄는 중에도 따라가기 + 2D 에서 매번 오프셋 재기, 수정 후 = view 있음 + 끄는 중 건너뛰기 + 오프셋 한 번(BaseOffset).
/// </summary>
public class Map3DPopupJitterTests
{
    private readonly ITestOutputHelper _out;
    public Map3DPopupJitterTests(ITestOutputHelper output) => _out = output;

    private const double Cx = 600, Cy = 400, FrameMs = 1000d / 60d;
    private const string Key = "camera-popup:7";
    private static readonly (double X, double Y) Pivot = (218.0, 100.0);   // 메르카토르 세계 좌표(줌 0, 256 px) — 3D 궤도 중심 = 2D 지도 중심

    private sealed class Scenario
    {
        public bool Fixed = true;              // 수정 후 = 3D 가 그 프레임 view 를 싣고 · GIS 가 끄는 중 건너뛰고 · 오프셋을 한 번만 잰다. false = 수정 전 경로
        public bool ScreenFirst;               // 같은 프레임에서 overlay.screen 이 camera 보다 먼저 도착
        public bool DropCamera;                // camera 메시지가 하나도 안 온다
        public int DropScreenEvery;            // n 번째 overlay.screen 마다 버린다(0 = 안 버림)
        public double Omega0 = 4, Zeta0;       // 놓을 때 회전 속도(°/프레임) · 줌 속도(/프레임)
        public double Bearing0 = 30, Zoom0 = 19.2;
        public int Frames = 240;
        public (double X, double Y) MarkerOff = (0.0004, 0.00025);   // 마커 − 궤도 중심(세계 좌표)
        public (double X, double Y) AnchorOff = (0.0002, -0.0004);   // 앵커 − 마커(세계 좌표) — 2D 오프셋
        public Func<int, bool> Hidden = _ => false;                  // 마커가 카메라 뒤인 프레임
        public (int From, int To, double Dx)? Drag;                  // 헤더 끌기(프레임마다 Dx px)
    }

    private sealed record Frame(int K, double Left, double Top, double IdealLeft, double IdealTop, bool Hidden, bool Dragging, int Applies, int From2D);

    private sealed record Result(List<Frame> Frames, int Flips, int Reversals, double MaxErr, int MaxAppliesPerFrame, int From2D, double FinalErr)
    {
        public override string ToString() => $"왕복(진행 방향 오차 부호 바뀜)={Flips} 역행={Reversals} 최대오차={MaxErr:F2}px 프레임당 최대 적용={MaxAppliesPerFrame} 2D통지 적용={From2D} 멈춘 뒤 오차={FinalErr:F2}px";
    }

    // ── 3D(뷰어) ──

    private static (double X, double Y) Rot(double x, double y, double bearing) => Map3DScreen.Offset2DTo3D(x, y, 0, 0, 0, bearing);

    private static (double X, double Y) Project(double zoom, double bearing, (double X, double Y) world)
    {
        double s = Math.Pow(2, zoom);
        var (rx, ry) = Rot((world.X - Pivot.X) * s, (world.Y - Pivot.Y) * s, bearing);
        return (Cx + rx, Cy + ry);
    }

    /// <summary>overlay-screen.js ScreenReporter 와 같은 규칙(0.5 px · visible · view 바뀌면 전부).</summary>
    private sealed class Reporter
    {
        private (double? X, double? Y, bool V)? _last;
        private (double Z, double B)? _view;

        public bool Changed((double? X, double? Y, bool V) cur, (double Z, double B)? view)
        {
            if (view is { } v && !(_view is { } p && Math.Abs(p.Z - v.Z) < 1e-4 && Math.Abs(p.B - v.B) < 0.01)) { _view = v; _last = null; }
            if (_last is { } l && l.V == cur.V && Near(l.X, cur.X) && Near(l.Y, cur.Y)) return false;
            _last = cur;
            return true;
        }

        private static bool Near(double? a, double? b) => a == null || b == null ? a == b : Math.Abs(a.Value - b.Value) < 0.5;
    }

    private static string CameraJson(string reason, double zoom, double bearing)
        => new JObject { ["type"] = "camera", ["reason"] = reason, ["lon"] = 127.0, ["lat"] = 37.5, ["zoom"] = zoom, ["bearing"] = bearing, ["pitch"] = 50.0 }.ToString();

    private static string ScreenJson((double? X, double? Y, bool V) p, (double Z, double B)? view)
    {
        var item = new JObject { ["key"] = Key, ["x"] = p.X, ["y"] = p.Y, ["visible"] = p.V };
        var o = new JObject { ["type"] = "overlay.screen", ["items"] = new JArray(item) };
        if (view is { } v) o["view"] = new JObject { ["zoom"] = v.Z, ["bearing"] = v.B };
        return o.ToString();
    }

    // ── GIS(MapViewModel 경로) ──

    private sealed class Gis
    {
        private readonly Scenario _sc;
        private readonly Dictionary<string, (double X, double Y)> _screens = new();
        private Map3DScreenView? _view;
        private Map3DCamera? _last;
        private double _z2, _b2;
        private (double X, double Y)? _base, _baseAnchor;   // MapViewModel._map3DPopupOffsets — 앵커가 같으면 다시 재지 않는다
        public (double X, double Y) MarkerW, AnchorW;
        public double Left, Top;
        public bool Dragging;
        public int Applies, From2D;

        public Gis(Scenario sc, double zoom, double bearing, (double X, double Y) markerW, (double X, double Y) anchorW)
        {
            _sc = sc; _z2 = Math.Round(zoom * 2) / 2; _b2 = bearing; MarkerW = markerW; AnchorW = anchorW;
            (Left, Top) = Proj2D(anchorW);   // 열 때는 2D 규칙 자리
        }

        public void Feed(string json)
        {
            var m = Map3DMath.ParseMessage(json)!;
            if (m.Type == "overlay.screen")
            {
                foreach (var s in m.Screens!)
                    if (s.X is double x && s.Y is double y) _screens[s.Key] = (x, y); else _screens.Remove(s.Key);
                if (m.ScreenView != null) _view = m.ScreenView;
                Place(fromScreens: true);
                return;
            }
            var cam = m.Camera!.Value;
            _last = cam;
            _z2 = Math.Round(cam.Zoom * 2) / 2;                      // Apply3DCameraTo2D — 2D 줌은 0.5 칸
            _b2 = Map3DMath.ToMap3DBearing(Map3DMath.ToGMapBearing(cam.Bearing));
            for (int i = 0; i < 3; i++) Place(fromScreens: false);   // 2D 통지(줌 · 위치 · 회전) → RefreshCameraPopupPositions
        }

        /// <summary>MapViewModel.TryPlaceCameraPopupsIn3D 와 같은 순서.</summary>
        private void Place(bool fromScreens)
        {
            if (!fromScreens && !Map3DScreen.PlaceOn2DChange(_view)) return;
            if (Dragging && _sc.Fixed) return;
            if (!_screens.TryGetValue(Key, out var cam)) return;
            var (z3, b3) = Map3DScreen.OffsetView3D(_view, _last, _z2, _b2);
            if (!_sc.Fixed) (Left, Top) = Map3DScreen.PopupTopLeft3D(cam, Proj2D(MarkerW), Proj2D(AnchorW), _z2, z3, _b2, b3);
            else
            {
                if (_base == null || _baseAnchor != AnchorW) { _base = Map3DScreen.BaseOffset(Proj2D(MarkerW), Proj2D(AnchorW), _z2, _b2); _baseAnchor = AnchorW; }
                (Left, Top) = Map3DScreen.PopupTopLeft3DFromBase(cam, _base.Value, z3, b3);
            }
            Applies++;
            if (!fromScreens) From2D++;
        }

        /// <summary>끌기 끝 — OnCameraPopupDragCompleted → Map3DDraggedAnchor(3D 오프셋을 2D 로 되돌린 앵커, GMap 처럼 정수 화면점에서 위경도로).</summary>
        public void DragCompleted()
        {
            Dragging = false;
            if (!_screens.TryGetValue(Key, out var cam)) return;
            var (z3, b3) = Map3DScreen.OffsetView3D(_view, _last, _z2, _b2);
            var (x, y) = Map3DScreen.DraggedAnchor2D((Left, Top), cam, Proj2D(MarkerW), _z2, z3, _b2, b3);
            AnchorW = Unproj2D((int)x, (int)y);
        }

        private (double X, double Y) Proj2D((double X, double Y) w) { var (x, y) = Project(_z2, _b2, w); return (Math.Round(x), Math.Round(y)); }

        private (double X, double Y) Unproj2D(double x, double y)
        {
            double s = Math.Pow(2, _z2);
            var (rx, ry) = Rot(x - Cx, y - Cy, -_b2);
            return (Pivot.X + rx / s, Pivot.Y + ry / s);
        }
    }

    private static Result Simulate(Scenario sc)
    {
        var markerW = (Pivot.X + sc.MarkerOff.X, Pivot.Y + sc.MarkerOff.Y);
        var gis = new Gis(sc, sc.Zoom0, sc.Bearing0, markerW, (markerW.Item1 + sc.AnchorOff.X, markerW.Item2 + sc.AnchorOff.Y));
        var reporter = new Reporter();
        double b = sc.Bearing0, z = sc.Zoom0, omega = sc.Omega0, zeta = sc.Zeta0, liveT = double.NegativeInfinity;
        int screensSent = 0;
        var frames = new List<Frame>();
        for (int k = 0; k < sc.Frames; k++)
        {
            double t = k * FrameMs;
            bool hidden = sc.Hidden(k);
            bool changed = Math.Abs(omega) > 1e-4 || Math.Abs(zeta) > 1e-6;
            if (changed) { b += omega; z += zeta; omega *= 0.95; zeta *= 0.95; }   // controls.update() — 감쇠
            var msgs = new List<string>();
            if (k == 0) msgs.Add(CameraJson("user", z, b));                         // 놓음(end)
            else if (changed && t - liveT >= 150) { liveT = t; msgs.Add(CameraJson("live", z, b)); }
            if (changed || k == 0)                                                    // 그린 프레임 뒤 reportScreens
            {
                var (sx, sy) = Project(z, b, markerW);
                var p = hidden ? ((double?)null, (double?)null, false) : ((double?)Math.Round(sx * 10) / 10, (double?)Math.Round(sy * 10) / 10, true);
                (double Z, double B)? view = sc.Fixed ? (Math.Round(z * 1e4) / 1e4, Math.Round(b * 100) / 100) : null;
                if (reporter.Changed(p, view) && !(sc.DropScreenEvery > 0 && ++screensSent % sc.DropScreenEvery == 0))
                    msgs.Add(ScreenJson(p, view));
            }
            if (sc.DropCamera) msgs.RemoveAll(m => m.Contains("\"camera\""));
            if (sc.ScreenFirst) msgs = msgs.OrderBy(m => m.Contains("\"camera\"") ? 1 : 0).ToList();

            int applies0 = gis.Applies, from2D0 = gis.From2D;
            if (sc.Drag is { } d0 && k == d0.From) gis.Dragging = true;   // 헤더를 누름(OnLeftButtonDown) — 이 프레임 메시지보다 먼저
            foreach (var m in msgs) gis.Feed(m);
            bool dragging = false;
            if (sc.Drag is { } d)
            {
                if (k >= d.From && k < d.To) { gis.Dragging = dragging = true; gis.Left += d.Dx; }   // OnMouseMove: CanvasLeft = CanvasLeft + dx
                else if (k == d.To) gis.DragCompleted();
            }
            var (mx, my) = Project(z, b, markerW);
            double s = Math.Pow(2, z);
            var (ox, oy) = Rot((gis.AnchorW.X - markerW.Item1) * s, (gis.AnchorW.Y - markerW.Item2) * s, b);
            frames.Add(new Frame(k, gis.Left, gis.Top, mx + ox, my + oy, hidden, dragging, gis.Applies - applies0, gis.From2D - from2D0));
        }
        return Measure(frames);
    }

    /// <summary>
    /// 왕복 = 진행 방향(이상 자리의 이동 방향) 오차의 부호가 바뀐 횟수(0.25 px 미만은 무시) — 늦었다(−) 따라잡는(+) 계단 · 앞뒤 흔들림을 센다.
    /// 역행 = 상자가 앞 프레임과 반대로 0.5 px 넘게 움직였는데 이상 자리는 그렇지 않은 횟수.
    /// 오차는 마커가 보이고 끌지 않는 프레임만(가려진 동안은 마지막 자리에 둔다).
    /// </summary>
    private static Result Measure(List<Frame> f)
    {
        int flips = 0, reversals = 0, lastSign = 0;
        double maxErr = 0;
        for (int i = 1; i < f.Count; i++)
        {
            Frame a = f[i - 1], c = f[i];
            if (c.Hidden || a.Hidden || c.Dragging || a.Dragging) { lastSign = 0; continue; }
            maxErr = Math.Max(maxErr, Math.Abs(c.Left - c.IdealLeft) + Math.Abs(c.Top - c.IdealTop));
            double vbx = c.Left - a.Left, vby = c.Top - a.Top, vix = c.IdealLeft - a.IdealLeft, viy = c.IdealTop - a.IdealTop;
            double vi = Math.Sqrt(vix * vix + viy * viy);
            if (vi > 1e-6)
            {
                double e = ((vbx - vix) * vix + (vby - viy) * viy) / vi;
                if (Math.Abs(e) >= 0.25)
                {
                    int sign = Math.Sign(e);
                    if (lastSign != 0 && sign != lastSign) flips++;
                    lastSign = sign;
                }
            }
            if (i >= 2 && !f[i - 2].Hidden && !f[i - 2].Dragging)
            {
                Frame p = f[i - 2];
                double pbx = a.Left - p.Left, pby = a.Top - p.Top, pix = a.IdealLeft - p.IdealLeft, piy = a.IdealTop - p.IdealTop;
                bool boxBack = Math.Sqrt(vbx * vbx + vby * vby) > 0.5 && Math.Sqrt(pbx * pbx + pby * pby) > 0.5 && vbx * pbx + vby * pby < 0;
                if (boxBack && vix * pix + viy * piy > 0) reversals++;
            }
        }
        var last = f[^1];
        return new Result(f, flips, reversals, maxErr, f.Max(x => x.Applies), f.Sum(x => x.From2D),
                          Math.Abs(last.Left - last.IdealLeft) + Math.Abs(last.Top - last.IdealTop));
    }

    private Result Run(string name, Scenario sc)
    {
        var r = Simulate(sc);
        _out.WriteLine($"{name}: {r}");
        return r;
    }

    // ── 감속 곡선 재현: 수정 전 → 수정 후 ──

    [Fact]
    public void should_jitter_when_frame_view_missing_before_fix()
    {
        var before = Run("수정 전(옛 3D · camera 150 ms 방위)", new Scenario { Fixed = false });
        Assert.True(before.Flips >= 5, before.ToString());     // 150 ms 마다 늦었다 따라잡는 계단
        Assert.True(before.MaxErr > 20, before.ToString());    // 마커 둘레에서 수십 px 흔들림
        Assert.True(before.MaxAppliesPerFrame >= 2, before.ToString());   // camera 프레임엔 2D 통지까지 여러 번
    }

    [Fact]
    public void should_have_no_back_and_forth_when_inertia_rotation_after_fix()
    {
        var after = Run("수정 후", new Scenario());
        Assert.Equal(0, after.Flips);
        Assert.Equal(0, after.Reversals);
        Assert.True(after.MaxErr <= 1.5, after.ToString());    // 0.1 px 반올림 + 2D 정수 오프셋 정도만
        Assert.Equal(1, after.MaxAppliesPerFrame);            // 프레임당 한 번
        Assert.Equal(0, after.From2D);                        // 2D 통지는 놓지 않는다
    }

    [Theory]
    [InlineData(false, false, 0, "screen 먼저")]
    [InlineData(true, false, 0, "camera 먼저")]
    [InlineData(true, true, 0, "camera 전부 누락")]
    public void should_not_jitter_when_message_order_varies(bool cameraFirst, bool dropCamera, int dropScreenEvery, string name)
    {
        var r = Run(name, new Scenario { ScreenFirst = !cameraFirst, DropCamera = dropCamera, DropScreenEvery = dropScreenEvery });
        Assert.Equal(0, r.Flips);
        Assert.Equal(0, r.Reversals);
        Assert.True(r.MaxErr <= 1.5, r.ToString());
        Assert.True(r.MaxAppliesPerFrame <= 1, r.ToString());
    }

    [Fact]
    public void should_never_move_backwards_when_some_screen_messages_missing()
    {
        // overlay.screen 이 빠진 프레임은 한 프레임 멈췄다 다음에 따라잡는다 — 뒤로 가거나 흔들리지 않고, 오차는 한 프레임 이동 정도
        var r = Run("screen 3번째마다 누락", new Scenario { DropScreenEvery = 3 });
        Assert.Equal(0, r.Reversals);
        double maxStep = r.Frames.Skip(1).Zip(r.Frames, (c, a) => Math.Abs(c.IdealLeft - a.IdealLeft) + Math.Abs(c.IdealTop - a.IdealTop)).Max();
        Assert.True(r.MaxErr <= maxStep + 1.5, $"{r} 한 프레임 이동={maxStep:F2}");
    }

    [Fact]
    public void should_stay_smooth_when_rotating_around_marker_itself()
    {
        // 마커를 궤도 중심에 두고 돌림 — 마커 화면 자리는 그대로라 옛 3D 는 overlay.screen 을 보내지 않아 상자가 camera 메시지 때만 돌았다
        var before = Run("마커 중심 회전 · 수정 전", new Scenario { Fixed = false, MarkerOff = (0, 0) });
        var after = Run("마커 중심 회전 · 수정 후", new Scenario { MarkerOff = (0, 0) });
        Assert.True(before.MaxErr > 20, before.ToString());
        Assert.Equal(0, after.Flips);
        Assert.True(after.MaxErr <= 1.5, after.ToString());
    }

    [Fact]
    public void should_stay_smooth_when_rotating_and_zooming_with_inertia()
    {
        var before = Run("회전 + 줌 관성 · 수정 전", new Scenario { Fixed = false, Zeta0 = 0.02 });
        var after = Run("회전 + 줌 관성 · 수정 후", new Scenario { Zeta0 = 0.02 });
        Assert.True(before.Flips > after.Flips, $"{before} / {after}");
        Assert.Equal(0, after.Flips);
        Assert.Equal(0, after.Reversals);
        Assert.True(after.MaxErr <= 1.5, after.ToString());
    }

    [Fact]
    public void should_keep_last_spot_then_resume_without_jump_when_marker_goes_behind_camera()
    {
        var r = Run("마커 카메라 뒤(40~70 프레임)", new Scenario { Hidden = k => k >= 40 && k < 70 });
        var hidden = r.Frames.Where(f => f.Hidden).ToList();
        Assert.All(hidden, f => Assert.Equal(0, f.Applies));                         // 가려진 동안은 놓지 않는다
        Assert.All(hidden, f => Assert.Equal((hidden[0].Left, hidden[0].Top), (f.Left, f.Top)));   // 마지막 자리 그대로
        var back = r.Frames.First(f => f.K == 70);
        Assert.True(Math.Abs(back.Left - back.IdealLeft) + Math.Abs(back.Top - back.IdealTop) <= 1.5);   // 돌아오면 바로 제자리
        Assert.Equal(0, r.Flips);
    }

    [Fact]
    public void should_follow_drag_only_when_dragging_box_during_3d_inertia()
    {
        var sc = new Scenario { Drag = (20, 60, 3) };
        var after = Run("끄는 중 3D 관성 · 수정 후", sc);
        var start = after.Frames[19];
        var during = after.Frames.Where(f => f.Dragging).ToList();
        for (int i = 0; i < during.Count; i++)
        {
            Assert.Equal(start.Left + 3 * (i + 1), during[i].Left, 6);   // 끈 만큼만 — 따라가기가 덮지 않는다
            Assert.Equal(start.Top, during[i].Top, 6);
        }
        // 놓은 자리가 새 앵커 — 다음 프레임부터 그 관계로 따라간다(튀지 않는다)
        Assert.True(after.Frames.Where(f => f.K >= 61).All(f => Math.Abs(f.Left - f.IdealLeft) + Math.Abs(f.Top - f.IdealTop) <= 2.5), after.ToString());
        Assert.Equal(0, after.Flips);

        var before = Run("끄는 중 3D 관성 · 수정 전", new Scenario { Fixed = false, Drag = (20, 60, 3) });
        var lost = before.Frames.Where(f => f.Dragging).Last();
        Assert.True(Math.Abs(lost.Left - (start.Left + 3 * during.Count)) > 30, $"수정 전 끝 자리 {lost.Left:F1} vs 끈 자리 {start.Left + 3 * during.Count:F1}");   // 따라가기가 끌기를 덮어 끈 거리를 잃는다
    }

    [Fact]
    public void should_land_on_existing_rule_spot_when_inertia_stops()
    {
        // 멈춘 뒤 = 기존 규칙(마커 3D 자리 + 2D 오프셋을 3D 줌 · 방위로) 그 자리 — 회귀 없음
        var r = Run("멈춘 뒤", new Scenario { Frames = 400 });
        Assert.True(r.FinalErr <= 1.5, r.ToString());
        var tail = r.Frames.Skip(380).ToList();
        Assert.All(tail, f => Assert.Equal((tail[0].Left, tail[0].Top), (f.Left, f.Top)));   // 멈추면 더 움직이지 않는다
    }

    // ── 메시지 해석 · 출처 판정(실코드) ──

    [Fact]
    public void should_parse_frame_view_when_overlay_screen_carries_view()
    {
        var m = Map3DMath.ParseMessage("""{"type":"overlay.screen","items":[{"key":"a","x":1,"y":2,"visible":true}],"view":{"zoom":19.25,"bearing":-12.5}}""")!;
        Assert.Equal(new Map3DScreenView(19.25, -12.5), m.ScreenView);
        Assert.Single(m.Screens!);
    }

    [Theory]
    [InlineData("""{"type":"overlay.screen","items":[]}""")]
    [InlineData("""{"type":"overlay.screen","items":[],"view":null}""")]
    [InlineData("""{"type":"overlay.screen","items":[],"view":{"zoom":"19","bearing":0}}""")]
    [InlineData("""{"type":"overlay.screen","items":[],"view":{"zoom":19}}""")]
    public void should_have_no_frame_view_when_view_missing_or_bad(string json)
        => Assert.Null(Map3DMath.ParseMessage(json)!.ScreenView);

    [Fact]
    public void should_prefer_frame_view_then_last_camera_then_2d_when_offset_view_chosen()
    {
        var cam = new Map3DCamera(127, 37.5, 18.5, 40, 50);
        Assert.Equal((19.1, 55.0), Map3DScreen.OffsetView3D(new Map3DScreenView(19.1, 55), cam, 18, 10));
        Assert.Equal((18.5, 40.0), Map3DScreen.OffsetView3D(null, cam, 18, 10));
        Assert.Equal((18.0, 10.0), Map3DScreen.OffsetView3D(null, null, 18, 10));
    }

    [Theory]
    [InlineData(19, 19.3, 30, 75)]
    [InlineData(17.5, 20.2, -170, 175)]
    [InlineData(18, 18, 0, 0)]
    [InlineData(20, 16.6, 359, 1)]
    public void should_equal_existing_rule_when_offset_measured_once_as_base(double z2, double z3, double b2, double b3)
    {
        (double X, double Y) m3 = (612.4, 333.1), m2 = (580, 300), a2 = (690, 120);
        var expected = Map3DScreen.PopupTopLeft3D(m3, m2, a2, z2, z3, b2, b3);
        var actual = Map3DScreen.PopupTopLeft3DFromBase(m3, Map3DScreen.BaseOffset(m2, a2, z2, b2), z3, b3);
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
    }

    [Fact]
    public void should_place_from_2d_change_only_when_no_frame_view()
    {
        Assert.True(Map3DScreen.PlaceOn2DChange(null));
        Assert.False(Map3DScreen.PlaceOn2DChange(new Map3DScreenView(19, 0)));
    }
}
