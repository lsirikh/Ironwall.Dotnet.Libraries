using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 카탈로그 §1 MAP-109~121 · 134~139 헤드리스 짝 — 속성창에서 바꾼 값이 마커(모델) → 지도 심볼 컨트롤(DP) → 그림(픽셀)까지
/// 즉시 내려가는지, 상태 변경(SYNC/되돌리기)이 사용자가 고른 색을 덮지 않는지, 다시 열거나 스냅샷으로 되살려도 같은지.
/// 창은 띄우지 않는다(부모 없는 Measure/Arrange + RenderTargetBitmap).
/// </summary>
public class MapSymbolReflectionTests
{
    private static ILogService Log() => Mock.Of<ILogService>();

    private static Color ColorOf(EnumColorType c) => ColorHelper.ToBrush(c).Color;
    private static Color ColorOf(Brush? b) => Assert.IsType<SolidColorBrush>(b).Color;

    private static GeometricSymbolModel Geometric(EnumColorType fill = EnumColorType.Blue) => new()
    {
        Title = "도형-검증", Latitude = 37.5, Longitude = 127.0, Width = 40, Height = 40, TitleSize = 12,
        FillColor = fill, StrokeColor = EnumColorType.Black, StrokeThickness = 1, ShapeType = EnumShapeType.Square, Opacity = 1.0,
        ShowShape = true, ShowTitle = true, Visible = true, OperationState = EnumOperationState.ACTIVATED,
    };

    private static GMapGeometricMarkerControl ControlOf(GMapGeometricMarker marker) => Assert.IsType<GMapGeometricMarkerControl>(marker.Shape);

    // ── MAP-115 · MAP-111 · MAP-112 · MAP-113 · MAP-114 · MAP-119 — 속성창 → 마커 → 심볼 컨트롤 ──

    [Trait("Scenario", "MAP-111,MAP-112,MAP-113,MAP-114,MAP-115,MAP-119")]
    [Fact]
    public void should_push_every_common_field_from_property_panel_to_map_symbol_control_immediately() => AppHost.Run(() =>
    {
        using var marker = new GMapGeometricMarker(Log(), Geometric());
        var control = ControlOf(marker);
        var panel = new GMapPropertyGeometricControl { SelectedMarker = marker };

        // Act — 운영자가 속성창 칸을 바꾼 것과 같은 DP 대입
        panel.MarkerFillColor = EnumColorType.Yellow;
        panel.MarkerStrokeColor = EnumColorType.Red;
        panel.MarkerStrokeThickness = 3;
        panel.MarkerTitle = "새 제목";
        panel.TitleSize = 22;
        panel.MarkerWidth = 64;
        panel.MarkerHeight = 48;
        panel.MarkerBearing = 60;
        panel.ShowShape = false;
        panel.ShowTitle = false;

        // Assert — 모델(영속 대상)과 지도 심볼 컨트롤(그림) 모두 같은 값
        var m = marker.Model;
        Assert.Equal(EnumColorType.Yellow, m.FillColor);
        Assert.Equal(EnumColorType.Red, m.StrokeColor);
        Assert.Equal(3, m.StrokeThickness);
        Assert.Equal("새 제목", m.Title);
        Assert.Equal(22, m.TitleSize);
        Assert.Equal(64, m.Width);
        Assert.Equal(48, m.Height);
        Assert.Equal(60, m.Bearing);
        Assert.False(m.ShowShape);
        Assert.False(m.ShowTitle);

        Assert.Equal(ColorOf(EnumColorType.Yellow), ColorOf(control.MarkerFill));
        Assert.Equal(ColorOf(EnumColorType.Red), ColorOf(control.MarkerStroke));
        Assert.Equal(3, control.MarkerStrokeThickness);
        Assert.Equal("새 제목", control.MarkerTitle);
        Assert.Equal(22, control.TitleSize);
        Assert.Equal(64, control.Width);
        Assert.Equal(48, control.Height);
        Assert.Equal(60, control.RotationAngle);
        Assert.False(control.ShowShape);
        Assert.False(control.ShowTitle);
        // 중심 기준 오프셋도 새 크기를 따른다(지도 위 위치가 튀지 않게)
        Assert.Equal(new Point(-32, -24), marker.Offset);
    });

    // ── MAP-116 · MAP-117 — 라벨 글꼴 · 색 · 폭 ──

