using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// map-tilt-25d PRD v1.1 — GMapCustomControl 틸트 배선(플랜 IMPL-F1/F2/F2a/F3/F4) STA 검증.
/// FR-01 ApplyViewTransform identity/합성/재대입(불변식 5) · FR-03 Dispatcher 코얼레싱(18.0 휠다운 → 17.0 → 17.5 = Hold) ·
/// FR-04 GetVisibleInnerRect·φ_layout 정착(G8) · FR-05 InnerToOuter 위임 · FR-06 스냅샷 TiltCos · G3 앵커 잠금 · FR-13 Tier0.
/// 렌더 타깃 없는 STA 스레드는 RenderCapability.Tier=0 이므로 <see cref="GMapCustomControl.SoftwareTierOverride"/>=false 로 하드웨어를 가정한다
/// (Tier0 강등 경로는 별도 테스트가 true 로 강제). 실기(RDP Tier 값·픽셀 동일)는 V-04/NFR-03 실기 항목.
/// </summary>
public class MapTiltWiringTests
{
    private const double Cos20 = 0.93969262078590838;   // cos 20°

    private static GMapCustomControl NewMap(double w = 640, double h = 480, int zoom = 18)
    {
        var map = new GMapCustomControl { Width = w, Height = h, MinZoom = 1, MaxZoom = 19, Position = new PointLatLng(37.5, 127) };
        map.Zoom = zoom;
        map.SoftwareTierOverride = false;
        HousingTests.Layout(map, w, h);
        Pump();                                           // Zoom 세터가 예약한 재평가(OFF → 무변화)를 소진
        return map;
    }

    private static GMapCustomControl NewTiltedMap(double angle = 20, double minZoom = 18, int zoom = 18)
    {
        var map = NewMap(zoom: zoom);
        map.TiltSettings = new MapTiltModel { MinZoom = minZoom, HysteresisSteps = 1, MaxAngleDeg = 35 };
        map.RequestedTiltDeg = angle;
        map.IsTiltFeatureEnabled = true;
        Pump();
        return map;
    }

    /// <summary>뷰 변환 항등 — 빌더는 null 을 대입하지만 WPF 기본값은 Transform.Identity 라 둘 다 항등으로 본다(NFR-03 픽셀 동일).</summary>
    private static void AssertIdentity(GMapCustomControl map)
        => Assert.True(map.RenderTransform == null || map.RenderTransform.Value.IsIdentity, $"RenderTransform not identity: {map.RenderTransform}");

