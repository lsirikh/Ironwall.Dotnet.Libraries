using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 지도 부품 이름표(L2) 오프스크린 렌더 — 48 · 64px × 라이트 · 다크 × 모두 정상 / 고장 1 / 고장 2 · 저하 1 / 미상 / 정상 + 선택.
/// 실제 심볼 컨트롤(PidsMarkerStyle, 제목 라벨 포함)을 지도 타일 색 바탕 위에 그린다(창 없음).
/// 환경 변수 <c>IRONWALL_PLATE_RENDER_DIR</c> 가 있으면 그 폴더에 <c>{IRONWALL_PLATE_RENDER_TAG}-plates.png</c> 를 남긴다.
/// </summary>
public class ComponentPlateRenderTests
{
    private const int CellW = 170, CellH = 150, HeadW = 70, HeadH = 26;

    private static readonly (string Name, (string Key, string Type, string? Label, bool? InService, string? State, string? Health, string? Reason, bool Observed)[] Parts, bool Selected)[] Cases =
    {
        ("모두 정상", Parts("OK", "OK", "OK", "OK"), false),
        ("고장 1", Parts("OK", "OK", "FAULT", "OK"), false),
        ("저하 1", Parts("OK", "OK", "DEGRADED", "OK"), false),
        ("고장 2 · 저하 1", Parts("FAULT", "OK", "DEGRADED", "FAULT"), false),
        ("긴 이름 고장", Parts("OK", "OK", "OK", "OK", nic: "FAULT"), false),
        ("미상", Parts(null, null, null, null), false),
        ("정상 + 선택", Parts("OK", "OK", "OK", "OK"), true),
    };

    private static (string, string, string?, bool?, string?, string?, string?, bool)[] Parts(string? ptz, string? tracker, string? radar, string? wiper, string? nic = "OK") => new[]
    {
        ("ptz", "PTZ_UNIT", (string?)"PTZ 구동부", (bool?)null, (string?)"IDLE", ptz, ptz == "FAULT" ? "COMM_ERROR" : null, ptz != null),
        ("tracker", "TRACKER", null, null, "ACTIVE", tracker, null, tracker != null),
        ("radar", "RADAR_UNIT", "레이더", null, "ON", radar, radar == "FAULT" ? "OVER_CURRENT" : null, radar != null),
        ("wiper", "WIPER", null, null, "OFF", wiper, null, wiper != null),
        ("nic", "NETWORK_INTERFACE", "네트워크 인터페이스", null, "ON", nic, null, ptz != null),
    };

    [Fact]
    public void should_render_plates_for_every_state_at_48_and_64_on_light_and_dark_tiles() => HousingTests.Sta(() =>
    {
        int width = HeadW + CellW * Cases.Length, height = HeadH + CellH * 4;
        var root = new Canvas { Width = width, Height = height, Background = Brushes.White };
        var row = 0;
        foreach (var dark in new[] { false, true })
            foreach (var size in new[] { 48, 64 })
            {
                var band = new Canvas { Width = width, Height = CellH, Background = new SolidColorBrush(dark ? Color.FromRgb(0x1B, 0x23, 0x30) : Color.FromRgb(0xE8, 0xE4, 0xDA)) };
                band.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml", UriKind.Relative) });
                band.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative) });
                band.Children.Add(Text($"{(dark ? "다크" : "라이트")}\n{size}px", 6, CellH / 2 - 14, dark ? Brushes.White : Brushes.Black));
                for (var i = 0; i < Cases.Length; i++)
                {
                    var control = Symbol(size, Cases[i].Parts, Cases[i].Selected);
                    Canvas.SetLeft(control, HeadW + CellW * i + (CellW - size) / 2.0);
                    Canvas.SetTop(control, 22);
                    band.Children.Add(control);
                }
                Canvas.SetTop(band, HeadH + CellH * row++);
                root.Children.Add(band);
            }
        for (var i = 0; i < Cases.Length; i++) root.Children.Add(Text(Cases[i].Name, HeadW + CellW * i + 8, 5, Brushes.Black));
        HousingTests.Layout(root, width, height);

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        Assert.True(bitmap.PixelWidth == width);
        if (Environment.GetEnvironmentVariable("IRONWALL_PLATE_RENDER_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(dir, $"{Environment.GetEnvironmentVariable("IRONWALL_PLATE_RENDER_TAG") ?? "after"}-plates.png"));
            encoder.Save(file);
        }
    });

    private static TextBlock Text(string text, double x, double y, Brush brush)
    {
        var block = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = brush };
        Canvas.SetLeft(block, x); Canvas.SetTop(block, y);
        return block;
    }

    private static GMapMarkerPidsControl Symbol(double size, (string Key, string Type, string? Label, bool? InService, string? State, string? Health, string? Reason, bool Observed)[] parts, bool selected)
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = "북측 PTZ-07", DeviceType = EnumDeviceType.IpCamera });
        marker.Width = size; marker.Height = size; marker.IsVisible = true;
        marker.LinkedDevice = new BaseDeviceModel
        {
            Id = 7, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED,
            Axes = ComponentHealthSummaryTests.Axes(true, parts),
        };
        var control = new GMapMarkerPidsControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        control.Width = size; control.Height = size;
        control.MarkerScreenScale = 1.0;
        if (selected) control.IsSelected = true;
        return control;
    }
}
