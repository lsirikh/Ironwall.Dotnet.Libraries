using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map IMPL-32 — 부대 콘솔 뷰의 관계도 칸 · 툴바 둘째 줄 · 상세 단추.
/// 뷰는 결선만 한다: 여기서는 ① 뷰가 묶는 뷰모델 표면(레이어 한 칸씩 · 권한 없으면 비활성 + 사유)과
/// ② 선언(자리표시 제거 · 계측 id · <c>x:Name</c> 추가 0)을 본다. 실창 렌더 · UIA 트리는 헤드 시험(Phase 5)이 본다.
/// </summary>
public class UnitConsoleMapViewWiringTests
{
    #region - 레이어 토글 (FR-26 — 한 칸씩 묶는다) -
    [Fact]
    public async Task should_turn_off_only_that_layer_when_one_layer_flag_is_cleared()
    {
        var kit = await MapKit.OpenAsync();
        var changed = new List<string?>();
        kit.Vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        kit.Vm.ShowAdjacencyLayer = false;

        Assert.Equal(new UnitMapLayers(Hierarchy: true, Adjacency: false, DeviceBadges: true), kit.Vm.Layers);
        Assert.False(kit.Vm.ShowAdjacencyLayer);
        Assert.True(kit.Vm.ShowHierarchyLayer);
        Assert.True(kit.Vm.ShowDeviceBadgesLayer);
        Assert.Contains(nameof(UnitMapViewModel.ShowAdjacencyLayer), changed);
    }

    [Fact]
    public async Task should_follow_layers_set_elsewhere_when_layer_flags_are_read()
    {
        var kit = await MapKit.OpenAsync();
        var changed = new List<string?>();
        kit.Vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        kit.Vm.SetLayers(new UnitMapLayers(Hierarchy: false, Adjacency: true, DeviceBadges: false));

        Assert.False(kit.Vm.ShowHierarchyLayer);
        Assert.False(kit.Vm.ShowDeviceBadgesLayer);
        Assert.Contains(nameof(UnitMapViewModel.ShowHierarchyLayer), changed);
        Assert.Contains(nameof(UnitMapViewModel.ShowDeviceBadgesLayer), changed);
    }
    #endregion

    #region - 초기화 단추 — 숨기지 않고 비활성 + 사유 (FR-09 · IMPL-32) -
    [Fact]
    public async Task should_disable_reset_layout_and_say_why_when_operator_cannot_edit_shared_layout()
    {
        var kit = await MapKit.OpenAsync(canEdit: false);

        Assert.Equal(UnitMapLayoutState.Shared, kit.Vm.LayoutState);
        Assert.False(kit.Vm.CanResetLayout);
        Assert.Equal(UnitMapText.NoPermission, kit.Vm.ResetLayoutDisabledReason);
    }

    [Fact]
    public async Task should_enable_reset_layout_without_reason_when_operator_can_edit_shared_layout()
    {
        var kit = await MapKit.OpenAsync();

        Assert.True(kit.Vm.CanResetLayout);
        Assert.Null(kit.Vm.ResetLayoutDisabledReason);
    }

    [Fact]
    public async Task should_enable_node_reset_only_when_selected_unit_was_moved()
    {
        var api = new FakeUnitLayoutApi();
        var six = UnitMapTestData.Standard200().IdOf("6중대");
        api.SimulateOtherWrite(six, 50, 0);
        var kit = await MapKit.OpenAsync(api);
        var other = kit.F.IdOf("5중대");

        kit.Vm.RequestSelect(other);
        Assert.False(kit.Vm.CanResetSelectedNodeLayout);
        Assert.Equal(UnitMapText.NodeNotMovedStatus("5중대"), kit.Vm.ResetSelectedNodeLayoutDisabledReason);

        kit.Vm.RequestSelect(six);
        Assert.True(kit.Vm.CanResetSelectedNodeLayout);
        Assert.Null(kit.Vm.ResetSelectedNodeLayoutDisabledReason);

        await kit.Vm.ResetSelectedNodeLayoutAsync();
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(new[] { six }, Assert.Single(api.Writes).Change.Clear);
        Assert.False(kit.Vm.CanResetSelectedNodeLayout);
    }
    #endregion

    #region - 선언 (IMPL-32 · FR-40 · ISSUE-58) -
    [Fact]
    public void should_place_the_map_canvas_instead_of_the_placeholder_when_the_view_is_declared()
    {
        var xaml = ReadView();

        Assert.DoesNotContain("Units.Adjacency.Placeholder", xaml);
        Assert.DoesNotContain("인접 관계도를 아직 제공하지 않습니다", xaml);
        Assert.Matches(new Regex(@"<map:UnitMapCanvas\b[^>]*Interaction=""\{Binding Map\}""", RegexOptions.Singleline), xaml);
    }

    [Theory]
    [InlineData("Units.Map.Layer.Hierarchy")]
    [InlineData("Units.Map.Layer.Adjacency")]
    [InlineData("Units.Map.Layer.Devices")]
    [InlineData("Units.Map.IncludeSubordinates")]
    [InlineData("Units.Map.ResetLayout")]
    [InlineData("Units.Map.LocateOnMap")]
    [InlineData("Units.Map.Status")]
    [InlineData("Units.Detail.LocateOnMap")]
    [InlineData("Units.Detail.ResetNodeLayout")]
    public void should_declare_the_console_side_map_automation_id_when_the_view_is_declared(string automationId)
    {
        var xaml = ReadView();

        Assert.Contains($"AutomationProperties.AutomationId=\"{automationId}\"", xaml);
    }

    [Theory]
    [InlineData("Units.Map.Layer.Hierarchy")]
    [InlineData("Units.Map.Layer.Adjacency")]
    [InlineData("Units.Map.Layer.Devices")]
    [InlineData("Units.Map.IncludeSubordinates")]
    public void should_declare_on_off_map_settings_as_kernel_check_boxes_not_filter_chips_when_the_view_is_declared(string automationId)
    {
        // 실앱(2026-09-28 다크): 켜고 끄는 설정을 단일 선택 필터 칩(Console.Chip)으로 그려 켜진 셋이 두꺼운 이중 윤곽이 됐다.
        var element = Regex.Match(ReadView(), $@"<(\w+)\b[^>]*AutomationProperties\.AutomationId=""{Regex.Escape(automationId)}""[^>]*/>", RegexOptions.Singleline);

        Assert.True(element.Success, automationId);
        Assert.Equal("CheckBox", element.Groups[1].Value);
        Assert.Contains("Style=\"{StaticResource Console.CheckBox}\"", element.Value);
    }

    [Fact]
    public void should_keep_only_the_existing_template_part_names_when_the_map_is_wired()
    {
        // x:Name 은 CM 바인딩 지시자다 — 추가 · 변경 · 삭제 0. 있던 것은 트리 행 ControlTemplate 의 부품 이름 3개뿐이다.
        var names = Regex.Matches(ReadView(), "x:Name=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();

        Assert.Equal(new[] { "Bd", "Bar", "Focus" }, names);
    }

    [Fact]
    public void should_block_the_tree_and_detail_while_the_map_waits_for_confirmation_when_the_view_is_declared()
    {
        var xaml = ReadView();

        Assert.Contains("IsEnabled=\"{Binding IsConsoleInteractive}\"", xaml);
    }

    private static string ReadView([CallerFilePath] string? thisFile = null)
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Consoles", "Units", "UnitConsoleView.xaml"));
    #endregion
}
