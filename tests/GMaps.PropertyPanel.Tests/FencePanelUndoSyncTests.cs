using System.Windows;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 사용자 보고(2026-09-08) "3D 철망 속성을 바꾼 뒤 undo/redo 가 안 된다".
/// <para>Undo 배선 자체(<c>IsReplayableProperty</c> · <c>ApplyProperty</c> · <c>ReadProperty</c>)는 3곳 모두 등재돼 있어
/// 모델은 정상적으로 되돌아간다. 문제는 <b>속성창이 그 되돌림을 따라가지 않는</b> 것이었다 — 3D 철망 절은 다른 속성과 달리
/// TwoWay 바인딩이 아니라 <c>SyncFenceFromMarker</c> 로 마커 로드 시 1회만 값을 받고, 패널은 마커의
/// <c>PropertyChanged</c> 를 구독하지 않았다. 그래서 undo 후에도 슬라이더가 옛 값에 머물러 "안 된다"로 보였다.</para>
/// </summary>
public class FencePanelUndoSyncTests
{
    private static GMapPidsGroupMarker NewGroupMarker()
    {
        var model = new PidsGroupSymbolModel { Title = "구역-undo", Latitude = 37.5, Longitude = 127.0, FenceMode = EnumFenceMode.Posts };
        model.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
        return new GMapPidsGroupMarker(Mock.Of<ILogService>(), model);
    }

    [Fact]
    public void should_follow_model_when_fence_height_is_undone() => AppHost.Run(() =>
    {
        using var marker = NewGroupMarker();
        var panel = new GMapPropertyPidsGroupControl { Is3DFeatureEnabled = true, SelectedMarker = marker };

        // 사용자가 높이를 3.5 m 로 바꾼 상태(커밋 완료) — 패널·모델 둘 다 3.5
        marker.FenceHeightM = 3.5;
        panel.FenceHeightM = 3.5;
        Assert.Equal(3.5, panel.FenceHeightM, 3);

        // Undo 재적용: 커맨드가 쓰는 것과 같은 단일 출처로 모델만 되돌린다
        UndoableCommandBase.ApplyProperty(marker, "FenceHeightM", 2.0);

        Assert.Equal(2.0, marker.FenceHeightM);
        Assert.Equal(2.0, panel.FenceHeightM, 3);           // ★ 구독이 없으면 3.5 로 남아 "undo 가 안 된 것처럼" 보였다
        Assert.False(panel.IsFenceHeightInherited);

        // Redo 도 같은 경로
        UndoableCommandBase.ApplyProperty(marker, "FenceHeightM", 3.5);
        Assert.Equal(3.5, panel.FenceHeightM, 3);
    });

    [Fact]
    public void should_show_inherited_default_when_height_is_undone_to_null() => AppHost.Run(() =>
    {
        using var marker = NewGroupMarker();
        var panel = new GMapPropertyPidsGroupControl { Is3DFeatureEnabled = true, SelectedMarker = marker };
        marker.FenceHeightM = 3.5; panel.FenceHeightM = 3.5;

        // '기본' 버튼 undo = NULL(전역 상속) 복원
        UndoableCommandBase.ApplyProperty(marker, "FenceHeightM", null);

        Assert.Null(marker.FenceHeightM);
        Assert.True(panel.IsFenceHeightInherited);
        Assert.Equal(panel.FenceHeightDefault, panel.FenceHeightM, 3);
    });

    [Fact]
    public void should_follow_model_for_every_fence_field() => AppHost.Run(() =>
    {
        using var marker = NewGroupMarker();
        var panel = new GMapPropertyPidsGroupControl { Is3DFeatureEnabled = true, SelectedMarker = marker };

        UndoableCommandBase.ApplyProperty(marker, "PostSpacingM", 5.0);
        Assert.Equal(5.0, panel.PostSpacingM, 3);
        Assert.False(panel.IsPostSpacingInherited);

        UndoableCommandBase.ApplyProperty(marker, "FenceMode", EnumFenceMode.SensorMount);
        Assert.Equal(EnumFenceMode.SensorMount, panel.FenceMode);

        UndoableCommandBase.ApplyProperty(marker, "Render3D", false);
        Assert.False(panel.Render3D);
    });

    [Fact]
    public void should_stop_following_after_marker_is_detached() => AppHost.Run(() =>
    {
        using var first = NewGroupMarker();
        using var second = NewGroupMarker();
        var panel = new GMapPropertyPidsGroupControl { Is3DFeatureEnabled = true, SelectedMarker = first };

        panel.SelectedMarker = second;                       // 마커 교체 → 이전 마커 구독 해제
        UndoableCommandBase.ApplyProperty(first, "FenceHeightM", 3.9);

        Assert.NotEqual(3.9, panel.FenceHeightM);            // 옛 마커의 변경이 새 마커 패널을 오염시키지 않는다
        UndoableCommandBase.ApplyProperty(second, "FenceHeightM", 3.9);
        Assert.Equal(3.9, panel.FenceHeightM, 3);            // 새 마커는 따라간다
    });
}
