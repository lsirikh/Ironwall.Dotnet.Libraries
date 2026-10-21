using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Weather;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 3D 환경(해 · 날씨 · 차 · 사람, env3d-contract A · B · C) 헤드리스 시나리오 — 실제 VM · 실제 설정 묶음 · 빠른 판(창 없이),
/// 가짜 날씨 제공자 · 가짜 저장 · 가짜 3D 보내기. 바깥(기상청 · 3D 엔진 · appsettings)에는 닿지 않는다.
/// </summary>
public class Map3DEnvironmentScenarioTests
{
    private sealed class FakeWeather : IWeatherProvider
    {
        public readonly Queue<Func<WeatherSnapshot>> Answers = new();
        public readonly List<(double Lat, double Lon)> Calls = new();
        public string Source => "kma";

        public Task<WeatherSnapshot> FetchAsync(double lat, double lon, CancellationToken ct = default)
        {
            Calls.Add((lat, lon));
            try { return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue()() : throw new WeatherFetchException(WeatherFailure.Unreachable, "답 없음")); }
            catch (Exception ex) { return Task.FromException<WeatherSnapshot>(ex); }
        }
    }

    private sealed record Rig(Map3DEnvironmentViewModel Vm, Map3DEnvironmentModel Model, FakeWeather Weather, List<string> Posted, List<string> Saved, Mock<ILogService> Log)
    {
        public JObject Last => JsonConvert.DeserializeObject<JObject>(Posted[^1], new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;
    }

    private static Rig Make(Map3DEnvironmentModel? model = null, bool knowsLocation = true, Func<Map3DEnvironmentModel, Task>? save = null)
    {
        (double, double)? location = knowsLocation ? (37.56, 126.98) : null;
        var m = model ?? new Map3DEnvironmentModel();
        var weather = new FakeWeather();
        var posted = new List<string>();
        var saved = new List<string>();
        var log = new Mock<ILogService>();
        var vm = new Map3DEnvironmentViewModel(m, weather, log.Object, save ?? (x => { saved.Add(JsonConvert.SerializeObject(x)); return Task.CompletedTask; }))
        {
            SaveDelay = TimeSpan.Zero,
        };
        vm.Attach(posted.Add, () => location);
        return new Rig(vm, m, weather, posted, saved, log);
    }

    private static WeatherSnapshot Snow(double temp = -1.5)
        => new("overcast", "snow", 0.6, 0, 3.2, 270, temp, "kma", new DateTimeOffset(DateTime.Today.AddHours(14)));

    // ── 바꾸면 바로 3D · 저장 ──

    [Fact]
    public void should_send_env_set_and_save_same_source_when_values_change() => OneSta.Sta(() =>
    {
        var r = Make();
        r.Vm.CarsEnabled = false;
        r.Vm.TimeMode = "manual";
        r.Vm.ManualTimeMinutes = 400;   // 끌기 막대 → 06:40
        r.Vm.PeopleDensity = 0.2;

        Assert.Equal("06:40", r.Model.ManualTime);
        Assert.Equal("env.set", (string?)r.Last["type"]);
        Assert.Equal("manual", (string?)r.Last["time"]!["mode"]);
        Assert.EndsWith("T06:40:00" + DateTimeOffset.Now.ToString("zzz"), (string)r.Last["time"]!["iso"]!);
        Assert.False((bool)r.Last["traffic"]!["cars"]!);
        Assert.Equal(0.2, (double)r.Last["traffic"]!["people_density"]!);
        Assert.Equal(4, r.Posted.Count);
        Assert.Contains("\"CarsEnabled\":false", r.Saved[^1]);
        Assert.Contains("\"ManualTime\":\"06:40\"", r.Saved[^1]);
        Assert.Equal("저장됨", r.Vm.SaveStatus);
    });

    [Fact]
    public void should_save_once_when_values_change_quickly() => OneSta.StaAsync(async () =>
    {
        var r = Make();
        r.Vm.SaveDelay = TimeSpan.FromMilliseconds(60);
        for (int i = 0; i <= 10; i++) r.Vm.CarDensity = i / 10.0;   // 막대를 끄는 동안
        Assert.Empty(r.Saved);
        await Task.Delay(300);
        Assert.Single(r.Saved);
        Assert.Contains("\"CarDensity\":1.0", r.Saved[0]);
        Assert.Equal(11, r.Posted.Count);   // 3D 에는 매번 바로
    });

    [Fact]
    public void should_report_save_failure_without_throwing() => OneSta.Sta(() =>
    {
        var r = Make(save: _ => throw new System.IO.IOException("디스크 가득"));
        r.Vm.ManualSky = "overcast";
        Assert.Equal("저장 실패 — 로그 확인", r.Vm.SaveStatus);
        Assert.Equal("overcast", (string?)r.Last["weather"]!["sky"]);
        r.Log.Verify(l => l.Error(It.Is<string>(s => s.Contains("디스크 가득")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    });

    [Fact]
    public void should_normalize_bad_input_from_screen() => OneSta.Sta(() =>
    {
        var r = Make();
        r.Vm.ManualTime = "25:99";
        r.Vm.ManualIntensity = 7;
        r.Vm.WeatherRefreshMinutes = 1;
        Assert.Equal(("12:00", 1.0, 10), (r.Model.ManualTime, r.Model.ManualIntensity, r.Model.WeatherRefreshMinutes));
    });

    // ── 자동 날씨 ──

    [Fact]
    public void should_not_call_weather_until_3d_scene_is_rendered() => OneSta.Sta(() =>
    {
        var r = Make();
        r.Weather.Answers.Enqueue(() => Snow());
        r.Vm.WeatherMode = "auto";
        Assert.Empty(r.Weather.Calls);
        Assert.Equal("기상청 날씨는 3D 를 열면 받습니다", r.Vm.WeatherStatus);

        r.Vm.OnSceneRendered();
        Assert.Single(r.Weather.Calls);
        Assert.Equal((37.56, 126.98), r.Weather.Calls[0]);
        Assert.Equal("kma", (string?)r.Last["weather"]!["source"]);
        Assert.StartsWith("기상청 · 흐림 · 눈 · -1.5℃ (14:00 관측)", r.Vm.WeatherStatus);

        r.Vm.OnSceneRendered();   // 다른 장면을 다시 열어도 갱신 고리는 하나
        Assert.Single(r.Weather.Calls);
    });

    [Fact]
    public void should_send_manual_weather_when_auto_never_succeeded() => OneSta.Sta(() =>
    {
        var r = Make(new Map3DEnvironmentModel { WeatherMode = "auto", ManualSky = "partly" });
        r.Vm.OnSceneRendered();   // 가짜는 답이 없으면 「닿지 않음」

        Assert.Equal(("manual", "partly"), ((string?)r.Last["weather"]!["source"], (string?)r.Last["weather"]!["sky"]));
        Assert.Equal("인터넷에 닿지 않습니다 — 폐쇄망이면 수동으로\n지금은 직접 고른 날씨로 보입니다", r.Vm.WeatherStatus);
        r.Log.Verify(l => l.Warning(It.Is<string>(s => s.Contains("Unreachable")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    });

    [Fact]
    public void should_keep_last_weather_when_later_fetch_fails() => OneSta.StaAsync(async () =>
    {
        var r = Make(new Map3DEnvironmentModel { WeatherMode = "auto" });
        r.Weather.Answers.Enqueue(() => Snow(2));
        r.Weather.Answers.Enqueue(() => throw new WeatherFetchException(WeatherFailure.KeyRejected, "resultCode=30"));
        r.Vm.OnSceneRendered();
        await r.Vm.RefreshWeatherAsync();

        Assert.Equal(("kma", "snow", 2.0), ((string?)r.Last["weather"]!["source"], (string?)r.Last["weather"]!["precip"], (double)r.Last["weather"]!["temp_c"]!));
        Assert.Equal("인증키가 맞지 않습니다\n마지막으로 받은 날씨 (14:00 관측)를 그대로 보입니다", r.Vm.WeatherStatus);
    });

    [Fact]
    public void should_survive_unexpected_provider_error() => OneSta.Sta(() =>
    {
        var r = Make(new Map3DEnvironmentModel { WeatherMode = "auto" });
        r.Weather.Answers.Enqueue(() => throw new InvalidOperationException("버그"));
        r.Vm.OnSceneRendered();
        Assert.StartsWith("날씨를 받지 못했습니다 — 로그 확인", r.Vm.WeatherStatus);
        r.Log.Verify(l => l.Error(It.Is<string>(s => s.Contains("버그")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    });

    [Fact]
    public void should_go_back_to_manual_values_when_mode_switches_to_manual() => OneSta.Sta(() =>
    {
        var r = Make(new Map3DEnvironmentModel { WeatherMode = "auto" });
        r.Weather.Answers.Enqueue(() => Snow());
        r.Vm.OnSceneRendered();
        Assert.Equal("kma", (string?)r.Last["weather"]!["source"]);

        r.Vm.WeatherMode = "manual";
        Assert.Equal(("manual", "clear"), ((string?)r.Last["weather"]!["source"], (string?)r.Last["weather"]!["sky"]));
        Assert.Equal("", r.Vm.WeatherStatus);
        Assert.False(r.Vm.HasWeatherStatus);
    });

    // ── [연결 시험] ──

    [Fact]
    public void should_show_connection_test_result_in_plain_words() => OneSta.StaAsync(async () =>
    {
        var r = Make(new Map3DEnvironmentModel { WeatherMode = "auto" });
        r.Weather.Answers.Enqueue(() => new WeatherSnapshot("clear", "none", 0, 0, 1, 90, 12, "kma", DateTimeOffset.Now));
        r.Weather.Answers.Enqueue(() => throw new WeatherFetchException(WeatherFailure.KeyRejected, "x"));

        await r.Vm.TestConnectionAsync();
        Assert.Equal("연결됨 — 맑음 · 12℃", r.Vm.ConnectionTestText);
        Assert.Equal("kma", (string?)r.Last["weather"]!["source"]);   // 자동 모드면 받은 값을 바로 쓴다

        await r.Vm.TestConnectionAsync();
        Assert.Equal("인증키가 맞지 않습니다", r.Vm.ConnectionTestText);

        var nowhere = Make(knowsLocation: false);
        await nowhere.Vm.TestConnectionAsync();
        Assert.Equal("지도 위치를 아직 모릅니다", nowhere.Vm.ConnectionTestText);
        Assert.Empty(nowhere.Weather.Calls);
    });

    // ── env.state ──

    [Fact]
    public void should_show_sunrise_and_sunset_from_engine() => OneSta.Sta(() =>
    {
        var r = Make();
        Assert.False(r.Vm.HasSunText);
        r.Vm.OnEngineState(Map3DMath.ParseMessage("{\"type\":\"env.state\",\"sun\":{\"sunrise\":\"06:41\",\"sunset\":\"17:52\"},\"cars\":3}")!.EnvState!);
        Assert.Equal("해 뜸 06:41 · 해 짐 17:52", r.Vm.SunText);
        Assert.True(r.Vm.HasSunText);
    });

    // ── MapViewModel 배선(rendered · env.state · 위치) ──

    [Fact]
    public void should_route_3d_messages_and_area_center_through_map_view_model() => OneSta.Sta(() =>
    {
        var env = Make(new Map3DEnvironmentModel { WeatherMode = "auto" });
        env.Weather.Answers.Enqueue(() => Snow());
        var map = Scenario.Uninit<MapViewModel>();
        Scenario.SetField(map, "_map3DScenes", new List<Map3DScene> { new("job-1", new[] { 127.0, 37.0, 127.2, 37.4 }) });
        Scenario.SetField(map, "_map3DJob", "job-1");

        var previous = IoC.GetAllInstances;
        IoC.GetAllInstances = t => t == typeof(Map3DEnvironmentViewModel) ? new object[] { env.Vm } : Array.Empty<object>();
        try { Assert.Same(env.Vm, map.Map3DEnvironment); }   // 판 바인딩이 처음 읽을 때 DI 싱글톤을 붙인다
        finally { IoC.GetAllInstances = previous; }

        Scenario.Call(map, "OnMap3DEnvironmentMessage", Map3DMath.ParseMessage("{\"type\":\"rendered\",\"job\":\"job-1\"}"));
        Assert.Single(env.Weather.Calls);
        Assert.Equal(37.2, env.Weather.Calls[0].Lat, 6);    // 3D 영역 가운데
        Assert.Equal(127.1, env.Weather.Calls[0].Lon, 6);

        Scenario.Call(map, "OnMap3DEnvironmentMessage", Map3DMath.ParseMessage("{\"type\":\"env.state\",\"sun\":{\"sunrise\":\"07:01\"}}"));
        Assert.Equal("해 뜸 07:01", env.Vm.SunText);
    });

    [Fact]
    public void should_leave_map_without_environment_panel_when_vm_is_not_registered() => OneSta.Sta(() =>
    {
        var map = Scenario.Uninit<MapViewModel>();
        var previous = IoC.GetAllInstances;
        IoC.GetAllInstances = _ => throw new InvalidOperationException("IoC is not initialized.");
        try { Assert.Null(map.Map3DEnvironment); }
        finally { IoC.GetAllInstances = previous; }
        Scenario.Call(map, "OnMap3DEnvironmentMessage", Map3DMath.ParseMessage("{\"type\":\"rendered\"}"));   // 던지지 않는다

        var panel = new Map3DEnvironmentQuickPanel { DataContext = null };
        Assert.Equal(Visibility.Collapsed, panel.Visibility);
    });

    // ── 화면(창 없이) ──

    private static T Find<T>(DependencyObject root, string automationId) where T : DependencyObject
        => Descendants(root).OfType<T>().First(e => AutomationProperties.GetAutomationId(e) == automationId);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }

    /// <summary>자기와 조상이 모두 Visible 인가(창 없이는 IsVisible 이 늘 false).</summary>
    private static bool Shown(DependencyObject e)
    {
        for (var d = e; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
        return true;
    }

    private static string Why(DependencyObject e)
    {
        var parts = new List<string>();
        for (var d = e; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is UIElement u) parts.Add($"{d.GetType().Name}:{u.Visibility}");
        return string.Join(" > ", parts);
    }

    [Fact]
    public void should_drive_settings_block_controls() => OneSta.Sta(() =>
    {
        var r = Make(new Map3DEnvironmentModel { KmaServiceKey = "SAVEDKEY" });
        var block = new Map3DEnvironmentSettingsBlock { DataContext = r.Vm };
        NoPopupHelp.Install(block);
        Scenario.Layout(block, 420, 900);
        block.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        var url = Find<TextBox>(block, "GMaps.Map3DEnv.KmaBaseUrl");
        Assert.False(Shown(url), Why(url));   // 수동 모드 — 기상청 칸은 숨김
        Assert.True(Shown(Find<ComboBox>(block, "GMaps.Map3DEnv.ManualSky")));

        Find<ComboBox>(block, "GMaps.Map3DEnv.WeatherMode").SelectedValue = "auto";
        Scenario.Layout(block, 420, 900);
        Assert.Equal("auto", r.Model.WeatherMode);
        Assert.True(Shown(url));
        Assert.False(Shown(Find<ComboBox>(block, "GMaps.Map3DEnv.ManualSky")));

        var key = Find<PasswordBox>(block, "GMaps.Map3DEnv.KmaServiceKey");
        Assert.Equal("SAVEDKEY", key.Password);   // 불러온 키를 가림 칸에
        key.Password = " NEWKEY ";
        key.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        Assert.Equal("NEWKEY", r.Model.KmaServiceKey);

        Find<ComboBox>(block, "GMaps.Map3DEnv.Refresh").SelectedItem = 60;
        Find<ComboBox>(block, "GMaps.Map3DEnv.TimeMode").SelectedValue = "cycle";
        Scenario.Layout(block, 420, 900);
        Find<Slider>(block, "GMaps.Map3DEnv.CycleSpeed").Value = 45;
        Find<CheckBox>(block, "GMaps.Map3DEnv.People").IsChecked = false;
        Assert.Equal((60, "cycle", 45, false), (r.Model.WeatherRefreshMinutes, r.Model.TimeMode, r.Model.CycleMinutesPerSecond, r.Model.PeopleEnabled));
        Assert.False(Find<Slider>(block, "GMaps.Map3DEnv.PeopleDensity").IsEnabled);
        Assert.Contains("\"PeopleEnabled\":false", r.Saved[^1]);
    });

    [Fact]
    public void should_open_quick_panel_and_change_same_source() => OneSta.Sta(() =>
    {
        var r = Make();
        var panel = new Map3DEnvironmentQuickPanel { DataContext = r.Vm };
        NoPopupHelp.Install(panel);
        Scenario.Layout(panel, 320, 600);
        Assert.Equal(Visibility.Visible, panel.Visibility);
        var body = Find<Border>(panel, "GMaps.Map3DEnv.QuickPanel");
        Assert.Equal(Visibility.Collapsed, body.Visibility);

        Find<Button>(panel, "GMaps.Map3DEnv.QuickToggle").Command.Execute(null);
        Scenario.Layout(panel, 320, 600);
        Assert.Equal(Visibility.Visible, body.Visibility);

        Find<ComboBox>(panel, "GMaps.Map3DEnv.QuickTimeMode").SelectedValue = "manual";
        Scenario.Layout(panel, 320, 600);
        Find<Slider>(panel, "GMaps.Map3DEnv.QuickTime").Value = 1080;   // 18:00 — 해 질 무렵
        Find<ComboBox>(panel, "GMaps.Map3DEnv.QuickPrecip").SelectedValue = "rain";
        Find<CheckBox>(panel, "GMaps.Map3DEnv.QuickCars").IsChecked = false;

        Assert.Equal(("manual", "18:00", "rain", false), (r.Model.TimeMode, r.Model.ManualTime, r.Model.ManualPrecip, r.Model.CarsEnabled));
        Assert.Equal("rain", (string?)r.Last["weather"]!["precip"]);
        Assert.Contains("\"ManualPrecip\":\"rain\"", r.Saved[^1]);   // 빠른 판에서 바꾼 값도 설정에 저장
    });
}
