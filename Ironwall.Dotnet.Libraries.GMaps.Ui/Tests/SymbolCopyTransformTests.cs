using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>MapSymbol_Shortcut_CopyPasteDelete FR-01 — 복사 코어의 순수 변형(ApplyCopyTransform) 단위테스트.
/// P0 버그(PIDS 미링크·Id 유실)·재배치·LinePoints 평행이동·제목 정책(D-01) 검증. DB 무관.</summary>
public class SymbolCopyTransformTests
{
    private static readonly PointLatLng Src = new PointLatLng(37.5000, 127.0000);
    private static readonly PointLatLng Dst = new PointLatLng(37.5010, 127.0020);   // Δ=(+0.0010, +0.0020)

    [Fact]
    public void should_unlink_device_when_copy_pids()
    {
        var m = new PidsSymbolModel { Id = 42, Title = "cam", LinkedDeviceId = 1968, Latitude = Src.Lat, Longitude = Src.Lng };
        MapViewModel.ApplyCopyTransform(m, Src, Dst, appendCopySuffix: false);
        Assert.Equal(0, m.LinkedDeviceId);   // 실장비 오참조 방지(D-02, `+1000` 버그 대체)
    }

    [Fact]
    public void should_unlink_group_when_copy_pidsgroup()
    {
        var m = new PidsGroupSymbolModel { Id = 7, Title = "grp", LinkedDeviceGroup = 116, Latitude = Src.Lat, Longitude = Src.Lng };
        MapViewModel.ApplyCopyTransform(m, Src, Dst, appendCopySuffix: false);
        Assert.Equal(0, m.LinkedDeviceGroup);
    }

    [Fact]
    public void should_reset_id_when_copy()
    {
        var m = new SymbolModel { Id = 99, Title = "s", Latitude = Src.Lat, Longitude = Src.Lng };
        MapViewModel.ApplyCopyTransform(m, Src, Dst, appendCopySuffix: false);
        Assert.Equal(0, m.Id);   // DB AUTO_INCREMENT 신규발급 유도(RecordAdd Id>0 가드 통과 → Undo 정상)
    }

    [Fact]
    public void should_move_position_to_target_when_copy()
    {
        var m = new SymbolModel { Id = 1, Title = "s", Latitude = Src.Lat, Longitude = Src.Lng };
        MapViewModel.ApplyCopyTransform(m, Src, Dst, appendCopySuffix: false);
        Assert.Equal(Dst.Lat, m.Latitude, 9);
        Assert.Equal(Dst.Lng, m.Longitude, 9);
    }

    [Fact]
    public void should_translate_linepoints_by_delta_when_copy_line()
    {
        var m = new LineSymbolModel
        {
            Id = 3, Title = "line", Latitude = Src.Lat, Longitude = Src.Lng,
            LinePoints = new List<GeoPoint>
            {
                new GeoPoint(37.5000, 127.0000, 0),
                new GeoPoint(37.5005, 127.0007, 0),
            }
        };
        MapViewModel.ApplyCopyTransform(m, Src, Dst, appendCopySuffix: false);
        // Δ = Dst - Src = (+0.0010, +0.0020) — 각 포인트가 동일 델타로 평행이동(상대 간격 유지, FR-03).
        Assert.Equal(37.5010, m.LinePoints[0].Latitude, 9);
        Assert.Equal(127.0020, m.LinePoints[0].Longitude, 9);
        Assert.Equal(37.5015, m.LinePoints[1].Latitude, 9);
        Assert.Equal(127.0027, m.LinePoints[1].Longitude, 9);
    }

    [Fact]
    public void should_append_copy_suffix_when_duplicate()
    {
        var m = new SymbolModel { Id = 1, Title = "제어기1", Latitude = Src.Lat, Longitude = Src.Lng };
        MapViewModel.ApplyCopyTransform(m, Src, Dst, appendCopySuffix: true);
        Assert.Equal("제어기1_Copy", m.Title);   // 복제 버튼(D-01)
    }

    [Fact]
    public void should_keep_title_when_paste()
    {
        var m = new SymbolModel { Id = 1, Title = "제어기1", Latitude = Src.Lat, Longitude = Src.Lng };
        MapViewModel.ApplyCopyTransform(m, Src, Dst, appendCopySuffix: false);
        Assert.Equal("제어기1", m.Title);   // 붙여넣기는 _Copy 미부가 → 반복 붙여넣기 시 X_Copy_Copy 방지(D-01)
    }
}
