using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
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
   Purpose      : 함체 환경 · 부품 판정 경고(이슈 #7 Q-4a) — NATS OPERATION_EVENT → 함체 심볼 위 가운데 삼각형 · 툴팁 줄,
                  그 이벤트의 ACTION_REPORT → 풀림. 창 없음.
   Created By   : Claude Code
   Created On   : 2026-10-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EnclosureAlertScenarioTests
{
    private static string Operation(int id, string reason, string severity, int enclosureId = 394) => $$"""
        { "id": "o-{{id}}", "m_type": "PUB", "cmd": "OPERATION_EVENT", "from": "DBApi",
          "body": { "id": {{id}}, "category_event": "operation", "type_event": "Operation", "action_reported": false,
                    "reason": "{{reason}}", "severity": "{{severity}}", "device": { "id": {{enclosureId}}, "category_device": "enclosure" },
                    "device_description": "[enclosure] 함체-394", "detail": { "field": "temperature", "value": 65, "threshold": 60, "direction": "HIGH" },
                    "created_at": "2026-10-15T12:00:00+09:00", "updated_at": "2026-10-15T12:00:00+09:00" } }
        """;

    private static string Report(int originId) => $$"""
        { "id": "a-{{originId}}", "m_type": "PUB", "cmd": "ACTION_REPORT", "from": "DBApi",
          "body": { "id": 900, "type_event": "Action", "content": "운영상태 확인", "user": "op01",
                    "from_event": { "id": {{originId}}, "category_event": "operation", "reason": "ENCLOSURE_TEMP_HIGH" } } }
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

    private static ComponentStatusOverlay FindOverlay(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ComponentStatusOverlay found) return found;
            try { return FindOverlay(child); } catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("ComponentStatusOverlay 없음");
    }

    [Fact]
    public void should_raise_warning_on_enclosure_symbol_for_environment_event_and_clear_it_on_action_report() => HousingTests.Sta(() =>
    {
        var log = new Mock<ILogService>();
        var handlers = new List<Func<MessageArgsModel, Task>>();
        var nats = new Mock<INatsService>();
        nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => handlers.Add(h));
        var service = new EnclosureMetricsNatsSyncService(log.Object, nats.Object);
        service.StartService();
        void Receive(string subject, string json) { foreach (var h in handlers) h(new MessageArgsModel(subject, null, json)).GetAwaiter().GetResult(); }

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
        var overlay = FindOverlay(control);

        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(vm, map);
        SetField(vm, "_log", log.Object);
        SetField(vm, "_enclosureMetricsSync", service);
        var apply = typeof(MapViewModel).GetMethod("ApplyEnclosureMetrics", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Apply() => apply.Invoke(vm, new object?[] { null });

        // 받기 전 — 경고 없음
        Apply();
        Assert.Null(marker.EnclosureAlert);
        Assert.False(overlay.IsAlertDrawn);

        // 개폐 · 정보 등급 · 다른 함체는 이 함체 심볼에 경고를 걸지 않는다
        Receive("sensorway.unit001.all.event.operation", Operation(10, "ENCLOSURE_DOOR_OPEN", "WARNING"));
        Receive("sensorway.unit001.all.event.operation", Operation(11, "ENCLOSURE_HUMIDITY_HIGH", "INFO"));
        Receive("sensorway.unit001.all.event.operation", Operation(12, "ENCLOSURE_TEMP_HIGH", "CRITICAL", enclosureId: 395));
        Apply();
        Assert.Null(marker.EnclosureAlert);

        // 함체 환경 운영 이벤트(경고) → 심볼 위 경고 삼각형 + 툴팁 줄
        Receive("sensorway.unit001.all.event.operation", Operation(20, "ENCLOSURE_VOLTAGE_LOW", "WARNING"));
        Apply();
        Assert.True(overlay.IsAlertDrawn);
        Assert.False(marker.EnclosureAlert!.IsCritical);
        Assert.Contains("환경 경고: 함체 저전압(경고)", control.SymbolToolTip!.Split(Environment.NewLine));

        // 더 급한 것(심각)이 오면 그것이 앞선다 — 등급은 severity
        Receive("sensorway.unit001.all.event.operation", Operation(21, "ENCLOSURE_TEMP_HIGH", "CRITICAL"));
        Apply();
        Assert.True(marker.EnclosureAlert!.IsCritical);
        Assert.Contains("환경 경고: 함체 고온(심각) 외 1건", control.SymbolToolTip!.Split(Environment.NewLine));

        // 조치보고(운영 원본) → 하나씩 풀린다
        Receive("sensorway.unit001.all.event.action", Report(21));
        Apply();
        Assert.Equal("함체 저전압", marker.EnclosureAlert!.ReasonText);
        Receive("sensorway.unit001.all.event.action", Report(20));
        Apply();
        Assert.Null(marker.EnclosureAlert);
        Assert.False(overlay.IsAlertDrawn);
        Assert.DoesNotContain("환경 경고", control.SymbolToolTip ?? string.Empty);
    });
}
