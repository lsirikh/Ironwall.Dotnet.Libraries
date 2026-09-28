using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// unit-relationship-map TEST-38 (FR-15 · FR-16 · FR-26 · NFR-14) — 첫 화면 · 개인 뷰 · 검색 · 제대 칩 · 레이어 기억.
/// 시나리오: SIM-V082~V087 · 조정자 필수 항목 2(검색은 Enter · 다음 일치 때만 — 글자마다 뛰지 않는다, 일치 없음은 분명히).
/// </summary>
public class UnitMapViewModelViewTests
{
    private static void SeedView(MapKit kit, double scale, double x, double y, int v = UnitMapViewModel.PrefVersion)
    {
        kit.Prefs.Entry.Extra ??= new Dictionary<string, JsonElement>();
        kit.Prefs.Entry.Extra[UnitMapViewModel.PrefKey] = JsonSerializer.SerializeToElement(new { v, scale, cx = x, cy = y });
    }

    #region - 첫 화면 (FR-15) -
    [Fact]
    public async Task should_restore_saved_personal_view_when_present()
    {
        var kit = MapKit.Create();
        SeedView(kit, 0.5, 1200, 300);
        await kit.Vm.OpenAsync();

        kit.Vm.AttachSurface(kit.Surface);

        Assert.Equal(new[] { "SetView:0.50@1200,300" }, kit.Surface.Calls);
    }

    [Fact]
    public async Task should_clamp_saved_scale_when_damaged()
    {
        // SIM-V083 — 배율은 범위로 자른다(중심은 캔버스 팬 한계가 조직 ≥20% 로 자른다)
        var kit = MapKit.Create();
        SeedView(kit, 7.0, 10, 20);
        await kit.Vm.OpenAsync();

        kit.Vm.AttachSurface(kit.Surface);

        Assert.Equal(new[] { "SetView:1.60@10,20" }, kit.Surface.Calls);
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("version")]
    public async Task should_fall_back_to_my_unit_at_half_scale_when_saved_view_is_unusable(string how)
    {
        // SIM-V087 · ISSUE-53 — JSON 이 깨졌거나 판(v)이 다르면 무시하고 폴백(다음 저장이 덮는다)
        var f = UnitMapTestData.Standard200();
        var kit = MapKit.Create(myUnitId: f.IdOf("7중대"));
        if (how == "corrupt") kit.Prefs.Entry.Extra = new Dictionary<string, JsonElement> { [UnitMapViewModel.PrefKey] = JsonSerializer.SerializeToElement("깨진 값") };
        else SeedView(kit, 0.5, 1200, 300, v: 9);
        await kit.Vm.OpenAsync();

        kit.Vm.AttachSurface(kit.Surface);

        Assert.Equal(new[] { $"CenterOn:{f.IdOf("7중대")}@0.50" }, kit.Surface.Calls);
    }

    [Fact]
    public async Task should_fit_all_when_no_saved_view_and_no_my_unit()
    {
        var kit = await MapKit.OpenAsync(myUnitId: 99999);   // 편제에 없는 내 부대(SIM-V086)

        kit.Vm.AttachSurface(kit.Surface);

        Assert.Equal(new[] { "Fit" }, kit.Surface.Calls);
    }

    [Fact]
    public async Task should_say_empty_and_keep_view_when_no_units()
    {
        // SIM-V019 — 부대 0: 빈 상태 · 배율 유지(맞춤 호출 0)
        var kit = MapKit.Create();
        kit.Vm.SetData(Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model.UnitTreeModel.Empty, System.Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.UnitDeviceItem>());
        await kit.Vm.OpenAsync();

        kit.Vm.AttachSurface(kit.Surface);

        Assert.Empty(kit.Surface.Calls);
        Assert.Equal(Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model.UnitMapText.EmptyMapStatus, kit.Vm.StatusText);
    }

    [Fact]
    public async Task should_save_one_namespaced_key_with_view_and_layers_only()
    {
        // ISSUE-53 · NFR-14 — Extra 에는 한 키 unitMap = {v, scale, cx, cy, layers}. 공유 배치(Δ)는 없다
        var api = new FakeUnitLayoutApi();
        var kit = await MapKit.OpenAsync(api);
        kit.Vm.AttachSurface(kit.Surface);
        kit.Vm.CompleteDrag(new UnitMapDropRequest(kit.Id("6중대"), 30, 0, null, false));
        await kit.Vm.WhenIdleAsync();
        kit.Surface.Scale = 0.72;
        kit.Surface.CenterWorld = new Point(500, 400);

        kit.Vm.SetLayers(new UnitMapLayers(Hierarchy: true, Adjacency: false, DeviceBadges: true));
        kit.Vm.SaveView();

        Assert.Equal(new[] { UnitMapViewModel.PrefKey }, kit.Prefs.Entry.Extra!.Keys);
        var pref = kit.Prefs.Entry.Extra[UnitMapViewModel.PrefKey];
        Assert.Equal(1, pref.GetProperty("v").GetInt32());
        Assert.Equal(0.72, pref.GetProperty("scale").GetDouble(), 9);
        Assert.Equal(500, pref.GetProperty("cx").GetDouble(), 9);
        Assert.Equal(400, pref.GetProperty("cy").GetDouble(), 9);
        Assert.False(pref.GetProperty("layers").GetProperty("adjacency").GetBoolean());
        Assert.DoesNotContain("dx", pref.ToString());
        Assert.True(kit.Prefs.Saves >= 1);
    }

