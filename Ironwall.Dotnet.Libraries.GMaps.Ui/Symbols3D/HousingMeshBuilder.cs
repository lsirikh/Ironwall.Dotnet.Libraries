using System.Windows.Media.Media3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

/// <summary>
/// 관절(FR-11): None=본체, Head=PTZ 렌즈 모듈(HeadYaw), DoorLeft/DoorRight=문짝(DoorOpen 0~1 → HousingMath.DoorAngle).
/// 관절마다 피벗이 하나이며 모두 Y(수직)축 회전이다.
/// </summary>
public enum HousingJoint { None, Head, DoorLeft, DoorRight }

public sealed record HousingPart(MeshGeometry3D Mesh, string Material, HousingJoint Joint = HousingJoint.None)
{
    /// <summary>하위 호환 — PTZ 헤드 여부.</summary>
    public bool IsHead => Joint == HousingJoint.Head;
}

/// <param name="Pivots">관절별 피벗(Head 는 HeadPivot 과 동일 값). 문 관절이 없으면 비어 있다.</param>
/// <param name="DoorOpenAngle">DoorOpen=1 일 때 좌측 문짝 각도(도, 음수=+Z 전면으로 열림). 우측 문짝은 거울(−).</param>
public sealed record HousingModel(IReadOnlyList<HousingPart> Parts, Point3D Lens, Rect3D Bounds, Point3D HeadPivot = default,
    IReadOnlyDictionary<HousingJoint, Point3D>? Pivots = null, double DoorOpenAngle = 0)
{
    public bool HasDoor => Parts.Any(p => p.Joint is HousingJoint.DoorLeft or HousingJoint.DoorRight);
    public Point3D Pivot(HousingJoint joint)
        => joint == HousingJoint.Head ? HeadPivot : Pivots is not null && Pivots.TryGetValue(joint, out var p) ? p : default;
}

/// <summary>Small indexed meshes, shared frozen across markers. No images or external rendering packages.</summary>
internal sealed class HousingMeshBuilder
{
    private readonly Dictionary<(string material, HousingJoint joint), MeshGeometry3D> _meshes = new();
    private readonly Dictionary<HousingJoint, Point3D> _pivots = new();
    /// <summary>이후 추가되는 삼각형이 속할 관절.</summary>
    public HousingJoint Joint { get; set; }
    /// <summary>하위 호환 — PTZ 헤드 on/off.</summary>
    public bool Head { get => Joint == HousingJoint.Head; set => Joint = value ? HousingJoint.Head : HousingJoint.None; }
    public Point3D HeadPivot { get => _pivots.GetValueOrDefault(HousingJoint.Head); set => _pivots[HousingJoint.Head] = value; }
    public void SetPivot(HousingJoint joint, Point3D pivot) => _pivots[joint] = pivot;
    /// <summary>DoorOpen=1 에서의 좌측 문짝 각도(도). 0 이면 문 관절이 없는 모델.</summary>
    public double DoorOpenAngle { get; set; }
    /// <summary>
    /// 문 관절의 열림 스윕(0→DoorOpenAngle)을 bounds 에 포함할지. 통문처럼 열린 문짝도 박스 안에 머물러야 하면 true,
    /// 함체처럼 닫힌 크기를 보존해야 하면 false(R-06 — 뷰포트는 ClipToBounds=false 라 열린 문은 박스 밖으로 그려진다).
    /// </summary>
    public bool IncludeDoorSweepInBounds { get; set; } = true;

    public void Triangle(string material, Point3D a, Point3D b, Point3D c)
    {
        if (!_meshes.TryGetValue((material, Joint), out var mesh))
            _meshes[(material, Joint)] = mesh = new MeshGeometry3D();
        var normal = Vector3D.CrossProduct(b - a, c - a);
        if (normal.LengthSquared < 1e-16) return;
        normal.Normalize();
        int n = mesh.Positions.Count;
        mesh.Positions.Add(a); mesh.Positions.Add(b); mesh.Positions.Add(c);
        mesh.Normals.Add(normal); mesh.Normals.Add(normal); mesh.Normals.Add(normal);
        mesh.TriangleIndices.Add(n); mesh.TriangleIndices.Add(n + 1); mesh.TriangleIndices.Add(n + 2);
    }

