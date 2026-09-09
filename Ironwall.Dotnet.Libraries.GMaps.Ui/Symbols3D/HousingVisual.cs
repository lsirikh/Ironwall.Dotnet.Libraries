using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

/// <summary>One orthographic scene with cached geometry and fixed studio lighting. Yaw never rotates the WPF hit box.</summary>
/// <remarks>
/// 지도 카드 틸트(ScaleY=cosφ)를 조상 RenderTransform 으로 승계(V-02 헤드리스 확정 2026-09-08 — RTB 소프트웨어 경로 50→43 px @0.85) —
/// 링:지도지면 상대비 0.574(=sin35) 불변, 철망은 2D 폴리라인과 같은 Canvas 라 정합 유지. 피치 35° 는 <see cref="HousingMath.Pitch"/> 고정이며
/// 틸트 역보정을 하지 않는다(map-tilt-25d PRD FR-14 정책 (i)). Housing.Tests 가 <c>FenceMath.PitchDeg</c> 와의 동일성을 단언한다.
/// </remarks>
public sealed class HousingVisual : Grid
{
    private static DependencyProperty Register(string name, Type type, object value, bool rebuild = false) =>
        DependencyProperty.Register(name, type, typeof(HousingVisual), new FrameworkPropertyMetadata(value,
            FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((HousingVisual)d).Refresh(rebuild, type == typeof(Brush))));
    public static readonly DependencyProperty ModelKeyProperty = Register(nameof(ModelKey), typeof(string), "camera", true);
    public static readonly DependencyProperty YawProperty = Register(nameof(Yaw), typeof(double), 0d);
    public static readonly DependencyProperty HeadYawProperty = Register(nameof(HeadYaw), typeof(double), 0d);
    public static readonly DependencyProperty BodyBrushProperty = Register(nameof(BodyBrush), typeof(Brush), Brushes.SlateGray);
    public static readonly DependencyProperty RingBrushProperty = Register(nameof(RingBrush), typeof(Brush), Brushes.DeepSkyBlue);
    public static readonly DependencyProperty RingThicknessProperty = Register(nameof(RingThickness), typeof(double), 1.5d);
    public static readonly DependencyProperty RoofBrushProperty = Register(nameof(RoofBrush), typeof(Brush), Brushes.SlateGray);
    public static readonly DependencyProperty MetalBrushProperty = Register(nameof(MetalBrush), typeof(Brush), Brushes.LightSlateGray);
    public static readonly DependencyProperty ElevationProperty = Register(nameof(Elevation), typeof(double), 0d);
    public static readonly DependencyProperty FootprintProperty = Register(nameof(Footprint), typeof(double), 100d);
    public static readonly DependencyProperty FloorCountProperty = Register(nameof(FloorCount), typeof(int), 1, true);
    public static readonly DependencyProperty BasementCountProperty = Register(nameof(BasementCount), typeof(int), 0);
    public static readonly DependencyProperty LockedProperty = Register(nameof(Locked), typeof(bool), false);
    public static readonly DependencyProperty HighlightedProperty = Register(nameof(Highlighted), typeof(bool), false);
    /// <summary>이벤트 상태색(FR-08) — 통문·함체 LED(mat_status). 템플릿 트리거 Setter 로만 바뀐다.</summary>
    public static readonly DependencyProperty StatusBrushProperty = Register(nameof(StatusBrush), typeof(Brush), Frozen(new SolidColorBrush(Color.FromRgb(74, 222, 213))));
    /// <summary>반투명 철망 패널(mat_mesh).</summary>
    public static readonly DependencyProperty MeshBrushProperty = Register(nameof(MeshBrush), typeof(Brush), Frozen(new SolidColorBrush(Color.FromArgb(96, 200, 212, 224))));
    private static Brush Frozen(SolidColorBrush brush) { brush.Freeze(); return brush; }   // DP 기본값은 Frozen 이어야 한다(스레드 귀속 예외)
    /// <summary>
    /// 개폐 진행도 0(닫힘)~1(열림) — 경량 콜백(FR-12): 문 관절 각도만 대입하고 재생성·재투영·ProjectionChanged 를 일으키지 않는다.
    /// 템플릿 DoubleAnimation(400ms) 이 매 프레임 쓰는 값이라 Refresh 경로를 타면 안 된다.
    /// </summary>
    public static readonly DependencyProperty DoorOpenProperty = DependencyProperty.Register(nameof(DoorOpen), typeof(double), typeof(HousingVisual),
        new PropertyMetadata(0d, (d, _) => ((HousingVisual)d).ApplyDoorAngles()));
    public string ModelKey { get => (string)GetValue(ModelKeyProperty); set => SetValue(ModelKeyProperty, value); }
    public double Yaw { get => (double)GetValue(YawProperty); set => SetValue(YawProperty, value); }
    public double HeadYaw { get => (double)GetValue(HeadYawProperty); set => SetValue(HeadYawProperty, value); }
    public Brush BodyBrush { get => (Brush)GetValue(BodyBrushProperty); set => SetValue(BodyBrushProperty, value); }
    public Brush RingBrush { get => (Brush)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public Brush RoofBrush { get => (Brush)GetValue(RoofBrushProperty); set => SetValue(RoofBrushProperty, value); }
    public Brush MetalBrush { get => (Brush)GetValue(MetalBrushProperty); set => SetValue(MetalBrushProperty, value); }
    public double RingThickness { get => (double)GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }
    public double Elevation { get => (double)GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }
    public double Footprint { get => (double)GetValue(FootprintProperty); set => SetValue(FootprintProperty, value); }
    public int FloorCount { get => (int)GetValue(FloorCountProperty); set => SetValue(FloorCountProperty, value); }
    public int BasementCount { get => (int)GetValue(BasementCountProperty); set => SetValue(BasementCountProperty, value); }
    public bool Locked { get => (bool)GetValue(LockedProperty); set => SetValue(LockedProperty, value); }
    public bool Highlighted { get => (bool)GetValue(HighlightedProperty); set => SetValue(HighlightedProperty, value); }
    public Brush StatusBrush { get => (Brush)GetValue(StatusBrushProperty); set => SetValue(StatusBrushProperty, value); }
    public Brush MeshBrush { get => (Brush)GetValue(MeshBrushProperty); set => SetValue(MeshBrushProperty, value); }
    public double DoorOpen { get => (double)GetValue(DoorOpenProperty); set => SetValue(DoorOpenProperty, value); }
    /// <summary>현재 문짝 각도(좌, 우) — 진단·테스트용.</summary>
    public (double Left, double Right) DoorAngles => (_doorL.Angle, _doorR.Angle);

    private readonly Viewport3D _viewport = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly ModelVisual3D _visual = new();
    private readonly OrthographicCamera _camera = new() { NearPlaneDistance = .1, FarPlaneDistance = 100 };
    private readonly AxisAngleRotation3D _yaw = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _head = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _doorL = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D _doorR = new(new Vector3D(0, 1, 0), 0);
    private readonly TranslateTransform3D _lift = new();
    private readonly ScaleTransform3D _footprint = new(1, 1, 1);   // D-8: 건축면적 → XZ 스케일(회전 앞, 중심 기준)
    private double _footprintScale = 1;
    private readonly Dictionary<string, DiffuseMaterial> _materials = new();
    private HousingModel? _model;
    private double _scale, _height, _elevation;
    private double _centerX, _centerZ;
    public event EventHandler? ProjectionChanged;
    public Point LensPoint
    {
        get
        {
            if (_model == null) return new(ActualWidth / 2, ActualHeight / 2);
            var lens = _model.Lens;
            double r = (_model.Parts.Any(p => p.IsHead) ? HousingMath.Normalize(HeadYaw) : 0) * Math.PI / 180;
            var pivot = _model.HeadPivot;
            double x = lens.X - pivot.X, z = lens.Z - pivot.Z;
            return Project(new(pivot.X + x * Math.Cos(r) + z * Math.Sin(r), lens.Y - _model.Bounds.Y + _elevation,
                pivot.Z - x * Math.Sin(r) + z * Math.Cos(r)), Yaw);
        }
    }

    public HousingVisual()
    {
        IsHitTestVisible = false;
        _viewport.Children.Add(_visual); Children.Add(_viewport);
        // A theme change updates the material without rebuilding or reparsing the frozen mesh.
        SetResourceReference(MetalBrushProperty, "TextSecondaryBrush");
        SizeChanged += (_, _) => Refresh(false);
        Loaded += (_, _) => Refresh(_model == null);
    }

    private Point Project(Point3D point, double yaw = 0)
    {
        var p = HousingMath.Project((point.X - _centerX) * _footprintScale, point.Y, (point.Z - _centerZ) * _footprintScale, yaw, _scale, ActualWidth, ActualHeight, _height);
        return new(p.x, p.y);
    }

    private void Refresh(bool rebuild, bool recolor = false)
    {
        if (_visual == null) return;
        if (string.IsNullOrWhiteSpace(ModelKey)) { _visual.Content = null; _model = null; return; }
        if (rebuild || _model == null)
        {
            recolor = true;
            _model = HousingModels.Get(ModelKey, FloorCount);
            _materials.Clear();
            var objects = new Model3DGroup();
            foreach (var part in _model.Parts)
            {
                if (!_materials.TryGetValue(part.Material, out var diffuse))
                    _materials[part.Material] = diffuse = new DiffuseMaterial(Brushes.Silver);
                var material = new MaterialGroup(); material.Children.Add(diffuse);
                if (part.Material is "mat_glass" or "mat_metal" or "mat_body")
                    material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(110, 235, 248, 255)), part.Material == "mat_glass" ? 80 : 28));
                var model = new GeometryModel3D(part.Mesh, material) { BackMaterial = material };
                switch (part.Joint)
                {
                    case HousingJoint.Head: model.Transform = new RotateTransform3D(_head, _model.HeadPivot); break;
                    case HousingJoint.DoorLeft: model.Transform = new RotateTransform3D(_doorL, _model.Pivot(HousingJoint.DoorLeft)); break;
                    case HousingJoint.DoorRight: model.Transform = new RotateTransform3D(_doorR, _model.Pivot(HousingJoint.DoorRight)); break;
                }
                objects.Children.Add(model);
            }
            var transform = new Transform3DGroup();
            _centerX = _model.Bounds.X + _model.Bounds.SizeX / 2;
            _centerZ = _model.Bounds.Z + _model.Bounds.SizeZ / 2;
            transform.Children.Add(new TranslateTransform3D(-_centerX, -_model.Bounds.Y, -_centerZ));
            transform.Children.Add(_footprint);
            transform.Children.Add(new RotateTransform3D(_yaw)); transform.Children.Add(_lift);
            // X mirror (outermost): the camera sits at -Z looking +Z, so world +X lands on screen LEFT in a right-handed frame.
            // Without it the enclosure/gate doors swing to the wrong side and yaw runs counter-clockwise against the 2D symbols
            // (HousingMirrorTests, same root cause as FenceRunVisual). Meshes are double-sided (BackMaterial) so the winding flip is safe.
            transform.Children.Add(new ScaleTransform3D(-1, 1, 1));
            objects.Transform = transform;
            var scene = new Model3DGroup();
            scene.Children.Add(new AmbientLight(Color.FromRgb(112, 125, 145)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(255, 248, 233), new Vector3D(-2, -3, 1)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(146, 203, 255), new Vector3D(2, -1, -2)));
            scene.Children.Add(objects); _visual.Content = scene;
        }
        _yaw.Angle = HousingMath.Normalize(Yaw); _head.Angle = HousingMath.Normalize(HeadYaw);
        ApplyDoorAngles();
        _elevation = double.IsFinite(Elevation) ? Math.Clamp(Elevation, 0, 100) * .008 : 0;
        _lift.OffsetY = _elevation;
        var bounds = _model!.Bounds;
        _height = Math.Max(.1, bounds.SizeY + _elevation);
        // 건축면적(㎡) → 바닥면 배율. 100㎡ = 1.0. Fit 에도 넣어 박스 안에 머물게 한다(D-8).
        _footprintScale = double.IsFinite(Footprint) ? Math.Clamp(Math.Sqrt(Math.Max(1, Footprint) / 100), .7, 1.6) : 1;
        _footprint.ScaleX = _footprint.ScaleZ = _footprintScale;
        _scale = HousingMath.Fit(ActualWidth, ActualHeight, Math.Max(.1, bounds.SizeX) * _footprintScale, _height, Math.Max(.1, bounds.SizeZ) * _footprintScale);
        if (_scale > 0)
        {
            _camera.Position = new Point3D(0, _height / 2 + 10 * HousingMath.SinPitch, -10 * HousingMath.CosPitch);
            _camera.LookDirection = new Vector3D(0, -HousingMath.SinPitch, HousingMath.CosPitch);
            _camera.UpDirection = new Vector3D(0, HousingMath.CosPitch, HousingMath.SinPitch);
            _camera.Width = ActualWidth / _scale;
            _viewport.Camera = _camera;
        }
        if (recolor) foreach (var (token, material) in _materials)
        {
            Color tint = BodyBrush is SolidColorBrush solid ? solid.Color : Colors.SlateGray;
            double strength = HousingAppearance.GetTintStrength(this);
            // 색 결정은 HousingPalette 단일 정본 — 상세 창 3D 프리뷰가 같은 표를 쓴다(같은 장비=같은 색).
            material.Brush = HousingPalette.Resolve(token, tint, strength, MetalBrush, StatusBrush, MeshBrush, RoofBrush);
        }
        InvalidateVisual(); ProjectionChanged?.Invoke(this, EventArgs.Empty);
    }
    internal void RefreshMaterials() => Refresh(false, true);

    /// <summary>DoorOpen → 문 관절 각도 대입만(재생성·재투영 없음). 문 관절이 없는 모델은 0 을 유지한다.</summary>
    private void ApplyDoorAngles()
    {
        double angle = _model?.DoorOpenAngle ?? 0;
        _doorL.Angle = HousingMath.DoorAngle(HousingJoint.DoorLeft, DoorOpen, angle);
        _doorR.Angle = HousingMath.DoorAngle(HousingJoint.DoorRight, DoorOpen, angle);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_model == null || _scale <= 0) return;
        var ground = Project(new(_centerX, 0, _centerZ));
        // 접지 링은 마커 박스(Width/Height)에 비례한다 — 크기를 바꾸면 링도 같이 바뀌어야 한다(사용자 결정 2026-09-07).
        // 건축면적 배율(D-8)은 모델에만 적용하고 링은 박스를 따른다.
        double rx = ActualWidth * .46;
        double ry = Math.Min(ActualHeight * .22, rx * HousingMath.SinPitch);
        var shadow = new RadialGradientBrush(Color.FromArgb(100, 5, 15, 28), Colors.Transparent);
        dc.DrawEllipse(shadow, null, ground, rx * 1.12, ry * 1.4);
        var pen = new Pen(RingBrush, double.IsFinite(RingThickness) ? Math.Clamp(RingThickness, 0, 8) : 1.5);
        if (Locked) pen.DashStyle = DashStyles.Dash;
        dc.PushOpacity(Highlighted ? 1 : .7); dc.DrawEllipse(null, pen, ground, rx, ry); dc.Pop();
        if (Highlighted) { dc.PushOpacity(.22); dc.DrawEllipse(null, new Pen(RingBrush, 4), ground, rx + 2, ry + 2); dc.Pop(); }
        if (BasementCount > 0)
        {
            // 지하층 수만큼 점선 링을 아래로 쌓는다(최대 4). 종전엔 값과 무관하게 1개라 B1↔B5 가 구분되지 않았다(D-7).
            var p = new Pen(RingBrush, 1) { DashStyle = DashStyles.Dash };
            int rings = Math.Min(BasementCount, 4);
            for (int i = 0; i < rings; i++)
            {
                double k = .86 - i * .1;
                dc.PushOpacity(.4 - i * .07); dc.DrawEllipse(null, p, new(ground.X, ground.Y + 3 + i * 2.5), rx * k, ry * k * .93); dc.Pop();
            }
        }
        if (_elevation > 0) dc.DrawLine(new Pen(MetalBrush, Math.Max(1, _scale * .035)), ground, Project(new(_centerX, _elevation, _centerZ)));
        if (Locked)
        {
            var c = new Point(ground.X + rx * .7, ground.Y);
            dc.DrawRoundedRectangle(RingBrush, null, new Rect(c.X - 2.5, c.Y, 5, 4), 1, 1);
            dc.DrawEllipse(null, new Pen(RingBrush, 1), new(c.X, c.Y), 1.6, 2);
        }
    }
}
