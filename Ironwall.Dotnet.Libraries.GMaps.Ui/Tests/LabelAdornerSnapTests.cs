using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 라벨 끌기 · 폭 조절을 놓을 때 저장값 정리(map-property-numeric-precision PRD — 제목 폭 정수 px).
/// 실창 r29 5회차 NUM-R29-A6: 라벨 폭 끌기 뒤 DB TitleMaxWidth=446.7 · LabelOffsetX=220.0000000000001.
/// 창 없이 — 어도너는 GetUninitializedObject 로 만들고 정리 함수만 부른다.
/// </summary>
public class LabelAdornerSnapTests
{
    private static LabelAdorner AdornerFor(object marker)
    {
        var adorner = (LabelAdorner)RuntimeHelpers.GetUninitializedObject(typeof(LabelAdorner));
        typeof(LabelAdorner).GetField("_marker", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(adorner, marker);
        return adorner;
    }

    private static void Snap(LabelAdorner adorner, bool includeWidth)
        => typeof(LabelAdorner).GetMethod("SnapStoredValues", BindingFlags.Instance | BindingFlags.NonPublic)!
                               .Invoke(adorner, new object[] { includeWidth });

    private static void RunSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { body(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new Xunit.Sdk.XunitException(error.ToString());
    }

    [Fact]
    [Trait("Scenario", "NUM-LBL-01")]
    public void should_store_integer_width_and_px_offset_when_point_symbol_label_resize_is_released()
    {
        RunSta(() =>
        {
            var model = new SymbolModel { Id = 1, Title = "R29" };
            var marker = new GMapCustomMarker(null!, model);
            marker.TitleMaxWidth = 446.7;
            marker.LabelOffsetX = 220.0000000000001;
            marker.LabelOffsetY = -13.4999;

            Snap(AdornerFor(marker), includeWidth: true);

            Assert.Equal(447d, model.TitleMaxWidth);
            Assert.Equal(220d, model.LabelOffsetX);
            Assert.Equal(-13d, model.LabelOffsetY);
        });
    }

    [Fact]
    [Trait("Scenario", "NUM-LBL-02")]
    public void should_keep_width_untouched_when_only_label_offset_drag_is_released()
    {
        RunSta(() =>
        {
            var model = new SymbolModel { Id = 2, Title = "R29" };
            var marker = new GMapCustomMarker(null!, model);
            marker.TitleMaxWidth = 200;
            marker.LabelOffsetX = 12.6;

            Snap(AdornerFor(marker), includeWidth: false);

            Assert.Equal(200d, model.TitleMaxWidth);
            Assert.Equal(13d, model.LabelOffsetX);
        });
    }

    [Fact]
    [Trait("Scenario", "NUM-LBL-03")]
    public void should_keep_four_ratio_digits_when_image_label_offset_is_released()
    {
        var model = new ImageModel { Id = 3, Title = "IMG" };
        var marker = new GMapImageMarker(null, model);
        marker.LabelOffsetU = 0.123456789;
        marker.LabelOffsetV = -1.000049;
        marker.TitleMaxWidth = 300.4;

        Snap(AdornerFor(marker), includeWidth: true);

        Assert.Equal(0.1235, model.LabelOffsetU);      // 비율은 정수로 자르면 위치가 무너진다 — 4자리
        Assert.Equal(-1.0, model.LabelOffsetV);
        Assert.Equal(300d, model.TitleMaxWidth);
    }
}
