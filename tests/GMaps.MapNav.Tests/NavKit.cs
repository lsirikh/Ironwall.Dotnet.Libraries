using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Caliburn.Micro;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;

namespace GMaps.MapNav.Tests;

/// <summary>
/// 위치 · 줌 · 회전 · 기울이기 · 사이트 고정 시나리오 공용 도우미(창 없음).
/// 지도 컨트롤은 부모 없는 Measure/Arrange + 벤더 <c>InitializeForBackgroundRendering</c> 으로 코어만 시작한다(Loaded 대체 — 창을 띄우지 않는다).
/// 설정 저장(<c>MapSettingsHelper</c>)은 실행 폴더(<c>AppContext.BaseDirectory</c>)의 appsettings.json 에 쓰므로, 시험 프로젝트의 bin 에 빈 파일을 깔고 거기서 영속을 읽는다(실제 앱 경로 아님).
/// </summary>
internal static class NavKit
{
    public const double Lat = 37.5, Lng = 127.0;

    public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>시험용 빈 설정 파일(bin 안) — 저장 경로가 파일을 읽고 덮어쓴다.</summary>
    public static void ResetSettings()
    {
        // 앞 시험의 fire-and-forget 저장이 아직 파일을 쥐고 있을 수 있다(스레드 종료로 끊긴 스트림은 종료자가 닫는다) — 잠깐 재시도
        for (int i = 0; ; i++)
        {
            try { File.WriteAllText(SettingsPath, "{\"AppSettings\":{}}"); return; }
            catch (IOException) when (i < 100) { GC.Collect(); GC.WaitForPendingFinalizers(); Thread.Sleep(30); }
        }
    }

    public static JToken? Setting(string key)
    {
        try { return JObject.Parse(File.ReadAllText(SettingsPath))["AppSettings"]?[key]; }
        catch (Exception) { return null; }   // 쓰는 도중 읽기 — 다음 회차에 다시
    }

    /// <summary>비동기 저장(fire-and-forget)이 끝날 때까지 설정 파일을 기다린다(디스패처를 돌리며).</summary>
    public static async Task<JToken?> WaitSetting(string key, Func<JToken, bool> ok, int timeoutMs = 4000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            var t = Setting(key);
            if (t != null && ok(t)) return t;
            await Task.Delay(25);
        }
        return Setting(key);
    }

    public static async Task WaitUntil(Func<bool> condition, int timeoutMs = 4000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(20);
    }

    /// <summary>코어가 시작된 지도(창 없음). 벤더 정적 템플릿 3종은 이 STA 스레드에서 다시 만든다(MapLocateFitTests 선례).</summary>
    public static GMapCustomControl NewMap(double w = 1200, double h = 800, int zoom = 16, double lat = Lat, double lng = Lng, int minZoom = 5, int maxZoom = 19)
    {
        var map = new GMapCustomControl { MinZoom = minZoom, MaxZoom = maxZoom, Position = new PointLatLng(lat, lng) };
        map.Manager.Mode = AccessMode.ServerOnly;   // 타일 캐시(로컬 DB) 를 만들지 않는다 — 공급자는 빈 지도라 네트워크도 없다
        map.Zoom = zoom;
        map.SoftwareTierOverride = false;
        var itemsHost = new FrameworkElementFactory(typeof(Canvas));
        itemsHost.SetValue(Panel.IsItemsHostProperty, true);
        map.ItemsPanel = new ItemsPanelTemplate(itemsHost);
        var itemPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        itemPresenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Shape"));
        map.ItemTemplate = new DataTemplate(typeof(GMapMarker)) { VisualTree = itemPresenter };
        var itemStyle = new Style();
        itemStyle.Setters.Add(new Setter(Canvas.LeftProperty, new System.Windows.Data.Binding("LocalPositionX")));
        itemStyle.Setters.Add(new Setter(Canvas.TopProperty, new System.Windows.Data.Binding("LocalPositionY")));
        map.ItemContainerStyle = itemStyle;
        Scenario.Layout(map, w, h);
        map.InitializeForBackgroundRendering((int)w, (int)h);
        Pump();
        return map;
    }

    /// <summary>생성자 없이 만든 MapViewModel 에 지도 · 설정 · 로그 · 집계기만 꽂고 내비 · 지도 · 파일 명령을 초기화.</summary>
    public static MapViewModel NewVm(GMapCustomControl? map, GMapSetupModel? setup = null, IEventAggregator? ea = null)
    {
        var vm = Scenario.Uninit<MapViewModel>();
        vm.IsNotifying = true;
        Scenario.SetField(vm, "<MainMap>k__BackingField", map);
        Scenario.SetField(vm, "_setupModel", setup ?? new GMapSetupModel());
        Scenario.SetField(vm, "_log", Mock.Of<ILogService>());
        Scenario.SetField(vm, "_eventAggregator", ea ?? new Mock<IEventAggregator>().Object);
        Scenario.Call(vm, "InitializeNavigationCommands");
        Scenario.Call(vm, "InitializeMapCommands");
        Scenario.Call(vm, "InitializeFileCommands");
        return vm;
    }

    public static MapAnchorModel Anchor(double north, double west, double south, double east, bool allowRotation = false, int minZoomFloor = 0, bool strict = false)
        => new() { IsEnabled = true, NorthWest = new CoordinateModel(north, west, 0), SouthEast = new CoordinateModel(south, east, 0), AllowRotation = allowRotation, MinZoomFloor = minZoomFloor, StrictContainment = strict };

    /// <summary>집계기 가짜 — 발행된 메시지를 모은다(MapExitCommandTests 와 같은 방식).</summary>
    public static (IEventAggregator Ea, List<object> Published) Recorder()
    {
        var published = new List<object>();
        var mock = new Mock<IEventAggregator>();
        mock.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
            .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => published.Add(m))
            .Returns(Task.CompletedTask);
        return (mock.Object, published);
    }

    public static void Pump() => Scenario.Pump(DispatcherPriority.Background);

    /// <summary>창 없이 키 인자를 만든다(KeyEventArgs 는 PresentationSource 가 필수 — 가짜 소스).</summary>
    public static KeyEventArgs KeyArgs(Key key) => new(Keyboard.PrimaryDevice, new FakeSource(), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };

    private sealed class FakeSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    /// <summary>회전 kill-switch 는 정적 — 시험마다 원상(OFF · 구독 없음)으로 되돌린다.</summary>
    public static void ResetRotationFeature()
    {
        RotationFeature.IsEnabled = false;
        typeof(RotationFeature).GetField("EnabledChanged", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, null);
    }

    public static T PrivateConst<T>(Type type, string name)
        => (T)type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetRawConstantValue()!;

    // ── 저장소 파일(정적 판독) ──
    public static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, relative);
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException(relative);
    }

    public static XDocument MapViewXaml() => XDocument.Load(RepoFile(Path.Combine("Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml")));

    /// <summary>MapView 루트 InputBindings 의 (수식키+키 → 명령 바인딩) 표.</summary>
    public static Dictionary<string, string> MapViewKeyBindings()
        => MapViewXaml().Descendants().Where(e => e.Name.LocalName == "KeyBinding")
            .ToDictionary(e => $"{(string?)e.Attribute("Modifiers")}+{(string?)e.Attribute("Key")}", e => (string?)e.Attribute("Command") ?? "");
}