    [Fact]
    public async Task should_save_once_after_view_goes_idle_not_per_pan_frame()
    {
        // 조정자 — 팬 프레임마다가 아니라 멈춘 뒤 한 번
        var kit = await MapKit.OpenAsync();
        kit.Vm.AttachSurface(kit.Surface);
        var saves = kit.Prefs.Saves;

        for (var i = 0; i < 30; i++) kit.Surface.RaiseViewChanged();
        Assert.Equal(saves, kit.Prefs.Saves);

        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();
        Assert.Equal(saves + 1, kit.Prefs.Saves);
    }

    [Fact]
    public async Task should_save_view_when_disposed()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.AttachSurface(kit.Surface);
        var saves = kit.Prefs.Saves;

        kit.Vm.Dispose();

        Assert.Equal(saves + 1, kit.Prefs.Saves);
    }
    #endregion

    #region - 레이어 (FR-26) -
    [Fact]
    public async Task should_restore_layers_from_same_key_and_put_them_in_scene()
    {
        var kit = MapKit.Create();
        kit.Vm.SetLayers(new UnitMapLayers(Hierarchy: false, Adjacency: true, DeviceBadges: false));
        kit.Vm.SaveView();
        var saved = kit.Prefs.Entry.Extra![UnitMapViewModel.PrefKey];

        var again = MapKit.Create();
        again.Prefs.Entry.Extra = new Dictionary<string, JsonElement> { [UnitMapViewModel.PrefKey] = saved };
        var reopened = new UnitMapViewModel(again.Commands, again.Gate, new UnitMapViewModelOptions { Prefs = again.Prefs.Entry, Delay = again.Delay.Run });
        reopened.SetData(again.F.Tree, again.Devices);
        await reopened.OpenAsync();

        Assert.Equal(new UnitMapLayers(false, true, false), reopened.Layers);
        Assert.Equal(new UnitMapLayers(false, true, false), reopened.Scene.Layers);
    }
    #endregion

    #region - 검색 (FR-16 · 필수 항목 2) -
    [Fact]
    public async Task should_select_and_center_first_match_then_next_match_when_search_repeated()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.AttachSurface(kit.Surface);
        kit.Surface.Calls.Clear();
        kit.Surface.Level = UnitMapLevel.L1;

        Assert.True(kit.Vm.SearchNext("대대"));
        var first = kit.Vm.SelectedUnitId;
        Assert.True(kit.Vm.SearchNext("대대"));
        var second = kit.Vm.SelectedUnitId;

        Assert.Equal(kit.Id("1대대"), first);
        Assert.Equal(kit.Id("2대대"), second);
        Assert.Equal(new[] { $"CenterOn:{first}", $"CenterOn:{second}" }, kit.Surface.Calls);   // 배율 유지
        Assert.Equal(2, kit.Commands.SelectCalls);
    }

    [Fact]
    public async Task should_zoom_to_l1_entry_when_search_jumps_from_overview()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.AttachSurface(kit.Surface);
        kit.Surface.Calls.Clear();
        kit.Surface.Level = UnitMapLevel.L0;

        kit.Vm.SearchNext("c0207");

        Assert.Equal(new[] { $"CenterOn:{kit.Id("7중대")}@0.40" }, kit.Surface.Calls);
    }

    [Fact]
    public async Task should_say_no_match_and_keep_selection_when_search_finds_nothing()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.RequestSelect(kit.Id("7중대"));

        Assert.False(kit.Vm.SearchNext("없는부대"));

        Assert.Equal(kit.Id("7중대"), kit.Vm.SelectedUnitId);
        Assert.Equal("‘없는부대’와 일치하는 부대가 없습니다.", kit.Vm.StatusText);
        Assert.False(kit.Vm.SearchNext("   "));
    }

    [Fact]
    public async Task should_wrap_to_first_match_when_last_match_passed()
    {
        var kit = await MapKit.OpenAsync();
        kit.Vm.SearchNext("연대");   // 1연대
        kit.Vm.SearchNext("연대");   // 2연대
        kit.Vm.SearchNext("연대");   // 3연대

        kit.Vm.SearchNext("연대");

        Assert.Equal(kit.Id("1연대"), kit.Vm.SelectedUnitId);
    }
    #endregion

    #region - 제대 칩 강조 (FR-16 · R-19) -
    [Fact]
    public async Task should_dim_other_echelons_without_removing_nodes_when_echelon_highlighted()
    {
        var kit = await MapKit.OpenAsync();

        kit.Vm.SetEchelonHighlight(EnumUnitEchelon.Company);

        Assert.Equal(200, kit.Vm.Scene.Positions.Count);
        Assert.False(kit.Vm.Scene.FactsOf(kit.Id("7중대")).IsDimmed);
        Assert.True(kit.Vm.Scene.FactsOf(kit.Id("2대대")).IsDimmed);
        Assert.Equal(EnumUnitEchelon.Company, kit.Vm.EchelonHighlight);

        kit.Vm.SetEchelonHighlight(null);
        Assert.DoesNotContain(kit.Vm.Scene.Facts.Values, f => f.IsDimmed);
    }
    #endregion
}