    [Trait("Scenario", "MAP-116")]
    [Fact]
    public void should_push_label_style_fields_from_property_panel_to_marker_model() => AppHost.Run(() =>
    {
        using var marker = new GMapGeometricMarker(Log(), Geometric());
        var panel = new GMapPropertyGeometricControl { SelectedMarker = marker };
        var raised = new List<string>();
        marker.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        panel.TitleColorType = EnumColorType.Red;
        panel.TitleBackgroundType = EnumColorType.Yellow;
        panel.TitleFontFamilyName = "Malgun Gothic";
        panel.TitleBoldFlag = true;
        panel.TitleItalicFlag = true;
        panel.TitleMaxWidthPx = 60;

        var m = marker.Model;
        Assert.Equal(EnumColorType.Red, m.TitleColor);
        Assert.Equal(EnumColorType.Yellow, m.TitleBackground);
        Assert.Equal("Malgun Gothic", m.TitleFontFamily);
        Assert.True(m.TitleBold);
        Assert.True(m.TitleItalic);
        Assert.Equal(60, m.TitleMaxWidth);
        // 라벨 어도너는 마커 INPC 로 다시 그린다 — 여섯 가지 모두 통지돼야 즉시 반영
        foreach (var name in new[] { "TitleColor", "TitleBackground", "TitleFontFamily", "TitleBold", "TitleItalic", "TitleMaxWidth" })
            Assert.Contains(name, raised);
    });

    [Fact]
    public void should_not_leak_pending_blanks_to_members_or_events_when_group_panel_is_built() => AppHost.Run(() =>
    {
        // 그룹 편집의 빈칸 표기(MIXED_COLOR · NaN · null)는 화면 표기일 뿐 — 구성원 모델 · 변경 이벤트로 새면 안 된다
        var a = Geometric(EnumColorType.Blue); var b = Geometric(EnumColorType.Red);
        a.TitleMaxWidth = 100; b.TitleMaxWidth = 200; b.ShowShape = false; b.TitleColor = EnumColorType.Red;
        using var ma = new GMapGeometricMarker(Log(), a);
        using var mb = new GMapGeometricMarker(Log(), b);
        var panel = new GMapPropertyGeometricControl { SelectedMarker = ma };
        var events = new List<string>();
        panel.MarkerPropertyChanged += (_, e) => events.Add(e.PropertyName);

        panel.EnterGroupMode(new IEditableMarker[] { ma, mb });
        panel.TitleColorType = (EnumColorType)(-1);
        panel.TitleMaxWidthPx = double.NaN;
        panel.ShowShape = null;

        Assert.Empty(events);
        Assert.Equal(EnumColorType.Blue, ma.FillColor); Assert.Equal(EnumColorType.Red, mb.FillColor);
        Assert.Equal(100, ma.TitleMaxWidth); Assert.Equal(200, mb.TitleMaxWidth);
        Assert.True(ma.ShowShape); Assert.False(mb.ShowShape);
    });

    // ── MAP-139 — 다시 열기(재구성) ──

    [Trait("Scenario", "MAP-139")]
    [Fact]
    public void should_show_same_values_when_property_panel_is_reopened_for_same_symbol() => AppHost.Run(() =>
    {
        using var marker = new GMapGeometricMarker(Log(), Geometric());
        var panel = new GMapPropertyGeometricControl { SelectedMarker = marker };
        panel.MarkerFillColor = EnumColorType.Purple;
        panel.TitleSize = 18;
        panel.TitleBoldFlag = true;
        panel.MarkerBearing = 45;

        panel.SelectedMarker = null!;   // 닫기(선택 해제)
        var reopened = new GMapPropertyGeometricControl { SelectedMarker = marker };

        Assert.Equal(EnumColorType.Purple, reopened.MarkerFillColor);
        Assert.Equal(18, reopened.TitleSize);
        Assert.True(reopened.TitleBoldFlag);
        Assert.Equal(45, reopened.MarkerBearing);
    });

