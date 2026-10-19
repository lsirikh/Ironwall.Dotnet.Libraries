using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 사용자 지시(2026-10-19) "같은 종류만 골랐을 때는 하나 골랐을 때 나오는 칸이 다 나와야 된다 — 다른 속성들도 마찬가지, 전체 아이콘에 대해 다 시험".
/// 종류(속성 판 8종 + PIDS 장비 DeviceType 20종)마다 창 없이 실제 VM 경로(단일 = ShowPropertyPanel · 다중 = ShowGroupPropertyPanelIfNeeded)로 판을 열어
/// ① 보이는 칸 집합(라벨 글 · AutomationId · 입력 칸 수)이 같은지 ② 공통 값 하나를 바꾸면 전원 반영 · 다른 값 불변 · 마커별 저장 · Undo 1회 원복
/// ③ 다른 값은 '여러 값' 표시 ④ 심볼마다 고유한 값은 비활성 ⑤ 잠긴 마커는 제외 를 본다.
/// </summary>
public class SameKindGroupPanelParityTests
{
    private static readonly double Lat = Property3DRig.HomeLat, Lng = Property3DRig.HomeLng;
    private static ILogService Log() => Mock.Of<ILogService>();

    // ── 종류별 마커 만들기(id · 잠금 · 값 변형 v=0/1) ──
    private static IEditableMarker Make(string kind, int id, int v, bool locked = false)
    {
        double d = id * 0.0001;
        IEditableMarker m = kind switch
        {
            "Geometric" => new GMapGeometricMarker(Log(), new GeometricSymbolModel { Id = id, Title = $"도형{id}", Latitude = Lat + d, Longitude = Lng, Width = 40, Height = 40, Opacity = v == 0 ? 0.5 : 0.8, ShowShape = true, IsLocked = locked }),
            "Custom" => new GMapCustomMarker(Log(), new SymbolModel { Id = id, Title = $"핀{id}", Latitude = Lat + d, Longitude = Lng, Width = 32, Height = 32, Zoom = v == 0 ? 3 : 7, IsLocked = locked }),
            "Line" => Line(id, v, locked),
            "PidsGroup" => Zone(id, v, locked),
            "Infra" => new GMapInfraMarker(Log(), new InfraSymbolModel { Id = id, Title = $"건물{id}", Latitude = Lat + d, Longitude = Lng, Width = 40, Height = 40, FloorCount = v == 0 ? 2 : 5, BuildingArea = v == 0 ? 100 : 300, IsLocked = locked }),
            "Military" => new GMapMilitarySymbolMarker(Log(), new MilitarySymbolModel { Id = id, Title = $"부대{id}", Latitude = Lat + d, Longitude = Lng, Width = 40, Height = 40, Affiliation = v == 0 ? EnumMilitaryAffiliation.Friend : EnumMilitaryAffiliation.Hostile, UnitDesignator = $"{id}대대", CallSign = $"콜{id}", IsLocked = locked }),
            "Image" => new GMapImageMarker(Log(), new ImageModel { Id = id, Title = $"이미지{id}", FilePath = $"C:\\img\\{id}.png", Left = Lng, Top = Lat + d + 0.0005, Right = Lng + 0.0005, Bottom = Lat + d, Width = 100, Height = 100, Opacity = v == 0 ? 0.4 : 0.9, Visibility = true, IsLocked = locked }),
            _ => new GMapPidsMarker(Log(), new PidsSymbolModel
            {
                Id = id, Title = $"{kind}{id}", DeviceType = Enum.Parse<EnumDeviceType>(kind), Latitude = Lat + d, Longitude = Lng, Width = 40, Height = 40,
                DetectionRange = v == 0 ? 50 : 80, IsLocked = locked,
            }),
        };
        return m;
    }

