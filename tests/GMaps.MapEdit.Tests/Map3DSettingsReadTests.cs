using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>3D 설정 읽기(MapSettingsHelper.ReadMap3D — LoadMap3D 가 AppSettings.Map3D 절을 읽는 길): 옛 파일 · 이상한 값 · 저장/다시 읽기 왕복(2026-10-19 EntryMode).</summary>
public class Map3DSettingsReadTests
{
    // ── 설정 파일 읽기(MapSettingsHelper.ReadMap3D = LoadMap3D 가 AppSettings.Map3D 절을 읽는 길) ──

    private static Map3DModel Read(string json)
    {
        var m = MapSettingsHelper.ReadMap3D(JObject.Parse(json));
        m.Normalize();
        return m;
    }

    [Fact]
    public void should_read_auto_and_keep_old_exit_zoom_when_old_file_has_only_exit_zoom()
    {
        var m = Read("{ \"IsEnabled\": true, \"EnterZoom\": 18, \"ExitZoom\": 17, \"PreloadZoom\": 15 }");
        Assert.Equal(Map3DModel.EntryAuto, m.EntryMode);
        Assert.Equal((18d, 17d, 15d), (m.EnterZoom, m.ExitZoom, m.PreloadZoom));   // 옛 이탈 줌은 내부 값으로 그대로
    }

    [Fact]
    public void should_read_defaults_when_section_missing()
    {
        var m = MapSettingsHelper.ReadMap3D(null);
        m.Normalize();
        Assert.Equal(new Map3DModel().ToString(), m.ToString());
    }

    [Theory]
    [InlineData("\"\"", "auto")]
    [InlineData("\"   \"", "auto")]
    [InlineData("null", "auto")]
    [InlineData("\"MANUAL\"", "manual")]
    [InlineData("\"Manual\"", "manual")]
    [InlineData("\" manual\"", "manual")]
    [InlineData("\"auto\"", "auto")]
    [InlineData("\"AUTO\"", "auto")]
    [InlineData("\"button\"", "auto")]
    [InlineData("\"수동\"", "auto")]
    [InlineData("1", "auto")]
    [InlineData("true", "auto")]
    public void should_fall_back_to_auto_unless_manual_when_entry_mode_odd(string raw, string expected)
        => Assert.Equal(expected, Read("{ \"EntryMode\": " + raw + " }").EntryMode);

    [Theory]
    [InlineData("auto")]
    [InlineData("manual")]
    public void should_round_trip_entry_mode_when_saved_and_read_again(string mode)
    {
        var saved = new Map3DModel { EntryMode = mode, EnterZoom = 18.5, ExitZoom = 18, PreloadZoom = 16 };
        var again = Read(JToken.FromObject(saved).ToString());   // 저장(SaveSettingAsync = JToken.FromObject) → 다시 읽기
        Assert.Equal(saved.ToString(), again.ToString());
        var twice = Read(JToken.FromObject(again).ToString());
        Assert.Equal(saved.ToString(), twice.ToString());
    }
}
