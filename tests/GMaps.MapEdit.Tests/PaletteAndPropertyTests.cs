using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 카탈로그 §1 팔레트(MAP-090 · 092 · 095 · 097 · 098) · 속성창(MAP-125 · 130 · 131 · 133).
/// 팔레트는 실제 스타일(SymbolPaletteStyle.xaml)로 부모 없이 Measure/Arrange 한다(창 · Application 없음).
/// </summary>
public class PaletteAndPropertyTests
{
    private static SymbolPaletteView Palette(EditRig rig)
    {
        var dict = new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolPaletteStyle.xaml", UriKind.Relative) };
        var view = new SymbolPaletteView { DataContext = rig.Vm };
        view.Resources.MergedDictionaries.Add(dict);
        view.Style = (Style)dict[typeof(SymbolPaletteView)];
        view.ApplyTemplate();
        Scenario.Layout(view, 384, 580);
        return view;
    }

    private static T Part<T>(SymbolPaletteView v, string name) where T : class => (T)v.Template.FindName(name, v)!;

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var d in Tree(VisualTreeHelper.GetChild(root, i))) yield return d;
    }

    private static List<string?> HelpKeys(DependencyObject root)
        => Tree(root).Where(d => d.GetType().Name == "HelpTip").Select(d => d.GetType().GetProperty("HelpKey")!.GetValue(d) as string).ToList();

    [Fact]
    [Trait("Scenario", "MAP-090")]
    public void should_hide_palette_when_close_button_is_clicked() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        rig.Vm.ToggleSymbolPalette();
        Assert.True(rig.Vm.IsSymbolPaletteVisible);
        var view = Palette(rig);
        var close = Part<Button>(view, "PART_CloseButton");
        Assert.Equal("GMaps.SymbolPalette.Close", System.Windows.Automation.AutomationProperties.GetAutomationId(close));

        close.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.False(rig.Vm.IsSymbolPaletteVisible);
    });

    [Fact]
    [Trait("Scenario", "MAP-092")]
    public void should_filter_cells_and_show_no_result_text_when_search_matches_nothing() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var view = Palette(rig);
        var search = Part<TextBox>(view, "PART_SearchBox");
        var items = Part<ListBox>(view, "PART_Items");
        var empty = Part<TextBlock>(view, "PART_EmptyLabel");
        int all = items.Items.Count;
        Assert.True(all > 1);
        Assert.Equal(Visibility.Collapsed, empty.Visibility);

        var first = (SymbolPaletteItem)items.Items[0];
        search.Text = first.Title;   // 거름
        Assert.InRange(items.Items.Count, 1, all);
        Assert.All(items.Items.Cast<SymbolPaletteItem>(), x => Assert.True(
            x.Title.Contains(first.Title, StringComparison.OrdinalIgnoreCase) || x.Type.ToString()!.Contains(first.Title, StringComparison.OrdinalIgnoreCase)
            || (x.ModelKey?.Contains(first.Title, StringComparison.OrdinalIgnoreCase) ?? false)));

        search.Text = "zzq엉뚱한글없음";   // 엉뚱한 글
        Assert.Empty(items.Items);
        Assert.Equal(Visibility.Visible, empty.Visibility);
        Assert.Equal("검색 결과가 없습니다", empty.Text);
    });

    [Fact]
    [Trait("Scenario", "MAP-095")]
    public void should_enter_placement_on_enter_and_cancel_without_row_on_escape() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var view = Palette(rig);
        var items = Part<ListBox>(view, "PART_Items");
        items.SelectedItem = items.Items[0];

        Scenario.Call(view, "ItemKeyDown", items, EditRig.Key(System.Windows.Input.Key.Enter));   // 셀에서 Enter
        Assert.True(rig.Map.IsSymbolPlacementMode);
        Assert.True(rig.Vm.IsAimStatusVisible);

        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForAim", rig.Map, EditRig.Key(System.Windows.Input.Key.Escape));   // 지도에서 Esc
        Assert.False(rig.Map.IsSymbolPlacementMode);
        Assert.Equal("배치를 취소했습니다.", rig.Vm.AimStatusMessage);
        Assert.DoesNotContain(rig.Db.Invocations, i => i.Method.Name.StartsWith("Insert"));   // 행 없음
        System.Windows.Input.Mouse.OverrideCursor = null;
    });

    [Fact]
    [Trait("Scenario", "MAP-097")]
    public void should_write_symbol3d_key_and_say_restart_needed_when_3d_housing_is_toggled() => Scenario.StaAsync(async () =>
    {
        // 저장 위치는 AppDomain.BaseDirectory/Config/appsettings.json(이 시험 bin) — 원본을 떠 두고 끝나면 되돌린다.
        var file = Ironwall.Dotnet.Libraries.Base.Services.AppSettingsLocation.PrepareAppSettingsWrite();
        var backup = File.Exists(file) ? File.ReadAllText(file) : null;
        try
        {
            if (backup == null) File.WriteAllText(file, "{ \"AppSettings\": { } }");   // 저장기는 기존 파일을 고쳐 쓴다
            var rig = EditRig.Create();
            var view = Palette(rig);
            var box = Part<CheckBox>(view, "PART_Enable3D");
            var status = Part<TextBlock>(view, "PART_FeatureStatus");
            bool want = !(box.IsChecked == true);
            box.IsChecked = want;
            box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            for (int i = 0; i < 40 && !box.IsEnabled; i++) await Task.Delay(25);

            Assert.Equal("저장됨 — 다시 시작해야 지도에 반영됩니다", status.Text);   // 재시작 필요
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));
            Assert.Equal(want, (bool)json["AppSettings"]!["Symbol3D"]!["IsEnabled"]!);
        }
        finally
        {
            if (backup != null) File.WriteAllText(file, backup); else if (File.Exists(file)) File.Delete(file);
        }
    });

    [Fact]
    [Trait("Scenario", "MAP-098")]
    public void should_attach_placement_and_symbol3d_help_tips_inside_palette() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var keys = HelpKeys(Palette(rig));
        Assert.Contains("Map.SymbolPalette.Placement", keys);
        Assert.Contains("Map.SymbolPalette.Symbol3D", keys);
    });

    [Fact]
    [Trait("Scenario", "MAP-133")]
    public void should_attach_label_housing_door_device_fence_help_tips_in_property_panel_styles()
    {
        // "그 키가 그 화면에 걸려 있는가" — 속성창 스타일 XAML 의 HelpTip 부착(등록 · 글은 MapsHelpTests 가 본다)
        var expect = new (string file, string key)[]
        {
            ("BasePropertyStyle.xaml", "Map.Property.Label"),
            ("PidsPropertyStyle.xaml", "Map.Property.Housing"),
            ("InfraPropertyStyle.xaml", "Map.Property.Housing"),
            ("PidsPropertyStyle.xaml", "Map.Property.Door"),
            ("PidsPropertyStyle.xaml", "Map.Property.Device"),
            ("PidsGroupPropertyStyle.xaml", "Map.Property.Fence"),
        };
        foreach (var (file, key) in expect)
        {
            var xaml = RepoFiles.Read(Path.Combine("Ironwall.Dotnet.Libraries.GMaps.Ui", "Themes", file));
            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(xaml, $@"<\w+:HelpTip[^>]*HelpKey=""{System.Text.RegularExpressions.Regex.Escape(key)}"""), $"{file} 에 {key} HelpTip 없음");
        }
    }

    [Fact]
    [Trait("Scenario", "MAP-125")]
    public void should_disable_device_link_combo_and_apply_button_when_two_symbols_are_selected() => Scenario.Sta(() =>
    {
        using var a = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Id = 1, Title = "가", DeviceType = EnumDeviceType.IpCamera, Latitude = 37.5, Longitude = 127 });
        using var b = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Id = 2, Title = "나", DeviceType = EnumDeviceType.IpCamera, Latitude = 37.5, Longitude = 127.001 });
        var panel = new GMapPropertyPidsControl { SelectedMarker = a };
        Assert.False(panel.IsGroupEditing);
        panel.EnterGroupMode(new IEditableMarker[] { a, b });
        Assert.True(panel.IsGroupEditing);

        // 연결디바이스 콤보 · [현재위치 적용] 은 IsGroupEditing=True 에서 IsEnabled=False(PidsPropertyStyle.xaml DataTrigger)
        var xaml = RepoFiles.Read(Path.Combine("Ironwall.Dotnet.Libraries.GMaps.Ui", "Themes", "PidsPropertyStyle.xaml"));
        var combo = xaml[xaml.IndexOf("SelectedItem=\"{Binding LinkedDevice", StringComparison.Ordinal)..];
        combo = combo[..combo.IndexOf("</ComboBox>", StringComparison.Ordinal)];
        Assert.Matches(@"Binding IsGroupEditing}"" Value=""True"">\s*<Setter Property=""IsEnabled"" Value=""False""", combo);
        var apply = xaml[xaml.IndexOf("x:Name=\"PART_ApplyDeviceLocationButton\"", StringComparison.Ordinal)..];
        apply = apply[..apply.IndexOf("</Button.Style>", StringComparison.Ordinal)];
        Assert.Matches(@"Binding IsGroupEditing}"" Value=""True"">\s*<Setter Property=""IsEnabled"" Value=""False""", apply);
    });

    private static GMapPidsGroupMarker FenceMarker()
    {
        var model = new PidsGroupSymbolModel { Id = 9, Title = "울타리", Latitude = 37.5, Longitude = 127.0, FenceMode = EnumFenceMode.Posts };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
        return new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
    }

    [Fact]
    [Trait("Scenario", "MAP-130")]
    public void should_set_post_spacing_to_2_3_5_m_by_presets_and_return_to_default_on_reset() => Scenario.Sta(() =>
    {
        using var marker = FenceMarker();
        var panel = new GMapPropertyPidsGroupControl { Is3DFeatureEnabled = true, SelectedMarker = marker };
        var events = new List<(string name, object? value)>();
        panel.MarkerPropertyChanged += (_, e) => events.Add((e.PropertyName, e.NewValue));

        foreach (var m in new[] { 2.0, 3.0, 5.0 })
        {
            panel.SetPostSpacingPresetCommand.Execute(m.ToString(System.Globalization.CultureInfo.InvariantCulture));
            panel.FlushDeferredCommits();
            Assert.Equal(m, panel.PostSpacingM, 3);        // 슬라이더
            Assert.Equal(m, marker.PostSpacingM);          // 마커(→ DB)
            Assert.False(panel.IsPostSpacingInherited);
            Assert.Contains(("PostSpacingM", (object?)m), events);
        }
        Assert.False(string.IsNullOrEmpty(panel.FenceLayoutSummary));   // 요약 갱신

        panel.ResetPostSpacingCommand.Execute(null);   // 초기화
        Assert.Null(marker.PostSpacingM);              // 전역 기본 상속
        Assert.True(panel.IsPostSpacingInherited);
        Assert.Equal(panel.FencePostSpacingDefault, panel.PostSpacingM, 3);
        Assert.Contains(("PostSpacingM", (object?)null), events);
    });

    [Fact]
    [Trait("Scenario", "MAP-131")]
    public void should_return_gate_width_to_default_when_reset_is_pressed() => Scenario.Sta(() =>
    {
        using var gate = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Id = 5, Title = "통문", DeviceType = EnumDeviceType.Gate, Latitude = 37.5, Longitude = 127, GateWidthM = 6.0 });
        var panel = new GMapPropertyPidsControl { SelectedMarker = gate };
        var events = new List<(string name, object? value)>();
        panel.MarkerPropertyChanged += (_, e) => events.Add((e.PropertyName, e.NewValue));

        panel.ResetGateWidthCommand.Execute(null);

        Assert.Null(gate.GateWidthM);   // 기본(상속)으로 영속
        Assert.True(panel.IsGateWidthInherited);
        Assert.Equal(Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence.FenceDefaults.GateWidthM, panel.GateWidthM, 3);
        Assert.Contains(("GateWidthM", (object?)null), events);
    });
}
