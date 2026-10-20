using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>3D 들어가는 방식(Map3D.EntryMode, 2026-10-19) — 자동(줌) · 수동(단추) 판정 · 「3D 로 보기」 단추 보임 · 직접 진입 · 설정 기본값.</summary>
public class Map3DEntryModeTests
{
    // ── 설정 기본값 · Normalize ──

    [Fact]
    public void should_default_to_auto_when_constructed()
        => Assert.Equal(Map3DModel.EntryAuto, new Map3DModel().EntryMode);

    [Fact]
    public void should_read_auto_when_old_settings_lack_entry_mode()
    {
        var loaded = JObject.Parse("{ \"IsEnabled\": true, \"EnterZoom\": 18 }").ToObject<Map3DModel>()!;
        loaded.Normalize();
        Assert.Equal(Map3DModel.EntryAuto, loaded.EntryMode);
    }

    [Theory]
    [InlineData("manual", "manual")]
    [InlineData(" Manual ", "manual")]
    [InlineData("auto", "auto")]
    [InlineData("zoom", "auto")]
    [InlineData("", "auto")]
    [InlineData(null, "auto")]
    public void should_keep_only_auto_or_manual_when_normalized(string? input, string expected)
    {
        var m = new Map3DModel { EntryMode = input! };
        m.Normalize();
        Assert.Equal(expected, m.EntryMode);
    }

    [Fact]
    public void should_round_trip_manual_when_serialized()
    {
        var token = JObject.Parse(JToken.FromObject(new Map3DModel { EntryMode = Map3DModel.EntryManual }).ToString());
        Assert.Equal("manual", token["EntryMode"]!.ToString());
        Assert.Equal(Map3DModel.EntryManual, token.ToObject<Map3DModel>()!.EntryMode);
    }

    // ── 진입 판정 ──

    [Theory]
    [InlineData("auto", true)]
    [InlineData(null, true)]
    [InlineData("manual", false)]
    [InlineData("MANUAL", false)]
    public void should_tell_zoom_entry_when_entry_mode_given(string? mode, bool expected)
        => Assert.Equal(expected, Map3DMath.IsZoomEntry(mode));

    [Theory]
    [InlineData(16.9, false)]
    [InlineData(17.0, true)]
    [InlineData(19.0, true)]
    public void should_enter_by_zoom_when_auto(double zoom, bool expected)
        => Assert.Equal(expected, Map3DMath.DecideEntry(zoom, wasActive: false, hasScene: true, 17, 16.5, zoomEntry: true, directEntry: false));

    [Theory]
    [InlineData(17.0, false)]
    [InlineData(20.5, false)]
    [InlineData(17.0, true)]   // 준비 중이어도 줌 게이트 진입이면(직접 진입 아님) 유지하지 않는다
    public void should_never_enter_by_zoom_when_manual(double zoom, bool wasActive)
        => Assert.False(Map3DMath.DecideEntry(zoom, wasActive, hasScene: true, 17, 16.5, zoomEntry: false, directEntry: false));

    [Theory]
    [InlineData(false, 12.0)]   // 수동 · 진입 줌 한참 아래
    [InlineData(true, 12.0)]    // 자동 · 진입 줌 아래(「3D 로 보기」 단추)
    [InlineData(false, 17.5)]   // 수동 · 「이 영역 3D 로 보기」(줌 17.5 로 옮긴 뒤)
    public void should_keep_direct_entry_pending_when_zoom_below_gate(bool zoomEntry, double zoom)
        => Assert.True(Map3DMath.DecideEntry(zoom, wasActive: true, hasScene: true, 17, 16.5, zoomEntry, directEntry: true));

    [Fact]
    public void should_drop_direct_entry_when_center_left_scene()
        => Assert.False(Map3DMath.DecideEntry(18, wasActive: true, hasScene: false, 17, 16.5, zoomEntry: true, directEntry: true));

    [Fact]
    public void should_not_start_direct_entry_from_gate_when_not_pending()
        => Assert.False(Map3DMath.DecideEntry(12, wasActive: false, hasScene: true, 17, 16.5, zoomEntry: false, directEntry: true));

    // ── 「3D 로 보기」 단추 보임 ──

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]    // 3D 꺼짐
    [InlineData(true, false, true, false)]    // 3D 표시 · 준비 중
    [InlineData(true, true, false, false)]    // 화면 가운데가 3D 영역 밖
    public void should_show_enter_button_only_in_2d_inside_area_when_enabled(bool enabled, bool is3DOff, bool inside, bool expected)
        => Assert.Equal(expected, Map3DMath.IsEnterButtonVisible(enabled, is3DOff, inside));
}