    [Fact]
    public void should_keep_every_edited_field_when_symbol_is_restored_from_snapshot() => AppHost.Run(() =>
    {
        // 삭제 → 되돌리기(스냅샷 딥클론) 뒤 다시 만든 심볼도 같은 속성이어야 한다(MAP-106 · MAP-139)
        using var marker = new GMapGeometricMarker(Log(), Geometric());
        marker.FillColor = EnumColorType.Teal; marker.TitleColor = EnumColorType.Red; marker.TitleBold = true;
        marker.TitleMaxWidth = 77; marker.LabelOffsetX = 12; marker.LabelOffsetY = -8; marker.IsLocked = true; marker.Visible = false;
        marker.UpdateRotation(33); marker.UpdateSize(52, 36);

        var snapshot = SymbolSnapshot.Capture(marker)!;
        var clone = Assert.IsType<GeometricSymbolModel>(snapshot.CloneModel());
        using var restored = new GMapGeometricMarker(Log(), clone);
        var control = ControlOf(restored);

        Assert.Equal(EnumColorType.Teal, restored.FillColor);
        Assert.Equal(EnumColorType.Red, restored.TitleColor);
        Assert.True(restored.TitleBold);
        Assert.Equal(77, restored.TitleMaxWidth);
        Assert.Equal(12, restored.LabelOffsetX);
        Assert.Equal(-8, restored.LabelOffsetY);
        Assert.True(restored.IsLocked);
        Assert.False(restored.Visible);
        Assert.Equal(33, restored.Bearing);
        Assert.Equal(52, restored.Width);
        Assert.Equal(ColorOf(EnumColorType.Teal), ColorOf(control.MarkerFill));
    });

    // ── 결함 D1 — 상태 변경이 사용자 채우기 색을 덮고 이후 색 변경이 안 먹던 것 ──

    [Theory]
    [InlineData(EnumOperationState.DEACTIVATED)]
    [InlineData(EnumOperationState.ERROR)]
    [InlineData(EnumOperationState.NONE)]
    public void should_keep_user_fill_color_and_follow_later_edits_when_operation_state_changes(EnumOperationState state) => AppHost.Run(() =>
    {
        using var marker = new GMapGeometricMarker(Log(), Geometric(EnumColorType.Blue));
        var control = ControlOf(marker);
        Assert.Equal(ColorOf(EnumColorType.Blue), ColorOf(control.MarkerFill));

        marker.OperationState = state;            // 되돌리기 재생(UndoableCommandBase "OperationState") · 그룹 SYNC 와 같은 경로
        Assert.Equal(ColorOf(EnumColorType.Blue), ColorOf(control.MarkerFill));

        marker.FillColor = EnumColorType.Orange;  // 그 뒤 속성창에서 색을 바꾼다
        Assert.Equal(ColorOf(EnumColorType.Orange), ColorOf(control.MarkerFill));
    });

    [Fact]
    public void should_keep_zone_line_fill_binding_when_group_state_is_notified_by_sync() => AppHost.Run(() =>
    {
        var model = new PidsGroupSymbolModel { Title = "구역", Latitude = 37.5, Longitude = 127.0, FillColor = EnumColorType.Purple, OperationState = EnumOperationState.ACTIVATED };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
        using var marker = new GMapPidsGroupMarker(Log(), model);
        var control = Assert.IsAssignableFrom<GMapMarkerPidsGroupControl>(marker.Shape);

        model.OperationState = EnumOperationState.ERROR;   // SYNC_DEVICE → 모델 갱신 → PidsGroupModel_Update 가 OperationState 통지
        model.SetUpdate();
        AppHost.Layout(control, 10, 10);

        Assert.Equal(EnumOperationState.ERROR, control.MarkerState);
        marker.FillColor = EnumColorType.Yellow;
        Assert.Equal(ColorOf(EnumColorType.Yellow), ColorOf(control.MarkerFill));
    });

    // ── 픽셀 — 지도 심볼 그림이 실제로 바뀐다 ──

