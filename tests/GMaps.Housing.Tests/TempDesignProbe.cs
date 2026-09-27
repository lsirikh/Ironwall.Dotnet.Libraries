using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Xunit;

namespace GMaps.Housing.Tests;

/// TEMP design probe. Renders contact sheets to compare the current 3D look
/// against candidate improvements. Delete after the design decision is made.
public class TempDesignProbe
{
    private const int Floors = 3;
    private static readonly Color Bg = Color.FromRgb(15, 24, 37);

    private static string? Out => Environment.GetEnvironmentVariable("SYMBOL3D_ARTIFACTS");

    private static readonly string[] Keys =
    {
        "camera", "camera.dome", "camera.ptz", "controller", "iocontroller", "multi",
        "smartmulti", "pir", "radar", "speaker", "lamp", "enclosure", "fencegate", "fence"
    };

    private static string[] Sample()
    {
        var infra = SymbolPaletteItem.All.Where(i => i.ModelKey?.StartsWith("infra.") == true)
                                         .Select(i => i.ModelKey!).Take(3).ToArray();
        return Keys.Concat(infra).ToArray();
    }

    private static string Label(string key)
        => SymbolPaletteItem.All.FirstOrDefault(i => i.ModelKey == key)?.Title ?? key;

    // ---- scene surgery (no library change) --------------------------------
    private static Model3DGroup Scene(HousingVisual v)
        => (Model3DGroup)((ModelVisual3D)((Viewport3D)v.Children[0]).Children[0]).Content;

    private static Model3DGroup Objects(HousingVisual v)
        => (Model3DGroup)Scene(v).Children.Last();

    private static Color Family(string key) =>
          key.StartsWith("camera") ? Color.FromRgb(126, 168, 205)
        : key is "controller" or "iocontroller" ? Color.FromRgb(206, 163, 99)
        : key is "speaker" or "lamp" ? Color.FromRgb(176, 139, 201)
        : key is "enclosure" or "fencegate" ? Color.FromRgb(143, 160, 176)
        : key.StartsWith("infra.") ? Color.FromRgb(172, 166, 156)
        : Color.FromRgb(120, 181, 154);

    private static void Relight(HousingVisual v)
    {
        var scene = Scene(v);
        scene.Children[0] = new AmbientLight(Color.FromRgb(54, 62, 76));
        scene.Children[1] = new DirectionalLight(Color.FromRgb(255, 251, 240), new Vector3D(-2.0, -3.4, 1.1));
        scene.Children[2] = new DirectionalLight(Color.FromRgb(104, 146, 202), new Vector3D(2.2, -0.6, -1.6));
        scene.Children.Insert(3, new DirectionalLight(Color.FromRgb(46, 58, 78), new Vector3D(0, 1, 0.25)));
    }

