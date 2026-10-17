using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
/****************************************************************************
   Purpose      : PIDS 그룹 3D 철망(FR-05) — HousingVisual 자매. 자체 Viewport3D + 피치 35° 직교 카메라,
                  scale 1 = 1 지도 픽셀, 루트 ScaleTransform3D(-1,1,1/sin35) 로 지면 평면 항등(FenceMath.Project).
   Created By   : Claude
   Created On   : 2026-09-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 좌표 계약: <see cref="Layout"/> 은 컨트롤 로컬 px 프레임(FencePoint.X = x_px, FencePoint.Z = −y_px)이며 이 컨트롤의 (0,0) 이 그 원점이다.
/// 지면 점 (X, 0, Z) 는 정확히 화면 (X, −Z) 에 놓이고 높이 y 는 y·cos35 만큼 위로 올라간다(2.5D — 지도 타일은 2D, 지형·가림 없음).
/// 메시는 3버킷: 정적(기둥·레일·패널) / 비활성 노드 / 활성 노드(mat_status). ActiveNodes 변경은 노드 버킷만 재생성한다(FR-08).
/// </summary>
public sealed class FenceRunVisual : Grid
{
    private static DependencyProperty Register(string name, Type type, object? value, Action<FenceRunVisual> onChanged) =>
        DependencyProperty.Register(name, type, typeof(FenceRunVisual), new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => onChanged((FenceRunVisual)d)));

    public static readonly DependencyProperty LayoutProperty = Register(nameof(Layout), typeof(FenceLayoutResult), null, v => v.Rebuild(RebuildScope.All));
    public static readonly DependencyProperty ModeProperty = Register(nameof(Mode), typeof(EnumFenceMode), EnumFenceMode.Posts, v => v.Rebuild(RebuildScope.All));
    public static readonly DependencyProperty HeightPxProperty = Register(nameof(HeightPx), typeof(double), 24d, v => v.Rebuild(RebuildScope.All));
    public static readonly DependencyProperty ActiveNodesProperty = Register(nameof(ActiveNodes), typeof(IReadOnlySet<int>), null, v => v.Rebuild(RebuildScope.Nodes));
    public static readonly DependencyProperty StatusBrushProperty = Register(nameof(StatusBrush), typeof(Brush), Frozen(new SolidColorBrush(Color.FromRgb(74, 222, 213))), v => v.Recolor());
    public static readonly DependencyProperty MeshBrushProperty = Register(nameof(MeshBrush), typeof(Brush), Frozen(new SolidColorBrush(Color.FromArgb(96, 200, 212, 224))), v => v.Recolor());
    public static readonly DependencyProperty MetalBrushProperty = Register(nameof(MetalBrush), typeof(Brush), Brushes.LightSlateGray, v => v.Recolor());
    /// <summary>탐지 펄스 0~1 — 활성 버킷 브러시 Opacity 만 바꾼다(재생성·재색 없음, 템플릿 DoubleAnimation 용).</summary>
    public static readonly DependencyProperty StatusPulseProperty = DependencyProperty.Register(nameof(StatusPulse), typeof(double), typeof(FenceRunVisual),
        new PropertyMetadata(1d, (d, _) => ((FenceRunVisual)d).ApplyPulse()));
    /// <summary>
    /// 레이아웃 px → m 환산 계수(C17). <see cref="Layout"/> 은 px 프레임이라 <c>TotalLengthM</c> 이 px 이므로 삼각형 예산(R-01)은 이 값을 곱한 미터로 판정한다.
    /// 기본 1(px = m, 단독 사용·테스트). 재생성 콜백 없음 — 호출자가 Layout 과 함께 세팅하고 다음 BuildStatic 에서 읽는다.
    /// </summary>
    public static readonly DependencyProperty MetersPerPixelProperty = DependencyProperty.Register(nameof(MetersPerPixel), typeof(double), typeof(FenceRunVisual), new PropertyMetadata(1d));
    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }

    public FenceLayoutResult? Layout { get => (FenceLayoutResult?)GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    public double MetersPerPixel { get => (double)GetValue(MetersPerPixelProperty); set => SetValue(MetersPerPixelProperty, value); }
    public EnumFenceMode Mode { get => (EnumFenceMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public double HeightPx { get => (double)GetValue(HeightPxProperty); set => SetValue(HeightPxProperty, value); }
    public IReadOnlySet<int>? ActiveNodes { get => (IReadOnlySet<int>?)GetValue(ActiveNodesProperty); set => SetValue(ActiveNodesProperty, value); }
    public Brush StatusBrush { get => (Brush)GetValue(StatusBrushProperty); set => SetValue(StatusBrushProperty, value); }
    public Brush MeshBrush { get => (Brush)GetValue(MeshBrushProperty); set => SetValue(MeshBrushProperty, value); }
    public Brush MetalBrush { get => (Brush)GetValue(MetalBrushProperty); set => SetValue(MetalBrushProperty, value); }
    public double StatusPulse { get => (double)GetValue(StatusPulseProperty); set => SetValue(StatusPulseProperty, value); }

    private enum RebuildScope { All, Nodes }

    private readonly Viewport3D _viewport = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly ModelVisual3D _visual = new();
    // 직교 카메라라 거리는 클리핑 범위만 정한다. 종전 고정 4096/근평면 1 은 프레임 높이 ≈5.7k px(남북 z20 ≈680 m)부터 남단이 근평면에 잘렸다(C18)
    // → 거리를 프레임에 비례시키고(FenceMath.CameraDistance) 근평면 0.1·원평면 4·D 로 UpdateCamera 에서 함께 갱신한다.
    private readonly OrthographicCamera _camera = new() { NearPlaneDistance = FenceMath.CameraNearPlane, FarPlaneDistance = FenceMath.MinCameraDistance * 4 };
    private readonly Model3DGroup _staticGroup = new(), _nodeGroup = new(), _activeGroup = new();
    private readonly TranslateTransform3D _center = new();
    private readonly Dictionary<string, DiffuseMaterial> _materials = new();
    private SolidColorBrush? _activeBrush;   // StatusBrush 의 비동결 사본 — 펄스는 Opacity 만 만진다
    private bool _budgetDegraded;
    public int RebuildCount { get; private set; }

    public FenceRunVisual()
    {
        IsHitTestVisible = false;
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(112, 125, 145)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(255, 248, 233), new Vector3D(-2, -3, 1)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(146, 203, 255), new Vector3D(2, -1, -2)));
        var objects = new Model3DGroup();
        objects.Children.Add(_staticGroup); objects.Children.Add(_nodeGroup); objects.Children.Add(_activeGroup);
        // 루트: 로컬 px → 중심 기준 → Z 신장(지면 항등, FR-05)
        var root = new Transform3DGroup();
        root.Children.Add(_center);
        root.Children.Add(new ScaleTransform3D(-1, 1, FenceMath.GroundStretch));   // X mirror: camera at -Z looking +Z shows world +X on screen LEFT (right-handed); mirror restores screen_x = X (FenceMirrorTests, user bug 2026-09-08). Meshes are double-sided (BackMaterial) so the winding flip is safe.
        objects.Transform = root;
        scene.Children.Add(objects);
        _visual.Content = scene;
        _viewport.Children.Add(_visual); Children.Add(_viewport);
        PlaceCamera(FenceMath.MinCameraDistance);
        _camera.LookDirection = new Vector3D(0, -FenceMath.SinPitch, FenceMath.CosPitch);
        _camera.UpDirection = new Vector3D(0, FenceMath.CosPitch, FenceMath.SinPitch);
        _viewport.Camera = _camera;
        SetResourceReference(MetalBrushProperty, "TextSecondaryBrush");   // 테마 토큰은 매번 재해석(캐싱 금지)
        SizeChanged += (_, _) => UpdateCamera();
    }

    /// <summary>진단·테스트: 버킷별 파트 수.</summary>
    public (int Static, int Nodes, int Active) PartCounts => (_staticGroup.Children.Count, _nodeGroup.Children.Count, _activeGroup.Children.Count);
    /// <summary>진단·테스트: 전체 삼각형 수.</summary>
    public int TriangleCount => new[] { _staticGroup, _nodeGroup, _activeGroup }.SelectMany(g => g.Children).OfType<GeometryModel3D>()
        .Sum(m => ((MeshGeometry3D)m.Geometry).TriangleIndices.Count / 3);
    /// <summary>진단·테스트: 정적 버킷의 메시 참조(노드 재분할 시 불변이어야 한다).</summary>
    public IReadOnlyList<MeshGeometry3D> StaticMeshes => _staticGroup.Children.OfType<GeometryModel3D>().Select(m => (MeshGeometry3D)m.Geometry).ToList();
    /// <summary>삼각형 예산 초과로 레일을 생략했는가(FR-05 R-01).</summary>
    public bool IsBudgetDegraded => _budgetDegraded;
    /// <summary>진단·테스트: 현재 카메라 거리(px) — <see cref="FenceMath.CameraDistance"/> 로 프레임 높이에 비례(C18).</summary>
    public double CameraDistancePx { get; private set; }

    private void PlaceCamera(double distance)
    {
        CameraDistancePx = distance;
        _camera.Position = new Point3D(0, distance * FenceMath.SinPitch, -distance * FenceMath.CosPitch);
        _camera.FarPlaneDistance = distance * 4;   // 최대 깊이 D + 0.714·H ≤ 4D (H ≤ 4.2·D)
    }

    private void UpdateCamera()
    {
        double w = ActualWidth, h = ActualHeight;
        if (!(w > 0) || !(h > 0)) return;
        // 로컬 px (X, −y) → 중심 기준: x = X − W/2, z = Z + H/2  ⇒  화면 = FenceMath.Project(x, y, z, W/2, H/2) = (X, y_px − y·cos35)
        // (X mirror in root: camera at -Z looking +Z puts world +X on screen left; ScaleX=-1 makes screen_x = X hold for bent lines — FenceMirrorTests)
        _center.OffsetX = -w / 2; _center.OffsetZ = h / 2;
        _camera.Width = w;
        double distance = FenceMath.CameraDistance(h, HeightPx);
        if (distance != CameraDistancePx) PlaceCamera(distance);   // 정사영 — 거리는 클리핑 범위 외 화면 결과에 영향 없음
    }

    private void Rebuild(RebuildScope scope)
    {
        var layout = Layout;
        if (scope == RebuildScope.All)
        {
            _staticGroup.Children.Clear();
            if (layout is not null && layout.Panels.Count > 0) BuildStatic(layout);
        }
        _nodeGroup.Children.Clear(); _activeGroup.Children.Clear();
        if (layout is not null && Mode == EnumFenceMode.SensorMount) BuildNodes(layout);
        RebuildCount++;
        Recolor(); ApplyPulse(); UpdateCamera();
    }

    private double PostWidth => Math.Max(1.5, HeightPx * .06);

    private void BuildStatic(FenceLayoutResult layout)
    {
        double h = Math.Max(1, HeightPx), w = PostWidth, rail = Math.Max(.8, h * .025);
        // C17: Layout 은 px 프레임이라 TotalLengthM 이 px — m 로 환산해 넘겨야 R-01 레일 생략이 살아난다(px 그대로면 Full3D 에서 수학적으로 초과 불가).
        double mpp = MetersPerPixel; if (!(mpp > 0) || !double.IsFinite(mpp)) mpp = 1;
        _budgetDegraded = FenceMath.ExceedsBudget(layout.Panels.Count, Mode == EnumFenceMode.SensorMount, layout.TotalLengthM * mpp);
        var m = new HousingMeshBuilder();
        foreach (var post in layout.Posts)
        {
            double pw = post.IsThick ? w * 1.5 : w;
            m.Box(post.IsThick ? "mat_trim" : "mat_metal", post.Position.X, 0, post.Position.Z, pw, h * 1.02, pw);
        }
        foreach (var panel in layout.Panels)
        {
            var a = new Point3D(panel.A.X, 0, panel.A.Z); var b = new Point3D(panel.B.X, 0, panel.B.Z);
            if (panel.LengthM <= 1e-6) continue;
            if (!_budgetDegraded)
            {
                m.Tube("mat_metal", a + new Vector3D(0, h * .08, 0), b + new Vector3D(0, h * .08, 0), rail, rail, 4, false);
                m.Tube("mat_metal", a + new Vector3D(0, h * .92, 0), b + new Vector3D(0, h * .92, 0), rail, rail, 4, false);
            }
            // 반투명 철망 패널 — 단일 쿼드, BackMaterial 로 양면
            m.Quad("mat_mesh", a + new Vector3D(0, h * .05, 0), b + new Vector3D(0, h * .05, 0), b + new Vector3D(0, h * .95, 0), a + new Vector3D(0, h * .95, 0));
        }
        foreach (var part in m.Build(default).Parts) _staticGroup.Children.Add(MakeModel(part.Mesh, part.Material));
    }

    private void BuildNodes(FenceLayoutResult layout)
    {
        double h = Math.Max(1, HeightPx), s = Math.Max(2, h * .14);
        var active = ActiveNodes;
        var normal = new HousingMeshBuilder(); var lit = new HousingMeshBuilder();
        foreach (var node in layout.Nodes)
        {
            bool on = active is not null && active.Contains(node.GlobalIndex);
            (on ? lit : normal).Box(on ? "mat_status" : "mat_body", node.Position.X, h * .5 - s / 2, node.Position.Z, s, s, s * .6);
        }
        foreach (var part in normal.Build(default).Parts) _nodeGroup.Children.Add(MakeModel(part.Mesh, part.Material));
        foreach (var part in lit.Build(default).Parts) _activeGroup.Children.Add(MakeModel(part.Mesh, part.Material));
    }

    private GeometryModel3D MakeModel(MeshGeometry3D mesh, string token)
    {
        if (!_materials.TryGetValue(token, out var diffuse)) _materials[token] = diffuse = new DiffuseMaterial(Brushes.Silver);
        var material = new MaterialGroup(); material.Children.Add(diffuse);
        if (token is "mat_metal" or "mat_trim") material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(90, 235, 248, 255)), 24));
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private void Recolor()
    {
        foreach (var (token, material) in _materials)
        {
            material.Brush = token switch
            {
                "mat_metal" => MetalBrush,
                "mat_trim" => new SolidColorBrush(Color.FromRgb(35, 47, 60)),
                "mat_mesh" => MeshBrush,
                "mat_body" => new SolidColorBrush(Color.FromRgb(205, 214, 224)),
                "mat_status" => ActiveBrush(),
                _ => Brushes.Silver,
            };
        }
    }

    private SolidColorBrush ActiveBrush()
    {
        var source = StatusBrush as SolidColorBrush;
        _activeBrush = source is null ? new SolidColorBrush(Color.FromRgb(74, 222, 213)) : (SolidColorBrush)source.CloneCurrentValue();
        _activeBrush.Opacity = PulseOpacity(StatusPulse);
        return _activeBrush;
    }

    private static double PulseOpacity(double pulse) => double.IsFinite(pulse) ? .45 + .55 * Math.Clamp(pulse, 0, 1) : 1;

    private void ApplyPulse()
    {
        if (_activeBrush is { IsFrozen: false }) _activeBrush.Opacity = PulseOpacity(StatusPulse);
    }
}
