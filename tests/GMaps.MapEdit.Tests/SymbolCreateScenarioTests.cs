using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 카탈로그 §1 MAP-093(심볼 · 끌어 만들기) · MAP-094(심볼 · 클릭 만들기) — 「놓은/클릭한 곳에 심볼 · DB 행」.
/// 실제 지도(AdornerDecorator 안, 창 없음) · 실제 VM 배선(SetupAdornerIntegration) · 실제 마커 팩토리 · 실제 팔레트 뷰.
/// DB 행 = 가짜 심볼 DB(Moq)의 Insert 호출 인자(좌표 · 종류 · 제목).
/// 끌기: OLE 끌기 고리(DragDrop.DoDragDrop)는 창 · 실제 마우스가 필요해 부르지 않고, 팔레트가 끌기 직전에 하는 일(BeginPalettePlacement)과
/// 지도에 도착하는 DragOver/Drop 라우트 이벤트(같은 데이터 형식 SymbolPaletteDrag · 같은 좌표 변환)를 그대로 낸다.
/// 클릭: 지도 OnMouseLeftButtonDown 의 배치 분기가 내는 SymbolPlacementClicked(SymbolPlacementLocation(점), 점)를 똑같이 낸다.
/// 헤디드 몫: 실제 마우스 끌기 손동작 · 커서 모양(⃠/복사) · 끌기 고스트 그림.
/// </summary>
public class SymbolCreateScenarioTests
{
    // ───────────────────────── 공용 ─────────────────────────

    private static SymbolPaletteItem Item(EnumMarkerCategory category, Func<SymbolPaletteItem, bool>? pick = null)
        => SymbolPaletteItem.All.First(x => x.Category == category && (pick?.Invoke(x) ?? true));

    /// <summary>가짜 DB — Insert 로 받은 기하 심볼에 id 를 붙이고 Fetch 로 그대로 돌려준다(DB 왕복 흉내).</summary>
    private static List<IGeometricSymbolModel> FakeGeometryDb(Mock<IGMapDbSymbolService> db, int id)
    {
        var rows = new List<IGeometricSymbolModel>();
        db.Setup(d => d.InsertGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
          .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => { m.Id = id + rows.Count; rows.Add(m); })
          .ReturnsAsync(() => rows[^1].Id);
        db.Setup(d => d.FetchGeometrySymbolAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((int i, CancellationToken _) => rows.FirstOrDefault(r => r.Id == i));
        return rows;
    }

    private static List<IPidsSymbolModel> FakePidsDb(Mock<IGMapDbSymbolService> db, int id)
    {
        var rows = new List<IPidsSymbolModel>();
        db.Setup(d => d.InsertPidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
          .Callback<IPidsSymbolModel, CancellationToken>((m, _) => { m.Id = id + rows.Count; rows.Add(m); })
          .ReturnsAsync(() => rows[^1].Id);
        db.Setup(d => d.FetchPidsSymbolAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((int i, CancellationToken _) => rows.FirstOrDefault(r => r.Id == i));
        return rows;
    }

    /// <summary>팔레트가 DoDragDrop 에 싣는 것과 같은 데이터(내부 형식 SymbolPaletteDrag(항목, 지도)).</summary>
    private static DataObject DragData(SymbolPaletteItem item, GMapCustomControl map)
    {
        var type = typeof(SymbolPaletteView).Assembly.GetType("Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps.SymbolPaletteDrag", throwOnError: true)!;
        return new DataObject(type, Activator.CreateInstance(type, item, map)!);
    }

    /// <summary>OLE 끌기 고리가 지도에 내는 DragEventArgs(대상 = 지도, 점 = 지도 좌표) — 생성자가 internal 이라 리플렉션으로 만든다.</summary>
    private static DragEventArgs DragArgs(IDataObject data, GMapCustomControl map, Point point, RoutedEvent routed)
    {
        var ctor = typeof(DragEventArgs).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point) }, null)!;
        var e = (DragEventArgs)ctor.Invoke(new object[] { data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Copy, map, point });
        e.RoutedEvent = routed;
        return e;
    }

    private static void AssertNear(PointLatLng want, double lat, double lng)
    {
        Assert.Equal(want.Lat, lat, 9);
        Assert.Equal(want.Lng, lng, 9);
    }

