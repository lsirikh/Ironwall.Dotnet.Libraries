using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 사용자 보고(2026-10-19) "PIDS 그룹 멀티셀렉트 했는데 펜스 높이 이런 거 설정하는 게 안 나타난다 — 공통 속성인데".
/// 다중 선택 속성 판(그룹 Pending 모드)에도 3D 철망 칸을 보이고, 기존 그룹 편집 관용구(동일=값 / 다름=— ·
/// 바꾼 필드만 전원 적용 · 마커별 DB 저장 · Undo 1단위)를 따르는지 창 없이 실제 VM 경로(ShowGroupPropertyPanelIfNeeded →
/// OnMarkerPropertyChanged → ApplyGroupPropertyChangeAsync)와 실제 칸(슬라이더 · 체크 칸 · 콤보 · 「기본」 단추)으로 확인한다.
/// </summary>
public class PidsGroupFenceGroupEditScenarioTests
{
    private const string GroupUpdate = "UpdatePidsGroupSymbolAsync";

    private static GMapPidsGroupMarker NewZone(int id, double? spacing = null, double? height = null,
        EnumFenceMode mode = EnumFenceMode.Posts, bool render3D = true, bool locked = false)
    {
        var model = new PidsGroupSymbolModel
        {
            Id = id, Title = $"구역-{id}", Latitude = Property3DRig.HomeLat, Longitude = Property3DRig.HomeLng,
            PostSpacingM = spacing, FenceHeightM = height, FenceMode = mode, Render3D = render3D, IsLocked = locked,
        };
        double d = id * 0.0001;
        model.LinePoints = new List<GeoPoint> { new(Property3DRig.HomeLat + d, Property3DRig.HomeLng, 0), new(Property3DRig.HomeLat + d, Property3DRig.HomeLng + 0.0005, 0) };
        return new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
    }

