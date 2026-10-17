using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Door = Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Door;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 심볼 상세 보기 창 오프스크린 렌더 — PRD symbol-detail-and-door-control TEST-04/05.
/// 라이트·다크 두 테마에서 그려지는지, 탭 활성 규칙이 화면에 반영되는지,
/// 그리고 <b>프리뷰 오빗 카메라가 지도 심볼 카메라와 독립</b>인지(FR-16 전제)를 픽셀로 확인한다.
/// </summary>
public class SymbolDetailRenderTests
{
    private static ResourceDictionary Tokens(string name)
        => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{name}.xaml", UriKind.Relative) };

    private static ResourceDictionary Theme(string themeName) => new()
    {
        MergedDictionaries =
        {
            Tokens("Shared"), Tokens(themeName),
            new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolDetailStyle.xaml", UriKind.Relative) },
        }
    };

    private static GMapPidsMarker Marker(EnumDeviceType type, bool linked)
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Title = "외곽-07",
            DeviceType = type,
            Latitude = 37.5,
            Longitude = 127.0,
            Width = 40,
            Height = 40,
            Zoom = 16,
        });
        if (linked)
            marker.LinkedDevice = new SensorDeviceModel
            {
                Id = 142,
                DeviceNumber = 7,
                DeviceName = "외곽-07",
                DeviceType = type,
                Status = EnumDeviceStatus.ACTIVATED,
                IsEnable = true,
                Version = "v1.4.2",
                Location = "서측 외곽 3구간",
            };
        return marker;
    }

    private static SymbolDetailControl Panel(GMapPidsMarker marker, in SymbolDetailContext context, string themeName, out SymbolDetailViewModel vm)
    {
        var viewModel = new SymbolDetailViewModel();
        viewModel.Load(marker, in context, layerName: "센서");
        var panel = new SymbolDetailControl { DataContext = viewModel };
        // 다크는 패널 로컬 토큰 교체로 확인한다 — 앱 수준 교체는 이미 로드된 Window 만 무효화한다.
        panel.Resources.MergedDictionaries.Add(Theme(themeName));
        panel.Style = (Style)panel.Resources[typeof(SymbolDetailControl)];
        vm = viewModel;
        return panel;
    }

    private static byte[] Pixels(FrameworkElement view, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var buffer = new byte[width * height * 4];
        bitmap.CopyPixels(buffer, width * 4, 0);
        return buffer;
    }

    private static int NonTransparentCount(byte[] pixels)
    {
        int count = 0;
        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 0) count++;
        return count;
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void should_render_detail_window_when_theme_is_applied(string themeName) => AppHost.Run(() =>
    {
        using var marker = Marker(EnumDeviceType.SmartSensor, linked: true);
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true, WebServerEnabled: true);
        var panel = Panel(marker, in context, themeName, out var vm);
        AppHost.Layout(panel, 720, 430);

        var pixels = Pixels(panel, 720, 430);
        // 스타일이 안 붙거나 XAML 파싱이 깨지면 여기서 빈 화면이 된다(ControlTemplate 미적용 = 0 px).
        Assert.True(NonTransparentCount(pixels) > 720 * 430 / 2, $"{themeName}: 창이 사실상 비었다");
        // 유형별 탭은 내려둔 상태 — 기본·심볼 둘만(사용자 결정 2026-09-09).
        Assert.Equal(new[] { SymbolDetailTab.Basic, SymbolDetailTab.Symbol }, vm.Tabs.Select(t => t.Tab));
        Assert.All(vm.Tabs, tab => Assert.True(tab.IsEnabled));
        vm.Dispose();
    });

    [Fact]
    public void should_differ_between_light_and_dark() => AppHost.Run(() =>
    {
        using var marker = Marker(EnumDeviceType.SmartSensor, linked: true);
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true, WebServerEnabled: true);

        var light = Panel(marker, in context, "Light", out var lightVm);
        AppHost.Layout(light, 720, 430);
        var lightPixels = Pixels(light, 720, 430);

        var dark = Panel(marker, in context, "Dark", out var darkVm);
        AppHost.Layout(dark, 720, 430);
        var darkPixels = Pixels(dark, 720, 430);

        Assert.False(lightPixels.AsSpan().SequenceEqual(darkPixels), "라이트와 다크가 같은 그림이다 — 토큰이 안 먹었다");
        lightVm.Dispose(); darkVm.Dispose();
    });

    [Fact]
    public void should_enable_only_symbol_tab_when_device_is_not_linked() => AppHost.Run(() =>
    {
        using var marker = Marker(EnumDeviceType.Lamp, linked: false);
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: false);
        var panel = Panel(marker, in context, "Light", out var vm);
        AppHost.Layout(panel, 720, 430);

        Assert.All(vm.Tabs, tab => Assert.Equal(tab.Tab == SymbolDetailTab.Symbol, tab.IsEnabled));
        Assert.Equal(SymbolDetailTab.Symbol, vm.SelectedTab!.Tab);
        // 심볼 탭은 장비가 없어도 실제 값이 차 있어야 한다 — 이게 미연결 심볼을 식별하는 유일한 창구다.
        Assert.Contains(vm.SelectedTab.Fields, f => f.Label == "심볼 제목" && f.Value == "외곽-07");
        Assert.Contains(vm.SelectedTab.Fields, f => f.Label == "레이어" && f.Value == "센서");
        Assert.All(vm.Actions.Where(a => a.Action != SymbolDetailAction.ShowOnMap && a.Action != SymbolDetailAction.DevicePage),
            a => Assert.False(a.IsEnabled));
        vm.Dispose();
    });

    /// <summary>
    /// 실기에서 잡은 결함(2026-09-08): 장비 Id 는 있는데 장비 목록에 그 장비가 없으면
    /// 종전엔 탭이 전부 활성인 채 값만 전부 "—" 로 떠서 "연결됐는데 정보가 없다"로 보였다.
    /// 이제 미연결로 취급하되 <b>사유를 구분</b>해 원인을 추적할 수 있어야 한다.
    /// </summary>
    [Fact]
    public void should_distinguish_missing_device_from_unlinked_symbol() => AppHost.Run(() =>
    {
        // ① 애초에 연결 안 한 심볼
        using var plain = Marker(EnumDeviceType.Lamp, linked: false);
        var unlinked = new SymbolDetailContext(plain.DeviceType, HasDevice: false);
        var vmA = new SymbolDetailViewModel();
        vmA.Load(plain, in unlinked);
        Assert.Contains("연결되지 않", vmA.NoDeviceReason);
        Assert.DoesNotContain("#", vmA.NoDeviceReason);

        // ② Id 는 있는데 장비 객체를 못 찾은 경우 — 번호가 사유에 드러나야 추적이 된다
        using var dangling = Marker(EnumDeviceType.IpCamera, linked: false);
        dangling.LinkedDeviceId = 5911;
        var missing = new SymbolDetailContext(dangling.DeviceType, HasDevice: false);
        var vmB = new SymbolDetailViewModel();
        vmB.Load(dangling, in missing);
        Assert.Contains("5911", vmB.NoDeviceReason);
        Assert.All(vmB.Tabs, t => Assert.Equal(t.Tab == SymbolDetailTab.Symbol, t.IsEnabled));

        // ③ 정상 연결이면 사유가 없다
        using var ok = Marker(EnumDeviceType.IpCamera, linked: true);
        var linkedCtx = new SymbolDetailContext(ok.DeviceType, HasDevice: true);
        var vmC = new SymbolDetailViewModel();
        vmC.Load(ok, in linkedCtx);
        Assert.Equal(string.Empty, vmC.NoDeviceReason);

        vmA.Dispose(); vmB.Dispose(); vmC.Dispose();
    });

    /// <summary>장비 등록 좌표(서버 geolocation)가 기본 탭에 GPS 형식으로 나오고, 심볼과의 차이도 보인다.</summary>
    [Fact]
    public void should_show_device_gps_position_in_basic_tab() => AppHost.Run(() =>
    {
        using var marker = Marker(EnumDeviceType.IpCamera, linked: true);
        marker.Position = new GMap.NET.PointLatLng(37.400000, 126.968000);   // 장비에서 약 800 m 떨어뜨림
        var device = (SensorDeviceModel)marker.LinkedDevice!;
        device.Latitude = 37.392779; device.Longitude = 126.967959; device.Altitude = 12.5; device.Heading = 135;

        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true);
        var panel = Panel(marker, in context, "Light", out var vm);
        AppHost.Layout(panel, 720, 430);

        var basic = vm.Tabs.First(t => t.Tab == SymbolDetailTab.Basic).Fields.ToList();
        Assert.Contains(basic, f => f.Label == "위도" && f.Value.StartsWith("37.392779") && f.Value.Contains("N"));
        Assert.Contains(basic, f => f.Label == "경도" && f.Value.StartsWith("126.967959") && f.Value.Contains("E"));
        Assert.Contains(basic, f => f.Label == "고도" && f.Value == "12.5 m");
        Assert.Contains(basic, f => f.Label == "방위" && f.Value == "135°");
        // 심볼을 멀리 떨어뜨렸으니 차이가 경고로 표시돼야 한다 — '현재위치 적용' 누락을 눈에 띄게 한다.
        Assert.Contains(basic, f => f.Label == "심볼과 차이" && f.Tone == "warn");
        vm.Dispose();
    });

    /// <summary>서버 geolocation 이 비면 모델 좌표가 0 으로 남는다 — 그걸 좌표로 그리면 안 된다.</summary>
    [Fact]
    public void should_mark_origin_coordinates_as_unregistered() => AppHost.Run(() =>
    {
        using var marker = Marker(EnumDeviceType.Lamp, linked: true);   // Marker() 는 좌표를 채우지 않는다 → 0,0
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true);
        var panel = Panel(marker, in context, "Light", out var vm);
        AppHost.Layout(panel, 720, 430);

        var basic = vm.Tabs.First(t => t.Tab == SymbolDetailTab.Basic).Fields.ToList();
        Assert.Contains(basic, f => f.Label == "등록 좌표" && f.Value == "미등록" && f.Tone == "warn");
        Assert.DoesNotContain(basic, f => f.Label == "위도");
        vm.Dispose();
    });

    [Fact]
    public void should_show_2d_stage_when_type_has_no_housing_model() => AppHost.Run(() =>
    {
        // Cable 은 하우징 모델이 없다 → 2D 고정 표시(FR-16). 3D 프리뷰는 숨는다.
        using var marker = Marker(EnumDeviceType.Cable, linked: true);
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true);
        var panel = Panel(marker, in context, "Light", out var vm);
        AppHost.Layout(panel, 720, 430);

        Assert.Null(HousingModels.DeviceKey(EnumDeviceType.Cable));
        Assert.False(vm.Is3D);
        Assert.True(vm.Is2D);
        Assert.Equal("2D 심볼 · 회전 없음", vm.KindText);
        vm.Dispose();
    });

    /// <summary>TEST-05 — 상세 창 카메라를 돌려도 지도 심볼(피치 35° 고정)은 한 픽셀도 바뀌지 않아야 한다.</summary>
    [Fact]
    public void should_keep_map_symbol_camera_independent_when_preview_angle_changes() => AppHost.Run(() =>
    {
        var mapSymbol = new HousingVisual { ModelKey = "enclosure", Width = 120, Height = 120 };
        AppHost.Layout(mapSymbol, 120, 120);
        var before = Pixels(mapSymbol, 120, 120);

        var preview = new SymbolPreview3DControl { ModelKey = "enclosure", Width = 120, Height = 120, IsAutoRotating = false };
        AppHost.Layout(preview, 120, 120);
        var previewFront = Pixels(preview, 120, 120);

        preview.YawDeg = 90;
        preview.PitchDeg = 70;
        AppHost.Layout(preview, 120, 120);
        var previewTurned = Pixels(preview, 120, 120);

        AppHost.Layout(mapSymbol, 120, 120);
        var after = Pixels(mapSymbol, 120, 120);

        Assert.True(before.AsSpan().SequenceEqual(after), "상세 창 카메라가 지도 심볼을 건드렸다");
        Assert.False(previewFront.AsSpan().SequenceEqual(previewTurned), "프리뷰 각도를 바꿨는데 그림이 그대로다");
        preview.Stop();
    });

    [Fact]
    public void should_return_to_front_when_reset_is_requested() => AppHost.Run(() =>
    {
        var preview = new SymbolPreview3DControl { ModelKey = "camera", Width = 120, Height = 120, IsAutoRotating = false };
        AppHost.Layout(preview, 120, 120);
        preview.YawDeg = 137;
        preview.PitchDeg = -40;

        preview.ResetToFront();

        Assert.Equal(0, preview.YawDeg, 6);
        Assert.Equal(OrbitCameraMath.DefaultPitchDeg, preview.PitchDeg, 6);
        preview.Stop();
    });

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe) yield return fe;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    /// <summary>
    /// 창은 Visibility 토글로 닫힌다 — 닫을 때 VM 구독까지 끊으면 <b>다시 열었을 때 `⟲ 정면` 이 죽는다</b>.
    /// 실제로 두 번 열어봐야만 잡히는 종류의 결함이라 회귀 테스트로 고정한다.
    /// </summary>
    [Fact]
    public void should_keep_reset_working_after_close_and_reopen() => AppHost.Run(() =>
    {
        using var marker = Marker(EnumDeviceType.Enclosure, linked: true);
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true);
        var panel = Panel(marker, in context, "Light", out var vm);
        AppHost.Layout(panel, 720, 430);

        var preview = Descendants(panel).OfType<SymbolPreview3DControl>().Single();
        preview.IsAutoRotating = false;

        panel.ShutDown();                                   // 닫기
        vm.Unload();
        vm.Load(marker, in context, layerName: "함체");      // 다시 열기(같은 VM 재사용)
        AppHost.Layout(panel, 720, 430);

        preview.YawDeg = 210;
        vm.ResetViewCommand.Execute(null);
        Assert.Equal(0, preview.YawDeg, 6);                 // 배선이 살아 있어야 0 으로 돌아온다
        vm.Dispose();
    });

    [Fact]
    public void should_release_marker_subscription_when_window_is_closed() => AppHost.Run(() =>
    {
        using var marker = Marker(EnumDeviceType.Gate, linked: true);
        marker.DoorState = EnumDoorState.Closed;
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true, DoorState: Door.DoorUiState.Closed);
        var panel = Panel(marker, in context, "Light", out var vm);
        AppHost.Layout(panel, 720, 430);

        marker.DoorState = EnumDoorState.Open;
        Assert.Equal(1, vm.DoorOpen);       // 열려 있는 동안엔 문이 따라 움직인다

        panel.ShutDown();
        vm.Dispose();
        marker.DoorState = EnumDoorState.Closed;
        Assert.Equal(1, vm.DoorOpen);       // 닫은 뒤에는 더 이상 따라오지 않는다(구독 해제)
    });
}
