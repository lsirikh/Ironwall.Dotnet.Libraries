using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/****************************************************************************
   Purpose      : 심볼 상세 창 3D 프리뷰 스테이지 — PRD symbol-detail-and-door-control FR-16/17
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 상세 창 좌측 스테이지의 3D 심볼 프리뷰. <b>자체 오빗 카메라</b>로 45°씩 자동 회전하고,
/// 드래그로 자유 회전하며 놓으면 1.5초 뒤 자동 회전을 재개한다.
///
/// <para><b>지도 심볼(<see cref="HousingVisual"/>)과 분리한 이유</b>: 지도 위 하우징은 피치 35° 고정 카메라로
/// 2D 라인·철망과 지면 정합이 걸려 있어 각도를 바꿀 수 없다. 여기서는 정합 제약이 없으므로
/// 별도 <see cref="Viewport3D"/> 에 <see cref="OrbitCameraMath"/> 카메라를 둔다 — 지도 규약은 건드리지 않는다.</para>
///
/// <para><b>공유하는 것</b>: 메시(<see cref="HousingModels"/> 캐시)와 재질 색표(<see cref="HousingPalette"/>).
/// 형상·색이 갈라지면 "지도와 상세 창의 같은 장비가 다르게 보이는" 상태가 된다.</para>
///
/// <para><b>드래그 규약</b>(`.claude/rules/common/drag-first-ux.md`): <c>PreviewMouseDown</c> 선점 →
/// 데드존 8 DIU 통과 후 진행 → 단일 <c>FinishDrag</c> 를 <c>MouseUp</c>·<c>LostMouseCapture</c> 양쪽에서 호출.
/// 맵 컨트롤의 자식이 아니라 오버레이 창 안이므로 좌드래그가 맵 팬과 다투지 않는다.</para>
/// </summary>
public sealed class SymbolPreview3DControl : Grid
{
    #region - Dependency Properties -

    /// <summary>하우징 모델 키(<see cref="HousingModels.DeviceKey"/>). 비면 아무것도 그리지 않는다.</summary>
    public static readonly DependencyProperty ModelKeyProperty = DependencyProperty.Register(
        nameof(ModelKey), typeof(string), typeof(SymbolPreview3DControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((SymbolPreview3DControl)d).Rebuild()));

    /// <summary>본체 착색 색(심볼 채우기 색).</summary>
    public static readonly DependencyProperty BodyBrushProperty = DependencyProperty.Register(
        nameof(BodyBrush), typeof(Brush), typeof(SymbolPreview3DControl),
        new FrameworkPropertyMetadata(Brushes.SlateGray, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((SymbolPreview3DControl)d).Recolor()));

    /// <summary>가이드·바닥 링 색.</summary>
    public static readonly DependencyProperty GuideBrushProperty = DependencyProperty.Register(
        nameof(GuideBrush), typeof(Brush), typeof(SymbolPreview3DControl),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>문 열림 진행도 0~1 — 통문·함체. 상세 창은 실제 문 상태를 그대로 보여준다(FR-16).</summary>
    public static readonly DependencyProperty DoorOpenProperty = DependencyProperty.Register(
        nameof(DoorOpen), typeof(double), typeof(SymbolPreview3DControl),
        new PropertyMetadata(0d, (d, _) => ((SymbolPreview3DControl)d).ApplyDoorAngles()));

    /// <summary>자동 회전 on/off — `⏸ 회전 멈춤` 토글이 쓴다.</summary>
    public static readonly DependencyProperty IsAutoRotatingProperty = DependencyProperty.Register(
        nameof(IsAutoRotating), typeof(bool), typeof(SymbolPreview3DControl),
        new PropertyMetadata(true, (d, _) => ((SymbolPreview3DControl)d).SyncTimer()));

    /// <summary>현재 방위각(도) — 테스트·진단용으로 읽고 쓸 수 있다.</summary>
    public static readonly DependencyProperty YawDegProperty = DependencyProperty.Register(
        nameof(YawDeg), typeof(double), typeof(SymbolPreview3DControl),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((SymbolPreview3DControl)d).ApplyCamera()));

    /// <summary>현재 올려본각(도).</summary>
    public static readonly DependencyProperty PitchDegProperty = DependencyProperty.Register(
        nameof(PitchDeg), typeof(double), typeof(SymbolPreview3DControl),
        new FrameworkPropertyMetadata(OrbitCameraMath.DefaultPitchDeg, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((SymbolPreview3DControl)d).ApplyCamera()));

    public string? ModelKey { get => (string?)GetValue(ModelKeyProperty); set => SetValue(ModelKeyProperty, value); }
    public Brush BodyBrush { get => (Brush)GetValue(BodyBrushProperty); set => SetValue(BodyBrushProperty, value); }
    public Brush GuideBrush { get => (Brush)GetValue(GuideBrushProperty); set => SetValue(GuideBrushProperty, value); }
    public double DoorOpen { get => (double)GetValue(DoorOpenProperty); set => SetValue(DoorOpenProperty, value); }
    public bool IsAutoRotating { get => (bool)GetValue(IsAutoRotatingProperty); set => SetValue(IsAutoRotatingProperty, value); }
    public double YawDeg { get => (double)GetValue(YawDegProperty); set => SetValue(YawDegProperty, value); }
    public double PitchDeg { get => (double)GetValue(PitchDegProperty); set => SetValue(PitchDegProperty, value); }

    #endregion

    #region - Fields -

    /// <summary>타이머 주기(초) — 45° 한 칸(2초)보다 촘촘해야 재개 유예 1.5초를 잴 수 있다.</summary>
    private const double TickSeconds = 0.25;

    private readonly Viewport3D _viewport = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly ModelVisual3D _visual = new();
    private readonly OrthographicCamera _camera = new() { NearPlaneDistance = 0.01, FarPlaneDistance = 1000 };
    private readonly AxisAngleRotation3D _doorL = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _doorR = new(new Vector3D(0, 1, 0), 0);
    private readonly Dictionary<string, DiffuseMaterial> _materials = new();
    private readonly DispatcherTimer _timer;

    private HousingModel? _model;
    private double _extent = 1;          // 모델 최대 치수(월드) — 카메라 거리·폭 계산 기준
    private double _autoBaseYaw;         // 자동 회전의 기준 각(드래그로 돌린 위치에서 이어 돈다)
    private double _autoElapsed;         // 기준 각 이후 누적 시간(초)
    private double _resumeCountdown;     // 놓은 뒤 재개까지 남은 시간(초)

    // 캡처 드래그 상태 — _pressed(눌림)와 _dragging(데드존 통과)을 분리해 데드존 미만은 클릭으로 흘린다.
    private bool _pressed, _dragging;
    private Point _pressPoint, _lastPoint;
    private double _dragStartYaw, _dragStartPitch;

    #endregion

    public SymbolPreview3DControl()
    {
        Background = Brushes.Transparent;      // 히트 테스트 대상이 되어야 드래그가 시작된다
        ClipToBounds = true;
        _viewport.Children.Add(_visual);
        Children.Add(_viewport);

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(TickSeconds) };
        _timer.Tick += OnTick;

        SizeChanged += (_, _) => ApplyCamera();
        Loaded += (_, _) => { if (_model is null) Rebuild(); SyncTimer(); };
        // 창이 닫히면 타이머가 남아 지도 프레임을 갉아먹지 않도록 반드시 멈춘다(NFR-01).
        Unloaded += (_, _) => Stop();
        // 창을 닫는 방식이 Visibility 토글이라 Unloaded 가 오지 않는다 — 보이는지로 직접 판단해야
        // ① 숨은 동안 타이머가 계속 돌거나 ② 다시 열었을 때 영영 안 도는 두 오류를 모두 피한다.
        // 2D 심볼도 프리뷰가 Collapsed 라 같은 경로로 자동 정지한다.
        IsVisibleChanged += (_, _) => { if (IsVisible) SyncTimer(); else Stop(); };

        PreviewMouseDown += OnPreviewMouseDown;      // 실제로 터널링하는 이벤트에서 선점
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += (_, _) => FinishDrag();
        LostMouseCapture += (_, _) => FinishDrag();
    }

    /// <summary>타이머·캡처를 모두 놓는다. 창을 닫을 때 호출된다(IMPL-C8).</summary>
    public void Stop()
    {
        _timer.Stop();
        if (IsMouseCaptured) ReleaseMouseCapture();
        _pressed = _dragging = false;
    }

    /// <summary>정면(yaw 0 · 기본 피치)으로 되돌리고 자동 회전을 처음부터 다시 시작한다 — `⟲ 정면`.</summary>
    public void ResetToFront()
    {
        var (yaw, pitch) = OrbitCameraMath.Front();
        _autoBaseYaw = yaw;
        _autoElapsed = 0;
        _resumeCountdown = 0;
        YawDeg = yaw;
        PitchDeg = pitch;
    }

    #region - Scene -

    private void Rebuild()
    {
        _materials.Clear();
        if (string.IsNullOrWhiteSpace(ModelKey))
        {
            _visual.Content = null;
            _model = null;
            InvalidateVisual();
            return;
        }

        _model = HousingModels.Get(ModelKey!);
        var objects = new Model3DGroup();
        foreach (var part in _model.Parts)
        {
            if (!_materials.TryGetValue(part.Material, out var diffuse))
                _materials[part.Material] = diffuse = new DiffuseMaterial(Brushes.Silver);
            var material = new MaterialGroup();
            material.Children.Add(diffuse);
            if (part.Material is "mat_glass" or "mat_metal" or "mat_body")
                material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(110, 235, 248, 255)), part.Material == "mat_glass" ? 80 : 28));
            var geometry = new GeometryModel3D(part.Mesh, material) { BackMaterial = material };
            geometry.Transform = part.Joint switch
            {
                HousingJoint.DoorLeft => new RotateTransform3D(_doorL, _model.Pivot(HousingJoint.DoorLeft)),
                HousingJoint.DoorRight => new RotateTransform3D(_doorR, _model.Pivot(HousingJoint.DoorRight)),
                _ => Transform3D.Identity,
            };
            objects.Children.Add(geometry);
        }

        // 모델 중심을 원점으로 — 오빗 카메라가 원점을 바라보므로 회전축이 물체 한가운데를 지난다.
        var bounds = _model.Bounds;
        objects.Transform = new TranslateTransform3D(
            -(bounds.X + bounds.SizeX / 2), -(bounds.Y + bounds.SizeY / 2), -(bounds.Z + bounds.SizeZ / 2));
        _extent = Math.Max(0.2, Math.Max(bounds.SizeY, Math.Sqrt(bounds.SizeX * bounds.SizeX + bounds.SizeZ * bounds.SizeZ)));

        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(112, 125, 145)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(255, 248, 233), new Vector3D(-2, -3, 1)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(146, 203, 255), new Vector3D(2, -1, -2)));
        scene.Children.Add(objects);
        _visual.Content = scene;

        Recolor();
        ApplyDoorAngles();
        ApplyCamera();
    }

    private void Recolor()
    {
        if (_materials.Count == 0) return;
        Color tint = BodyBrush is SolidColorBrush solid ? solid.Color : Colors.SlateGray;
        double strength = HousingAppearance.GetTintStrength(this);
        var metal = (Brush?)TryFindResource("TextSecondaryBrush") ?? Brushes.LightSlateGray;
        var status = (Brush?)TryFindResource("StatusNormalBrush") ?? Brushes.MediumTurquoise;
        var mesh = new SolidColorBrush(Color.FromArgb(96, 200, 212, 224));
        foreach (var (token, material) in _materials)
            material.Brush = HousingPalette.Resolve(token, tint, strength, metal, status, mesh, metal);
    }

    private void ApplyDoorAngles()
    {
        double angle = _model?.DoorOpenAngle ?? 0;
        _doorL.Angle = HousingMath.DoorAngle(HousingJoint.DoorLeft, DoorOpen, angle);
        _doorR.Angle = HousingMath.DoorAngle(HousingJoint.DoorRight, DoorOpen, angle);
    }

    private void ApplyCamera()
    {
        if (_model is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        double distance = _extent * 4;
        var (x, y, z) = OrbitCameraMath.CameraPosition(YawDeg, PitchDeg, distance);
        _camera.Position = new Point3D(x, y, z);
        _camera.LookDirection = new Vector3D(-x, -y, -z);
        _camera.UpDirection = new Vector3D(0, 1, 0);   // 피치가 ±80° 로 묶여 있어 시선과 겹치지 않는다
        // 세로가 긴 스테이지에서도 잘리지 않도록 세로 가시 범위가 모델보다 크게 폭을 잡는다.
        _camera.Width = _extent * 1.35 * Math.Max(1, ActualWidth / ActualHeight);
        _viewport.Camera = _camera;
        InvalidateVisual();
    }

    #endregion

    #region - Auto rotation -

    private void SyncTimer()
    {
        if (IsAutoRotating && IsLoaded && _model is not null) _timer.Start();
        else _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_dragging) return;

        if (_resumeCountdown > 0)
        {
            // 놓자마자 홱 돌아가면 방금 맞춘 각을 볼 시간이 없다 — 1.5초 유예.
            _resumeCountdown -= TickSeconds;
            return;
        }

        _autoElapsed += TickSeconds;
        double yaw = OrbitCameraMath.Normalize360(_autoBaseYaw + OrbitCameraMath.AutoYawDeg(_autoElapsed));
        if (Math.Abs(yaw - YawDeg) > 1e-6) YawDeg = yaw;   // 45° 칸이 바뀔 때만 실제 갱신(초당 4회 재투영 방지)
    }

    #endregion

    #region - Drag (캡처 드래그, 데드존 8 DIU) -

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _model is null) return;
        _pressed = true;
        _dragging = false;
        _pressPoint = _lastPoint = e.GetPosition(this);
        _dragStartYaw = YawDeg;
        _dragStartPitch = PitchDeg;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        var current = e.GetPosition(this);

        if (!_dragging)
        {
            double dx = current.X - _pressPoint.X, dy = current.Y - _pressPoint.Y;
            if (dx * dx + dy * dy < DragDeadZone * DragDeadZone) return;   // 미세 떨림은 클릭으로 흘린다
            _dragging = true;
            _timer.Stop();
            Cursor = Cursors.SizeAll;
            InvalidateVisual();                                            // 점선 구 가이드 등장
        }

        // 화면을 오른쪽으로 끌면 물체가 오른쪽으로 도는 감각 → 카메라는 반대로 돈다(부호 반전).
        var (yaw, pitch) = OrbitCameraMath.ApplyDrag(_dragStartYaw, _dragStartPitch,
            -(current.X - _pressPoint.X), current.Y - _pressPoint.Y);
        YawDeg = yaw;
        PitchDeg = pitch;
        _lastPoint = current;
    }

    /// <summary>드래그 종료 단일 경로 — 플래그 → 시각 → 캡처 해제 → 재개 예약 순서(캡처를 먼저 풀면 재진입한다).</summary>
    private void FinishDrag()
    {
        if (!_pressed) return;
        bool wasDragging = _dragging;
        _pressed = _dragging = false;
        ClearValue(CursorProperty);
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (!wasDragging) return;

        InvalidateVisual();                                   // 가이드 소거
        _autoBaseYaw = YawDeg;                                // 놓은 자리에서 이어 돈다
        _autoElapsed = 0;
        _resumeCountdown = OrbitCameraMath.ResumeDelaySeconds;
        SyncTimer();
    }

    /// <summary>드래그 판정 데드존(DIU) — 사내 고정값. 새 상수를 만들지 않는다.</summary>
    private const double DragDeadZone = 8.0;

    #endregion

    #region - Guide (점선 구) -

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!_dragging || _model is null) return;

        // 잡고 도는 동안만 "지금 어느 축으로 도는지"를 점선 구(球)로 보여준다 —
        // 구 윤곽 + 적도(수평 대원) + 자오선 2개. 정사영에서 대원은 타원이라 수식이 정확하다.
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double radius = Math.Max(24, Math.Min(ActualWidth, ActualHeight) * 0.40);
        double pitch = PitchDeg * Math.PI / 180.0;

        var pen = new Pen(GuideBrush, 1.0)
        {
            DashStyle = new DashStyle(new double[] { 1.5, 2.5 }, 0),
            DashCap = PenLineCap.Round,
        };
        dc.PushOpacity(0.55);
        dc.DrawEllipse(null, pen, center, radius, radius);                                  // 구 윤곽
        dc.DrawEllipse(null, pen, center, radius, radius * Math.Abs(Math.Sin(pitch)));      // 적도
        dc.DrawEllipse(null, pen, center, radius, radius * Math.Abs(Math.Cos(pitch)));      // 화면과 나란한 자오선
        dc.DrawLine(pen, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));   // 정면 자오선(옆에서 보면 직선)
        dc.Pop();
    }

    #endregion
}