    /// <summary>3D 하우징(Symbol3D ON 경로) — 3D 전용 행(색상 강도 · 설치 높이 · 모델 변형)이 보이는 상태. kind = "3D:{DeviceType}" · "3D:Infra".</summary>
    private static IEditableMarker Make3D(string kind, int id, int v, bool locked = false)
    {
        var inner = kind.Substring(3);
        var m = Make(inner, id, v, locked);
        FrameworkElement control = m is GMapInfraMarker infra
            ? new Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols.GMapMarkerInfra3DControl(infra)
            : new Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols.GMapMarker3DHousingControl((GMapPidsMarker)m);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        ((GMap.NET.WindowsPresentation.GMapMarker)m).Shape = control;
        return m;
    }

    private static GMapLineMarker Line(int id, int v, bool locked)
    {
        var model = new LineSymbolModel { Id = id, Title = $"선{id}", Latitude = Lat, Longitude = Lng, LineOpacity = v == 0 ? 0.5 : 1.0, IsLocked = locked };
        model.LinePoints = new List<GeoPoint> { new(Lat + id * 0.0001, Lng, 0), new(Lat + id * 0.0001, Lng + 0.0005, 0) };
        return new GMapLineMarker(Log(), model);
    }

    private static GMapPidsGroupMarker Zone(int id, int v, bool locked)
    {
        var model = new PidsGroupSymbolModel { Id = id, Title = $"구역{id}", Latitude = Lat, Longitude = Lng, FenceHeightM = v == 0 ? 2.0 : 3.5, LineOpacity = 1.0, IsLocked = locked };
        model.LinePoints = new List<GeoPoint> { new(Lat + id * 0.0001, Lng, 0), new(Lat + id * 0.0001, Lng + 0.0005, 0) };
        return new GMapPidsGroupMarker(Log(), model);
    }

    private static string StyleFile(string kind) => kind switch
    {
        "Geometric" => "GeometricPropertyStyle.xaml",
        "Custom" => "CustomPropertyStyle.xaml",
        "Line" => "LinePropertyStyle.xaml",
        "PidsGroup" => "PidsGroupPropertyStyle.xaml",
        "Infra" => "InfraPropertyStyle.xaml",
        "Military" => "MilitaryPropertyStyle.xaml",
        "Image" => "ImagePropertyStyle.xaml",
        "3D:Infra" => "InfraPropertyStyle.xaml",
        _ => "PidsPropertyStyle.xaml",
    };

    /// <summary>속성 판 8종 + PIDS 장비 DeviceType 전부(NONE · Fence_Group(=PIDS 그룹 판) 제외).</summary>
    public static IEnumerable<object[]> Kinds()
    {
        foreach (var k in new[] { "Geometric", "Custom", "Line", "PidsGroup", "Infra", "Military", "Image" }) yield return new object[] { k };
        foreach (var t in Enum.GetValues<EnumDeviceType>())
            if (t is not EnumDeviceType.NONE and not EnumDeviceType.Fence_Group) yield return new object[] { t.ToString() };
        // 3D 하우징 판(3D 전용 행 포함) — PIDS 장비 전 종류 + 건물
        yield return new object[] { "3D:Infra" };
        foreach (var t in Enum.GetValues<EnumDeviceType>())
            if (t is not EnumDeviceType.NONE and not EnumDeviceType.Fence_Group) yield return new object[] { "3D:" + t };
    }

