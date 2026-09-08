using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GMaps.Housing.Tests;

public class HousingTests
{
    internal static void Sta(System.Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previous = IoC.GetInstance;
            IoC.GetInstance = (type, key) => type == typeof(ILogService) ? Mock.Of<ILogService>() : type == typeof(IEventAggregator) ? new EventAggregator() : throw new InvalidOperationException(type.Name);
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { IoC.GetInstance = previous; Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "STA timeout");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    internal static void Layout(FrameworkElement view, double width = 80, double height = 80)
    { view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout(); }
    private static ResourceDictionary Styles() => new() { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) };

    [Theory]
    [InlineData(0, 0, -1)]
    [InlineData(90, 1, 0)]
    [InlineData(180, 0, 1)]
    [InlineData(270, -1, 0)]
    public void should_project_north_clockwise(double yaw, int xSign, int ySign)
    {
        var p = HousingMath.Project(0, 0, 1, yaw, 1, 0, 0, 0);
        Assert.Equal(xSign, Math.Sign(Math.Round(p.x, 8))); Assert.Equal(ySign, Math.Sign(Math.Round(p.y, 8)));
    }
    [Theory]
    [InlineData(32, 32)]
    [InlineData(80, 24)]
    [InlineData(25, 90)]
    public void should_fit_every_catalog_mesh_through_full_yaw(double width, double height) => Sta(() =>
    {
        foreach (var item in SymbolPaletteItem.All.Where(i => i.ModelKey != null))
        {
            var model = HousingModels.Get(item.ModelKey!); var b = model.Bounds;
            double scale = HousingMath.Fit(width, height, b.SizeX, b.SizeY, b.SizeZ);
            for (int yaw = 0; yaw < 360; yaw += 15)
                foreach (var p in model.Parts.SelectMany(p => p.Mesh.Positions))
                {
                    var q = HousingMath.Project(p.X - b.X - b.SizeX / 2, p.Y - b.Y, p.Z - b.Z - b.SizeZ / 2, yaw, scale, width, height, b.SizeY);
                    Assert.InRange(q.x, -.0001, width + .0001); Assert.InRange(q.y, -.0001, height + .0001);
                }
        }
    });
    [Fact]
    public void should_reuse_frozen_geometry_and_keep_camera_variants_distinct() => Sta(() =>
    {
        var camera = HousingModels.Get("camera"); Assert.Same(camera, HousingModels.Get("camera"));
        Assert.All(camera.Parts, p => Assert.True(p.Mesh.IsFrozen));
        Assert.NotEqual(camera.Bounds, HousingModels.Get("camera.ptz").Bounds);
        Assert.Equal(12, Enum.GetValues<EnumBuildingType>().Length);
        Assert.Null(HousingModels.DeviceKey(EnumDeviceType.Fence_Group));
        Assert.Null(HousingModels.DeviceKey(EnumDeviceType.Cable));
    });
    [Theory]
    [InlineData("camera", 0)]
    [InlineData("camera.dome", .18)]
    [InlineData("camera.ptz", .18)]
    public void should_rotate_optics_about_its_mount_and_fit_all_head_angles(string key, double pivotZ) => Sta(() =>
    {
        var view = new HousingVisual { ModelKey = key, HeadYaw = 90 };
        Layout(view, 83, 41);
        var model = HousingModels.Get(key);
        Assert.Equal(pivotZ, model.HeadPivot.Z);
        var viewport = Assert.IsType<Viewport3D>(view.Children[0]);
        var scene = Assert.IsType<System.Windows.Media.Media3D.Model3DGroup>(viewport.Children[0].GetValue(System.Windows.Media.Media3D.ModelVisual3D.ContentProperty));
        var objects = Assert.IsType<System.Windows.Media.Media3D.Model3DGroup>(scene.Children.Last());
        foreach (var part in objects.Children.OfType<System.Windows.Media.Media3D.GeometryModel3D>())
            if (part.Transform is System.Windows.Media.Media3D.RotateTransform3D rotation)
            {
                Assert.Equal(pivotZ, rotation.CenterZ);
                Assert.Equal(90, ((System.Windows.Media.Media3D.AxisAngleRotation3D)rotation.Rotation).Angle);
            }
        var b = model.Bounds;
        double scale = HousingMath.Fit(83, 41, b.SizeX, b.SizeY, b.SizeZ);
        for (int head = 0; head < 360; head += 45)
            foreach (var part in model.Parts)
                foreach (var vertex in part.Mesh.Positions)
                {
                    var rotation = new System.Windows.Media.Media3D.RotateTransform3D(new System.Windows.Media.Media3D.AxisAngleRotation3D(new(0, 1, 0), head), model.HeadPivot);
                    var point = part.IsHead ? rotation.Transform(vertex) : vertex;
                    for (int yaw = 0; yaw < 360; yaw += 30)
                    {
                        var screen = HousingMath.Project(point.X - b.X - b.SizeX / 2, point.Y - b.Y, point.Z - b.Z - b.SizeZ / 2, yaw, scale, 83, 41, b.SizeY);
                        Assert.InRange(screen.x, 0, 83); Assert.InRange(screen.y, 0, 41);
                    }
                }
    });
    [Theory]
    [InlineData("camera", false)]
    [InlineData("camera.dome", false)]
    [InlineData("camera.ptz", true)]
    public void should_give_independent_head_only_to_ptz(string key, bool hasHead)
    {
        // D-11: 고정형·돔형은 하우징 전체가 Bearing 으로 돌고, PTZ 만 렌즈 모듈이 탐지방향을 독립 추종한다.
        Assert.Equal(hasHead, HousingModels.Get(key).Parts.Any(p => p.IsHead));
    }
    [Theory]
    [InlineData("infra.watchtower")]
    [InlineData("infra.antenna")]
    [InlineData("infra.powerpole")]
    [InlineData("infra.factory")]
    public void should_grow_with_floor_count_when_type_has_floors(string key)
    {
        // D-6: 층수 개념이 있는 종류는 높이가 층수에 반응한다(기본 3층 형상은 종전과 동일).
        Assert.True(HousingModels.Get(key, 10).Bounds.SizeY > HousingModels.Get(key, 1).Bounds.SizeY);
    }
    [Theory]
    [InlineData("infra.gate")]
    [InlineData("infra.helipad")]
    [InlineData("infra.bridge")]
    public void should_ignore_floor_count_when_type_has_no_floors(string key)
    {
        Assert.Equal(HousingModels.Get(key, 1).Bounds, HousingModels.Get(key, 10).Bounds);
    }
    [Theory]
    [InlineData(EnumBuildingType.Factory, true)]
    [InlineData(EnumBuildingType.Watchtower, true)]
    [InlineData(EnumBuildingType.Gate, false)]
    [InlineData(EnumBuildingType.WaterTank, false)]
    [InlineData(EnumBuildingType.Helipad, false)]
    public void should_gate_floor_sliders_by_building_type(EnumBuildingType type, bool supports)
        => Assert.Equal(supports, Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties.GMapPropertyInfraControl.BuildingTypeSupportsFloors(type));
    [Fact]
    public void should_scale_footprint_safely_across_area_range() => Sta(() =>
    {
        // D-8: 면적 극단값에서도 레이아웃·투영이 유한하고 예외가 없다. 링/렌즈 앵커는 같은 배율을 쓴다.
        foreach (var area in new[] { double.NaN, 0, 1, 100, 400, 2500, 1e9 })
        {
            var view = new HousingVisual { ModelKey = "infra.factory", Footprint = area };
            Layout(view, 83, 41);
            var lens = view.LensPoint;
            Assert.True(double.IsFinite(lens.X) && double.IsFinite(lens.Y));
        }
    });
    [Fact]
    public void should_read_symbol3d_flag_from_app_bin_appsettings_when_present()
    {
        // 런타임 진단: 앱이 실제로 읽는 파일을 같은 코드로 해석한다. 파싱 예외가 있으면 error 에 드러난다(런타임엔 Trace 로만 삼켜짐).
        var path = @"C:\workspace_app\Dotnet.Monitoring.Solution\Dotnet.Monitoring.Solution\bin\Debug\net8.0-windows7.0\appsettings.json";
        if (!File.Exists(path)) return;   // 다른 머신에서는 조용히 통과
        var (enabled, directory, error) = Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.Symbol3DFeature.ReadFile(path);
        Assert.True(error is null, "appsettings 해석 오류: " + error);
        Assert.True(enabled, "bin appsettings 의 Symbol3D.IsEnabled 가 true 여야 한다 (directory=" + directory + ")");
    }
    [Fact]
    public void should_notify_door_state_when_model_update_fires() => Sta(() =>
    {
        // 실기 결함(2026-09-08 00:27): 이벤트 배선은 모델 DoorState 만 바꾸고 SetUpdate 로 통지한다 — 마커가 DoorState 를 재통지하지 않으면 3D 컨트롤 바인딩(OneWay)이 멈춘다.
        var model = new PidsSymbolModel { DeviceType = EnumDeviceType.Gate };
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), model);
        var names = new List<string>();
        marker.PropertyChanged += (_, e) => names.Add(e.PropertyName ?? "");
        model.DoorState = EnumDoorState.Open;   // DeviceSymbolLookupModel.ApplyDoorState 와 동일 경로
        model.SetUpdate();
        Assert.Contains("DoorState", names);
        Assert.Equal(EnumDoorState.Open, marker.DoorState);
    });

    [Fact]
    public void should_roundtrip_fence_and_gate_fields_in_json_clone_and_snapshot()
    {
        // FR-12/16: 영속 필드는 JSON·Clone·Undo 스냅샷을 통과하고, DoorState(형태 축)·ActiveSensorDeviceIds(런타임)는 통과하지 않는다.
        var group = new PidsGroupSymbolModel { PostSpacingM = 2.5, FenceHeightM = 1.8, FenceMode = EnumFenceMode.SensorMount, Render3D = false, ReverseSensorOrder = true, ActiveSensorDeviceIds = new HashSet<int> { 7 } };
        var groupJson = JsonConvert.SerializeObject(group);
        Assert.Contains("\"post_spacing_m\":2.5", groupJson); Assert.DoesNotContain("ActiveSensorDeviceIds", groupJson);
        var g2 = JsonConvert.DeserializeObject<PidsGroupSymbolModel>(groupJson)!;
        Assert.Equal(2.5, g2.PostSpacingM); Assert.Equal(1.8, g2.FenceHeightM); Assert.Equal(EnumFenceMode.SensorMount, g2.FenceMode);
        Assert.False(g2.Render3D); Assert.True(g2.ReverseSensorOrder); Assert.Null(g2.ActiveSensorDeviceIds);
        var gSnap = (PidsGroupSymbolModel)SymbolSnapshot.Capture(group, 1, nameof(GMapPidsGroupMarker), false)!.CloneModel();
        Assert.Equal(2.5, gSnap.PostSpacingM); Assert.Equal(EnumFenceMode.SensorMount, gSnap.FenceMode);
        Assert.Null(new PidsGroupSymbolModel().PostSpacingM);   // 기본 NULL → 전역 설정 폴백

        var gate = new PidsSymbolModel { DeviceType = EnumDeviceType.Gate, GateWidthM = 3.5, OpenOnContactOn = false, DoorState = EnumDoorState.Open };
        var gateJson = JsonConvert.SerializeObject(gate);
        Assert.Contains("\"gate_width_m\":3.5", gateJson); Assert.DoesNotContain("DoorState", gateJson);
        var p2 = JsonConvert.DeserializeObject<PidsSymbolModel>(gateJson)!;
        Assert.Equal(3.5, p2.GateWidthM); Assert.False(p2.OpenOnContactOn); Assert.Equal(EnumDoorState.Unknown, p2.DoorState);
        var clone = gate.Clone();
        Assert.Equal(3.5, clone.GateWidthM); Assert.False(clone.OpenOnContactOn); Assert.Equal(EnumDoorState.Unknown, clone.DoorState);
        Assert.True(new PidsSymbolModel().OpenOnContactOn);
    }

    [Fact]
    public void should_apply_and_read_fence_gate_properties_symmetrically_when_undo() => Sta(() =>
    {
        // T-B07: Read/Apply 대칭 + IsReplayableProperty — 기존 PidsGroup 4속성은 종전 '죽은 undo 엔트리'였다.
        using var group = new GMapPidsGroupMarker(Mock.Of<ILogService>(), new PidsGroupSymbolModel { Latitude = 37.5, Longitude = 127.0 });
        foreach (var (prop, val) in new (string, object)[]
        {
            ("PostSpacingM", 2.5), ("FenceHeightM", 1.8), ("FenceMode", EnumFenceMode.SensorMount), ("Render3D", false), ("ReverseSensorOrder", true),
            ("LinkedDeviceGroup", 7), ("LinePattern", EnumLinePattern.Dashed), ("LineOpacity", 0.4), ("IsClosedPath", false),
        })
        {
            Assert.True(UndoableCommandBase.IsReplayableProperty(prop), prop);
            UndoableCommandBase.ApplyProperty(group, prop, val);
            Assert.Equal(val, UndoableCommandBase.ReadProperty(group, prop));
        }
        UndoableCommandBase.ApplyProperty(group, "PostSpacingM", null);
        Assert.Null(UndoableCommandBase.ReadProperty(group, "PostSpacingM"));   // NULL(전역 폴백)도 왕복
        UndoableCommandBase.ApplyProperty(group, "FenceMode", "Posts");          // 문자열 스냅샷도 enum 으로 복원
        Assert.Equal(EnumFenceMode.Posts, UndoableCommandBase.ReadProperty(group, "FenceMode"));

        using var gate = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.Gate });
        foreach (var (prop, val) in new (string, object)[] { ("GateWidthM", 3.5), ("OpenOnContactOn", false) })
        {
            Assert.True(UndoableCommandBase.IsReplayableProperty(prop), prop);
            UndoableCommandBase.ApplyProperty(gate, prop, val);
            Assert.Equal(val, UndoableCommandBase.ReadProperty(gate, prop));
        }
        Assert.False(UndoableCommandBase.IsReplayableProperty("DoorState"));   // 형태 축은 이벤트가 쓰는 런타임 값 — undo 대상 아님
        Assert.Null(UndoableCommandBase.ReadProperty(gate, "PostSpacingM"));    // 타입 불일치는 null(무해)
    });

    [Fact]
    public void should_fallback_to_defaults_when_symbol3d_keys_absent()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ \"AppSettings\": { \"Symbol3D\": { \"IsEnabled\": true, \"Directory\": null } } }");
            var (s, error) = Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.Symbol3DFeature.ReadSettings(path);
            Assert.Null(error);
            Assert.True(s.IsEnabled); Assert.Null(s.Directory);
            Assert.Equal(3.0, s.FencePostSpacingM); Assert.Equal(2.4, s.FenceHeightM); Assert.True(s.DoorContactFallback);
            var (enabled, _, err2) = Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.Symbol3DFeature.ReadFile(path);   // 하위 호환 튜플
            Assert.True(enabled); Assert.Null(err2);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void should_read_fence_keys_when_symbol3d_section_has_them()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ \"Symbol3D\": { \"IsEnabled\": false, \"Directory\": \"D:/obj\", \"FencePostSpacingM\": 5, \"FenceHeightM\": 1.8, \"DoorContactFallback\": false } }");
            var (s, error) = Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.Symbol3DFeature.ReadSettings(path);
            Assert.Null(error);
            Assert.False(s.IsEnabled); Assert.Equal("D:/obj", s.Directory);
            Assert.Equal(5.0, s.FencePostSpacingM); Assert.Equal(1.8, s.FenceHeightM); Assert.False(s.DoorContactFallback);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void should_keep_defaults_and_report_error_when_fence_keys_invalid()
    {
        var path = Path.GetTempFileName();
        try
        {
            // 타입 오류(문자열) + 범위 밖(간격 50m) — 해당 키만 기본값, 나머지는 살아있고 error 에 사유가 남는다
            File.WriteAllText(path, "{ \"AppSettings\": { \"Symbol3D\": { \"IsEnabled\": true, \"FencePostSpacingM\": 50, \"FenceHeightM\": \"tall\", \"DoorContactFallback\": \"false\" } } }");
            var (s, error) = Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.Symbol3DFeature.ReadSettings(path);
            Assert.NotNull(error);
            Assert.Contains("FencePostSpacingM", error); Assert.Contains("FenceHeightM", error);
            Assert.True(s.IsEnabled);
            Assert.Equal(3.0, s.FencePostSpacingM); Assert.Equal(2.4, s.FenceHeightM); Assert.False(s.DoorContactFallback);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void should_roundtrip_variant_in_json_copy_and_undo_snapshot()
    {
        var model = new PidsSymbolModel { DeviceType = EnumDeviceType.IpCamera, ModelVariant = "Ptz", BaseBearing = 61 };
        var restored = JsonConvert.DeserializeObject<PidsSymbolModel>(JsonConvert.SerializeObject(model))!;
        Assert.Equal("Ptz", restored.ModelVariant); Assert.Equal(EnumDeviceType.IpCamera, restored.DeviceType);
        Assert.Equal("Ptz", model.Clone().ModelVariant); Assert.Equal(61, model.Clone().BaseBearing);
        var snapshot = SymbolSnapshot.Capture(model, 1, nameof(GMapPidsMarker), false)!;
        Assert.Equal("Ptz", ((PidsSymbolModel)snapshot.CloneModel()).ModelVariant);
    }
    [Fact]
    public void should_keep_size_binding_alive_when_marker_updates_size_through_adorner_path() => Sta(() =>
    {
        // 어도너 리사이즈·그룹 일괄편집·크기 Undo 는 전부 GMapBaseMarker.UpdateSize → UpdateShapeSize 로 합류한다.
        // 3D(OneWay) 에서 그 경로가 로컬값을 쓰면 바인딩이 죽어 이후 속성창 크기 입력이 무시된다(D-1).
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.IpCamera, Width = 83, Height = 41 });
        marker.Width = 83; marker.Height = 41; marker.IsVisible = true;
        var control = new GMapMarker3DHousingControl(marker); control.Resources.MergedDictionaries.Add(Styles());
        control.Style = (Style)control.Resources[typeof(GMapMarker3DHousingControl)];
        marker.Shape = control;   // UpdateShapeSize 가 이 컨트롤에 쓰도록 연결
        Layout(control, 83, 41);
        marker.UpdateSize(50, 30);
        Assert.Equal(50, marker.Width); Assert.Equal(30, marker.Height);
        Assert.Equal(50, control.Width); Assert.Equal(30, control.Height);
        Assert.NotNull(BindingOperations.GetBindingExpression(control, FrameworkElement.WidthProperty));
        Assert.NotNull(BindingOperations.GetBindingExpression(control, FrameworkElement.HeightProperty));
        // 속성창 경로(모델 setter)가 어도너 이후에도 컨트롤에 도달한다.
        marker.Width = 64; marker.Height = 36;
        Assert.Equal(64, control.Width); Assert.Equal(36, control.Height);
    });

    [Fact]
    public void should_preserve_stored_size_and_keep_hit_box_upright() => Sta(() =>
    {
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.IpCamera, Width = 83, Height = 41, Bearing = 123 });
        // The existing 2D constructor establishes defaults; restore the persisted editor dimensions before attaching 3D.
        marker.Width = 83; marker.Height = 41; marker.IsVisible = true;
        var control = new GMapMarker3DHousingControl(marker); control.Resources.MergedDictionaries.Add(Styles());
        control.Style = (Style)control.Resources[typeof(GMapMarker3DHousingControl)];
        using var source = new HwndSource(new HwndSourceParameters("Housing input test") { Width = 100, Height = 80, WindowStyle = unchecked((int)0x80000000) }) { RootVisual = control };
        Layout(control, 83, 41); control.OnMapBearingChanged(47);
        Assert.False(control.RotatesIn2D); Assert.True(control.AppliesMapRotation);
        Assert.Equal(0, ((RotateTransform)((TransformGroup)control.RenderTransform).Children[0]).Angle);
        Assert.Equal(BindingMode.OneWay, BindingOperations.GetBinding(control, FrameworkElement.WidthProperty)!.Mode);
        // 렌더 크기 변경은 SetCurrentValue 로만 들어와야 한다(D-1). 로컬값 대입(control.Width = …)은 OneWay 바인딩을 영구 제거한다.
        control.SetCurrentValue(FrameworkElement.WidthProperty, 27d); Layout(control, 27, 41);
        Assert.Equal(27, control.ActualWidth);
        Assert.Equal(83, marker.Width); Assert.Equal(41, marker.Height);
        Assert.NotNull(BindingOperations.GetBindingExpression(control, FrameworkElement.WidthProperty));   // 바인딩 생존
        marker.Width = 60; Assert.Equal(60, control.Width);                                              // 모델 → 컨트롤 여전히 흐른다
        marker.Width = 83;
        var housing = (HousingVisual)control.Template.FindName("PART_Housing3D", control);
        Assert.Equal(76, HousingMath.Normalize(housing.Yaw));
        Assert.NotNull(control.Template.FindName("PART_HitBackplate", control));
        Assert.NotNull(control.InputHitTest(new Point(1, 1)));
        control.ShowShape = false; Assert.Equal(Visibility.Collapsed, housing.Visibility);
    });
    [Fact]
    public void should_load_palette_without_application_or_database() => Sta(() =>
    {
        var view = new SymbolPaletteView();
        var paletteStyle = new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolPaletteStyle.xaml", UriKind.Relative) };
        view.Style = (Style)paletteStyle[typeof(SymbolPaletteView)];
        view.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml", UriKind.Relative) });
        Layout(view, 384, 580);
        string? output = Environment.GetEnvironmentVariable("SYMBOL3D_ARTIFACTS");
        if (output != null)
        {
            SaveImage(view, 384, 580, Path.Combine(output, "housing-palette-dark.png"));
            view.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) });
            Layout(view, 384, 580);
            SaveImage(view, 384, 580, Path.Combine(output, "housing-palette-light.png"));
        }
        var list = (ListBox)view.Template.FindName("PART_Items", view); Assert.Equal(20, list.Items.Count);   // +통문(D2)
        ((TextBox)view.Template.FindName("PART_SearchBox", view)).Text = "PTZ"; Assert.Single(list.Items.Cast<object>());
        ((TextBox)view.Template.FindName("PART_SearchBox", view)).Text = "no-such-symbol"; Assert.Empty(list.Items.Cast<object>());
    });

    [Fact]
    public void should_drag_palette_inside_canvas_and_fit_small_viewports() => Sta(() =>
    {
        var view = new SymbolPaletteView();
        var style = new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolPaletteStyle.xaml", UriKind.Relative) };
        view.Style = (Style)style[typeof(SymbolPaletteView)];
        var host = new ContentPresenter { Content = view };
        var canvas = new Canvas();
        canvas.Children.Add(host);
        Canvas.SetLeft(host, 50); Canvas.SetTop(host, 60);
        Layout(canvas, 960, 700);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var header = (System.Windows.Controls.Primitives.Thumb)view.Template.FindName("PART_HeaderDrag", view);
        void Drag(double x, double y) => header.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(x, y)
        { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
        Drag(120, 10);
        Assert.Equal(170, Canvas.GetLeft(host)); Assert.Equal(70, Canvas.GetTop(host));
        Drag(10000, 10000);
        Assert.Equal(568, Canvas.GetLeft(host)); Assert.Equal(112, Canvas.GetTop(host));
        Layout(canvas, 260, 410);
        Assert.True(view.ActualWidth <= 244); Assert.True(view.ActualHeight <= 394);
        Assert.Equal(8, Canvas.GetLeft(host)); Assert.Equal(8, Canvas.GetTop(host));
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    internal static void SaveImage(FrameworkElement view, int width, int height, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }

    [Fact]
    public void should_parse_obj_indices_materials_axes_and_lens_without_database() => Sta(() =>
    {
        const string obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\nusemtl painted\nf -4 -3 -2\nf 1/1 4/2 2/3\nf 1 3 4\nf 2 4 3";
        var model = HousingObjLoader.Parse(obj, "{\"lens\":[0,1,0],\"front\":[0,0,1],\"up\":[0,1,0]}", "newmtl painted\nKd 1 0.5 0");
        Assert.Single(model.Parts); Assert.Equal("#FF7F00", model.Parts[0].Material);
        Assert.True(model.Parts[0].Mesh.IsFrozen); Assert.Equal(12, model.Parts[0].Mesh.TriangleIndices.Count);
        Assert.Equal(1, model.Lens.Y);
        Assert.Throws<FormatException>(() => HousingObjLoader.Parse(obj.Replace("f 2 4 3", "f 99 4 3")));
        Assert.Throws<FormatException>(() => HousingObjLoader.Parse(obj.Replace("v 0 0 0", "v NaN 0 0")));
        Assert.Throws<FormatException>(() => HousingObjLoader.Parse(obj, "{\"front\":[0,1,0],\"up\":[0,1,0]}"));
    });

    [Fact]
    public void should_preserve_new_fields_in_database_read_mappers_without_connecting()
    {
        var assembly = typeof(Ironwall.Dotnet.Libraries.GMaps.Db.Services.IGMapDbSymbolService).Assembly;
        object Dto(string name) => Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == name))!;
        var pids = Dto("PidsSymbolSQL"); pids.GetType().GetProperty("ModelVariant")!.SetValue(pids, "Dome");
        Assert.Equal("Dome", ((PidsSymbolModel)pids.GetType().GetMethod("ToPidsDomain")!.Invoke(pids, null)!).ModelVariant);
        foreach (var building in Enum.GetValues<EnumBuildingType>())
        {
            var dto = Dto("InfraSymbolSQL"); dto.GetType().GetProperty("BuildingType")!.SetValue(dto, building.ToString());
            var model = (InfraSymbolModel)dto.GetType().GetMethod("ToInfraDomain")!.Invoke(dto, null)!;
            Assert.Equal(building, model.BuildingType);
        }
    }

    [Fact]
    public void should_replay_variant_and_building_properties() => Sta(() =>
    {
        using var camera = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.IpCamera, ModelVariant = "Fixed" });
        Assert.True(UndoableCommandBase.IsReplayableProperty("ModelVariant"));
        UndoableCommandBase.ApplyProperty(camera, "ModelVariant", "Ptz"); Assert.Equal("Ptz", camera.ModelVariant);
        UndoableCommandBase.ApplyProperty(camera, "ModelVariant", null); Assert.Null(camera.ModelVariant);
        using var building = new GMapInfraMarker(Mock.Of<ILogService>(), new InfraSymbolModel());
        UndoableCommandBase.ApplyProperty(building, "BuildingType", EnumBuildingType.Watchtower);
        UndoableCommandBase.ApplyProperty(building, "FloorCount", 4);
        Assert.Equal(EnumBuildingType.Watchtower, building.BuildingType); Assert.Equal(4, building.FloorCount);
    });

    [Fact]
    public void should_update_theme_and_tint_without_mutating_shared_geometry() => Sta(() =>
    {
        var view = new HousingVisual { ModelKey = "enclosure", BodyBrush = Brushes.Red };
        view.Resources["TextSecondaryBrush"] = Brushes.Gold; Layout(view);
        Assert.Equal(Brushes.Gold, view.MetalBrush);
        var mesh = HousingModels.Get("enclosure").Parts[0].Mesh;
        view.Resources["TextSecondaryBrush"] = Brushes.Silver;
        HousingAppearance.SetTintStrength(view, 1);
        Assert.Equal(Brushes.Silver, view.MetalBrush); Assert.Same(mesh, HousingModels.Get("enclosure").Parts[0].Mesh);
    });

    [Fact]
    public void should_keep_fractional_placement_coordinates_at_digital_zoom_and_rotation() => Sta(() =>
    {
        var map = new GMapCustomControl { Width = 640, Height = 480, Zoom = 12, Position = new GMap.NET.PointLatLng(37.5, 127) };
        Layout(map, 640, 480);
        foreach (double scale in new[] { 1d, 1.25, 1.5, 2 })
        {
            map.RenderTransform = new ScaleTransform(scale, scale);
            foreach (float bearing in new[] { 0f, 45f, 90f, 180f })
            {
                map.Bearing = bearing;
                var a = map.SymbolPlacementLocation(new Point(300.1, 200.1));
                var b = map.SymbolPlacementLocation(new Point(300.9, 200.9));
                Assert.NotEqual(a, b);
                Assert.InRange(a.Lat, -90, 90); Assert.InRange(a.Lng, -180, 180);
            }
        }
    });

    [Fact]
    public void should_render_missing_2d_devices_with_existing_rotation_contract() => Sta(() =>
    {
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.Enclosure });
        marker.IsVisible = true;
        var control = new GMapMarkerPidsFallbackControl(marker); control.Resources.MergedDictionaries.Add(Styles());
        control.Style = (Style)control.Resources[typeof(GMapMarkerPidsFallbackControl)]; Layout(control, 32, 32);
        // [map-tilt-25d 결정③/FR-10] 2D 폴백 아이콘도 PIDS 2D 빌보드 계약을 상속한다 — 루트 각 0(종전 −30 회전 계약은 정책 반전으로 대체).
        //   RotatesIn2D 는 그대로 true(속성창 3D 행 트리거 누출 없음), CurrentDisplayAngle 은 FOV 입력으로만 유지된다.
        Assert.True(control.RotatesIn2D); Assert.True(control.IsBillboard); control.OnMapBearingChanged(30);
        Assert.Equal(0, ((RotateTransform)((TransformGroup)control.RenderTransform).Children[0]).Angle);
        Assert.NotNull(control.Template.FindName("PART_Housing3D", control));
    });
    [Fact]
    public void should_render_native_gallery_and_record_rotation_cost() => Sta(() =>
    {
        var panel = new WrapPanel { Width = 1200, Background = new SolidColorBrush(Color.FromRgb(15, 24, 37)) };
        foreach (var item in SymbolPaletteItem.All.Where(i => i.ModelKey != null))
        {
            var cell = new StackPanel { Width = 144, Height = 152, Margin = new Thickness(3) };
            cell.Children.Add(new HousingVisual { Width = 136, Height = 118, ModelKey = item.ModelKey!, Yaw = 145, BodyBrush = Brushes.SlateGray, RingBrush = Brushes.Turquoise, FloorCount = 3 });
            cell.Children.Add(new TextBlock { Text = item.Title, Foreground = Brushes.White, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(cell);
        }
        Layout(panel, 1200, 632);
        string? output = Environment.GetEnvironmentVariable("SYMBOL3D_ARTIFACTS");
        if (output != null)
        {
            Directory.CreateDirectory(output);
            var bitmap = new RenderTargetBitmap(1200, 632, 96, 96, PixelFormats.Pbgra32); bitmap.Render(panel);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(output, "housing-native-gallery.png"))) encoder.Save(file);
            var views = Enumerable.Range(0, 100).Select(_ => new HousingVisual { ModelKey = "camera", Width = 40, Height = 40 }).ToArray();
            foreach (var view in views) Layout(view, 40, 40);
            var times = new List<double>();
            for (int tick = 0; tick < 80; tick++) { var watch = Stopwatch.StartNew(); foreach (var view in views) view.Yaw = tick * 3; times.Add(watch.Elapsed.TotalMilliseconds); }
            times.Sort(); File.WriteAllText(Path.Combine(output, "rotation-cpu.txt"), $"100 visible-sized controls, yaw update CPU p95: {times[(int)(times.Count * .95)]:F3} ms. Offscreen; excludes GPU presentation, RDP and map overlays.");
        }
        Assert.Equal(32, panel.Children.Count);   // +통문(D2)
    });
}