    /// <summary>새로 생긴 마커의 화면 위치가 놓은 점과 1px 안인가.</summary>
    private static void AssertAtScreen(GMapCustomControl map, IEditableMarker marker, Point point)
    {
        var px = map.FromLatLngToLocal(marker.Position);
        Assert.InRange(px.X, point.X - 1.5, point.X + 1.5);
        Assert.InRange(px.Y, point.Y - 1.5, point.Y + 1.5);
    }

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var d in Tree(VisualTreeHelper.GetChild(root, i))) yield return d;
    }

    private static SymbolPaletteView Palette(WiredMapRig rig)
    {
        var dict = new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolPaletteStyle.xaml", UriKind.Relative) };
        var view = new SymbolPaletteView { DataContext = rig.Vm };
        view.Resources.MergedDictionaries.Add(dict);
        view.Style = (Style)dict[typeof(SymbolPaletteView)];
        view.ApplyTemplate();
        Scenario.Layout(view, 384, 580);
        return view;
    }

    // ───────────────────────── MAP-093 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-093")]
    public void should_create_symbol_at_drop_point_and_insert_db_row_when_palette_cell_is_dropped_on_empty_map() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var rows = FakeGeometryDb(rig.Db, 9301);
        var item = Item(EnumMarkerCategory.GEOMETRICS);
        int before = rig.Map.Markers.Count;
        var point = new Point(812.4, 233.7);   // 지도 안 빈 곳(중심 아님 · 소수 좌표)

        // 팔레트 셀 끌기 시작 = BeginPalettePlacement(팔레트 ItemMouseMove 가 DoDragDrop 직전에 부르는 것)
        Assert.True(rig.Vm.BeginPalettePlacement(item));
        Assert.True(rig.Map.IsSymbolPlacementMode);
        var data = DragData(item, rig.Map);

        // 지도 위를 지남 → 복사 허용
        var over = DragArgs(data, rig.Map, point, DragDrop.DragOverEvent);
        rig.Map.RaiseEvent(over);
        Assert.True(over.Handled);
        Assert.Equal(DragDropEffects.Copy, over.Effects);

        // 놓음
        var drop = DragArgs(data, rig.Map, point, DragDrop.DropEvent);
        rig.Map.RaiseEvent(drop);
        await Task.Delay(50);

        // DB 행 하나 — 놓은 점의 위경도 · 고른 종류 · 제목
        var row = Assert.Single(rows);
        rig.Db.Verify(d => d.InsertGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()), Times.Once);
        var want = rig.Map.SymbolPlacementLocation(point);
        AssertNear(want, row.Latitude, row.Longitude);
        Assert.Equal(item.Type, row.ShapeType);
        Assert.Equal(item.Title, row.Title);
        Assert.Equal(EnumMarkerCategory.GEOMETRICS, row.Category);

        // 지도에 심볼 — 그 행(id)으로 만든 마커가 놓은 화면 점에 있다
        Assert.Equal(before + 1, rig.Map.Markers.Count);
        var marker = Assert.Single(rig.Map.Markers.OfType<IEditableMarker>(), m => m.Id == row.Id);
        Assert.IsType<GMapGeometricMarker>(marker);
        AssertAtScreen(rig.Map, marker, point);
        Assert.Equal(item.Title, marker.Title);

        // 배치 모드는 한 번 놓으면 끝, 추가는 되돌릴 수 있다
        Assert.False(rig.Map.IsSymbolPlacementMode);
        Assert.True(rig.Vm.CanUndo);
        Assert.Empty(rig.Errors);
        Mouse.OverrideCursor = null;
    });

    [Fact]
    [Trait("Scenario", "MAP-093")]
    public void should_insert_pids_row_with_device_type_and_variant_at_drop_point_when_pids_cell_is_dropped() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var rows = FakePidsDb(rig.Db, 9311);
        var item = Item(EnumMarkerCategory.PIDS_EQUIPMENT, x => x.Type is EnumDeviceType.IpCamera);
        var point = new Point(150.25, 640.5);

        Assert.True(rig.Vm.BeginPalettePlacement(item));
        rig.Map.RaiseEvent(DragArgs(DragData(item, rig.Map), rig.Map, point, DragDrop.DropEvent));
        await Task.Delay(50);

        var row = Assert.Single(rows);
        AssertNear(rig.Map.SymbolPlacementLocation(point), row.Latitude, row.Longitude);
        Assert.Equal(EnumDeviceType.IpCamera, row.DeviceType);
        Assert.Equal(item.ModelVariant, row.ModelVariant);
        Assert.Equal(item.Title, row.Title);
        Assert.Equal(0, row.LinkedDeviceId);   // 새 심볼은 장비 미연결
        var marker = Assert.Single(rig.Map.Markers.OfType<GMapPidsMarker>(), m => m.Id == row.Id);
        AssertAtScreen(rig.Map, marker, point);
        Assert.Empty(rig.Errors);
        Mouse.OverrideCursor = null;
    });

    [Fact]
    [Trait("Scenario", "MAP-093")]
    public void should_not_insert_row_when_drop_lands_outside_map_or_placement_was_not_started() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var rows = FakeGeometryDb(rig.Db, 9321);
        var item = Item(EnumMarkerCategory.GEOMETRICS);
        int before = rig.Map.Markers.Count;

        // ① 배치 시작 없이 도착한 끌기(다른 지도 · 늦은 드롭 등) → 거부
        var over = DragArgs(DragData(item, rig.Map), rig.Map, new Point(400, 300), DragDrop.DragOverEvent);
        rig.Map.RaiseEvent(over);
        Assert.Equal(DragDropEffects.None, over.Effects);
        rig.Map.RaiseEvent(DragArgs(DragData(item, rig.Map), rig.Map, new Point(400, 300), DragDrop.DropEvent));
        await Task.Delay(30);
        Assert.Empty(rows);

        // ② 배치 중이어도 지도 밖 점 → 거부(행 · 마커 없음)
        Assert.True(rig.Vm.BeginPalettePlacement(item));
        rig.Map.RaiseEvent(DragArgs(DragData(item, rig.Map), rig.Map, new Point(1300, 300), DragDrop.DropEvent));
        await Task.Delay(30);
        Assert.Empty(rows);
        Assert.Equal(before, rig.Map.Markers.Count);
        rig.Vm.CancelPalettePlacement();
        Mouse.OverrideCursor = null;
    });

    // ───────────────────────── MAP-094 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-094")]
    public void should_create_symbol_at_clicked_point_and_insert_db_row_when_cell_is_selected_then_map_is_clicked() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var rows = FakeGeometryDb(rig.Db, 9401);
        rig.AssertWired("SymbolPlacementClicked", "OnSymbolPlacementClicked");   // 지도 배치 클릭 → VM 처리기(제품 배선)

        // 팔레트: 기하 탭 → 첫 셀을 눌렀다 뗌(팔레트 ItemMouseDown/Up 처리기)
        var view = Palette(rig);
        Tree(view).OfType<RadioButton>().Single(r => AutomationProperties.GetAutomationId(r) == "GMaps.SymbolPalette.Category.GEOMETRICS").IsChecked = true;
        Scenario.Layout(view, 384, 580);
        var items = (ListBox)view.Template.FindName("PART_Items", view)!;
        var item = (SymbolPaletteItem)items.Items[0];
        var cell = (ListBoxItem)items.ItemContainerGenerator.ContainerFromIndex(0);
        var cellText = Tree(cell).OfType<TextBlock>().First(t => t.Text == item.Title);
        // (Preview)MouseLeftButtonDown/Up 은 Direct 이벤트 — 터널 이벤트 Mouse.PreviewMouseDown/Up 을 내면 UIElement 가 경로의 각 요소에서 Left 판으로 다시 낸다(실입력과 같은 경로)
        cellText.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
        Assert.Same(item, Scenario.GetField<SymbolPaletteItem?>(view, "_pressed"));   // 팔레트 ItemMouseDown 이 그 셀을 잡았다
        cellText.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent });

        Assert.True(rig.Map.IsSymbolPlacementMode, "셀을 누르면 배치 모드여야 한다");
        Assert.Equal($"클릭한 위치에 '{item.Title}' 배치 — (ESC = 취소)", rig.Vm.AimStatusMessage);
        Assert.Empty(rows);   // 셀 선택만으로는 행이 생기지 않는다

        // 지도 클릭(배치 분기와 같은 인자)
        var point = new Point(321.6, 507.3);
        rig.Raise("SymbolPlacementClicked", rig.Map.SymbolPlacementLocation(point), point);
        await Task.Delay(50);

        var row = Assert.Single(rows);
        AssertNear(rig.Map.SymbolPlacementLocation(point), row.Latitude, row.Longitude);
        Assert.Equal(item.Type, row.ShapeType);
        Assert.Equal(item.Title, row.Title);
        var marker = Assert.Single(rig.Map.Markers.OfType<IEditableMarker>(), m => m.Id == row.Id);
        AssertAtScreen(rig.Map, marker, point);
        Assert.False(rig.Map.IsSymbolPlacementMode);   // 단발
        Assert.False(rig.Vm.IsAimStatusVisible);

        // 한 번 더 클릭해도(배치 끝남) 행이 더 생기지 않는다
        rig.Raise("SymbolPlacementClicked", rig.Map.SymbolPlacementLocation(new Point(600, 400)), new Point(600, 400));
        await Task.Delay(30);
        Assert.Single(rows);
        Assert.Empty(rig.Errors);
        Mouse.OverrideCursor = null;
    });

    [Fact]
    [Trait("Scenario", "MAP-094")]
    public void should_place_one_symbol_per_click_at_each_clicked_point_when_cell_is_reselected() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var rows = FakePidsDb(rig.Db, 9411);
        var item = Item(EnumMarkerCategory.PIDS_EQUIPMENT, x => x.Type is EnumDeviceType.Fence);
        var points = new[] { new Point(100.5, 100.5), new Point(1100.2, 90.8), new Point(600, 760.1) };   // 가장자리 근처 포함

        foreach (var p in points)
        {
            Assert.True(rig.Vm.BeginPalettePlacement(item));   // 셀 다시 고름(Enter/누름 공통 진입점)
            rig.Raise("SymbolPlacementClicked", rig.Map.SymbolPlacementLocation(p), p);
            await Task.Delay(30);
        }

        Assert.Equal(points.Length, rows.Count);
        for (int i = 0; i < points.Length; i++)
        {
            AssertNear(rig.Map.SymbolPlacementLocation(points[i]), rows[i].Latitude, rows[i].Longitude);
            Assert.Equal(EnumDeviceType.Fence, rows[i].DeviceType);
            var marker = Assert.Single(rig.Map.Markers.OfType<GMapPidsMarker>(), m => m.Id == rows[i].Id);
            AssertAtScreen(rig.Map, marker, points[i]);
        }
        Assert.Empty(rig.Errors);
        Mouse.OverrideCursor = null;
    });
}
