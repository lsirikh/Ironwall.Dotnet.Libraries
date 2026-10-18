using System;
using System.Linq;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>지도 3D 설정(AppSettings.Map3D) — 기본값 · Normalize · 게이트와 안내가 설정값을 따르는지 · 직렬화 왕복.</summary>
public class Map3DSettingsTests
{
    // ── 기본값 = 2026-10-17 까지의 고정값 ──

    [Fact]
    public void should_keep_previous_constants_when_constructed()
    {
        var m = new Map3DModel();
        Assert.Equal(Map3DMath.EnterZoom, m.EnterZoom);
        Assert.Equal(Map3DMath.ExitZoom, m.ExitZoom);
        Assert.Equal(Map3DMath.PreloadZoom, m.PreloadZoom);
        Assert.Equal(Map3DGuide.AreaOutlineMinZoom, m.AreaShowZoom);
        Assert.Equal(Map3DGuide.MaxAreaSideM, m.MaxAreaSideM);
        Assert.Equal(Map3DGuide.MinEnterShow.TotalSeconds, m.GuideMinSeconds);
        Assert.False(m.Normalize());
        Assert.Equal((17.0, 16.5, 15.0, 13.0, 2000d, 0.6), (m.EnterZoom, m.ExitZoom, m.PreloadZoom, m.AreaShowZoom, m.MaxAreaSideM, m.GuideMinSeconds));
    }

    // ── Normalize ──

    [Theory]
    [InlineData(18.0, 18.0, 15.0, 18.0, 17.5, 15.0)]   // 이탈 = 진입 → 진입 − 0.5
    [InlineData(17.0, 17.5, 15.0, 17.0, 16.5, 15.0)]   // 이탈 > 진입
    [InlineData(17.0, 16.0, 16.5, 17.0, 16.0, 16.0)]   // 미리 준비 > 이탈 → 이탈
    [InlineData(25.0, 9.0, 5.0, 20.5, 10.0, 10.0)]     // 범위 밖 → 10~20.5
    [InlineData(10.0, 10.0, 10.0, 10.5, 10.0, 10.0)]   // 진입이 하한이면 진입 10.5 · 이탈 10
    [InlineData(19.0, 18.0, 17.0, 19.0, 18.0, 17.0)]   // 올바른 값은 그대로
    public void should_order_zooms_when_normalized(double enter, double exit, double preload,
                                                   double expectedEnter, double expectedExit, double expectedPreload)
    {
        var m = new Map3DModel { EnterZoom = enter, ExitZoom = exit, PreloadZoom = preload };
        m.Normalize();
        Assert.Equal((expectedEnter, expectedExit, expectedPreload), (m.EnterZoom, m.ExitZoom, m.PreloadZoom));
    }