    private static void SaveRecorders(Property3DRig rig, List<int> saves)
    {
        rig.Db.Setup(d => d.UpdateLineSymbolAsync(It.IsAny<ILineSymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<ILineSymbolModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((ILineSymbolModel?)null);
        rig.Db.Setup(d => d.UpdateMilitarySymbolAsync(It.IsAny<IMilitarySymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<IMilitarySymbolModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((IMilitarySymbolModel?)null);
        rig.Db.Setup(d => d.UpdateSymbolAsync(It.IsAny<ISymbolModel>(), It.IsAny<CancellationToken>()))
              .Callback<ISymbolModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((ISymbolModel?)null);
        rig.Db.Setup(d => d.UpdateImageAsync(It.IsAny<IImageModel>(), It.IsAny<CancellationToken>()))
              .Callback<IImageModel, CancellationToken>((m, _) => saves.Add(m.Id)).ReturnsAsync((IImageModel?)null);
    }

    private static void Style(GMapPropertyBaseControl panel, string kind)
    {
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            MergedDictionaries =
            {
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml", UriKind.Relative) },
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) },
                new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{StyleFile(kind)}", UriKind.Relative) },
            }
        });
        panel.Style = (Style)panel.Resources[panel.GetType()];
        if (panel is GMapPropertyPidsGroupControl zone) zone.Is3DFeatureEnabled = true;
        Property3DRig.LayoutPanel(panel);
    }

    private static GMapPropertyBaseControl OpenSingle(Property3DRig rig, string kind, IEditableMarker m)
    {
        rig.Vm.SelectedMarker = m;
        Property3DRig.Call(rig.Vm, "ShowPropertyPanel");
        var panel = rig.Vm.PropertyPanel!;
        Style(panel, kind);
        return panel;
    }

    private static GMapPropertyBaseControl OpenGroup(Property3DRig rig, string kind, params IEditableMarker[] ms)
    {
        var group = (GroupSelectionService)typeof(Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.MapViewModel)
            .GetField("_groupSelection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(rig.Vm)!;
        group.SetSelection(ms);
        Property3DRig.Call(rig.Vm, "ShowGroupPropertyPanelIfNeeded");
        var panel = rig.Vm.PropertyPanel!;
        Assert.True(panel.IsGroupEditing);
        Style(panel, kind);
        return panel;
    }

    /// <summary>보이는 칸 집합 — 라벨 글(Grid 첫 칸 TextBlock) · AutomationId · 입력 칸 종류별 수.</summary>
    private static SortedSet<string> Fields(FrameworkElement panel)
    {
        var set = new SortedSet<string>();
        var counts = new Dictionary<string, int>();
        foreach (var e in Property3DRig.Descendants(panel).OfType<FrameworkElement>())
        {
            if (!Property3DRig.IsShown(e)) continue;
            var id = AutomationProperties.GetAutomationId(e);
            if (!string.IsNullOrEmpty(id)) set.Add("id:" + id);
            if (e is TextBlock t && string.IsNullOrEmpty(id) && System.Windows.Media.VisualTreeHelper.GetParent(t) is Grid && Grid.GetColumn(t) == 0 && !string.IsNullOrWhiteSpace(t.Text)
                && !t.Text.Contains('—') && !char.IsDigit(t.Text[0]))
                set.Add("label:" + t.Text);
            if (e is Slider or ComboBox or TextBox or ToggleButton or ButtonBase)
            {
                var key = e is ToggleButton ? "ToggleButton" : e is ButtonBase ? "Button" : e.GetType().Name;
                if (e.TemplatedParent is ComboBox or Slider or ScrollBar or TextBox) continue;   // 콤보 · 슬라이더 속 부품 단추는 세지 않는다
                counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
            }
        }
        foreach (var (k, n) in counts) set.Add($"count:{k}={n}");
        return set;
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void should_show_every_single_selection_field_when_same_kind_is_multi_selected(string kind) => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        Func<string, int, int, bool, IEditableMarker> make = kind.StartsWith("3D:") ? Make3D : Make;
        var a = make(kind, 1, 0, false); var b = make(kind, 2, 1, false);
        foreach (var m in new[] { a, b }) rig.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)m);
        rig.LayoutMap();
        var single = Fields(OpenSingle(rig, kind, a));
        if (kind.StartsWith("3D:")) Assert.Contains("label:설치 높이", single);   // 시험 전제: 3D 전용 행이 실제로 보이는 판
        var group = Fields(OpenGroup(rig, kind, a, b));