    private static void Recolor(HousingVisual v, string key, bool family)
    {
        var model = HousingModels.Get(key, Floors);
        var objects = Objects(v);
        for (int i = 0; i < objects.Children.Count && i < model.Parts.Count; i++)
        {
            if (objects.Children[i] is not GeometryModel3D g || g.Material is not MaterialGroup group) continue;
            string token = model.Parts[i].Material;
            var diffuse = group.Children.OfType<DiffuseMaterial>().FirstOrDefault();
            if (family && diffuse != null)
            {
                var c = Family(key);
                diffuse.Brush = token switch
                {
                    "mat_body" => new SolidColorBrush(c),
                    "mat_roof" => new SolidColorBrush(Color.FromRgb((byte)(c.R * .55), (byte)(c.G * .5), (byte)(c.B * .5))),
                    "mat_trim" => new SolidColorBrush(Color.FromRgb(26, 34, 45)),
                    _ => diffuse.Brush
                };
            }
            if (token is "mat_led" or "mat_status")
                group.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(74, 222, 213))));
        }
    }

    private static MeshGeometry3D Smooth(MeshGeometry3D src, double minDot)
    {
        var p = src.Positions;
        int faces = p.Count / 3;
        var fn = new Vector3D[faces];
        for (int t = 0; t < faces; t++)
        {
            var n = Vector3D.CrossProduct(p[3 * t + 1] - p[3 * t], p[3 * t + 2] - p[3 * t]);
            if (n.LengthSquared > 1e-18) n.Normalize();
            fn[t] = n;
        }
        static (long, long, long) K(Point3D q) => ((long)Math.Round(q.X * 10000), (long)Math.Round(q.Y * 10000), (long)Math.Round(q.Z * 10000));
        var map = new Dictionary<(long, long, long), List<int>>();
        for (int t = 0; t < faces; t++)
            for (int k = 0; k < 3; k++)
            {
                var key = K(p[3 * t + k]);
                if (!map.TryGetValue(key, out var list)) map[key] = list = new List<int>();
                list.Add(t);
            }
        var mesh = new MeshGeometry3D();
        for (int t = 0; t < faces; t++)
            for (int k = 0; k < 3; k++)
            {
                var point = p[3 * t + k];
                var acc = new Vector3D();
                foreach (int f in map[K(point)])
                    if (Vector3D.DotProduct(fn[f], fn[t]) >= minDot) acc += fn[f];
                if (acc.LengthSquared > 1e-18) acc.Normalize(); else acc = fn[t];
                mesh.Positions.Add(point); mesh.Normals.Add(acc);
                mesh.TriangleIndices.Add(mesh.Positions.Count - 1);
            }
        mesh.Freeze();
        return mesh;
    }

    private static void SmoothAll(HousingVisual v)
    {
        var objects = Objects(v);
        for (int i = 0; i < objects.Children.Count; i++)
            if (objects.Children[i] is GeometryModel3D g && g.Geometry is MeshGeometry3D m)
                g.Geometry = Smooth(m, 0.62);
    }

    private static HousingVisual Build(string key, double size, int variant)
    {
        var v = new HousingVisual
        {
            Width = size, Height = size * .92, ModelKey = key, Yaw = 145,
            BodyBrush = Brushes.SlateGray, RingBrush = Brushes.Turquoise, FloorCount = Floors
        };
        HousingTests.Layout(v, size, size * .92);
        if (variant >= 1) Relight(v);
        if (variant >= 1) Recolor(v, key, family: variant >= 2);
        if (variant >= 3) SmoothAll(v);
        return v;
    }

    // ---- sheet C: what the map actually shows today -----------------------
    private static readonly Color MapDefault = Color.FromRgb(33, 150, 243);   // EnumColorType.Blue #2196F3

    /// Re-blend mat_body over a darker base so the family colour carries on a dark map.
    private static void DeepenBody(HousingVisual v, string key, Color baseColor, double strength)
    {
        var model = HousingModels.Get(key, Floors);
        var objects = Objects(v);
        var tint = Family(key);
        var blended = new SolidColorBrush(Color.FromRgb(
            (byte)(baseColor.R * (1 - strength) + tint.R * strength),
            (byte)(baseColor.G * (1 - strength) + tint.G * strength),
            (byte)(baseColor.B * (1 - strength) + tint.B * strength)));
        for (int i = 0; i < objects.Children.Count && i < model.Parts.Count; i++)
            if (model.Parts[i].Material == "mat_body" && objects.Children[i] is GeometryModel3D g
                && g.Material is MaterialGroup group)
                foreach (var d in group.Children.OfType<DiffuseMaterial>()) d.Brush = blended;
    }

    private static HousingVisual BuildMap(string key, double size, int variant)
    {
        var body = variant >= 2 ? Family(key) : MapDefault;
        var v = new HousingVisual
        {
            Width = size, Height = size * .92, ModelKey = key, Yaw = 145,
            BodyBrush = new SolidColorBrush(body), RingBrush = Brushes.Turquoise, FloorCount = Floors
        };
        HousingTests.Layout(v, size, size * .92);
        if (variant >= 1) { Relight(v); Recolor(v, key, family: false); }
        if (variant >= 3) { DeepenBody(v, key, Color.FromRgb(148, 156, 167), .55); SmoothAll(v); }
        return v;
    }

    [Fact]
    public void should_render_real_map_default_sheet() => HousingTests.Sta(() =>
    {
        if (Out == null) return;
        var keys = Sample();
        string[] variants =
        {
            "0 map default (all #2196F3, tint .35)",
            "1 + lighting + LED glow",
            "2 + per-type default colour",
            "3 + darker body base + smooth",
        };
        var grid = new Grid { Background = new SolidColorBrush(Bg) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        foreach (var _ in keys) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        foreach (var _ in variants) { grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(128) }); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) }); }
        for (int c = 0; c < keys.Length; c++)
        {
            var t = Text(Label(keys[c]), 11, true); Grid.SetRow(t, 0); Grid.SetColumn(t, c + 1); grid.Children.Add(t);
        }
        for (int r = 0; r < variants.Length; r++)
        {
            var t = Text(variants[r], 12); Grid.SetRow(t, 1 + r * 2); Grid.SetColumn(t, 0); grid.Children.Add(t);
            var t2 = Text("at 30px", 10, true); Grid.SetRow(t2, 2 + r * 2); Grid.SetColumn(t2, 0); grid.Children.Add(t2);
            for (int c = 0; c < keys.Length; c++)
            {
                var big = new Border { Child = BuildMap(keys[c], 112, r), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(big, 1 + r * 2); Grid.SetColumn(big, c + 1); grid.Children.Add(big);
                var small = new Border { Child = BuildMap(keys[c], 30, r), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(small, 2 + r * 2); Grid.SetColumn(small, c + 1); grid.Children.Add(small);
            }
        }
        Save(grid, 190 + keys.Length * 120, 26 + variants.Length * 174, "C-map-default.png");
    });

    private static void Save(FrameworkElement root, int w, int h, string name)
    {
        HousingTests.Layout(root, w, h);
        Directory.CreateDirectory(Out!);
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(root);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(Path.Combine(Out!, name)); enc.Save(f);
    }

    private static TextBlock Text(string s, double size = 11, bool dim = false) => new()
    {
        Text = s, FontSize = size,
        Foreground = new SolidColorBrush(dim ? Color.FromRgb(140, 155, 175) : Colors.White),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center
    };

    [Fact]
    public void should_render_size_reality_and_variant_sheets() => HousingTests.Sta(() =>
    {
        if (Out == null) return;
        var keys = Sample();
        double[] sizes = { 30, 44, 72, 136 };

        // Sheet A - the same icons at map size vs gallery size.
        var gridA = new Grid { Background = new SolidColorBrush(Bg) };
        gridA.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
        foreach (var _ in keys) gridA.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        gridA.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        foreach (var _ in sizes) gridA.RowDefinitions.Add(new RowDefinition { Height = new GridLength(156) });
        for (int c = 0; c < keys.Length; c++)
        {
            var t = Text(Label(keys[c]), 11, true); Grid.SetRow(t, 0); Grid.SetColumn(t, c + 1); gridA.Children.Add(t);
        }
        for (int r = 0; r < sizes.Length; r++)
        {
            var t = Text($"{sizes[r]:0}px", 12, true); Grid.SetRow(t, r + 1); Grid.SetColumn(t, 0); gridA.Children.Add(t);
            for (int c = 0; c < keys.Length; c++)
            {
                var host = new Border { Child = Build(keys[c], sizes[r], 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(host, r + 1); Grid.SetColumn(host, c + 1); gridA.Children.Add(host);
            }
        }
        Save(gridA, 74 + keys.Length * 150, 26 + sizes.Length * 156, "A-size-reality.png");

        // Sheet B - candidate variants at a readable size and at map size.
        string[] variants = { "0 current", "1 lighting", "2 +family color", "3 +smooth normals" };
        var gridB = new Grid { Background = new SolidColorBrush(Bg) };
        gridB.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        foreach (var _ in keys) gridB.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        gridB.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        foreach (var _ in variants) { gridB.RowDefinitions.Add(new RowDefinition { Height = new GridLength(128) }); gridB.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) }); }
        for (int c = 0; c < keys.Length; c++)
        {
            var t = Text(Label(keys[c]), 11, true); Grid.SetRow(t, 0); Grid.SetColumn(t, c + 1); gridB.Children.Add(t);
        }
        for (int r = 0; r < variants.Length; r++)
        {
            var t = Text(variants[r], 12); Grid.SetRow(t, 1 + r * 2); Grid.SetColumn(t, 0); gridB.Children.Add(t);
            var t2 = Text("at 30px", 10, true); Grid.SetRow(t2, 2 + r * 2); Grid.SetColumn(t2, 0); gridB.Children.Add(t2);
            for (int c = 0; c < keys.Length; c++)
            {
                var big = new Border { Child = Build(keys[c], 112, r), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(big, 1 + r * 2); Grid.SetColumn(big, c + 1); gridB.Children.Add(big);
                var small = new Border { Child = Build(keys[c], 30, r), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(small, 2 + r * 2); Grid.SetColumn(small, c + 1); gridB.Children.Add(small);
            }
        }
        Save(gridB, 120 + keys.Length * 120, 26 + variants.Length * 174, "B-variants.png");
    });
}
