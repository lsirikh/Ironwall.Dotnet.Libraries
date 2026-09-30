using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>정적 카탈로그 출처(<see cref="MapComponentCatalog"/>)를 바꾸는 시험은 다른 시험과 동시에 돌지 않는다.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MapCatalogStaticCollection
{
    public const string Name = "MapComponentCatalogStatic";
}

/// <summary>
/// 지도 쪽 배선 — component-display-unify FR-04 · FR-05 · FR-07 리뷰 반영:
/// 밀도 재계산(뷰포트 · SYNC_DEVICE) · 지도 → 심볼 → 부품 층 값 흐름 · 조립 카드 수명(누름/뗌 · 팬 · Esc) ·
/// 카탈로그 한글 출처(성공 · 실패 폴백) · 카테고리 대표 칸 · 라벨 피하기.
/// </summary>
[Collection(MapCatalogStaticCollection.Name)]
public class ComponentMapWiringTests
{
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static DeviceAxesModel Axes(bool received = true, string health = "OK")
        => ComponentHealthSummaryTests.Axes(received,
            ("ptz", "PTZ_UNIT", null, null, "IDLE", health, null, true),
            ("ir", "IR_LED", null, null, "ON", "OK", null, true));

    private static GMapPidsMarker Camera(int id, double size = 60, DeviceAxesModel? axes = null, string? title = null)
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = title ?? $"cam{id}", DeviceType = EnumDeviceType.IpCamera, Zoom = 1 });
        marker.Width = size; marker.Height = size; marker.IsLayerEnabled = true; marker.IsVisible = true;
        marker.LinkedDevice = new CameraDeviceModel { Id = id, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED, Axes = axes ?? Axes() };
        return marker;
    }

    private static GMapCustomControl MapWith(int count, double size = 60)
    {
        var map = new GMapCustomControl { Width = 640, Height = 480, Zoom = 18 };
        for (int i = 0; i < count; i++) map.Markers.Add(Camera(100 + i, size));
        foreach (var marker in map.Markers.OfType<GMapPidsMarker>()) { marker.IsLayerEnabled = true; marker.IsVisible = true; }
        return map;
    }

    // ── 밀도 · 값 흐름 ──

    [Fact]
    public void should_push_crowded_flag_to_every_marker_when_more_than_thirty_large_icons_are_counted() => HousingTests.Sta(() =>
    {
        var map = MapWith(31);
        map.RefreshComponentStripDensity();

        Assert.True(map.IsComponentStripCrowded);
        Assert.All(map.Markers.OfType<GMapPidsMarker>(), m => Assert.True(m.ComponentStripCrowded));

        var late = Camera(999);
        map.Markers.Add(late);
        Assert.True(late.ComponentStripCrowded);                                       // 들어올 때 현재 값을 받는다
    });

    [Fact]
    public void should_recount_when_viewport_digital_zoom_changes() => HousingTests.Sta(() =>
    {
        var map = MapWith(31, size: 40);                                                // 40px × 1.0 = 칸 줄 대상 아님
        map.RefreshComponentStripDensity();
        Assert.False(map.IsComponentStripCrowded);

        map.DigitalZoomLevel = 2;                                                       // × 1.5 = 60px — 뷰포트 변경이 재계산을 예약한다
        foreach (var marker in map.Markers.OfType<GMapPidsMarker>()) { marker.IsLayerEnabled = true; marker.IsVisible = true; }
        Pump();

        Assert.True(map.IsComponentStripCrowded);
        Assert.All(map.Markers.OfType<GMapPidsMarker>(), m => Assert.True(m.ComponentStripCrowded));
    });

    [Fact]
    public void should_recount_without_viewport_change_when_sync_device_replaces_axes() => HousingTests.Sta(() =>
    {
        var map = MapWith(31);
        map.RefreshComponentStripDensity();
        Assert.True(map.IsComponentStripCrowded);

        // SYNC_DEVICE 재조회: 장비 모델의 축 묶음 참조가 통째로 갈리고 SetUpdate 가 온다 — 이 심볼은 관측 축을 잃었다(칸 줄 없음).
        var one = map.Markers.OfType<GMapPidsMarker>().First();
        one.LinkedDevice!.Axes = Axes(received: false);
        one.Model.SetUpdate();
        Pump();

        Assert.False(map.IsComponentStripCrowded);                                     // 31 → 30
        Assert.All(map.Markers.OfType<GMapPidsMarker>(), m => Assert.False(m.ComponentStripCrowded));
    });

    [Fact]
    public void should_rebuild_summary_only_when_axes_reference_changes_on_set_update() => HousingTests.Sta(() =>
    {
        using var marker = Camera(1);
        var first = marker.ComponentSummary;

        marker.Model.SetUpdate();                                                       // 같은 참조 — 다시 만들지 않는다
        Assert.Same(first, marker.ComponentSummary);

        marker.LinkedDevice!.Axes = Axes(health: "FAULT");                              // 새 참조(SYNC_DEVICE)
        marker.Model.SetUpdate();
        Assert.NotSame(first, marker.ComponentSummary);
        Assert.Equal(ComponentHealthLevel.Fault, marker.ComponentSummary.Health);
    });

    [Fact]
    public void should_flow_crowded_value_from_marker_into_template_overlay() => HousingTests.Sta(() =>
    {
        using var marker = Camera(7);
        var control = new GMapMarkerPidsControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        HousingTests.Layout(control, 60, 60);
        var overlay = FindOverlay(control);
        Assert.False(ComponentStatusOverlay.GetIsStripCrowded(overlay));

        marker.ComponentStripCrowded = true;                                            // 지도가 내려 준 값

        Assert.True(ComponentStatusOverlay.GetIsStripCrowded(control));
        Assert.True(ComponentStatusOverlay.GetIsStripCrowded(overlay));
        Assert.Equal(60, overlay.CurrentScreenPixels());                                // 밀도와 같은 크기 함수(심볼 크기 × 배율)
    });

    // ── 라벨 피하기 ──

    [Fact]
    public void should_place_strip_below_title_label_only_when_label_is_shown() => HousingTests.Sta(() =>
    {
        using var marker = Camera(8, size: 60, title: "북측 PTZ-07");
        var control = new GMapMarkerPidsControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        HousingTests.Layout(control, 60, 60);
        var overlay = FindOverlay(control);
        var defaultTop = 60 + ComponentStatusOverlay.Overhang + ComponentStatusOverlay.StripGap;

        marker.ShowTitle = false;
        Assert.Equal(defaultTop, overlay.StripTopFor(60, 60, 46));                      // 라벨 없음 — 아이콘 바로 아래

        marker.ShowTitle = true;
        var label = control.ComponentLabelBox;
        Assert.False(label.IsEmpty);
        var top = overlay.StripTopFor(60, 60, 46);
        Assert.Equal(60 / 2.0 + label.Bottom + ComponentStatusOverlay.StripGap, top, 6);  // 같은 라벨 계량의 아래 + 틈
        Assert.True(top > defaultTop);

        marker.LabelOffsetY = -80;                                                      // 라벨을 위로 옮겼다 — 겹치지 않으면 기본 자리
        Assert.Equal(defaultTop, overlay.StripTopFor(60, 60, 46));
    });

    [Theory]
    [InlineData(59, 18, 79)]     // 라벨(59..77)이 기본 자리(66..74)를 덮는다 → 라벨 아래 + 틈 2
    [InlineData(100, 18, 66)]    // 라벨이 멀리 아래 — 겹치지 않는다 → 기본
    [InlineData(20, 10, 66)]     // 라벨이 위에 있다 → 기본
    public void should_move_strip_under_label_only_when_boxes_intersect(double labelTop, double labelHeight, double expected)
    {
        var strip = new Rect(0, 66, 46, 8);
        Assert.Equal(expected, ComponentStripRules.StripTop(strip, new Rect(-10, labelTop, 70, labelHeight), 2));
        Assert.Equal(66, ComponentStripRules.StripTop(strip, Rect.Empty, 2));
    }

    // ── 카테고리 대표 칸 ──

    [Theory]
    [InlineData(EnumDeviceType.Enclosure, "DOOR_SENSOR,DOOR_LOCK,HEATER,FAN")]
    [InlineData(EnumDeviceType.Gate, "DOOR_ACTUATOR,DOOR_SENSOR,DOOR_LOCK,LIMIT_SWITCH")]
    [InlineData(EnumDeviceType.IpCamera, "PTZ_UNIT,TRACKER,IR_LED,WIPER")]
    [InlineData(EnumDeviceType.SmartSensor2, "VIBRATION_SENSOR,PIR_SENSOR,RADAR_UNIT,EO_CAMERA")]
    [InlineData(EnumDeviceType.SmartMultisensor2, "VIBRATION_SENSOR,PIR_SENSOR,RADAR_UNIT,EO_CAMERA")]
    [InlineData(EnumDeviceType.SmartSensor, "VIBRATION_SENSOR,ULTRASONIC_SENSOR,PIR_SENSOR,UPS")]
    [InlineData(EnumDeviceType.Multi, "VIBRATION_SENSOR,PIR_SENSOR,THERMAL_CAMERA,UPS")]
    [InlineData(EnumDeviceType.Fence, "VIBRATION_SENSOR,UPS,FAN,HEATER")]
    [InlineData(EnumDeviceType.Lamp, "LAMP_LIGHT,BUZZER,UPS,FAN")]
    [InlineData(EnumDeviceType.IpSpeaker, "AMPLIFIER,MIC,UPS,FAN")]
    [InlineData(EnumDeviceType.Controller, "NETWORK_INTERFACE,CONTACT_INPUT,UPS,FAN")]
    [InlineData(EnumDeviceType.PIR, "UPS,FAN,HEATER,WIPER")]                       // 표에 없는 종류 — 선언 순서
    public void should_follow_category_representative_table_when_nothing_is_faulted(EnumDeviceType type, string expected)
    {
        // 표에 든 유형을 거꾸로 · 섞어 선언해 선언 순서가 아니라 표 순서임을 확인한다(표 밖 UPS · FAN · HEATER · WIPER 는 선언 순서로 뒤에).
        var declared = new[] { "UPS", "FAN", "HEATER", "WIPER", "EO_CAMERA", "RADAR_UNIT", "LIMIT_SWITCH", "DOOR_LOCK", "IR_LED", "TRACKER",
            "THERMAL_CAMERA", "ULTRASONIC_SENSOR", "PIR_SENSOR", "DOOR_SENSOR", "DOOR_ACTUATOR", "PTZ_UNIT", "VIBRATION_SENSOR",
            "BUZZER", "LAMP_LIGHT", "MIC", "AMPLIFIER", "CONTACT_INPUT", "NETWORK_INTERFACE" };
        var snapshot = ComponentSnapshot.Build(ComponentHealthSummaryTests.Axes(true,
            declared.Select(t => (t.ToLowerInvariant(), t, (string?)null, (bool?)null, (string?)null, (string?)"OK", (string?)null, true)).ToArray()));

        // 대표 표 밖 유형은 대표 칸 뒤에 선언 순서(UPS · FAN · HEATER · WIPER …)로 채운다.
        var strip = ComponentStripRules.Build(snapshot, type);
        var actual = strip.Chips.Select(c => snapshot.Rows.Single(r => r.Key == c.Key).Type).ToArray();
        Assert.Equal(expected.Split(','), actual);
        Assert.Equal(declared.Length - 4, strip.MoreCount);
    }

    [Fact]
    public void should_put_faults_first_then_representatives_when_a_non_representative_part_faults()
    {
        var snapshot = ComponentSnapshot.Build(ComponentHealthSummaryTests.Axes(true,
            ("ups", "UPS", null, null, null, "FAULT", "POWER_LOSS", true),
            ("fan", "FAN", null, null, "ON", "OK", null, true),
            ("heater", "HEATER", null, null, "OFF", "DEGRADED", null, true),
            ("lock", "DOOR_LOCK", null, null, "LOCKED", "OK", null, true),
            ("door", "DOOR_SENSOR", null, null, "CLOSED", "OK", null, true),
            ("spare", "DOOR_SENSOR", null, false, null, "OK", null, true)));   // 사용 안 함 · 두 번째 문 센서

        var keys = ComponentStripRules.Build(snapshot, EnumDeviceType.Enclosure).Chips.Select(c => c.Key);

        Assert.Equal(new[] { "ups", "heater", "door", "lock" }, keys);           // 고장 → 저하 → 대표 순서(문 · 잠금 · [히터] · 팬)
    }

    // ── 카탈로그 한글 출처 ──

    [Fact]
    public void should_use_injected_catalog_labels_and_fall_back_to_builtin_when_catalog_fails() => HousingTests.Sta(() =>
    {
        try
        {
            MapComponentCatalog.Use(new Lazy<IComponentTypeLabels>(() => new Labels(("PTZ_UNIT", "팬틸트 구동부"))));
            using (var withCatalog = Camera(1))
                Assert.Equal("팬틸트 구동부", withCatalog.ComponentSummary.Snapshot.Rows[0].Name);

            MapComponentCatalog.Use(new Lazy<IComponentTypeLabels>(() => throw new InvalidOperationException("catalog down")));
            Assert.Null(MapComponentCatalog.Labels);
            using (var fallback = Camera(2))
                Assert.Equal("PTZ 구동부", fallback.ComponentSummary.Snapshot.Rows[0].Name);   // 내장 사전
        }
        finally { MapComponentCatalog.Reset(); }
    });

    [Fact]
    public void should_keep_retrying_until_catalog_becomes_available_when_first_lookup_is_null() => HousingTests.Sta(() =>
    {
        // 컨테이너 찾기(기본 출처)는 값을 얻을 때까지 매번 다시 찾는다 — 부팅 전(IoC 미준비)에 한 번 null 이었다고 굳히지 않는다.
        var previous = Caliburn.Micro.IoC.GetAllInstances;
        try
        {
            Caliburn.Micro.IoC.GetAllInstances = _ => Enumerable.Empty<object>();
            Assert.Null(MapComponentCatalog.Labels);
            var ready = new Labels(("PTZ_UNIT", "구동부(카탈로그)"));
            Caliburn.Micro.IoC.GetAllInstances = t => t == typeof(IComponentTypeLabels) ? new object[] { ready } : Enumerable.Empty<object>();
            Assert.Same(ready, MapComponentCatalog.Labels);
        }
        finally { Caliburn.Micro.IoC.GetAllInstances = previous; MapComponentCatalog.Reset(); }
    });

    // ── 조립 카드 수명(누름 · 뗌) ──

    [Fact]
    public void should_run_card_lifecycle_on_release_without_stealing_pan() => HousingTests.Sta(() =>
    {
        var host = new FakeHost();
        var service = new ComponentCardService(host);
        using var marker = Camera(1);

        // 심볼 위에서 시작한 팬 — 열지 않는다(드래그 통지 · 데드존 밖 둘 다)
        service.OnMarkerPressed(marker, new Point(100, 100)); service.OnMapDragged(); service.OnReleased(new Point(101, 100));
        Assert.Null(host.Current);
        service.OnMarkerPressed(marker, new Point(100, 100)); service.OnReleased(new Point(109, 100));
        Assert.Null(host.Current);

        // 심볼 클릭(데드존 안에서 뗌) — 연다
        service.OnMarkerPressed(marker, new Point(100, 100)); service.OnReleased(new Point(104, 103));
        Assert.Same(marker, host.Current);
        Assert.True(marker.IsComponentCardOpen);

        // 빈 곳에서 시작한 팬 — 카드는 그대로(지도를 따라간다)
        service.OnEmptyPressed(new Point(300, 300)); service.OnMapDragged(); service.OnReleased(new Point(420, 330));
        Assert.Same(marker, host.Current);
        service.OnEmptyPressed(new Point(300, 300)); service.OnReleased(new Point(320, 300));   // 움직임만으로도 팬
        Assert.Same(marker, host.Current);

        // 휠 줌 — 누름이 없고 선택 해제 통지만 온다
        marker.IsSelected = false;
        service.OnReleased(new Point(0, 0));
        Assert.Same(marker, host.Current);
        Assert.Equal(0, host.CloseCount);

        // Esc — 닫는다
        service.OnEscape();
        Assert.Null(host.Current);
        Assert.False(marker.IsComponentCardOpen);

        // 다시 열고 빈 곳 클릭(움직임 없음) — 닫는다
        service.OnMarkerPressed(marker, new Point(10, 10)); service.OnReleased(new Point(10, 10));
        Assert.Same(marker, host.Current);
        service.OnEmptyPressed(new Point(300, 300)); service.OnReleased(new Point(300, 300));
        Assert.Null(host.Current);
    });

    [Fact]
    public void should_drop_stale_press_candidate_when_press_is_cancelled_or_image_is_clicked() => HousingTests.Sta(() =>
    {
        var host = new FakeHost();
        var service = new ComponentCardService(host);
        using var marker = Camera(1);

        service.OnMarkerPressed(marker, new Point(10, 10)); service.OnPressCancelled();   // 지도 밖으로 · 캡처 상실
        service.OnReleased(new Point(10, 10));
        Assert.Null(host.Current);

        service.OnMarkerPressed(marker, new Point(10, 10)); service.OnOtherPressed();     // 오버레이 이미지 누름
        service.OnReleased(new Point(10, 10));
        Assert.Null(host.Current);
    });

    [Fact]
    public void should_not_open_card_for_legacy_device_and_close_existing_card_when_clicked() => HousingTests.Sta(() =>
    {
        var host = new FakeHost();
        var service = new ComponentCardService(host);
        using var marker = Camera(1);
        using var legacy = Camera(2);
        legacy.LinkedDevice!.Axes = null;                                                // 6.3 — 축 없음
        legacy.RefreshComponentSummary(force: true);

        service.OnMarkerPressed(marker, new Point(10, 10)); service.OnReleased(new Point(10, 10));
        Assert.Same(marker, host.Current);
        service.OnMarkerPressed(legacy, new Point(50, 50)); service.OnReleased(new Point(50, 50));
        Assert.Null(host.Current);
    });

    // ── helpers ──

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

    private sealed class FakeHost : IComponentCardHost
    {
        public GMapPidsMarker? Current { get; private set; }
        public int CloseCount { get; private set; }
        public void Open(GMapPidsMarker marker, Action onCloseRequested) => Current = marker;
        public void Close() { Current = null; CloseCount++; }
        public void Refresh() { }
        public void Relayout() { }
    }

    private sealed class Labels : IComponentTypeLabels
    {
        private readonly Dictionary<string, string> _labels;
        public Labels(params (string Code, string Label)[] labels) => _labels = labels.ToDictionary(l => l.Code, l => l.Label, StringComparer.OrdinalIgnoreCase);
        public string? TypeLabel(string? type) => type != null && _labels.TryGetValue(type, out var label) ? label : null;
    }
}
