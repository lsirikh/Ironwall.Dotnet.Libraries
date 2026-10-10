using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>카탈로그 §1 속성창 순서 단추(MAP-121) · 최소 줌 화면 숨김(MAP-120) — 창 없이 VM · 지도 컨트롤 판정만 본다.</summary>
public class PropertyOrderZoomTests
{
    private static int Z(Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols.IEditableMarker m) => m.ZOrder;

    [Fact]
    [Trait("Scenario", "MAP-121")]
    public void should_raise_draw_order_and_persist_db_zorder_when_to_top_is_pressed() => Scenario.StaAsync(async () =>
    {
        var rig = EditRig.Create();
        var batches = new List<List<(int id, int zOrder)>>();
        rig.Db.Setup(d => d.BatchUpdateZOrderAsync(It.IsAny<List<(int id, int zOrder)>>(), It.IsAny<CancellationToken>()))
              .Callback<List<(int id, int zOrder)>, CancellationToken>((l, _) => batches.Add(l.ToList())).Returns(Task.CompletedTask);
        var a = rig.AddSymbol(61, "아래");
        var b = rig.AddSymbol(62, "위", 30);
        rig.SelectSingle(a);

        Scenario.Call(rig.Vm, "OnPropertyPanelZOrderChangeRequested", null, new ZOrderChangeRequestedEventArgs(ZOrderDirection.ToTop));
        await Task.Delay(30);

        Assert.True(Z(a) > Z(b), $"맨 위로 뒤 a={Z(a)} b={Z(b)}");   // 그리기 순서
        Assert.True(a.ZIndex > b.ZIndex, $"ZIndex a={a.ZIndex} b={b.ZIndex}");
        var last = Assert.Single(batches.Where(x => x.Any(p => p.id == 61)));
        Assert.Equal(Z(a), last.Single(p => p.id == 61).zOrder);                    // DB ZOrder
        Assert.True(rig.Vm.CanUndo);

        Scenario.Call(rig.Vm, "OnPropertyPanelZOrderChangeRequested", null, new ZOrderChangeRequestedEventArgs(ZOrderDirection.ToBottom));
        await Task.Delay(30);
        Assert.True(Z(a) < Z(b), $"맨 아래로 뒤 a={Z(a)} b={Z(b)}");
    });

    [Fact]
    [Trait("Scenario", "MAP-120")]
    public void should_hide_symbol_until_zoomed_in_when_min_zoom_exceeds_current_zoom() => Scenario.Sta(() =>
    {
        var rig = EditRig.Create();
        var m = rig.AddSymbol(71, "최소줌");
        rig.Map.RefreshMarkerVisibility(m);
        Assert.True(m.IsVisible);

        m.Zoom = 20;                          // 「최소 줌」 20 > 지금 18
        rig.Map.RefreshMarkerVisibility(m);   // 속성창 변경 경로(OnMarkerPropertyChanged "Zoom")가 부르는 것
        Assert.False(m.IsVisible);

        rig.Map.Zoom = 20;                    // 줌인
        rig.Map.RefreshMarkerVisibility(m);
        Assert.True(m.IsVisible);
    });
}
