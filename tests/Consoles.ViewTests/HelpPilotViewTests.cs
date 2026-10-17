using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Accounts.Ui.ViewTests;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Tests;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// "?" 말풍선 시범(help-callout H-1) — 실제 뷰 · 실제 앱 사전(라이트/다크)에서 설명 목록 등록이 뷰보다 먼저 끝나 있고,
/// 화면이 거는 키가 전부 풀리며, 설정 콘솔 섹션의 설명 줄이 "?" 로 옮겨 갔는지 본다.
/// </summary>
/// <remarks>
/// 글 훑기(<see cref="HelpSourceScan"/>)는 Utils.Tests 의 것을 연결해 쓴다 — 거기서는 글끼리(키 ↔ 목록) 견주고,
/// 여기서는 그 키가 <b>런타임 등록(모듈 초기화)</b>으로 실제 풀리는지 견준다.
/// </remarks>
public class HelpPilotViewTests
{
    private const string DoubleClickKey = "Settings.CameraPopup.DoubleClick";

    [Theory]
    [InlineData("Ironwall.Dotnet.Libraries.Devices.Ui", typeof(Ironwall.Dotnet.Libraries.Devices.Ui.Help.DevicesHelp))]
    [InlineData("Ironwall.Dotnet.Libraries.Events.Ui", typeof(Ironwall.Dotnet.Libraries.Events.Ui.Help.EventsHelp))]
    public void should_resolve_every_screen_help_key_when_the_ui_assembly_has_loaded(string project, Type anyTypeInAssembly)
    {
        // Arrange — 어셈블리가 쓰이기 시작했다(뷰가 뜨기 직전과 같은 조건): 모듈 초기화가 설명 목록을 등록한다
        RuntimeHelpers.RunModuleConstructor(anyTypeInAssembly.Module.ModuleHandle);
        var keys = HelpSourceScan.XamlKeys(HelpSourceScan.RepoRoot(ThisFile())).Where(k => k.Project == project).Select(k => k.Key).Distinct().ToList();

        // Act
        var missing = keys.Where(k => !HelpCatalog.TryGet(k, out _)).ToList();

        // Assert
        Assert.NotEmpty(keys);
        Assert.True(missing.Count == 0, $"등록되지 않은 키: {string.Join(", ", missing)}");
    }

    [Fact]
    public void should_show_a_section_question_mark_and_drop_the_explanatory_note_in_the_camera_popup_settings_view() => AppHost.Run(() =>
    {
        var view = new CameraPopupSettingsView { DataContext = NewViewModel() };
        var window = AppHost.Show(view);
        try
        {
            var section = Descendants<ConsoleSection>(view).Single(s => s.HelpKey == DoubleClickKey);
            var tip = Descendants<HelpTip>(section).Single();
            var texts = Descendants<TextBlock>(view).Where(t => t.IsVisible).Select(t => KoreanWordWrap.Strip(t.Text)).ToList();

            Assert.Equal($"Help.{DoubleClickKey}", AutomationProperties.GetAutomationId(tip));
            Assert.True(tip.IsEnabled);                                                   // 등록이 뷰보다 먼저 끝났다
            Assert.True(tip.IsVisible);
            Assert.DoesNotContain(texts, t => t.Contains("지도 좌표로 기억", StringComparison.Ordinal));   // 칸 아래 설명 줄은 "?" 로

            tip.Open();
            AppHost.Pump();
            var body = tip.Callout!;
            Assert.True(tip.CalloutPopup!.IsOpen);
            Assert.Equal($"Help.{DoubleClickKey}.Body", AutomationProperties.GetAutomationId(body));
            Assert.Contains("지도 좌표로 기억", UIElementAutomationPeer.CreatePeerForElement(body).GetName());

            // 라이트/다크 — 말풍선 겉은 앱 사전의 토큰을 매번 다시 찾는다
            Assert.Same(Application.Current.FindResource("SurfaceAltBrush"), body.Background);
            AppHost.SetDark(true);
            Assert.Same(Application.Current.FindResource("SurfaceAltBrush"), body.Background);
            tip.Close();
        }
        finally
        {
            AppHost.SetDark(false);
            window.Close();
        }
    });

    private static CameraPopupSettingsViewModel NewViewModel()
    {
        var port = new Mock<ICameraPopupSettingsPort>();
        port.Setup(p => p.LoadCameraPopup()).Returns(new CameraPopupSettings().Normalize());
        port.SetupGet(p => p.CameraPopupClientId).Returns("gis-test");
        var monitors = new Mock<IDisplayMonitorProvider>();
        monitors.Setup(m => m.GetMonitors()).Returns(new List<DisplayMonitorInfo>
        {
            new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true),
        });
        return new CameraPopupSettingsViewModel(port.Object, monitors.Object);
    }

    private static string ThisFile([CallerFilePath] string? path = null) => path!;

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deep in Descendants<T>(child)) yield return deep;
        }
    }
}
