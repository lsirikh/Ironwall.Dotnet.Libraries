using System.Windows;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;
using Key = System.Windows.Input.Key;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 선행 작업(787e)이 "의심"으로 남긴 3건의 판정 시험(t-20261011-081026-0754).
/// EDT-S1 편집 모드 Esc 선택 해제(MAP-107 · 138) · EDT-S2 팔레트 Esc 취소 안내(MAP-095) · EDT-S3 잠금 N개 제외 안내(MAP-136).
/// 셋 다 카탈로그 기대 · 도움말("Esc = 취소" · "잠긴 심볼은 옮기거나 지우지 않습니다") · 같은 화면의 다른 경로와 어긋나 결함으로 판정했다.
/// </summary>
public class EscapeAndLockedDeleteTests
{
    private static System.Windows.Input.KeyEventArgs Esc() => EditRig.Key(Key.Escape);

    // ───────────────────────── EDT-S1 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-107")]
    [Trait("Defect", "EDT-S1")]
    public void should_release_single_and_group_selection_and_close_property_panel_when_escape_is_pressed_on_map_in_edit_mode() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var a = rig.AddSymbol(61, "가", -60);
        var b = rig.AddSymbol(62, "나", 60);
        rig.SelectSingle(a); a.IsSelected = true;
        rig.Vm.IsPropertyPanelVisible = true;

        var e = Esc();
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForAim", rig.Map, e);   // 지도에서 Esc(남은 지도 모드 없음)

        Assert.True(e.Handled);
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.False(a.IsSelected);
        Assert.False(rig.Vm.IsPropertyPanelVisible);                       // 속성창 닫힘
        Assert.False(rig.Vm.HasSelectedItem);
        Assert.Equal("선택 해제", rig.Vm.AimStatusMessage);

        rig.SelectGroup(a, b);                                             // 그룹 선택도 같은 Esc 로
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForAim", rig.Map, Esc());
        Assert.False(rig.Group.HasSelection);
    });

    [Fact]
    [Trait("Scenario", "MAP-107")]
    public void should_keep_selection_when_escape_comes_from_outside_map_or_edit_mode_is_off() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var a = rig.AddSymbol(63, "가");
        rig.SelectSingle(a);

        // 창 후킹으로 온 다른 곳(대화상자 · 다른 패널)의 Esc — 원본이 지도 밖이면 선택을 건드리지 않는다
        var outside = Esc();
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForAim", new Border(), outside);
        Assert.False(outside.Handled);
        Assert.Same(a, rig.Vm.SelectedMarker);

        // 선택이 없으면 Esc 를 가로채지 않는다(다음 처리기 몫)
        var none = EditRig.Create();
        var e = Esc();
        Scenario.Call(none.Vm, "OnMapPreviewKeyDownForAim", none.Map, e);
        Assert.False(e.Handled);
    });

    [Fact]
    [Trait("Scenario", "MAP-138")]
    [Trait("Defect", "EDT-S1")]
    public void should_select_every_symbol_with_ctrl_a_then_release_all_with_escape() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var ms = new[] { rig.AddSymbol(71, "가", -60), rig.AddSymbol(72, "나", 0), rig.AddSymbol(73, "다", 60) };
        rig.Map.SetMultiSelectMode(true);

        Scenario.Call(rig.Map, "SelectAllMarkers");                       // Ctrl+A 분기
        Assert.All(ms, m => Assert.True(m.IsSelected));

        var e = Esc();
        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForAim", rig.Map, e);   // Esc
        Assert.True(e.Handled);
        Assert.All(ms, m => Assert.False(m.IsSelected));
        Assert.All(ms, m => Assert.Contains(m, rig.Map.Markers));         // 해제만 — 지우지 않는다
    });

    // ───────────────────────── EDT-S2 ─────────────────────────

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

    [Fact]
    [Trait("Scenario", "MAP-095")]
    [Trait("Defect", "EDT-S2")]
    public void should_say_placement_cancelled_without_row_when_escape_is_pressed_in_palette_list() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var view = Palette(rig);
        var items = (ListBox)view.Template.FindName("PART_Items", view)!;
        items.SelectedItem = items.Items[0];
        try
        {
            Scenario.Call(view, "ItemKeyDown", items, EditRig.Key(Key.Enter));    // 셀에서 Enter
            Assert.True(rig.Map.IsSymbolPlacementMode);

            var e = Esc();
            Scenario.Call(view, "ItemKeyDown", items, e);                         // 포커스가 팔레트에 남은 채 Esc
            Assert.True(e.Handled);
            Assert.False(rig.Map.IsSymbolPlacementMode);
            Assert.True(rig.Vm.IsAimStatusVisible);
            Assert.Equal("배치를 취소했습니다.", rig.Vm.AimStatusMessage);           // 지도 Esc 경로와 같은 안내
            Assert.DoesNotContain(rig.Db.Invocations, i => i.Method.Name.StartsWith("Insert"));

            // 배치 중이 아니면 안내도 없다(끌어 놓기 뒤 정리 · 다른 도구로 넘어갈 때와 구별)
            rig.Vm.IsAimStatusVisible = false;
            Scenario.Call(view, "ItemKeyDown", items, Esc());
            Assert.False(rig.Vm.IsAimStatusVisible);
        }
        finally { System.Windows.Input.Mouse.OverrideCursor = null; }
    });

    // ───────────────────────── EDT-S3 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-136")]
    [Trait("Defect", "EDT-S3")]
    public void should_tell_locked_exclusion_in_confirm_and_result_when_group_with_locked_member_is_deleted() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        rig.Db.Setup(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var a = rig.AddSymbol(81, "가", -60);
        var b = rig.AddSymbol(82, "나", 60);
        var locked = rig.AddSymbol(83, "잠김", 0, 60, locked: true);
        rig.SelectGroup(a, b, locked);

        Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForGroup", rig.Map, EditRig.Key(Key.Delete));
        await Task.Delay(20);
        var ask = Assert.Single(rig.Published.OfType<OpenConfirmPopupMessageModel>());
        Assert.Contains("2개", ask.Explain);
        Assert.Contains("잠금 1개 제외", ask.Explain);                    // 왜 3개가 아니라 2개인지 묻기 전에 알린다

        await rig.Vm.HandleAsync(new CallDeleteGroupSymbolsProcessMessageModel(), CancellationToken.None);

        Assert.Equal("2개 삭제(잠금 1개 제외)", rig.Vm.AimStatusMessage);  // 완료 글도 같은 말
        Assert.Contains(locked, rig.Map.Markers);

        // 잠긴 것이 없으면 덧붙이지 않는다
        var plain = EditRig.Create();
        plain.Db.Setup(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        plain.SelectGroup(plain.AddSymbol(84, "가", -60), plain.AddSymbol(85, "나", 60));
        Scenario.Call(plain.Vm, "OnMapPreviewKeyDownForGroup", plain.Map, EditRig.Key(Key.Delete));
        await Task.Delay(20);
        Assert.DoesNotContain("잠금", Assert.Single(plain.Published.OfType<OpenConfirmPopupMessageModel>()).Explain);
        await plain.Vm.HandleAsync(new CallDeleteGroupSymbolsProcessMessageModel(), CancellationToken.None);
        Assert.Equal("2개 삭제", plain.Vm.AimStatusMessage);
    });
}
