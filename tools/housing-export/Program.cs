using System.Globalization;
using System.IO;
using IOPath = System.IO.Path;
using System.Text;
using System.Text.Json;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

// 지도 3D 하우징 모형 → OBJ(재질 토큰을 usemtl 로, 관절을 g 로) + 키별 JSON(경계 · 렌즈 · 관절 피벗 · 문 각도) + index.json.
// 좌표는 모형 단위 그대로(+Y 위, +Z 정면). 1 단위 = 200 mm(HousingModels "브로셔 실측 mm 를 200 mm = 1 단위") — 3D 쪽이 0.2 를 곱한다.
var outDir = args.Length > 0 ? args[0] : IOPath.Combine(Environment.CurrentDirectory, "models");
Directory.CreateDirectory(outDir);
string[] keys =
[
    "camera", "camera.dome", "camera.ptz", "sensor", "multi", "smartmulti", "speaker", "controller", "iocontroller",
    "fence", "underground", "contact", "pir", "laser", "radar", "lamp", "enclosure", "fencegate",
];
var inv = CultureInfo.InvariantCulture;
string F(double v) => v.ToString("0.#####", inv);
var index = new List<object>();

foreach (var key in keys)
{
    var model = HousingModels.Get(key);
    var obj = new StringBuilder();
    obj.AppendLine($"# {key} — Ironwall GMaps.Ui Symbols3D (1 unit = 200 mm, +Y up, +Z front)");
    int baseIndex = 1, tris = 0;
    for (int p = 0; p < model.Parts.Count; p++)
    {
        var part = model.Parts[p];
        var mesh = part.Mesh;
        obj.AppendLine($"o part{p}_{part.Joint}");   // 관절(None · Head · DoorLeft · DoorRight)을 이름에 — OBJLoader 는 o 이름을 메시 이름으로 쓴다
        obj.AppendLine($"usemtl {part.Material}");
        foreach (var v in mesh.Positions) obj.AppendLine($"v {F(v.X)} {F(v.Y)} {F(v.Z)}");
        var idx = mesh.TriangleIndices;
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            obj.AppendLine($"f {idx[i] + baseIndex} {idx[i + 1] + baseIndex} {idx[i + 2] + baseIndex}");
            tris++;
        }
        baseIndex += mesh.Positions.Count;
    }
    File.WriteAllText(IOPath.Combine(outDir, key + ".obj"), obj.ToString(), new UTF8Encoding(false));

    var b = model.Bounds;
    var pivots = new Dictionary<string, double[]>();
    foreach (var j in new[] { HousingJoint.Head, HousingJoint.DoorLeft, HousingJoint.DoorRight })
        if (model.Parts.Any(x => x.Joint == j)) { var pv = model.Pivot(j); pivots[j.ToString()] = [pv.X, pv.Y, pv.Z]; }
    var meta = new
    {
        key,
        unit_m = 0.2,
        bounds = new { min = new[] { b.X, b.Y, b.Z }, size = new[] { b.SizeX, b.SizeY, b.SizeZ } },
        lens = new[] { model.Lens.X, model.Lens.Y, model.Lens.Z },
        pivots,
        door_open_angle = model.DoorOpenAngle,
        materials = model.Parts.Select(x => x.Material).Distinct().ToArray(),
        triangles = tris,
    };
    File.WriteAllText(IOPath.Combine(outDir, key + ".json"), JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    index.Add(new { key, height_m = b.SizeY * 0.2, triangles = tris });
    Console.WriteLine($"{key,-14} parts={model.Parts.Count,3} tris={tris,6} size(m)={F(b.SizeX * .2)}×{F(b.SizeY * .2)}×{F(b.SizeZ * .2)}");
}
File.WriteAllText(IOPath.Combine(outDir, "index.json"), JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
Console.WriteLine($"→ {outDir}");