    /// <summary>Render 우선순위 예약(ReevaluateTilt·QueueViewportSnapshot)을 소진 — Background 프레임까지 펌프.</summary>
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs) return false;
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background,
                (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
            timer.Start();
            Dispatcher.PushFrame(frame);
            timer.Stop();
        }
        return true;
    }

    // ───────────────────────────────── FR-01 ApplyViewTransform

    [Fact]
    public void should_keep_render_transform_null_when_no_digital_zoom_and_no_tilt() => HousingTests.Sta(() =>
    {
        var map = NewMap();
        AssertIdentity(map);                 // NFR-03: kill-switch OFF 픽셀 동일(변환 항등)
        Assert.Equal(0.0, map.TiltDeg);
        Assert.Equal(TiltState.Off, map.TiltState);
        Assert.Equal(TiltMath.ReasonOff, map.TiltReason);
        map.DigitalZoomLevel = 2;                         // 1.5×
        var st = Assert.IsType<ScaleTransform>(map.RenderTransform);
        Assert.Equal(1.5, st.ScaleX, 9); Assert.Equal(1.5, st.ScaleY, 9);
        Assert.Equal(320, st.CenterX, 9); Assert.Equal(240, st.CenterY, 9);
        map.DigitalZoomLevel = 0;
        AssertIdentity(map);                 // 항등 복원
    });

    [Fact]
    public void should_compose_scale_with_cos_phi_when_tilt_active_and_digital_zoom() => HousingTests.Sta(() =>
    {
        var map = NewTiltedMap();
        Assert.Equal(20.0, map.TiltDeg, 9);
        Assert.Equal(TiltState.Active, map.TiltState);
        var st = Assert.IsType<ScaleTransform>(map.RenderTransform);
        Assert.Equal(1.0, st.ScaleX, 9); Assert.Equal(Cos20, st.ScaleY, 9);

        map.DigitalZoomLevel = 2;                         // s=1.5 → ScaleTransform(1.5, 1.5·cos20, W/2, H/2)
        st = Assert.IsType<ScaleTransform>(map.RenderTransform);
        Assert.Equal(1.5, st.ScaleX, 9);
        Assert.Equal(1.5 * Cos20, st.ScaleY, 9);
        Assert.Equal(320, st.CenterX, 9); Assert.Equal(240, st.CenterY, 9);
    });

    [Fact]
    public void should_reassign_new_transform_object_when_reapplied() => HousingTests.Sta(() =>
    {
        // 불변식 5: 어도너는 in-place ScaleY 변경을 추종하지 않는다 → 매 변경 새 객체 재대입, 옛 객체는 불변
        var map = NewTiltedMap();
        var first = Assert.IsType<ScaleTransform>(map.RenderTransform);
        double firstScaleY = first.ScaleY;
        map.RequestedTiltDeg = 25;
        Pump();
        var second = Assert.IsType<ScaleTransform>(map.RenderTransform);
        Assert.NotSame(first, second);
        Assert.Equal(firstScaleY, first.ScaleY, 12);      // 옛 객체 in-place 변경 없음
        Assert.Equal(Math.Cos(25 * Math.PI / 180), second.ScaleY, 9);

        map.DigitalZoomLevel = 1;                         // s 변경도 재대입
        var third = Assert.IsType<ScaleTransform>(map.RenderTransform);
        Assert.NotSame(second, third);
    });

    [Fact]
    public void should_reset_to_identity_immediately_when_toggled_off() => HousingTests.Sta(() =>
    {
        var map = NewTiltedMap();
        Assert.NotNull(map.RenderTransform);
        map.ToggleTiltFeature();                          // G10: OFF = 즉시 φ=0(회전의 각도 유지와 다름)
        Assert.False(map.IsTiltFeatureEnabled);
        Pump();
        Assert.Equal(0.0, map.TiltDeg);
        Assert.Equal(TiltState.Off, map.TiltState);
        AssertIdentity(map);
        Assert.Equal(0.0, map.TiltLayoutDeg);
        Assert.Equal(1.0, map.CurrentViewportSnapshot.TiltCos, 9);
    });

    // ───────────────────────────────── FR-03 코얼레싱(Dispatcher Render, 프레임당 1회)

    [Fact]
    public void should_hold_tilt_when_wheel_down_passes_17_then_settles_at_17_5_in_one_frame() => HousingTests.Sta(() =>
    {
        // SIM-C011: 벤더 정수 줌(18→17, OnMapZoomChanged 동기) 뒤 dzl(0→1) — 같은 프레임에 두 요청 → 최종 17.5 로 1회 판정 = Hold(Active 유지)
        var map = NewTiltedMap();
        int decided = 0;
        map.TiltDecided += (_, d) => decided++;
        map.Zoom = 17;
        map.DigitalZoomLevel = 1;
        Assert.Equal(17.5, map.EffectiveZoom, 9);
        Assert.Equal(20.0, map.TiltDeg, 9);              // 커밋 전 — 중간 17.0 으로 φ 가 떨어지지 않았다
        Pump();
        Assert.Equal(TiltState.Hold, map.TiltState);
        Assert.Equal(TiltMath.ReasonHold, map.TiltReason);
        Assert.Equal(20.0, map.TiltDeg, 9);
        Assert.Equal(1, decided);                         // 프레임당 1회(Active→Hold 상태 변경 1건)
        Assert.Equal(Cos20, map.CurrentViewportSnapshot.TiltCos, 9);
    });

    [Fact]
    public void should_stay_flat_when_zoom_in_from_17_reaches_17_5() => HousingTests.Sta(() =>
    {
        // 진입 방향 구분: 17.0 Below → 17.5 는 밴드지만 wasActive=false → φ=0 유지
        var map = NewTiltedMap(zoom: 17);
        Assert.Equal(TiltState.Below, map.TiltState);
        Assert.Equal(0.0, map.TiltDeg);
        map.DigitalZoomLevel = 1;
        Pump();
        Assert.Equal(17.5, map.EffectiveZoom, 9);
        Assert.Equal(0.0, map.TiltDeg);
        var st = Assert.IsType<ScaleTransform>(map.RenderTransform);   // s=1.25 → 변환은 있지만 φ=0(ScaleY=ScaleX)
        Assert.Equal(1.25, st.ScaleX, 9);
        Assert.Equal(1.25, st.ScaleY, 9);
        Assert.Equal(1.0, map.CurrentViewportSnapshot.TiltCos, 9);
    });

    [Fact]
    public void should_drop_tilt_when_zoom_reaches_17_and_reenter_at_18() => HousingTests.Sta(() =>
    {
        // SIM-R001/R002 왕복: 18.0 → 17.0 이탈 → 18.0 재진입
        var map = NewTiltedMap();
        map.SetEffectiveZoom(17.0);
        Pump();
        Assert.Equal(TiltState.Below, map.TiltState);
        Assert.Equal(0.0, map.TiltDeg);
        Assert.Equal(0.0, map.TiltLayoutDeg);             // 게이트 OFF 전이 = φ_layout 즉시 0
        map.SetEffectiveZoom(18.0);
        Pump();
        Assert.Equal(TiltState.Active, map.TiltState);
        Assert.Equal(20.0, map.TiltDeg, 9);
        Assert.Equal(20.0, map.TiltLayoutDeg, 9);         // 게이트 ON 전이 = φ_layout 즉시 φ
    });

    [Fact]
    public void should_commit_pending_reevaluation_when_flushed() => HousingTests.Sta(() =>
    {
        var map = NewMap();
        map.IsTiltFeatureEnabled = true;                  // 예약만
        Assert.Equal(0.0, map.TiltDeg);
        Assert.True(map.FlushTiltReevaluation());
        Assert.Equal(20.0, map.TiltDeg, 9);              // 기본 요청각 20(G2)
        Assert.False(map.FlushTiltReevaluation());        // 대기 없음
        Pump();                                           // 잔여 Dispatcher 콜백은 무동작
        Assert.Equal(20.0, map.TiltDeg, 9);
    });

    // ───────────────────────────────── FR-04 φ_layout 정착(G8)·GetVisibleInnerRect

    [Fact]
    public void should_preexpand_layout_to_max_then_settle_when_angle_changed_while_active() => HousingTests.Sta(() =>
    {
        var map = NewTiltedMap();
        Assert.Equal(20.0, map.TiltLayoutDeg, 9);         // 게이트 ON: 즉시 φ
        map.RequestedTiltDeg = 25;
        Pump();
        Assert.Equal(25.0, map.TiltDeg, 9);              // 렌더 φ 는 즉시(스냅)
        Assert.Equal(35.0, map.TiltLayoutDeg, 9);         // 레이아웃은 φmax 선확장
        map.RequestedTiltDeg = 30;                        // 조작 중 재변경 — 디바운스 재시작, 선확장 유지
        Pump();
        Assert.Equal(35.0, map.TiltLayoutDeg, 9);
        Assert.True(WaitUntil(() => Math.Abs(map.TiltLayoutDeg - 30.0) < 1e-6, timeoutMs: 3000), "150 ms 정착 미도달");
        Assert.Equal(30.0, map.TiltDeg, 9);
    });

    [Fact]
    public void should_return_full_control_rect_when_flat_and_no_zoom() => HousingTests.Sta(() =>
    {
        var map = NewMap();
        Assert.Equal(new Rect(0, 0, 640, 480), map.GetVisibleInnerRect());
        Assert.Equal(0.0, map.ViewportOffsetY);
    });

    [Fact]
    public void should_shrink_visible_inner_rect_by_scale_and_cos_phi_when_tilted() => HousingTests.Sta(() =>
    {
        var map = NewTiltedMap();
        map.DigitalZoomLevel = 2;                         // s=1.5
        var r = map.GetVisibleInnerRect();
        Assert.Equal(640 / 1.5, r.Width, 6);
        Assert.Equal(480 / (1.5 * Cos20), r.Height, 6);
        Assert.Equal(320, r.Left + r.Width / 2, 6);       // 중심 불변
        Assert.Equal(240, r.Top + r.Height / 2, 6);
    });

    [Fact]
    public void should_account_overscan_margin_when_visible_rect_and_inner_to_outer_computed() => HousingTests.Sta(() =>
    {
        // FR-04 레이아웃(G 그룹 MultiBinding)을 흉내: Margin=(0,−Δ,0,−Δ), 컨트롤 높이 = H_view/cosφ_layout
        var map = NewTiltedMap();
        double hView = 480, hCtrl = TiltOverscanMath.Height(hView, 20), delta = TiltOverscanMath.Delta(hView, 20);
        map.Height = double.NaN;
        map.Margin = new Thickness(0, -delta, 0, -delta);
        map.Measure(new Size(640, hView));
        map.Arrange(new Rect(0, 0, 640, hView));
        map.UpdateLayout();
        Assert.Equal(hCtrl, map.ActualHeight, 3);         // 음수 마진만큼 커진 컨트롤(오버스캔)
        Assert.Equal(-delta, map.ViewportOffsetY, 9);

        var r = map.GetVisibleInnerRect();                // s=1, φ_layout=φ → 컨트롤이 화면을 정확히 채운다(상단 0 · 하단 H_ctrl)
        Assert.Equal(0.0, r.Top, 6);
        Assert.Equal(hCtrl, r.Bottom, 3);
        Assert.Equal(640, r.Width, 6);

        var center = map.InnerToOuter(new Point(320, hCtrl / 2));
        Assert.Equal(320, center.X, 9);
        Assert.Equal(hView / 2, center.Y, 6);             // 컨트롤 중심 → 뷰포트 중심
        var expected = TiltOverscanMath.InnerToOuter((100, 100), 1.0, 20, 640, map.ActualHeight, delta);
        var actual = map.InnerToOuter(new Point(100, 100));
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
        var back = map.OuterToInner(actual);
        Assert.Equal(100, back.X, 6); Assert.Equal(100, back.Y, 6);
    });

    [Fact]
    public void should_delegate_inner_to_outer_to_tilt_overscan_math_when_digital_zoom_only() => HousingTests.Sta(() =>
    {
        // 회귀(NFR-03): 틸트 OFF + 디지털줌 = 종전 수식(중심 기준 s 배)
        var map = NewMap();
        map.DigitalZoomLevel = 3;                         // 2.0×
        var o = map.InnerToOuter(new Point(420, 340));
        Assert.Equal(320 + 100 * 2, o.X, 9);
        Assert.Equal(240 + 100 * 2, o.Y, 9);
        var i = map.OuterToInner(o);
        Assert.Equal(420, i.X, 9); Assert.Equal(340, i.Y, 9);
    });

    // ───────────────────────────────── FR-08 ③ 각도 스텝 · G3 앵커 · FR-13 Tier0

    [Fact]
    public void should_clamp_requested_angle_when_stepped_beyond_range() => HousingTests.Sta(() =>
    {
        var map = NewTiltedMap(angle: 33);
        map.StepTiltAngle(GMapCustomControl.TILT_STEP_COARSE);
        Assert.Equal(35.0, map.RequestedTiltDeg, 9);      // G1 클램프
        map.StepTiltAngle(-GMapCustomControl.TILT_STEP_FINE);
        Assert.Equal(34.0, map.RequestedTiltDeg, 9);
        map.RequestedTiltDeg = 2;
        map.StepTiltAngle(-GMapCustomControl.TILT_STEP_COARSE);
        Assert.Equal(0.0, map.RequestedTiltDeg, 9);
        map.RequestedTiltDeg = 90;                        // 하드 클램프(코어스)
        Assert.Equal(MapTiltModel.HardMaxAngleDeg, map.RequestedTiltDeg, 9);
        Assert.Equal(5.0, GMapCustomControl.TILT_STEP_COARSE);
        Assert.Equal(1.0, GMapCustomControl.TILT_STEP_FINE);
    });

    [Fact]
    public void should_lock_tilt_when_anchor_is_rotation_locked_and_allow_when_rotation_allowed() => HousingTests.Sta(() =>
    {
        var map = NewTiltedMap();
        var site = new RectLatLng(37.51, 126.99, 0.02, 0.02);
        map.SetAnchorSite(site, allowRotation: false);   // A모드
        Pump();
        Assert.Equal(TiltState.Locked, map.TiltState);
        Assert.Equal(TiltMath.ReasonAnchorLock, map.TiltReason);
        Assert.Equal(0.0, map.TiltDeg);
        map.SetAnchorSite(site, allowRotation: true);    // B모드
        Pump();
        Assert.Equal(TiltState.Active, map.TiltState);
        Assert.Equal(20.0, map.TiltDeg, 9);
        map.SetAnchorSite(null);
        Pump();
        Assert.Equal(TiltState.Active, map.TiltState);
    });

    [Fact]
    public void should_force_flat_when_software_tier_detected() => HousingTests.Sta(() =>
    {
        var map = NewTiltedMap();
        map.SoftwareTierOverride = true;                  // SIM-C008: Tier0(RDP) 강등
        map.ReevaluateTilt("tier");
        Pump();
        Assert.Equal(TiltState.Locked, map.TiltState);
        Assert.Equal(TiltMath.ReasonTier0, map.TiltReason);
        Assert.Equal(0.0, map.TiltDeg);
        AssertIdentity(map);
    });

    [Fact]
    public void should_clamp_phi_to_settings_max_when_settings_replaced() => HousingTests.Sta(() =>
    {
        var map = NewTiltedMap(angle: 30);
        Assert.Equal(30.0, map.TiltDeg, 9);
        map.TiltSettings = new MapTiltModel { MinZoom = 18, HysteresisSteps = 1, MaxAngleDeg = 25 };
        Pump();
        Assert.Equal(25.0, map.TiltDeg, 9);              // G1: clamp(30, 0, 25)
        var st = Assert.IsType<ScaleTransform>(map.RenderTransform);
        Assert.Equal(Math.Cos(25 * Math.PI / 180), st.ScaleY, 9);
    });
}