    [Trait("Scenario", "MAP-115,MAP-119")]
    [Fact]
    public void should_repaint_symbol_pixels_when_fill_color_changes_and_survive_state_change() => AppHost.Run(() =>
    {
        using var marker = new GMapGeometricMarker(Log(), Geometric(EnumColorType.Blue));
        var control = ControlOf(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/GeometricMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[typeof(GMapGeometricMarkerControl)];

        Assert.Equal(ColorOf(EnumColorType.Blue), CenterPixel(control));
        marker.FillColor = EnumColorType.Yellow;
        Assert.Equal(ColorOf(EnumColorType.Yellow), CenterPixel(control));
        marker.OperationState = EnumOperationState.DEACTIVATED;
        Assert.Equal(ColorOf(EnumColorType.Yellow), CenterPixel(control));   // D1 전에는 회색으로 바뀌었다
        marker.ShowShape = false;
        Assert.NotEqual(ColorOf(EnumColorType.Yellow), CenterPixel(control)); // 「모양」 끔 → 그림 사라짐
    });

    private static Color CenterPixel(FrameworkElement control)
    {
        AppHost.Layout(control, 40, 40);
        var bmp = new RenderTargetBitmap(40, 40, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(control);
        var px = new byte[4];
        // 한가운데는 흰 중심점(PART_CenterPoint 6px)이 덮으므로 도형 안쪽 · 테두리 밖 한 점을 본다
        bmp.CopyPixels(new Int32Rect(10, 10, 1, 1), px, 4, 0);
        return Color.FromArgb(px[3], px[2], px[1], px[0]);
    }

    // ── MAP-137 · MAP-134 — 그룹 편집 ──

    [Fact]
    public void should_show_blank_for_mixed_fields_and_value_for_shared_fields_when_group_editing() => AppHost.Run(() =>
    {
        var a = Geometric(EnumColorType.Blue); var b = Geometric(EnumColorType.Red);
        b.TitleSize = 12; b.Title = "다른 제목"; b.ShowTitle = false;
        using var ma = new GMapGeometricMarker(Log(), a);
        using var mb = new GMapGeometricMarker(Log(), b);
        var panel = new GMapPropertyGeometricControl { SelectedMarker = ma };
        panel.EnterGroupMode(new IEditableMarker[] { ma, mb });

        Assert.True(panel.IsGroupEditing);
        Assert.Equal((EnumColorType)(-1), panel.MarkerFillColor);   // 다름 → 빈칸
        Assert.Equal(string.Empty, panel.MarkerTitle);                // 다름 → 빈칸
        Assert.Null(panel.ShowTitle);                                  // 다름 → 3상태
        Assert.Equal(12, panel.TitleSize);                             // 같음 → 값
        Assert.Equal(EnumColorType.Black, panel.MarkerStrokeColor);    // 같음 → 값
    });

    [Trait("Scenario", "MAP-137")]
    [Fact]
    public void should_not_write_representative_directly_but_raise_event_when_group_field_changes() => AppHost.Run(() =>
    {
        using var ma = new GMapGeometricMarker(Log(), Geometric(EnumColorType.Blue));
        using var mb = new GMapGeometricMarker(Log(), Geometric(EnumColorType.Red));
        var panel = new GMapPropertyGeometricControl { SelectedMarker = ma };
        panel.EnterGroupMode(new IEditableMarker[] { ma, mb });
        var events = new List<(string Name, object? Value)>();
        panel.MarkerPropertyChanged += (_, e) => events.Add((e.PropertyName, e.NewValue));

        panel.MarkerFillColor = EnumColorType.Green;

        // 대표 마커에 바로 쓰지 않는다 — MapViewModel.ApplyGroupPropertyChangeAsync 가 전원에 같은 경로로 적용
        Assert.Equal(EnumColorType.Blue, ma.FillColor);
        Assert.Contains(events, e => e.Name == "FillColor" && Equals(e.Value, EnumColorType.Green));

        // 그 VM 경로(ApplyProperty)를 전원에 적용하면 두 심볼 그림이 모두 바뀐다
        foreach (var m in new IEditableMarker[] { ma, mb }) UndoableCommandBase.ApplyProperty(m, "FillColor", EnumColorType.Green);
        Assert.Equal(ColorOf(EnumColorType.Green), ColorOf(ControlOf(ma).MarkerFill));
        Assert.Equal(ColorOf(EnumColorType.Green), ColorOf(ControlOf(mb).MarkerFill));
    });

    [Theory]
    [InlineData("TitleSize", 20.0)]
    [InlineData("Bearing", 90.0)]
    [InlineData("Width", 70.0)]
    [InlineData("StrokeThickness", 4.0)]
    [InlineData("ShowShape", false)]
    [InlineData("ShowTitle", false)]
    [InlineData("IsLocked", true)]
    [InlineData("TitleBold", true)]
    [InlineData("TitleMaxWidth", 120.0)]
    [InlineData("LabelOffsetX", 15.0)]
    public void should_round_trip_group_and_undo_property_through_apply_and_read(string prop, object value) => AppHost.Run(() =>
    {
        // 그룹 편집 · 되돌리기 · 다시 실행이 모두 쓰는 단일 출처(ApplyProperty/ReadProperty)가 마커에 그대로 반영되는가
        using var marker = new GMapGeometricMarker(Log(), Geometric());
        var before = UndoableCommandBase.ReadProperty(marker, prop);
        UndoableCommandBase.ApplyProperty(marker, prop, value);
        Assert.Equal(value, UndoableCommandBase.ReadProperty(marker, prop));
        UndoableCommandBase.ApplyProperty(marker, prop, before);
        Assert.Equal(before, UndoableCommandBase.ReadProperty(marker, prop));
    });
}