        var missing = single.Except(group).ToList();
        var extra = group.Except(single).ToList();
        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"[{kind}] 단일 선택과 다중 선택의 칸이 다르다 — 다중에서 빠짐: {string.Join(", ", missing)} / 다중에만: {string.Join(", ", extra)}");
        (rig.Vm.PropertyPanel as GMapPropertyPidsGroupControl)?.FlushDeferredCommits();
    });

    // ── ② 공통 값 하나 적용 · ③ 혼합 표시 · ⑤ 잠금 제외 ──

    /// <summary>종류별 공통 값(이벤트 이름 · 새 값 · 마커에서 읽기 · 판의 혼합 표시 판정 · 안 바뀌어야 할 다른 값).</summary>
    private static (string Prop, object Value, Func<GMapPropertyBaseControl, bool> IsMixed, string Other) Common(string kind) => kind switch
    {
        "Geometric" => ("MarkerOpacity", 0.3, p => double.IsNaN(((GMapPropertyGeometricControl)p).MarkerOpacity), "Width"),
        "Custom" => ("Zoom", 10.0, p => double.IsNaN(p.MarkerZoom), "Width"),
        "Line" => ("LineOpacity", 0.3, p => double.IsNaN(((GMapPropertyLineControl)p).LineOpacity), "IsClosedPath"),
        "PidsGroup" => ("FenceHeightM", 3.0, p => double.IsNaN(((GMapPropertyPidsGroupControl)p).FenceHeightM), "LineOpacity"),
        "Infra" => ("FloorCount", 9, p => ((GMapPropertyInfraControl)p).IsFloorCountMixed, "Width"),
        "Military" => ("Affiliation", EnumMilitaryAffiliation.Neutral, p => !Enum.IsDefined(((GMapPropertyMilitaryControl)p).Affiliation), "Width"),
        "Image" => ("Opacity", 0.6, p => double.IsNaN(((GMapPropertyImageControl)p).ImageOpacity), "MaintainAspectRatio"),
        "3D:Infra" => ("Altitude", 40.0, p => double.IsNaN(p.MarkerAltitude), "Width"),
        _ when kind.StartsWith("3D:") => ("Altitude", 40.0, p => double.IsNaN(p.MarkerAltitude), "Width"),
        _ => ("DetectionRange", 120.0, p => double.IsNaN(((GMapPropertyPidsControl)p).DetectionRange), "Width"),
    };

    private static void SetOnPanel(GMapPropertyBaseControl p, string prop, object value)
    {
        switch (prop)
        {
            case "MarkerOpacity": ((GMapPropertyGeometricControl)p).MarkerOpacity = (double)value; break;
            case "Zoom": p.MarkerZoom = (double)value; break;
            case "LineOpacity": ((GMapPropertyLineControl)p).LineOpacity = (double)value; break;
            case "FenceHeightM": ((GMapPropertyPidsGroupControl)p).FenceHeightM = (double)value; ((GMapPropertyPidsGroupControl)p).FlushDeferredCommits(); break;
            case "FloorCount": ((GMapPropertyInfraControl)p).FloorCount = (int)value; break;
            case "Affiliation": ((GMapPropertyMilitaryControl)p).Affiliation = (EnumMilitaryAffiliation)value; break;
            case "Opacity": ((GMapPropertyImageControl)p).ImageOpacity = (double)value; break;
            case "DetectionRange": ((GMapPropertyPidsControl)p).DetectionRange = (double)value; break;
            case "Altitude": Property3DRig.ById<Slider>(p, "GMaps.Property.Altitude").Value = (double)value; break;   // 실제 슬라이더(3D 전용 행)
        }
    }

    private static void SetAltitude(IEditableMarker m, float v) { if (m is GMapPidsMarker p) p.Altitude = v; else if (m is GMapInfraMarker i) i.Altitude = v; }

    private static string ReadProp(string prop) => prop == "MarkerOpacity" ? "MarkerOpacity" : prop;

    [Theory]
    [MemberData(nameof(Kinds))]
    public void should_apply_common_value_to_unlocked_members_and_undo_once_when_same_kind_group_edits(string kind) => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var saves = new List<int>();
        SaveRecorders(rig, saves);
        Func<string, int, int, bool, IEditableMarker> make = kind.StartsWith("3D:") ? Make3D : Make;
        var a = make(kind, 1, 0, false); var b = make(kind, 2, 1, false); var locked = make(kind, 3, 0, true);
        if (kind.StartsWith("3D:")) SetAltitude(b, 20f);   // 설치 높이를 서로 다르게
        foreach (var m in new[] { a, b, locked }) rig.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)m);
        rig.LayoutMap();
        var (prop, value, isMixed, other) = Common(kind);
        var before = new[] { a, b, locked }.Select(m => UndoableCommandBase.ReadProperty(m, ReadProp(prop))).ToArray();
        var otherBefore = new[] { a, b }.Select(m => UndoableCommandBase.ReadProperty(m, other) ?? (other == "Width" ? m.Width : null)).ToArray();

        var panel = OpenGroup(rig, kind, a, b, locked);
        Assert.True(isMixed(panel), $"[{kind}] 서로 다른 {prop} 가 '여러 값'으로 안 보인다");

        SetOnPanel(panel, prop, value);
        Property3DRig.Pump();

        Assert.Equal(Convert.ToString(value), Convert.ToString(UndoableCommandBase.ReadProperty(a, ReadProp(prop))));
        Assert.Equal(Convert.ToString(value), Convert.ToString(UndoableCommandBase.ReadProperty(b, ReadProp(prop))));
        Assert.Equal(before[2], UndoableCommandBase.ReadProperty(locked, ReadProp(prop)));   // ⑤ 잠금 제외
        Assert.Equal(otherBefore[0], UndoableCommandBase.ReadProperty(a, other) ?? (other == "Width" ? a.Width : null));   // 다른 값 그대로
        Assert.Equal(otherBefore[1], UndoableCommandBase.ReadProperty(b, other) ?? (other == "Width" ? b.Width : null));
        var saved = rig.DbCalls.Select(c => (int)c.Snapshot["Id"]!).Concat(saves).Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2 }, saved);   // 마커별 기존 저장 경로 — 잠긴 3 은 저장 안 함

        Assert.True(rig.Undo.UndoAsync().GetAwaiter().GetResult());   // Undo 1회 = 전원 원복
        Assert.Equal(before[0], UndoableCommandBase.ReadProperty(a, ReadProp(prop)));
        Assert.Equal(before[1], UndoableCommandBase.ReadProperty(b, ReadProp(prop)));
        Assert.False(rig.Undo.CanUndo);
    });

    // ── ④ 심볼마다 고유한 값 = 대표값 + 비활성 ──

    [Theory]
    [InlineData("Geometric", "GMaps.Property.Title")]
    [InlineData("Military", "부대 지시자")]
    [InlineData("Military", "콜사인")]
    [InlineData("Image", "표시")]
    [InlineData("PidsGroup", "장비 그룹")]
    [InlineData("IpCamera", "연결디바이스")]
    [InlineData("Gate", "GMaps.Property.DoorOpen")]
    public void should_disable_unique_field_with_representative_value_when_group_editing(string kind, string field) => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var a = Make(kind, 1, 0); var b = Make(kind, 2, 1);
        foreach (var m in new[] { a, b }) rig.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)m);
        var single = OpenSingle(rig, kind, a);
        var singleCtl = Find(single, field);
        bool singleEnabled = singleCtl.IsEnabled;
        var group = OpenGroup(rig, kind, a, b);
        var ctl = Find(group, field);
        Assert.True(Property3DRig.IsShown(ctl), $"[{kind}] {field} 가 안 보인다");
        Assert.False(ctl.IsEnabled, $"[{kind}] 그룹 편집에서 고유 값 {field} 가 켜져 있다");
        if (kind == "Military") Assert.Equal(field == "콜사인" ? "콜1" : "1대대", ((TextBox)ctl).Text);   // 대표값
        if (field == "GMaps.Property.Title") Assert.True(singleEnabled);   // 단일에선 켜져 있다
        (group as GMapPropertyPidsGroupControl)?.FlushDeferredCommits();
    });

    private static FrameworkElement Find(FrameworkElement panel, string field)
    {
        if (field.StartsWith("GMaps.")) return Property3DRig.ById<FrameworkElement>(panel, field);
        var row = Property3DRig.RowOf(panel, field);
        return Property3DRig.Descendants(row).OfType<FrameworkElement>().First(e => e is TextBox or ComboBox or CheckBox or Slider);
    }

    [Fact]
    public void should_apply_gate_width_and_model_variant_to_all_when_same_device_type_group_edits() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var g1 = (GMapPidsMarker)Make("Gate", 1, 0); var g2 = (GMapPidsMarker)Make("Gate", 2, 1);
        g1.GateWidthM = 3.0; g2.GateWidthM = null;
        foreach (var m in new[] { g1, g2 }) rig.Map.Markers.Add(m);
        var panel = (GMapPropertyPidsControl)OpenGroup(rig, "Gate", g1, g2);
        Assert.True(panel.IsGate);
        Assert.True(double.IsNaN(panel.GateWidthM));
        Assert.True(Property3DRig.IsShown(Property3DRig.ById<Slider>(panel, "GMaps.Property.GateWidth")));
        panel.GateWidthM = 5.0;
        Property3DRig.PumpFor(Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence.FenceDefaults.SliderCommitDelayMs * 3);
        Assert.Equal(5.0, g1.GateWidthM); Assert.Equal(5.0, g2.GateWidthM);
        Assert.True(rig.Undo.UndoAsync().GetAwaiter().GetResult());
        Assert.Equal(3.0, g1.GateWidthM); Assert.Null(g2.GateWidthM);

        var rig2 = Property3DRig.Create();
        var c1 = (GMapPidsMarker)Make("IpCamera", 1, 0); var c2 = (GMapPidsMarker)Make("IpCamera", 2, 1);
        c1.ModelVariant = "Fixed"; c2.ModelVariant = "Dome";
        foreach (var m in new[] { c1, c2 }) rig2.Map.Markers.Add(m);
        var cams = (GMapPropertyPidsControl)OpenGroup(rig2, "IpCamera", c1, c2);
        Assert.Null(cams.ModelVariant);   // 다름 = 무선택
        cams.ModelVariant = "Ptz";
        Assert.Equal("Ptz", c1.ModelVariant); Assert.Equal("Ptz", c2.ModelVariant);
    });

    [Fact]
    public void should_hide_device_type_rows_when_pids_group_mixes_device_types() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var gate = Make("Gate", 1, 0); var cam = Make("IpCamera", 2, 0);
        foreach (var m in new[] { gate, cam }) rig.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)m);
        var panel = (GMapPropertyPidsControl)OpenGroup(rig, "Gate", gate, cam);
        Assert.True(panel.IsMixedDeviceType);
        Assert.False(panel.IsGate);
        Assert.False(Property3DRig.IsShown(Property3DRig.ById<FrameworkElement>(panel, "GMaps.Property.GateSection")));
    });

    [Fact]
    public void should_sum_read_only_line_info_when_zones_are_multi_selected() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var a = Zone(1, 0, false); var b = Zone(2, 1, false);
        foreach (var m in new IEditableMarker[] { a, b }) rig.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)m);
        var panel = (GMapPropertyPidsGroupControl)OpenGroup(rig, "PidsGroup", a, b);
        Assert.Equal(4, panel.PointCount);
        Assert.Equal(a.TotalDistance + b.TotalDistance, panel.TotalDistance, 3);
        Assert.Equal("—", panel.FenceLayoutSummary);
        panel.FlushDeferredCommits();
    });
}
