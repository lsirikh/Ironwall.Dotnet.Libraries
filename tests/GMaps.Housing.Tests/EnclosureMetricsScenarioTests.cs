using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;
using DeviceProvider = Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider;

namespace GMaps.Housing.Tests;

/****************************************************************************
   Purpose      : 함체 계측(ENCLOSURE_METRICS · 이슈 #7-20) — NATS 수신 → 함체 심볼 툴팁 · 상세 창 기본 탭. 창 없음.
   Created By   : Claude Code
   Created On   : 2026-10-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EnclosureMetricsScenarioTests
{
    private const string Metrics = """
        { "id": "m-1", "m_type": "PUB", "cmd": "ENCLOSURE_METRICS", "from": "DBApi",
          "body": { "id": 90, "enclosure_id": 394, "temperature": 25.5, "humidity": 48.0, "voltage": 220.0, "current": 3.2,
                    "vibration": 12, "ups_battery_level": 95, "ups_charging": true, "detail": null,
                    "observed_at": "2026-09-12T12:30:00.000000+09:00", "created_at": "2026-09-12T12:15:34.400323+09:00" },
          "created": "2026-09-12T12:15:39.004321+09:00" }
        """;

    private static void SetField(object target, string name, object? value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) { f.SetValue(target, value); return; }
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    [Fact]
    public void should_show_latest_metrics_in_enclosure_tooltip_and_detail_then_mark_old_value() => HousingTests.Sta(() =>
    {
        var log = new Mock<ILogService>();
        var handlers = new List<Func<MessageArgsModel, Task>>();
        var nats = new Mock<INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => handlers.Add(h));
        var service = new EnclosureMetricsNatsSyncService(log.Object, nats.Object);
        service.StartService();

        var devices = new DeviceProvider();
        devices.Add(new EnclosureDeviceModel { Id = 394, DeviceType = EnumDeviceType.Enclosure, Status = EnumDeviceStatus.ACTIVATED });
        var map = new GMapCustomControl { Width = 640, Height = 480, Position = new PointLatLng(37.3936, 126.9665), Zoom = 18 };
        var model = new PidsSymbolModel
        {
            Id = 1394, Title = "함체-394", DeviceType = EnumDeviceType.Enclosure, LinkedDeviceId = 394,
            Latitude = map.Position.Lat, Longitude = map.Position.Lng, Width = 48, Height = 48, ShowShape = true,
        };
        var marker = (GMapPidsMarker)new MarkerFactory(log.Object, devices).CreateMarker(model);
        var control = new GMapMarkerPidsFallbackControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) });
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        marker.Shape = control;
        HousingTests.Layout(control, 48, 48);
        map.Markers.Add(marker);

        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(vm, map);
        SetField(vm, "_log", log.Object);
        SetField(vm, "_enclosureMetricsSync", service);
        var apply = typeof(MapViewModel).GetMethod("ApplyEnclosureMetrics", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // 받기 전 — 툴팁에 계측 절 없음, 상세 창은 '받지 못함'
        var detail = new SymbolDetailViewModel();
        detail.Load(marker, new SymbolDetailContext(EnumDeviceType.Enclosure, HasDevice: true));
        Assert.Contains(("계측", "받지 못함"), Fields(detail));
        Assert.DoesNotContain("온도", control.SymbolToolTip);

        // 다른 주제 · 다른 cmd 는 무시
        foreach (var h in handlers) h(new MessageArgsModel("sensorway.unit001.gis.broadcast-status", null, Metrics)).GetAwaiter().GetResult();
        Assert.False(service.TryGetLatest(394, out _));

        // ENCLOSURE_METRICS 수신 → 심볼에 반영
        foreach (var h in handlers) h(new MessageArgsModel("sensorway.unit001.gis.enclosure-metrics", null, Metrics)).GetAwaiter().GetResult();
        apply.Invoke(vm, new object?[] { null });
        Assert.NotNull(marker.EnclosureMetrics);
        Assert.False(marker.EnclosureMetrics!.IsStale);
        var lines = control.SymbolToolTip!.Split(Environment.NewLine);
        Assert.Equal("함체-394", lines[0]);
        Assert.Contains("온도 25.5 ℃ · 습도 48 % · 전압 220 V · 전류 3.2 A · 진동 12 · UPS 배터리 95 % · 충전 중", lines);
        Assert.StartsWith("받은 시각 ", lines[^1]);
        Assert.DoesNotContain("오래된 값", control.SymbolToolTip);

        var fields = Fields(detail);   // 열린 창이 따라간다
        Assert.Contains(("온도", "25.5 ℃"), fields);
        Assert.Contains(("UPS 배터리", "95 % · 충전 중"), fields);
        Assert.DoesNotContain(("계측", "받지 못함"), fields);

        // 30초 넘게 새 계측이 없으면 '오래된 값'
        marker.EnclosureMetrics = marker.EnclosureMetrics with { IsStale = true };
        Assert.EndsWith("· 오래된 값", control.SymbolToolTip!.Split(Environment.NewLine)[^1]);
        var received = detail.Tabs.Single(t => t.Tab == SymbolDetailTab.Basic).Fields.Single(f => f.Label == "받은 시각");
        Assert.EndsWith("오래된 값", received.Value);
        Assert.Equal("warn", received.Tone);
    });

    private static List<(string, string)> Fields(SymbolDetailViewModel vm)
        => vm.Tabs.Single(t => t.Tab == SymbolDetailTab.Basic).Fields.Select(f => (f.Label, f.Value)).ToList();
}