    [Theory]
    [InlineData(100, 200)]
    [InlineData(9000, 5000)]
    [InlineData(1500, 1500)]
    [InlineData(double.NaN, 2000)]
    public void should_clamp_area_size_when_normalized(double input, double expected)
    {
        var m = new Map3DModel { MaxAreaSideM = input };
        m.Normalize();
        Assert.Equal(expected, m.MaxAreaSideM);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 3)]
    [InlineData(1.2, 1.2)]
    [InlineData(double.PositiveInfinity, 0.6)]
    public void should_clamp_guide_seconds_when_normalized(double input, double expected)
    {
        var m = new Map3DModel { GuideMinSeconds = input };
        m.Normalize();
        Assert.Equal(expected, m.GuideMinSeconds);
    }

    [Theory]
    [InlineData(8, 10)]
    [InlineData(22, 20.5)]
    [InlineData(double.NaN, 13)]
    public void should_clamp_area_show_zoom_when_normalized(double input, double expected)
    {
        var m = new Map3DModel { AreaShowZoom = input };
        m.Normalize();
        Assert.Equal(expected, m.AreaShowZoom);
    }

    // ── 게이트 · 안내가 설정값을 따른다 ──

    [Theory]
    [InlineData(18.9, false, false)]   // 진입 19 밑
    [InlineData(19.0, false, true)]    // 진입
    [InlineData(18.5, true, true)]     // 3D 였으면 이탈 18.5 까지 유지
    [InlineData(18.4, true, false)]
    public void should_use_configured_zooms_when_deciding_gate(double zoom, bool wasActive, bool expected)
        => Assert.Equal(expected, Map3DMath.Decide(zoom, wasActive, hasScene: true, enterZoom: 19, exitZoom: 18.5));

    [Fact]
    public void should_use_configured_exit_zoom_when_leaving_or_staying()
    {
        Assert.True(Map3DMath.ShouldLeave3D(18.4, 18.5));
        Assert.False(Map3DMath.ShouldLeave3D(17.0, 16.0));
        Assert.True(Map3DMath.ShouldStay3DAfterMove(16.2, insideScene: true, exitZoom: 16.0));
        Assert.False(Map3DMath.ShouldStay3DAfterMove(16.2, insideScene: true, exitZoom: 16.5));
    }

    [Theory]
    [InlineData(13.9, 14.0, false)]
    [InlineData(14.0, 14.0, true)]
    [InlineData(14.0, 15.0, false)]
    public void should_use_configured_preload_zoom_when_preloading(double zoom, double preloadZoom, bool expected)
        => Assert.Equal(expected, Map3DMath.ShouldPreload(zoom, "s1", null, hostLoading: false, blocked: false, preloadZoom));

    [Fact]
    public void should_use_configured_area_values_when_showing_and_drawing()
    {
        Assert.False(Map3DGuide.ShouldShowAreaOutlines(14, 15));
        Assert.True(Map3DGuide.ShouldShowAreaOutlines(15, 15));
        Assert.True(Map3DGuide.ShouldShowChip(17.5, centerInsideArea: true, is3DOff: true, enterZoom: 18));
        Assert.False(Map3DGuide.ShouldShowChip(18, centerInsideArea: true, is3DOff: true, enterZoom: 18));
        Assert.True(Map3DGuide.IsAreaTooLarge(1001, 500, 1000));
        Assert.False(Map3DGuide.IsAreaTooLarge(2500, 500, 3000));
    }

    [Theory]
    [InlineData(2000, "2 km")]
    [InlineData(1500, "1.5 km")]
    [InlineData(800, "800 m")]
    public void should_format_limit_when_area_size_configured(double meters, string expected)
        => Assert.Equal(expected, Map3DGuide.LimitText(meters));

    [Fact]
    public void should_show_configured_values_in_texts_when_settings_changed()
    {
        Assert.Equal("3D 가능 — 줌 18 이상", Map3DGuide.ChipTextFor(18));
        Assert.Equal(Map3DGuide.ChipText, Map3DGuide.ChipTextFor(Map3DMath.EnterZoom));
        Assert.Contains("한 변 3 km 이하", Map3DGuide.AreaTooLargeText(3300, 500, 3000));
        Assert.Equal(Map3DGuide.AreaTooLargeText(3300, 500), Map3DGuide.AreaTooLargeText(3300, 500, 2000));
        Assert.EndsWith(" — 1 km 초과", Map3DGuide.AreaDrawLabel(new[] { 127.0, 37.0, 127.005, 37.01 }, 1000).Text);
        Assert.Contains("줌 18 밑", Map3DGuide.StageText("login", 18));
        Assert.Equal(Map3DGuide.StageText("login"), Map3DGuide.StageText("login", Map3DMath.ExitZoom));
        Assert.Equal(Map3DGuide.StageText("scene"), Map3DGuide.StageText("scene", 18));
    }

    [Theory]
    [InlineData(17.0, 17.5)]
    [InlineData(19.0, 19.0)]
    public void should_view_area_at_enter_zoom_or_higher_when_enter_zoom_raised(double enter, double expected)
        => Assert.Equal(expected, Map3DGuide.AreaViewZoomFor(enter));

    // ── 직렬화 왕복 — 저장(MapSettingsHelper.SaveSettingAsync = JToken.FromObject)과 불러오기(LoadMap3D 가 읽는 키 이름) ──

    /// <summary>LoadMap3D 가 읽는 키 — 저장되는 키 이름과 같아야 한다(속성 이름이 바뀌면 이 시험이 깨진다).</summary>
    private static readonly string[] LoadedKeys =
    {
        "IsEnabled", "ServerUrl", "UseCompositionHost", "HwndWhenRemote", "OverlayMode",
        "EnterZoom", "ExitZoom", "PreloadZoom", "AreaShowZoom", "MaxAreaSideM", "GuideMinSeconds",
    };

    [Fact]
    public void should_round_trip_all_keys_when_serialized()
    {
        var model = new Map3DModel
        {
            IsEnabled = false, ServerUrl = "http://10.0.0.5:9000", UseCompositionHost = false, HwndWhenRemote = false, OverlayMode = "extract",
            EnterZoom = 18.5, ExitZoom = 18, PreloadZoom = 16, AreaShowZoom = 14, MaxAreaSideM = 3000, GuideMinSeconds = 1.5,
        };
        var token = JObject.Parse(JToken.FromObject(model).ToString());
        Assert.Equal(LoadedKeys.OrderBy(k => k), token.Properties().Select(p => p.Name).OrderBy(k => k));

        var loaded = token.ToObject<Map3DModel>()!;
        Assert.False(loaded.Normalize());
        Assert.Equal(model.ToString(), loaded.ToString());
    }

    [Fact]
    public void should_fall_back_to_defaults_when_old_settings_lack_new_keys()
    {
        var loaded = JObject.Parse("{ \"IsEnabled\": true, \"ServerUrl\": \"http://127.0.0.1:8765\" }").ToObject<Map3DModel>()!;
        loaded.Normalize();
        Assert.Equal(new Map3DModel().ToString(), loaded.ToString());
    }
}
