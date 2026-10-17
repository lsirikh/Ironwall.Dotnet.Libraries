using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

public class MapBearingProjectionTests
{
    [Fact]
    public void map_rotation_changes_visible_faces_without_changing_world_position_or_bearing()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previousIoC = IoC.GetInstance;
            bool previousRotation = RotationFeature.IsEnabled;
            IoC.GetInstance = (type, _) => type == typeof(ILogService) ? Mock.Of<ILogService>()
                : type == typeof(IEventAggregator) ? new EventAggregator() : throw new InvalidOperationException(type.Name);
            try { VerifyProjection(); }
            catch (Exception ex) { failure = ex; }
            finally
            {
                RotationFeature.IsEnabled = previousRotation;
                IoC.GetInstance = previousIoC;
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "STA timeout");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void VerifyProjection()
    {
        using var pids = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            DeviceType = EnumDeviceType.IpCamera, ModelVariant = "Fixed", Bearing = 17, BaseBearing = 13,
            DetectionBearing = 47, Latitude = 37.5, Longitude = 127, Width = 128, Height = 128
        });
        using var infra = new GMapInfraMarker(Mock.Of<ILogService>(), new InfraSymbolModel
        {
            BuildingType = EnumBuildingType.Factory, Bearing = 31, Latitude = 37.5001, Longitude = 127.0001,
            Width = 128, Height = 128
        });
        var styles = new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) };
        var camera = new GMapMarker3DHousingControl(pids) { Style = (Style)styles[typeof(GMapMarker3DHousingControl)], Width = 128, Height = 128 };
        var building = new GMapMarkerInfra3DControl(infra) { Style = (Style)styles[typeof(GMapMarkerInfra3DControl)], Width = 128, Height = 128 };
        pids.Shape = camera;
        infra.Shape = building;
        var map = new GMapCustomControl { Width = 640, Height = 480 };
        map.Markers.Add(pids);
        map.Markers.Add(infra);
        RotationFeature.IsEnabled = true;

        var host = new StackPanel { Orientation = Orientation.Horizontal, Width = 256, Height = 128 };
        host.Children.Add(camera);
        host.Children.Add(building);
        using var source = new HwndSource(new HwndSourceParameters("Synthetic map bearing projection")
        { Width = 256, Height = 128, WindowStyle = unchecked((int)0x80000000) }) { RootVisual = host };
        host.Measure(new Size(256, 128));
        host.Arrange(new Rect(0, 0, 256, 128));
        host.UpdateLayout();
        var cameraView = (HousingVisual)camera.Template.FindName("PART_Housing3D", camera);
        var buildingView = (HousingVisual)building.Template.FindName("PART_Housing3D", building);
        var cameraHashes = new List<string>();
        var buildingHashes = new List<string>();
        string? output = Environment.GetEnvironmentVariable("SYMBOL3D_ARTIFACTS");
        var sheet = new StackPanel { Width = 640, Background = Brushes.White };
        foreach (int bearing in new[] { 0, 90, 180, 270, 360 })
        {
            map.SetMapRotation(bearing);
            host.UpdateLayout();
            Assert.Equal(17, pids.Bearing);
            Assert.Equal(13, pids.BaseBearing);
            Assert.Equal(47, pids.DetectionBearing);
            Assert.Equal(new PointLatLng(37.5, 127), pids.Position);
            Assert.Equal(31, infra.Bearing);
            Assert.Equal(new PointLatLng(37.5001, 127.0001), infra.Position);
            Assert.Equal(HousingMath.Normalize(30 - bearing), HousingMath.Normalize(cameraView.Yaw));
            Assert.Equal(17, cameraView.HeadYaw);
            Assert.Equal(HousingMath.Normalize(31 - bearing), HousingMath.Normalize(buildingView.Yaw));
            foreach (var control in new Control[] { camera, building })
                Assert.Equal(0, ((RotateTransform)((TransformGroup)control.RenderTransform).Children[0]).Angle);

            var cameraImage = Render(cameraView);
            var buildingImage = Render(buildingView);
            cameraHashes.Add(Hash(cameraImage));
            buildingHashes.Add(Hash(buildingImage));
            if (bearing < 360)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = $"Map {bearing} degrees", Width = 150, VerticalAlignment = VerticalAlignment.Center, FontSize = 16, Margin = new Thickness(10) });
                row.Children.Add(new Image { Source = cameraImage, Width = 160, Height = 160 });
                row.Children.Add(new Image { Source = buildingImage, Width = 160, Height = 160 });
                sheet.Children.Add(row);
            }
        }
        Assert.Equal(4, cameraHashes.Take(4).Distinct().Count());
        Assert.Equal(4, buildingHashes.Take(4).Distinct().Count());
        Assert.Equal(cameraHashes[0], cameraHashes[4]);
        Assert.Equal(buildingHashes[0], buildingHashes[4]);
        if (output != null)
        {
            Directory.CreateDirectory(output);
            sheet.Measure(new Size(640, 640)); sheet.Arrange(new Rect(0, 0, 640, 640)); sheet.UpdateLayout();
            var bitmap = new RenderTargetBitmap(640, 640, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(sheet);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, "map-bearing-projection.png")); png.Save(file);
        }
        map.Markers.Clear();
    }

    private static RenderTargetBitmap Render(FrameworkElement control)
    {
        var bitmap = new RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(control);
        return bitmap;
    }

    private static string Hash(BitmapSource bitmap)
    {
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        Assert.Contains(pixels, value => value != 0);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
}