    /// <summary>다중 선택 → VM 의 실제 그룹 속성 판 경로 → 앱과 같은 스타일 · 3D 기능 켬 → 부모 없는 배치.</summary>
    private static GMapPropertyPidsGroupControl OpenGroup(Property3DRig rig, params GMapPidsGroupMarker[] markers)
    {
        var group = (GroupSelectionService)typeof(Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.MapViewModel)
            .GetField("_groupSelection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(rig.Vm)!;
        foreach (var m in markers) rig.Map.Markers.Add(m);   // Undo 는 지도에서 Id 로 마커를 찾는다
        group.SetSelection(markers);
        Property3DRig.Call(rig.Vm, "ShowGroupPropertyPanelIfNeeded");
        var panel = Assert.IsType<GMapPropertyPidsGroupControl>(rig.Vm.PropertyPanel);
        Assert.True(panel.IsGroupEditing);
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            MergedDictionaries =
            {
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml", UriKind.Relative) },
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) },
                new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsGroupPropertyStyle.xaml", UriKind.Relative) },
            }
        });
        panel.Style = (Style)panel.Resources[typeof(GMapPropertyPidsGroupControl)];
        panel.Is3DFeatureEnabled = true;
        Property3DRig.LayoutPanel(panel);
        return panel;
    }

    private static T Id<T>(FrameworkElement panel, string id) where T : FrameworkElement => Property3DRig.ById<T>(panel, id);
    private static string Text(FrameworkElement panel, string sliderId)
    {
        // 슬라이더 오른쪽 값 글(같은 Grid 행의 굵은 TextBlock)
        var slider = Id<Slider>(panel, sliderId);
        var grid = (Grid)System.Windows.Media.VisualTreeHelper.GetParent(slider);
        int row = Grid.GetRow(slider);
        var cell = grid.Children.OfType<StackPanel>().First(s => Grid.GetRow(s) == row && Grid.GetColumn(s) == 2);
        return cell.Children.OfType<TextBlock>().First().Text;
    }

    private static void Commit() => Property3DRig.PumpFor(FenceDefaults.SliderCommitDelayMs * 3);

    [Fact]
    public void should_show_fence_section_when_pids_groups_are_multi_selected() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, height: 3.0);
        using var b = NewZone(2, height: 3.0);
        var panel = OpenGroup(rig, a, b);

        var section = Id<Border>(panel, "GMaps.Property.FenceSection");
        Assert.True(Property3DRig.IsShown(section), "다중 선택 속성 판에 3D 철망 칸이 안 보인다(사용자 보고 2026-10-19)");
        foreach (var id in new[] { "GMaps.Property.FenceMode", "GMaps.Property.Render3D", "GMaps.Property.PostSpacing", "GMaps.Property.PostSpacingReset", "GMaps.Property.FenceHeight", "GMaps.Property.FenceHeightReset" })
            Assert.True(Property3DRig.IsShown(Id<FrameworkElement>(panel, id)), $"{id} 가 안 보인다");
        // 맨 밑 규칙 유지 — 특화 StackPanel 의 마지막 자식
        var host = (StackPanel)System.Windows.Media.VisualTreeHelper.GetParent(section);
        Assert.Same(section, host.Children[host.Children.Count - 1]);
        panel.FlushDeferredCommits();
    });

    [Fact]
    public void should_show_common_values_when_all_selected_zones_share_them() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, spacing: 5.0, height: 3.2, mode: EnumFenceMode.SensorMount, render3D: false);
        using var b = NewZone(2, spacing: 5.0, height: 3.2, mode: EnumFenceMode.SensorMount, render3D: false);
        var panel = OpenGroup(rig, a, b);

        Assert.Equal(5.0, panel.PostSpacingM, 3); Assert.False(panel.IsPostSpacingInherited);
        Assert.Equal(3.2, panel.FenceHeightM, 3);
        Assert.Equal(EnumFenceMode.SensorMount, panel.FenceMode);
        Assert.False(panel.Render3DState);
        Assert.Equal("5.0 m", Text(panel, "GMaps.Property.PostSpacing"));
        Assert.Equal("3.2 m", Text(panel, "GMaps.Property.FenceHeight"));
        Assert.Equal(5.0, Id<Slider>(panel, "GMaps.Property.PostSpacing").Value, 3);
        Assert.Equal(EnumFenceMode.SensorMount, Id<ComboBox>(panel, "GMaps.Property.FenceMode").SelectedItem);
        Assert.False(Id<ToggleButton>(panel, "GMaps.Property.Render3D").IsChecked);
        Assert.Empty(rig.DbCalls);   // 여는 것만으로 저장하지 않는다
        panel.FlushDeferredCommits();
    });

    [Fact]
    public void should_show_dash_and_indeterminate_when_selected_zones_differ() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, spacing: 2.0, height: 2.0, mode: EnumFenceMode.Posts, render3D: true);
        using var b = NewZone(2, spacing: 5.0, height: 3.5, mode: EnumFenceMode.SensorMount, render3D: false);
        var panel = OpenGroup(rig, a, b);

        Assert.True(double.IsNaN(panel.PostSpacingM)); Assert.False(panel.IsPostSpacingInherited);
        Assert.True(double.IsNaN(panel.FenceHeightM));
        Assert.Null(panel.Render3DState);
        Assert.Equal("—", Text(panel, "GMaps.Property.PostSpacing"));
        Assert.Equal("—", Text(panel, "GMaps.Property.FenceHeight"));
        Assert.Null(Id<ComboBox>(panel, "GMaps.Property.FenceMode").SelectedItem);
        Assert.Null(Id<ToggleButton>(panel, "GMaps.Property.Render3D").IsChecked);
        // 슬라이더는 최소값에 파킹 — 강제 값이 원본(NaN)을 덮지 않는다
        Assert.Equal(1.0, Id<Slider>(panel, "GMaps.Property.PostSpacing").Value, 3);
        Assert.Equal(1.5, Id<Slider>(panel, "GMaps.Property.FenceHeight").Value, 3);
        Commit();
        Assert.True(double.IsNaN(panel.PostSpacingM));
        Assert.Empty(rig.DbCalls);
        // 마커 값은 그대로
        Assert.Equal(2.0, a.PostSpacingM); Assert.Equal(5.0, b.PostSpacingM);
        Assert.Equal(EnumFenceMode.Posts, a.FenceMode); Assert.Equal(EnumFenceMode.SensorMount, b.FenceMode);
    });

    [Fact]
    public void should_apply_only_height_to_every_zone_and_undo_as_one_unit_when_height_slider_moves() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, spacing: 2.0, height: 2.0, mode: EnumFenceMode.Posts, render3D: true);
        using var b = NewZone(2, spacing: 5.0, height: 3.5, mode: EnumFenceMode.SensorMount, render3D: false);
        using var c = NewZone(3, spacing: null, height: null);
        var panel = OpenGroup(rig, a, b, c);

        Id<Slider>(panel, "GMaps.Property.FenceHeight").Value = 3.0;
        Commit();

        Assert.Equal(3.0, a.FenceHeightM); Assert.Equal(3.0, b.FenceHeightM); Assert.Equal(3.0, c.FenceHeightM);
        // 다른 철망 값은 그대로
        Assert.Equal(2.0, a.PostSpacingM); Assert.Equal(5.0, b.PostSpacingM); Assert.Null(c.PostSpacingM);
        Assert.Equal(EnumFenceMode.Posts, a.FenceMode); Assert.Equal(EnumFenceMode.SensorMount, b.FenceMode);
        Assert.True(a.Render3D); Assert.False(b.Render3D);
        // 마커별 저장 1회씩(마커별 기존 저장 경로)
        Assert.Equal(3, rig.CountOf(GroupUpdate));
        Assert.Equal(new[] { 1, 2, 3 }, rig.DbCalls.Select(x => (int)x.Snapshot["Id"]!).OrderBy(x => x));
        Assert.All(rig.DbCalls, x => Assert.Equal(3.0, (double)x.Snapshot["FenceHeightM"]!));
        Assert.Equal("3.0 m", Text(panel, "GMaps.Property.FenceHeight"));

        // Undo 1회 = 전원 원복(상속 NULL 포함)
        Assert.Equal("그룹 속성 변경: FenceHeightM (3개)", rig.Undo.NextUndoDescription);
        Assert.True(rig.Undo.UndoAsync().GetAwaiter().GetResult());
        Assert.Equal(2.0, a.FenceHeightM); Assert.Equal(3.5, b.FenceHeightM); Assert.Null(c.FenceHeightM);
        Assert.False(rig.Undo.CanUndo);
    });

    [Fact]
    public void should_apply_spacing_preset_to_all_and_reset_to_inherited_default_when_reset_clicked() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, spacing: null);    // 상속(기본)
        using var b = NewZone(2, spacing: 5.0);     // 직접 값 — 상속 혼합
        var panel = OpenGroup(rig, a, b);
        Assert.True(double.IsNaN(panel.PostSpacingM)); Assert.False(panel.IsPostSpacingInherited);

        // 프리셋 3 m → 전원 3
        Id<Button>(panel, "GMaps.Property.PostSpacingPreset3").Command.Execute("3");
        Commit();
        Assert.Equal(3.0, a.PostSpacingM); Assert.Equal(3.0, b.PostSpacingM);
        Assert.Equal(2, rig.CountOf(GroupUpdate));

        // 「기본」 → 전원 NULL(전역 기본 상속) · 패널은 기본값 + 「기본」 표시
        var reset = Id<Button>(panel, "GMaps.Property.PostSpacingReset");
        reset.Command.Execute(reset.CommandParameter);
        Property3DRig.Pump();
        Assert.Null(a.PostSpacingM); Assert.Null(b.PostSpacingM);
        Assert.True(panel.IsPostSpacingInherited);
        Assert.Equal(panel.FencePostSpacingDefault, panel.PostSpacingM, 3);
        Assert.Equal(4, rig.CountOf(GroupUpdate));

        // 높이 「기본」도 그룹에 적용
        a.FenceHeightM = 3.3; b.FenceHeightM = 2.2;
        var hReset = Id<Button>(panel, "GMaps.Property.FenceHeightReset");
        hReset.Command.Execute(hReset.CommandParameter);
        Property3DRig.Pump();
        Assert.Null(a.FenceHeightM); Assert.Null(b.FenceHeightM);
        Assert.True(panel.IsFenceHeightInherited);

        // Undo 1회씩: 높이 기본 → 되돌림, 간격 기본 → 3 m 로
        Assert.True(rig.Undo.UndoAsync().GetAwaiter().GetResult());
        Assert.Equal(3.3, a.FenceHeightM); Assert.Equal(2.2, b.FenceHeightM);
        Assert.True(rig.Undo.UndoAsync().GetAwaiter().GetResult());
        Assert.Equal(3.0, a.PostSpacingM); Assert.Equal(3.0, b.PostSpacingM);
        Assert.True(rig.Undo.UndoAsync().GetAwaiter().GetResult());
        Assert.Null(a.PostSpacingM); Assert.Equal(5.0, b.PostSpacingM);   // 상속 혼합 원상태
    });

    [Fact]
    public void should_turn_3d_on_for_all_when_mixed_checkbox_clicked_and_off_again() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, render3D: true);
        using var b = NewZone(2, render3D: false);
        var panel = OpenGroup(rig, a, b);
        var box = Id<ToggleButton>(panel, "GMaps.Property.Render3D");
        Assert.Null(box.IsChecked);

        // 혼합에서 켬 — 대표(a)와 같은 값이어도 전원에 적용돼야 한다
        box.IsChecked = true;
        Property3DRig.Pump();
        Assert.True(a.Render3D); Assert.True(b.Render3D);
        Assert.Equal(2, rig.CountOf(GroupUpdate));

        box.IsChecked = false;
        Property3DRig.Pump();
        Assert.False(a.Render3D); Assert.False(b.Render3D);
        Assert.Equal(4, rig.CountOf(GroupUpdate));

        Assert.True(rig.Undo.UndoAsync().GetAwaiter().GetResult());
        Assert.True(a.Render3D); Assert.True(b.Render3D);
        Assert.True(rig.Undo.UndoAsync().GetAwaiter().GetResult());
        Assert.True(a.Render3D); Assert.False(b.Render3D);   // 혼합 원상태
    });

    [Fact]
    public void should_apply_fence_mode_to_all_when_mixed_combo_picks_one() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, mode: EnumFenceMode.Posts, height: 2.0);
        using var b = NewZone(2, mode: EnumFenceMode.SensorMount, height: 3.0);
        var panel = OpenGroup(rig, a, b);
        var combo = Id<ComboBox>(panel, "GMaps.Property.FenceMode");

        combo.SelectedItem = EnumFenceMode.SensorMount;
        Property3DRig.Pump();
        Assert.Equal(EnumFenceMode.SensorMount, a.FenceMode); Assert.Equal(EnumFenceMode.SensorMount, b.FenceMode);
        Assert.Equal(2.0, a.FenceHeightM); Assert.Equal(3.0, b.FenceHeightM);   // 다른 값 그대로
        Assert.Equal(2, rig.CountOf(GroupUpdate));
        panel.FlushDeferredCommits();
    });

    [Fact]
    public void should_skip_locked_zone_when_group_fence_value_is_applied() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, height: 2.0);
        using var b = NewZone(2, height: 2.0);
        using var locked = NewZone(3, height: 2.0, locked: true);
        var panel = OpenGroup(rig, a, b, locked);

        Id<Slider>(panel, "GMaps.Property.FenceHeight").Value = 3.5;
        Commit();
        Assert.Equal(3.5, a.FenceHeightM); Assert.Equal(3.5, b.FenceHeightM);
        Assert.Equal(2.0, locked.FenceHeightM);   // 잠긴 마커 제외(기존 그룹 규칙 — ApplyGroupPropertyChangeAsync)
        Assert.Equal(new[] { 1, 2 }, rig.DbCalls.Select(x => (int)x.Snapshot["Id"]!).OrderBy(x => x));
    });

    [Fact]
    public void should_show_that_markers_own_values_when_returning_to_single_selection() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1, spacing: 2.0, height: 2.0, mode: EnumFenceMode.Posts, render3D: true);
        using var b = NewZone(2, spacing: 5.0, height: 3.5, mode: EnumFenceMode.SensorMount, render3D: false);
        var group = OpenGroup(rig, a, b);
        Assert.True(double.IsNaN(group.FenceHeightM));

        var single = rig.OpenPanel<GMapPropertyPidsGroupControl>(b, "PidsGroupPropertyStyle.xaml");
        Assert.False(single.IsGroupEditing);
        Assert.Equal(5.0, single.PostSpacingM, 3); Assert.Equal(3.5, single.FenceHeightM, 3);
        Assert.Equal(EnumFenceMode.SensorMount, single.FenceMode);
        Assert.False(single.Render3D); Assert.False(single.Render3DState);
        Assert.NotEqual(string.Empty, single.FenceLayoutSummary);
        single.FlushDeferredCommits();
        group.FlushDeferredCommits();
    });

    [Fact]
    public void should_keep_device_group_combo_disabled_with_representative_value_in_group_editing() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1);
        using var b = NewZone(2);
        a.LinkedDeviceGroup = 7; b.LinkedDeviceGroup = 9;
        var panel = OpenGroup(rig, a, b);
        Assert.Equal(7, panel.LinkedDeviceGroup);   // 대표값
        var combo = Property3DRig.ByLabel<ComboBox>(panel, "장비 그룹");
        Assert.False(combo.IsEnabled);
        Assert.Equal(7, a.LinkedDeviceGroup); Assert.Equal(9, b.LinkedDeviceGroup);
        panel.FlushDeferredCommits();
    });

    [Fact]
    public void should_hide_fence_section_in_group_editing_when_3d_feature_is_off() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        using var a = NewZone(1);
        using var b = NewZone(2);
        var panel = OpenGroup(rig, a, b);
        panel.Is3DFeatureEnabled = false;
        Property3DRig.LayoutPanel(panel);
        Assert.Equal(Visibility.Collapsed, Id<Border>(panel, "GMaps.Property.FenceSection").Visibility);
        panel.FlushDeferredCommits();
    });
}
