using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests.Support;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Themes;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// 오프스크린 렌더(창을 띄우지 않음) — 라이트 · 다크 한 장씩 PNG. 토큰 해석 · 격자 · 머리/꼬리 · 타일 상태 · 우클릭 메뉴 구성을
/// 실제 XAML 로 확인한다. PNG 폴더: 환경 변수 <c>IRONWALL_T05_RENDER_DIR</c>, 없으면 %TEMP%\ironwall-camhost-render.
/// Application 은 AppDomain 당 하나라 두 테마를 한 시험 · 한 STA 스레드에서 차례로 그린다.
/// </summary>
[Collection(HostStaCollection.Name)]
public class EventWindowRenderTests
{
    private readonly ITestOutputHelper _output;
    private readonly HostStaThread _sta;

    public EventWindowRenderTests(ITestOutputHelper output, HostStaThread sta)
    {
        _output = output;
        _sta = sta;
    }

    private static string RenderDirectory()
    {
        var dir = Environment.GetEnvironmentVariable("IRONWALL_T05_RENDER_DIR");
        if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(Path.GetTempPath(), "ironwall-camhost-render");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void should_render_event_window_with_tokens_when_light_and_dark_themes_applied()
    {
        var files = new List<string>();
        var summaries = new List<string>();
        // Application 은 AppDomain 당 하나 — 호스트 시험이 함께 쓰는 STA 스레드(HostStaThread)에서 그린다.
        _sta.Invoke(() =>
        {
            var app = Application.Current!;
            foreach (var theme in new[] { HostTheme.Light, HostTheme.Dark })
            {
                HostTheme.Apply(app, theme);
                var (path, summary) = RenderOnce(theme);
                files.Add(path);
                summaries.Add(summary);
            }
        });

        foreach (var s in summaries) _output.WriteLine(s);
        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.True(new FileInfo(f).Length > 10_000, $"{f} looks empty"));
    }

    private static (string Path, string Summary) RenderOnce(string theme)
    {
        var clock = new FakeHostClock();
        var msg = new OpenEventWindow
        {
            Kind = EventWindowKind.Detection,
            EventId = "1042",
            Header = new EventWindowHeader { ZoneName = "구역-07", DeviceName = "펜스 센서 #104", EventTypeText = "침입", OccurredAt = new DateTimeOffset(2026, 9, 30, 9, 41, 7, TimeSpan.FromHours(9)) },
            GridColumns = 3,
            GridRows = 2,
            ExtraCameraCount = 1,
            TimerCloseSeconds = 60,
            Window = new PixelRect { X = 100, Y = 100, Width = 1000, Height = 620 },
            Cameras =
            {
                Cam("c1", "외곽 PTZ-3", ptz: true, target: "P2", delay: 19),
                Cam("c2", "외곽 PTZ-5", ptz: true, target: "P4"),
                Cam("c3", "정문 고정-1"),
                Cam("c4", "후문 고정-2"),
                Cam("c5", "외곽 PTZ-7", ptz: true, allowed: false),
            },
        };
        var vm = new EventWindowViewModel(msg, new TileCameraControlFactory(), clock, _ => { }, null);
        vm.Start();
        var tiles = vm.CameraTiles.ToArray();
        for (int i = 0; i < tiles.Length; i++) tiles[i].Video = FakeFrame(i);
        tiles[0].SetStreamState(StreamState.Playing, null);
        tiles[0].TogglePad();
        tiles[1].SetStreamState(StreamState.Playing, null);
        tiles[2].SetStreamState(StreamState.Playing, null);
        tiles[3].SetStreamState(StreamState.Failed, "open-timeout");
        tiles[3].Video = null;
        vm.Select(tiles[1]);
        clock.Advance(16); // 스토리보드처럼 "타이머 44초" · "이동 중 · 3초"
        vm.Tick();

        var view = new EventWindowView(vm);
        var root = view.RenderRoot;
        const double width = 1000, height = 620;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        DoEvents();
        root.UpdateLayout();

        // 우클릭 메뉴 구성(PTZ 권한 없는 타일) — 렌더 없이 항목 · AutomationId · 활성 상태만 확인.
        var menu = new ContextMenu();
        TileView.BuildMenu(menu, tiles[4], vm);
        var items = menu.Items.OfType<MenuItem>().ToArray();
        string MenuState(string suffix) => items.Single(m => AutomationProperties.GetAutomationId(m).EndsWith(suffix, StringComparison.Ordinal)).IsEnabled ? "on" : "off";
        string menuSummary = $"menu ptz={MenuState(".Ptz")} presets={MenuState(".Presets")} home={MenuState(".Home")} enlarge={MenuState(".Enlarge")} close={MenuState(".CloseTile")} reason='{items.Last().Header}'";
        Assert.Equal("off", MenuState(".Ptz"));
        Assert.Equal("on", MenuState(".CloseTile"));
        Assert.Contains(PtzAvailability.NoPermissionReason, (string)items.Last().Header);

        var dpi = 1.0;
        var bitmap = new RenderTargetBitmap((int)(width * dpi), (int)(height * dpi), 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(RenderDirectory(), $"event-window-{theme.ToLowerInvariant()}.png");
        using (var stream = File.Create(path)) encoder.Save(stream);

        // 토큰이 실제로 바뀌었는지 — 머리 바탕(SurfaceBrush)을 읽는다.
        var surface = (SolidColorBrush)Application.Current.FindResource("SurfaceBrush");
        view.CloseBySession();
        return (path, $"{theme}: {path} surface={surface.Color} timer='{vm.TimerText}' delay='{tiles[0].DelayText}' {menuSummary}");
    }

    private static EventWindowCamera Cam(string id, string name, bool ptz = false, bool allowed = true, string? target = null, int delay = 0) => new()
    {
        CameraId = id,
        Name = name,
        IsPtz = ptz,
        PtzAllowed = allowed,
        TargetPresetToken = target,
        TargetPresetName = target,
        DelaySeconds = delay,
        Provider = new VideoProviderInfo { Kind = VideoProviderKind.TestPattern },
    };

    /// <summary>영상 대신 — 카메라마다 다른 어두운 그라데이션 + 가로 띠.</summary>
    private static BitmapSource FakeFrame(int seed)
    {
        const int w = 320, h = 180;
        var pixels = new byte[w * h * 4];
        byte baseR = (byte)(30 + seed * 17 % 60), baseG = (byte)(45 + seed * 29 % 50), baseB = (byte)(55 + seed * 11 % 70);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                double t = (double)x / w * 0.6 + (double)y / h * 0.4;
                bool band = y > h * 0.62 && y < h * 0.7;
                pixels[i] = (byte)Math.Min(255, baseB + t * 60 + (band ? 60 : 0));
                pixels[i + 1] = (byte)Math.Min(255, baseG + t * 50 + (band ? 60 : 0));
                pixels[i + 2] = (byte)Math.Min(255, baseR + t * 40 + (band ? 60 : 0));
                pixels[i + 3] = 255;
            }
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        bmp.Freeze();
        return bmp;
    }

    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