    /// <summary>꼭짓점마다 법선을 따로 주는 삼각형 — 곡면(원통 옆면·반구)을 면 분할 없이 매끈하게 비춘다.</summary>
    public void Triangle(string material, Point3D a, Point3D b, Point3D c, Vector3D na, Vector3D nb, Vector3D nc)
    {
        if (Vector3D.CrossProduct(b - a, c - a).LengthSquared < 1e-16) return;
        if (!_meshes.TryGetValue((material, Joint), out var mesh))
            _meshes[(material, Joint)] = mesh = new MeshGeometry3D();
        na.Normalize(); nb.Normalize(); nc.Normalize();
        int n = mesh.Positions.Count;
        mesh.Positions.Add(a); mesh.Positions.Add(b); mesh.Positions.Add(c);
        mesh.Normals.Add(na); mesh.Normals.Add(nb); mesh.Normals.Add(nc);
        mesh.TriangleIndices.Add(n); mesh.TriangleIndices.Add(n + 1); mesh.TriangleIndices.Add(n + 2);
    }

    public void Quad(string mat, Point3D a, Point3D b, Point3D c, Point3D d)
    { Triangle(mat, a, b, c); Triangle(mat, a, c, d); }

    /// <summary>
    /// 감긴 방향과 무관하게 <paramref name="inside"/> 반대쪽(바깥)을 향하는 삼각형. 윤곽을 좌표로 찍어 만드는 각기둥처럼
    /// 감긴 방향을 손으로 맞추기 어려운 면에 쓴다 — 법선이 안쪽을 보면 앞면이 역광으로 어둡게 칠해진다.
    /// </summary>
    public void TriangleOut(string mat, Point3D a, Point3D b, Point3D c, Point3D inside)
    {
        var normal = Vector3D.CrossProduct(b - a, c - a);
        var centroid = new Point3D((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3, (a.Z + b.Z + c.Z) / 3);
        if (Vector3D.DotProduct(normal, centroid - inside) < 0) Triangle(mat, a, c, b); else Triangle(mat, a, b, c);
    }

    /// <summary>
    /// YZ 평면의 볼록 윤곽을 X 방향으로 <paramref name="x0"/>~<paramref name="x1"/> 만큼 밀어낸 각기둥.
    /// 사선 차양·쐐기형 케이스처럼 축 정렬 상자로는 못 만드는 옆모습에 쓴다.
    /// </summary>
    public void Prism(string mat, double x0, double x1, IReadOnlyList<(double Y, double Z)> outline)
    {
        int n = outline.Count;
        double cy = outline.Average(p => p.Y), cz = outline.Average(p => p.Z);
        var inside = new Point3D((x0 + x1) / 2, cy, cz);
        Point3D P(double x, int i) => new(x, outline[i % n].Y, outline[i % n].Z);
        for (int i = 0; i < n; i++)
        {
            TriangleOut(mat, P(x0, i), P(x0, i + 1), P(x1, i + 1), inside);
            TriangleOut(mat, P(x0, i), P(x1, i + 1), P(x1, i), inside);
        }
        for (int i = 1; i < n - 1; i++)
        {
            TriangleOut(mat, P(x0, 0), P(x0, i), P(x0, i + 1), inside);
            TriangleOut(mat, P(x1, 0), P(x1, i), P(x1, i + 1), inside);
        }
    }

    public void Box(string mat, double x, double y, double z, double w, double h, double d)
    {
        var p = new Point3D[] {
            new(x-w/2,y,z-d/2), new(x+w/2,y,z-d/2), new(x+w/2,y+h,z-d/2), new(x-w/2,y+h,z-d/2),
            new(x-w/2,y,z+d/2), new(x+w/2,y,z+d/2), new(x+w/2,y+h,z+d/2), new(x-w/2,y+h,z+d/2) };
        Quad(mat, p[0], p[3], p[2], p[1]); Quad(mat, p[4], p[5], p[6], p[7]);
        Quad(mat, p[0], p[4], p[7], p[3]); Quad(mat, p[1], p[2], p[6], p[5]);
        Quad(mat, p[3], p[7], p[6], p[2]); Quad(mat, p[0], p[1], p[5], p[4]);
    }

    public void BeveledBox(string mat, double x, double y, double z, double w, double h, double d, double bevel = .035)
    {
        double b = Math.Min(bevel, Math.Min(w, Math.Min(h, d)) * .2);
        var outline = new (double x, double z)[] { (-w / 2 + b, -d / 2), (w / 2 - b, -d / 2), (w / 2, -d / 2 + b), (w / 2, d / 2 - b), (w / 2 - b, d / 2), (-w / 2 + b, d / 2), (-w / 2, d / 2 - b), (-w / 2, -d / 2 + b) };
        for (int i = 0; i < 8; i++)
        {
            var a = outline[i]; var c = outline[(i + 1) % 8];
            var p = new Point3D(x + a.x, y, z + a.z); var q = new Point3D(x + c.x, y, z + c.z);
            Quad(mat, q, p, p + new Vector3D(0, h, 0), q + new Vector3D(0, h, 0));
            Triangle(mat, new(x, y + h, z), new(x + c.x, y + h, z + c.z), new(x + a.x, y + h, z + a.z));
            Triangle(mat, new(x, y, z), p, q);
        }
    }

    /// <param name="smooth">옆면 법선을 원통 반지름 방향으로 준다(면 분할이 안 보이는 매끈한 원통). 바깥에서 보는 면에만 쓴다 —
    /// 스피커 나팔처럼 안쪽을 들여다보는 면에 켜면 빛이 반대로 든다.</param>
    public void Tube(string mat, Point3D a, Point3D b, double r1, double r2, int sides = 12, bool caps = true, bool smooth = false)
    {
        var axis = b - a; double length = axis.Length; axis.Normalize();
        var u = Vector3D.CrossProduct(axis, Math.Abs(axis.Y) < .9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0)); u.Normalize();
        var v = Vector3D.CrossProduct(axis, u);
        for (int i = 0; i < sides; i++)
        {
            double t = i * 2 * Math.PI / sides, t2 = (i + 1) * 2 * Math.PI / sides;
            var n1 = Math.Cos(t) * u + Math.Sin(t) * v; var n2 = Math.Cos(t2) * u + Math.Sin(t2) * v;
            var p1 = a + r1 * n1; var p2 = a + r1 * n2; var p3 = b + r2 * n2; var p4 = b + r2 * n1;
            if (smooth)
            {
                // 원뿔대 옆면 법선 = 반지름 방향·길이 + 축 방향·(r1−r2) — 넓어지는 쪽으로 기운다.
                var s1 = n1 * length + axis * (r1 - r2); var s2 = n2 * length + axis * (r1 - r2);
                Triangle(mat, p1, p2, p3, s1, s2, s2); Triangle(mat, p1, p3, p4, s1, s2, s1);
            }
            else Quad(mat, p1, p2, p3, p4);
            if (caps) { Triangle(mat, a, p2, p1); Triangle(mat, b, p4, p3); }
        }
    }

