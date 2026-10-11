using System.Collections.ObjectModel;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// LAY-D2 수정(t-20261011-081026-0754)의 나머지 갈래 — 결함 증명 시험(LayerPanelScenarioTests)은 심볼만 본다.
/// 여기서는 ① 오버레이 이미지를 고른 채 레이어 패널에서 잠그면 선택이 풀리는지(심볼과 같은 규칙)
/// ② 다른 경로로 잠긴 선택이 남아 있어도 Delete 백스톱이 확인 창을 띄우지 않고 까닭을 알리는지 ③ 잠금을 풀면 다시 지울 수 있는지를 본다.
/// </summary>
public class LockWhileSelectedTests
{
    private static (MapVm f, GMapImageMarker image, GMapPidsMarker sym, LayerTreeNode images) Open()
    {
        var f = new MapVm();
        var (image, layer) = MapVm.Image(1, 11, "항공 A", @"C:\maps\overlay-a.png");
        var sym = MapVm.Pids(5, 50, 37.391, 126.961, "카메라-5");
        f.Map.Markers.Add(image); f.Map.Markers.Add(sym);
        f.Layers.Add(layer);
        var provider = new Ironwall.Dotnet.Libraries.GMaps.Providers.SymbolProvider();
        provider.Add(sym.Model);
        Scenario.SetField(f.Vm, "_symbolProvider", provider);
        f.CallAsync("LoadLayersFromDbAsync").GetAwaiter().GetResult();
        f.Vm.ShowLayerPanel();
        var images = f.Get<ObservableCollection<LayerTreeNode>>("_layerTreeNodes").Single(n => n.OverlayLayerType == "OverlayImage");
        return (f, image, sym, images);
    }

    [Trait("Scenario", "MAP-069")]
    [Fact]
    public void should_release_selection_and_offer_no_delete_when_selected_overlay_image_is_then_locked_from_layer_panel() => Scenario.Sta(() =>
    {
        var (f, image, _, images) = Open();
        f.Vm.SelectedMarker = image;                                  // 먼저 고른 이미지를

        images.Children[0].ToggleLockCommand.Execute(null);           // 레이어 패널에서 잠근다

        Assert.True(image.IsLocked);
        Assert.Null(f.Vm.SelectedMarker);                             // 선택이 풀려 끌기 · Delete 표적이 남지 않는다
        f.Call("ExecuteDeleteSelected", new object?[] { null });
        Assert.Empty(f.Of<OpenConfirmPopupMessageModel>());
    });

    [Trait("Scenario", "MAP-060")]
    [Fact]
    public void should_refuse_delete_with_reason_and_allow_it_again_after_unlock_when_locked_symbol_stays_selected() => Scenario.Sta(() =>
    {
        var (f, _, sym, _) = Open();
        sym.IsLocked = true;                                          // 레이어 패널을 거치지 않은 잠금(Undo · 그룹 잠금 등)
        f.Vm.SelectedMarker = sym;

        f.Call("ExecuteDeleteSelected", new object?[] { null });

        Assert.Empty(f.Of<OpenConfirmPopupMessageModel>());           // 확인 창 없음
        Assert.Equal("잠긴 항목은 삭제할 수 없습니다.", f.Vm.AimStatusMessage);
        Assert.True(f.Vm.IsAimStatusVisible);

        sym.IsLocked = false;                                         // 잠금을 풀면 원래대로 확인 창
        f.Call("ExecuteDeleteSelected", new object?[] { null });
        var ask = Assert.Single(f.Of<OpenConfirmPopupMessageModel>());
        Assert.Contains("'카메라-5'", ask.Explain);
    });

    // 카탈로그 기대 칸에 직접 대응하는 줄이 없어 표지를 달지 않는다(수정의 부작용 없음 확인용 대조).
    [Fact]
    public void should_keep_selection_when_selected_symbol_is_unlocked_or_another_symbol_is_locked() => Scenario.Sta(() =>
    {
        var (f, image, sym, images) = Open();
        f.Vm.SelectedMarker = sym;

        images.Children[0].ToggleLockCommand.Execute(null);           // 고르지 않은 이미지를 잠가도
        Assert.Same(sym, f.Vm.SelectedMarker);                        // 고른 심볼은 그대로
        Assert.True(image.IsLocked);
    });
}
