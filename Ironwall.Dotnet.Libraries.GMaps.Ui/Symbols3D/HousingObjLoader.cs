using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media.Media3D;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

/// <summary>Optional local OBJ assets. A missing or invalid override always falls back to a shipped/procedural model.</summary>
public static class HousingObjLoader
{
    public static HousingModel? TryLoad(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_')) return null;
        string[] roots ={Utils.Symbol3DFeature.Directory ?? Path.Combine(AppContext.BaseDirectory,"Symbols3D"),
            Path.Combine(AppContext.BaseDirectory,"Resources","Symbols3D")};
        foreach (string root in roots)
        {
            foreach (string candidate in key.StartsWith("camera.", StringComparison.Ordinal) ? new[] { key, "camera" } : new[] { key })
            {
                string path = Path.Combine(root, candidate + ".obj");
                try
                {
                    if (!File.Exists(path)) continue;
                    if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new FormatException("OBJ exceeds 8 MiB");
                    string? json = File.Exists(Path.ChangeExtension(path, ".json")) ? File.ReadAllText(Path.ChangeExtension(path, ".json")) : null;
                    string? mtl = File.Exists(Path.ChangeExtension(path, ".mtl")) ? File.ReadAllText(Path.ChangeExtension(path, ".mtl")) : null;
                    return Parse(File.ReadAllText(path), json, mtl);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException or OverflowException or Newtonsoft.Json.JsonException)
                { Trace.TraceWarning("Symbol3D asset {0}: {1}", candidate, ex.Message); }
            }
        }
        return null;
    }

    public static HousingModel Parse(string text, string? metadata = null, string? mtl = null)
    {
        var vertices = new List<Point3D>(); var builder = new HousingMeshBuilder();
        string material = "mat_body"; int triangles = 0;
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (mtl != null)
        {
            string name = "";
            foreach (string line in mtl.Split('\n'))
            {
                var a = Tokens(line); if (a.Length == 0) continue;
                if (a[0] == "newmtl" && a.Length > 1) name = a[1];
                if (a[0] == "Kd" && a.Length >= 4 && name.Length > 0)
                    colors[name] = $"#{(byte)(Math.Clamp(Number(a[1]), 0, 1) * 255):X2}{(byte)(Math.Clamp(Number(a[2]), 0, 1) * 255):X2}{(byte)(Math.Clamp(Number(a[3]), 0, 1) * 255):X2}";
            }
        }
        var meta = metadata == null ? null : JObject.Parse(metadata);
        var front = Vector(meta?["front"], new(0, 0, 1)); var up = Vector(meta?["up"], new(0, 1, 0));
        front.Normalize(); up.Normalize();
        if (Math.Abs(Vector3D.DotProduct(front, up)) > .001) throw new FormatException("front and up must be perpendicular");
        var right = Vector3D.CrossProduct(up, front); right.Normalize();
        double yaw = meta?["yawOffset"]?.Value<double>() ?? 0;
        if (!double.IsFinite(yaw)) throw new FormatException("Invalid yawOffset");
        Point3D Canonical(Point3D p)
        {
            var v = (Vector3D)p; double x = Vector3D.DotProduct(v, right), y = Vector3D.DotProduct(v, up), z = Vector3D.DotProduct(v, front);
            double a = yaw * Math.PI / 180; return new(x * Math.Cos(a) + z * Math.Sin(a), y, -x * Math.Sin(a) + z * Math.Cos(a));
        }
        foreach (string line in text.Split('\n'))
        {
            var a = Tokens(line); if (a.Length == 0) continue;
            switch (a[0])
            {
                case "v" when a.Length >= 4:
                    vertices.Add(Canonical(new(Number(a[1]), Number(a[2]), Number(a[3]))));
                    if (vertices.Count > 100000) throw new FormatException("Too many vertices"); break;
                case "usemtl" when a.Length > 1:
                    material = a[1].StartsWith("mat_", StringComparison.Ordinal) ? a[1] : colors.GetValueOrDefault(a[1], "mat_body"); break;
                case "f" when a.Length >= 4:
                    if (a.Length > 65) throw new FormatException("Face has too many vertices");
                    Point3D P(string token)
                    {
                        int raw = int.Parse(token.Split('/')[0], CultureInfo.InvariantCulture);
                        int index = raw < 0 ? vertices.Count + raw : raw - 1;
                        if (index < 0 || index >= vertices.Count) throw new FormatException("Face index outside vertex list");
                        return vertices[index];
                    }
                    for (int i = 2; i < a.Length - 1; i++) { builder.Triangle(material, P(a[1]), P(a[i]), P(a[i + 1])); triangles++; }
                    if (triangles > 100000) throw new FormatException("Too many triangles"); break;
            }
        }
        if (triangles == 0) throw new FormatException("OBJ contains no faces");
        var b = builder.Build(new()).Bounds;
        if (b.IsEmpty || !double.IsFinite(b.X + b.Y + b.Z + b.SizeX + b.SizeY + b.SizeZ) || b.SizeX <= 0 || b.SizeY <= 0 || b.SizeZ <= 0) throw new FormatException("OBJ must have finite volume");
        Point3D lens = meta?["lens"] is JArray array ? Canonical((Point3D)Vector(array, new())) : new(b.X + b.SizeX / 2, b.Y + b.SizeY * .6, b.Z + b.SizeZ);
        if (triangles > 500) Trace.TraceWarning("Symbol3D triangle budget exceeded: {0} (target 500)", triangles);
        return builder.Build(lens);
    }
    private static string[] Tokens(string line) => line.Split('#')[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    private static double Number(string value)
    { double n = double.Parse(value, CultureInfo.InvariantCulture); if (!double.IsFinite(n)) throw new FormatException("Non-finite vertex"); return n; }
    private static Vector3D Vector(JToken? token, Vector3D fallback)
    {
        if (token == null) return fallback;
        if (token is not JArray { Count: 3 } a) throw new FormatException("Expected three coordinates");
        var v = new Vector3D(Number(a[0]!.ToString()), Number(a[1]!.ToString()), Number(a[2]!.ToString()));
        if (v.LengthSquared < 1e-16 && fallback.LengthSquared > 0) throw new FormatException("Invalid axis");
        return v;
    }
}