    /// <summary>아래로 드리운 반구(돔 카메라 유리). 구의 법선을 그대로 써서 매끈하게 비춘다.</summary>
    public void Dome(string mat, double x, double y, double z, double radius)
        => Hemisphere(mat, new Point3D(x, y, z), new Vector3D(0, -1, 0), radius, 16, 5);

    /// <summary><paramref name="axis"/> 쪽으로 볼록한 반구 — 적외선(PIR) 렌즈 돔, 볼 헤드 등.</summary>
    public void Hemisphere(string mat, Point3D center, Vector3D axis, double radius, int sides = 14, int rings = 4)
    {
        axis.Normalize();
        var u = Vector3D.CrossProduct(axis, Math.Abs(axis.Y) < .9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0)); u.Normalize();
        var v = Vector3D.CrossProduct(axis, u);
        Vector3D N(double t, double p) => Math.Cos(p) * (Math.Cos(t) * u + Math.Sin(t) * v) + Math.Sin(p) * axis;
        // 극점의 퇴화 삼각형은 Triangle 이 건너뛰므로 "몇 개 추가했는가"가 아니라 시작 위치로 범위를 잡는다.
        int start = _meshes.TryGetValue((mat, Joint), out var existing) ? existing.Positions.Count : 0;
        for (int j = 0; j < rings; j++) for (int i = 0; i < sides; i++)
        {
            double t = i * 2 * Math.PI / sides, t2 = (i + 1) * 2 * Math.PI / sides, p = j * Math.PI / 2 / rings, p2 = (j + 1) * Math.PI / 2 / rings;
            Vector3D a = N(t, p), b = N(t2, p), c = N(t2, p2), d = N(t, p2);
            TriangleOut(mat, center + radius * a, center + radius * b, center + radius * c, center);
            TriangleOut(mat, center + radius * a, center + radius * c, center + radius * d, center);
        }
        SmoothFrom(mat, center, start);
    }

    /// <summary><paramref name="start"/> 이후 추가된 꼭짓점의 법선을 <paramref name="center"/> 에서 뻗는 구 법선으로 바꾼다.</summary>
    private void SmoothFrom(string mat, Point3D center, int start)
    {
        if (!_meshes.TryGetValue((mat, Joint), out var mesh)) return;
        for (int i = start; i < mesh.Positions.Count; i++)
        {
            var n = mesh.Positions[i] - center;
            if (n.LengthSquared > 1e-16) { n.Normalize(); mesh.Normals[i] = n; }
        }
    }

    public HousingModel Build(Point3D lens)
    {
        var parts = new List<HousingPart>(); var bounds = Rect3D.Empty;
        foreach (var (key, mesh) in _meshes)
        {
            mesh.Freeze(); bounds.Union(mesh.Bounds); parts.Add(new(mesh, key.material, key.joint));
            // Keep every articulated head angle inside the same normalization box.
            if (key.joint == HousingJoint.Head)
                foreach (var point in mesh.Positions)
                {
                    double dx = point.X - HeadPivot.X, dz = point.Z - HeadPivot.Z;
                    double radius = Math.Sqrt(dx * dx + dz * dz);
                    bounds.Union(new Point3D(HeadPivot.X - radius, point.Y, HeadPivot.Z - radius));
                    bounds.Union(new Point3D(HeadPivot.X + radius, point.Y, HeadPivot.Z + radius));
                }
            // 문 관절은 전체 원이 아니라 실제 열림 범위(0→DoorOpenAngle, 5단계)만 넣는다 — 박스가 불필요하게 커지지 않도록.
            else if (key.joint is HousingJoint.DoorLeft or HousingJoint.DoorRight && IncludeDoorSweepInBounds && DoorOpenAngle != 0)
            {
                var pivot = _pivots.GetValueOrDefault(key.joint);
                for (int step = 1; step <= 4; step++)
                {
                    var rotation = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), HousingMath.DoorAngle(key.joint, step / 4.0, DoorOpenAngle)), pivot);
                    foreach (var point in mesh.Positions) bounds.Union(rotation.Transform(point));
                }
            }
        }
        return new(parts.AsReadOnly(), lens, bounds, HeadPivot, new Dictionary<HousingJoint, Point3D>(_pivots), DoorOpenAngle);
    }
}
