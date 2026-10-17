using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 지도 3D 규약 v2 — 다리의 픽셀(아이콘 PNG · 이미지 PNG 4096 맞추기)과 새 마커 종류 DTO.
/// 창을 띄우지 않는다: 부모 없는 비주얼만 STA 에서 배치 · 렌더한다.
/// </summary>
public class Map3DOverlayPixelTests
{
    private static BitmapFrame Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        return BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
    }

    private static byte[] Pixel(BitmapSource bitmap, int x, int y)
    {
        var px = new byte[4];
        new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return px;   // B, G, R, A
    }

    // ── 아이콘 ──

    [Fact]
    public void should_render_icon_at_double_scale_without_root_rotation_when_shape_rotated() => HousingTests.Sta(() =>
    {
        var shape = new Rectangle { Width = 40, Height = 10, Fill = Brushes.Red, RenderTransform = new RotateTransform(90) };
        HousingTests.Layout(shape, 40, 10);

        var frame = Decode(Map3DOverlayBridge.RenderIconPng(shape)!);

        Assert.Equal(80, frame.PixelWidth);
        Assert.Equal(20, frame.PixelHeight);
        var right = Pixel(frame, 75, 10);   // 회전이 들어갔다면 비어 있을 자리
        Assert.Equal(255, right[3]);
        Assert.Equal(255, right[2]);
        Assert.Equal(0, right[1]);
    });

    [Fact]
    public void should_keep_only_marker_box_when_drawing_overflows() => HousingTests.Sta(() =>
    {
        var box = new System.Windows.Controls.Canvas { Width = 20, Height = 20, Background = Brushes.Blue };
        var overflow = new Rectangle { Width = 200, Height = 200, Fill = Brushes.Lime };   // 부채꼴처럼 칸 밖으로 넘친 그림
        System.Windows.Controls.Canvas.SetLeft(overflow, 10);
        System.Windows.Controls.Canvas.SetTop(overflow, 10);
        box.Children.Add(overflow);
        HousingTests.Layout(box, 20, 20);

        var frame = Decode(Map3DOverlayBridge.RenderIconPng(box)!);

        Assert.Equal(40, frame.PixelWidth);
        Assert.Equal(40, frame.PixelHeight);
        Assert.Equal(255, Pixel(frame, 5, 5)[0]);    // 왼쪽 위 = 파랑(칸 배경) — 넘친 그림 때문에 줄어들지 않는다
        Assert.Equal(255, Pixel(frame, 35, 35)[1]);  // 오른쪽 아래 = 초록(칸 안에 든 부분)
    });

    [Fact]
    public void should_return_null_icon_when_shape_has_no_size_or_is_collapsed() => HousingTests.Sta(() =>
    {
        var empty = new Rectangle();
        HousingTests.Layout(empty, 0, 0);
        var hidden = new Rectangle { Width = 10, Height = 10, Fill = Brushes.Red, Visibility = Visibility.Collapsed };
        HousingTests.Layout(hidden, 10, 10);

        Assert.Null(Map3DOverlayBridge.RenderIconPng(empty));
        Assert.Null(Map3DOverlayBridge.RenderIconPng(hidden));
    });

    // ── 이미지 ──

    [Theory]
    [InlineData(5000, 100, 4096, 82)]
    [InlineData(200, 100, 200, 100)]
    public void should_fit_image_png_to_4096_when_encoding_overlay_image(int w, int h, int ew, int eh) => HousingTests.Sta(() =>
    {
        var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, new byte[w * h * 4], w * 4);

        var frame = Decode(Map3DOverlayBridge.EncodeImagePng(source));

        Assert.Equal(ew, frame.PixelWidth);
        Assert.Equal(eh, frame.PixelHeight);
    });

    // ── 새 마커 종류 DTO ──

    [Fact]
    public void should_build_polygon_with_style_when_line_is_closed() => HousingTests.Sta(() =>
    {
        var model = new LineSymbolModel
        {
            Id = 41, Title = "경계 다각형", IsClosedPath = true, LineOpacity = 0.5, LinePattern = EnumLinePattern.Dashed,
            ShowArrowHead = false, FillColor = EnumColorType.Red, StrokeColor = EnumColorType.Blue, StrokeThickness = 3,
            LinePoints = new List<GeoPoint> { new(37.0, 127.0), new(37.1, 127.0), new(37.1, 127.1) },
        };
        var marker = new GMapLineMarker(Mock.Of<ILogService>(), model);

        var item = Map3DOverlayBridge.BuildItem(marker)!;

        Assert.Equal("polygon", Map3DOverlayBridge.KindOf(marker));
        Assert.Equal("polygon", item.Value<string>("kind"));
        Assert.Equal(41, item.Value<int>("id"));
        Assert.Equal("#F44336", item.Value<string>("fill"));
        Assert.Equal(0.5, item.Value<double>("fillOpacity"));
        Assert.Equal("Dash", item.Value<string>("pattern"));
        Assert.Equal(3, item["pts"]!.Count());

        model.IsClosedPath = false;
        Assert.Equal("line", Map3DOverlayBridge.KindOf(marker));
    });

    [Fact]
    public void should_build_point_kinds_with_label_when_geometric_military_infra() => HousingTests.Sta(() =>
    {
        var log = Mock.Of<ILogService>();
        var geometric = new GMapGeometricMarker(log, new GeometricSymbolModel
        {
            Id = 1, Title = "도형", Latitude = 37.5, Longitude = 127.1, TitleSize = 13, TitleColor = EnumColorType.White,
            TitleBackground = EnumColorType.Transparent, TitleBold = true, ShowTitle = true, Visible = true,
        });
        var military = new GMapMilitarySymbolMarker(log, new MilitarySymbolModel { Id = 2, Title = "부대", Visible = true });
        var infra = new GMapInfraMarker(log, new InfraSymbolModel { Id = 3, Title = "건물", Visible = true });

        var g = Map3DOverlayBridge.BuildItem(geometric)!;
        Assert.Equal("geometric", g.Value<string>("kind"));
        Assert.Equal(37.5, g.Value<double>("lat"));
        Assert.Equal("도형", g["label"]!.Value<string>("text"));
        Assert.Equal(13, g["label"]!.Value<double>("size"));
        Assert.Equal("#FFFFFF", g["label"]!.Value<string>("color"));
        Assert.Equal(Newtonsoft.Json.Linq.JTokenType.Null, g["label"]!["bg"]!.Type);   // 투명 배경 → null
        Assert.True(g["label"]!.Value<bool>("bold"));
        Assert.Null(g["fov"]);

        Assert.Equal("military", Map3DOverlayBridge.BuildItem(military)!.Value<string>("kind"));
        Assert.Equal("infra", Map3DOverlayBridge.BuildItem(infra)!.Value<string>("kind"));
    });

    [Fact]
    public void should_send_fov_only_when_pids_is_camera() => HousingTests.Sta(() =>
    {
        var log = Mock.Of<ILogService>();
        var camera = new GMapPidsMarker(log, new PidsSymbolModel
        {
            Id = 7, Title = "PTZ", DeviceType = EnumDeviceType.IpCamera, ShowFOV = true,
            DetectionRange = 150, DetectionAngle = 40, DetectionBearing = 120, FOVColor = EnumColorType.Green, FOVOpacity = 0.3,
        });
        var sensor = new GMapPidsMarker(log, new PidsSymbolModel { Id = 8, Title = "센서", DeviceType = EnumDeviceType.Controller, ShowFOV = true });

        var fov = Map3DOverlayBridge.BuildItem(camera)!["fov"]!;
        Assert.True(fov.Value<bool>("show"));
        Assert.Equal(150, fov.Value<double>("rangeM"));
        Assert.Equal(40, fov.Value<double>("angleDeg"));
        Assert.Equal(120, fov.Value<double>("bearingDeg"));
        Assert.Equal("#4CAF50", fov.Value<string>("color"));
        Assert.Equal(0.3, fov.Value<double>("opacity"), 6);
        Assert.Null(Map3DOverlayBridge.BuildItem(sensor)!["fov"]);
    });
}
