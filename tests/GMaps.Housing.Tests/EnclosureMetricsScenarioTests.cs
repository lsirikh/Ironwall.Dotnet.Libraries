using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using System.Net.Http;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
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
        // 판정 시계 = 잰 시각 직후(예시 계측의 observed_at 은 과거라 지금 시각으로 재면 바로 '오래된 값' 이다)
        SetField(vm, "_enclosureMetricsNow", (Func<DateTime>)(() => ObservedLocal.AddSeconds(5)));
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
        Assert.StartsWith("잰 시각 ", lines[^1]);   // 8.0.15: 받은 때가 아니라 잰 때(observed_at)
        Assert.DoesNotContain("오래된 값", control.SymbolToolTip);

        var fields = Fields(detail);   // 열린 창이 따라간다
        Assert.Contains(("온도", "25.5 ℃"), fields);
        Assert.Contains(("UPS 배터리", "95 % · 충전 중"), fields);
        Assert.DoesNotContain(("계측", "받지 못함"), fields);

        // 30초 넘게 새 계측이 없으면 '오래된 값'
        marker.EnclosureMetrics = marker.EnclosureMetrics with { IsStale = true };
        Assert.EndsWith("· 오래된 값", control.SymbolToolTip!.Split(Environment.NewLine)[^1]);
        var received = detail.Tabs.Single(t => t.Tab == SymbolDetailTab.Basic).Fields.Single(f => f.Label == "잰 시각");
        Assert.EndsWith("오래된 값", received.Value);
        Assert.Equal("warn", received.Tone);
    });

    // ── 8.0.15 G35 · G24 ──

    private static readonly DateTime ObservedLocal = DateTimeOffset.Parse("2026-09-12T12:30:00+09:00").LocalDateTime;

    private static (EnclosureMetricsNatsSyncService Service, Func<string, Task> Publish) MakeService(IDeviceApiService? source = null)
    {
        var handlers = new List<Func<MessageArgsModel, Task>>();
        var nats = new Mock<INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => handlers.Add(h));
        var service = source == null
            ? new EnclosureMetricsNatsSyncService(null, nats.Object)
            : new EnclosureMetricsNatsSyncService(null, nats.Object, source);
        return (service, async json =>
        {
            foreach (var h in handlers) await h(new MessageArgsModel("sensorway.unit001.gis.enclosure-metrics", null, json));
        });
    }

    private static string MetricsRow(int rowId, string observedAt)
        => Metrics.Replace("\"id\": 90", $"\"id\": {rowId}").Replace("2026-09-12T12:30:00.000000+09:00", observedAt);

    [Fact]
    public async Task should_mark_old_value_when_same_row_repeats_for_40_seconds()
    {
        var (service, publish) = MakeService();
        var now = ObservedLocal;
        service.Clock = () => now;
        var events = 0;
        service.MetricsReceived += _ => events++;
        await service.StartService();

        // 서버는 새 측정이 없으면 같은 id · observed_at 을 10초마다 다시 보낸다 — 40초 동안 5번
        for (var s = 0; s <= 40; s += 10)
        {
            now = ObservedLocal.AddSeconds(s);
            await publish(Metrics);
        }

        Assert.Equal(1, events);   // 같은 행은 한 번만 알린다
        Assert.True(service.TryGetLatest(394, out var held));
        Assert.Equal(90, held.MetricId);
        Assert.True(EnclosureMetricsText.IsStale(held, now));   // 받은 때로 재면 방금(0초)이지만 잰 지 40초
        var row = EnclosureMetricsText.Rows(held with { IsStale = EnclosureMetricsText.IsStale(held, now) }, now).Last();
        Assert.Equal("잰 시각", row.Label);
        Assert.EndsWith("오래된 값", row.Value);

        // 새 행(다른 id · 새 observed_at)이 오면 다시 새 값
        now = ObservedLocal.AddSeconds(50);
        await publish(MetricsRow(91, "2026-09-12T12:30:48.000000+09:00"));
        Assert.Equal(2, events);
        Assert.True(service.TryGetLatest(394, out held));
        Assert.False(EnclosureMetricsText.IsStale(held, now));
    }

    [Fact]
    public void should_mark_marker_old_value_by_measured_time_in_map() => HousingTests.Sta(() =>
    {
        var (service, publish) = MakeService();
        var now = ObservedLocal;
        service.Clock = () => now;
        service.StartService();

        var map = new GMapCustomControl { Width = 640, Height = 480, Position = new PointLatLng(37.3936, 126.9665), Zoom = 18 };
        var devices = new DeviceProvider();
        devices.Add(new EnclosureDeviceModel { Id = 394, DeviceType = EnumDeviceType.Enclosure, Status = EnumDeviceStatus.ACTIVATED });
        var model = new PidsSymbolModel
        {
            Id = 1394, Title = "함체-394", DeviceType = EnumDeviceType.Enclosure, LinkedDeviceId = 394,
            Latitude = map.Position.Lat, Longitude = map.Position.Lng, Width = 48, Height = 48,
        };
        var marker = (GMapPidsMarker)new MarkerFactory(new Mock<ILogService>().Object, devices).CreateMarker(model);
        map.Markers.Add(marker);

        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(vm, map);
        SetField(vm, "_enclosureMetricsSync", service);
        SetField(vm, "_enclosureMetricsNow", (Func<DateTime>)(() => now));
        var apply = typeof(MapViewModel).GetMethod("ApplyEnclosureMetrics", BindingFlags.Instance | BindingFlags.NonPublic)!;

        for (var s = 0; s <= 40; s += 10)
        {
            now = ObservedLocal.AddSeconds(s);
            publish(Metrics).GetAwaiter().GetResult();
            apply.Invoke(vm, new object?[] { null });   // 5초 타이머 · 수신 알림이 하는 일
            if (s <= 30) Assert.False(marker.EnclosureMetrics!.IsStale);
        }
        Assert.True(marker.EnclosureMetrics!.IsStale);
    });

    /// <summary>장비 API 가짜 — GET /api/enclosure-metrics 응답 본문을 실제 DTO 로 읽어 돌려준다(상태 코드 포함).</summary>
    private static Mock<IDeviceApiService> FakeApi(int status, string json)
    {
        var resp = JsonConvert.DeserializeObject<ApiListResponse<EnclosureLatestMetricsDto>>(json)!;
        resp.StatusCode = status;
        var api = new Mock<IDeviceApiService>();
        api.Setup(a => a.GetAllEnclosureLatestMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(resp);
        return api;
    }

    // GET /api/enclosure-metrics 응답(REST §5.5.13) — 항목은 NATS body 와 같은 키, enclosure_id 는 바깥
    private const string LatestList = """
        { "success": true, "message": "All enclosure metrics retrieved successfully",
          "data": [
            { "enclosure_id": 394, "enclosure_name": "GOP-ENC-01",
              "latest_metrics": { "id": 90, "enclosure_id": 394, "temperature": 25.5, "humidity": 48.0, "voltage": 220.0, "current": 3.2,
                                  "vibration": 12, "ups_battery_level": 95, "ups_charging": true, "detail": null,
                                  "observed_at": "2026-09-12T12:30:00.000000+09:00", "created_at": "2026-09-12T12:15:34.400323+09:00" } },
            { "enclosure_id": 395, "enclosure_name": "GOP-ENC-02", "latest_metrics": null },
            { "enclosure_id": 396, "enclosure_name": "GOP-ENC-03",
              "latest_metrics": { "id": 77, "temperature": 30.0, "observed_at": "2026-09-12T12:29:00+09:00" } }
          ],
          "pagination": null, "meta": { "timestamp": "2026-09-12T12:31:00+09:00", "request_id": "x" } }
        """;

    [Fact]
    public async Task should_fill_latest_from_rest_once_on_start_and_skip_null()
    {
        var api = FakeApi(200, LatestList);
        var (service, publish) = MakeService(api.Object);
        var filled = new List<int>();
        service.MetricsReceived += r => filled.Add(r.EnclosureId);

        await service.StartService();
        for (var i = 0; i < 50 && filled.Count < 2; i++) await Task.Delay(10);   // 기동 조회는 기다리지 않고 돈다

        api.Verify(a => a.GetAllEnclosureLatestMetricsAsync(It.IsAny<CancellationToken>()), Times.Once());
        Assert.True(service.TryGetLatest(394, out var a));
        Assert.Equal(90, a.MetricId);
        Assert.Equal(25.5, a.Temperature);
        Assert.Equal(ObservedLocal, a.MeasuredAt);
        Assert.False(service.TryGetLatest(395, out _));   // latest_metrics:null 은 건너뜀
        Assert.True(service.TryGetLatest(396, out var c));   // 항목 안에 enclosure_id 가 없어도 바깥 값으로
        Assert.Equal(30.0, c.Temperature);
        Assert.Equal(new[] { 394, 396 }, filled.OrderBy(x => x));

        // 같은 행이 NATS 로 다시 와도 알리지 않는다
        await publish(Metrics);
        Assert.Equal(2, filled.Count);

        // 다시 채우기 — NATS 로 받은 더 새 값은 옛 REST 값으로 덮지 않는다
        await publish(MetricsRow(95, "2026-09-12T12:40:00.000000+09:00"));
        Assert.Equal(0, await service.RefreshFromServerAsync());
        Assert.True(service.TryGetLatest(394, out a));
        Assert.Equal(95, a.MetricId);
    }

    [Theory]
    [InlineData(404)]   // 이 경로가 없는 옛 서버
    [InlineData(403)]
    [InlineData(500)]
    public async Task should_ignore_rest_failure_quietly(int status)
    {
        var (service, _) = MakeService(FakeApi(status, """{"success":false,"message":"Not Found"}""").Object);
        Assert.Equal(0, await service.RefreshFromServerAsync());
        Assert.False(service.TryGetLatest(394, out _));

        // 서버에 닿지 못해 예외가 나도 삼킨다
        var throwing = new Mock<IDeviceApiService>();
        throwing.Setup(s => s.GetAllEnclosureLatestMetricsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));
        var (service2, _) = MakeService(throwing.Object);
        Assert.Equal(0, await service2.RefreshFromServerAsync());
    }

    [Fact]
    public async Task should_skip_rest_fill_without_source()
    {
        var (service, _) = MakeService();
        Assert.Equal(0, await service.RefreshFromServerAsync());
    }

    private static List<(string, string)> Fields(SymbolDetailViewModel vm)
        => vm.Tabs.Single(t => t.Tab == SymbolDetailTab.Basic).Fields.Select(f => (f.Label, f.Value)).ToList();
}
