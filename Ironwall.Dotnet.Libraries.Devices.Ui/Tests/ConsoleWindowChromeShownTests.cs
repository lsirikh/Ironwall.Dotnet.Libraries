using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// B2 — 실제 창을 <b>띄워서</b>(화면 밖 · 활성화 없음) 콘솔 OS 창의 겉이 입혀지는지 본다. 판정은 창이 뜰 때
/// (틀이 창의 <see cref="PresentationSource"/> 에 붙을 때) 일어나므로 띄우는 길 그대로 확인한다.
/// </summary>
/// <remarks>
/// 이 시험 어셈블리에는 <see cref="Application"/> 을 만드는 시험이 없다 — 그래서 여기서는 <see cref="Window.Show"/> 가 창 원본을 만든다
/// (Utils.Tests 에서는 다른 반이 만든 Application 의 스레드가 끝나 Show 가 원본을 만들지 못해 판정 함수를 직접 부른다).
/// </remarks>
public class ConsoleWindowChromeShownTests
{
    [Fact]
    public void should_paint_the_caption_when_the_confirm_window_is_shown()
    {
        var result = OnSta(() =>
        {
            var view = new ConfirmPromptView { DataContext = new ConfirmPromptViewModel("부대 삭제", "제1중대를 지웁니다.") };
            var window = NewWindow(view, "부대 삭제", ResizeMode.NoResize);
            var closed = false;
            window.Closed += (_, _) => closed = true;
            window.Show();
            Pump();

            var applied = ConsoleWindowChrome.GetIsApplied(window);
            var close = ById(window, ConsoleWindowChrome.CloseAutomationId);
            var peer = close is null ? null : UIElementAutomationPeer.CreatePeerForElement(close);
            var snapshot = (
                Applied: applied,
                Title: window.Title,
                CaptionText: Descendants<TextBlock>(window).Any(t => t.Text == "부대 삭제" && t.FontSize == 12),
                CloseIsControl: peer?.IsControlElement() ?? false,
                OldIds: new[] { "Devices.Assembly.Confirm.Accept", "Devices.Assembly.Confirm.Cancel" }.All(id => ById(window, id) is not null),
                Minimize: ById(window, ConsoleWindowChrome.MinimizeAutomationId)?.Visibility);

            ((IInvokeProvider)peer!.GetPattern(PatternInterface.Invoke)).Invoke();
            Pump();
            return (snapshot, closed);
        });

        Assert.True(result.snapshot.Applied);
        Assert.Equal("부대 삭제", result.snapshot.Title);            // OS 제목(자동화가 창을 찾는 이름)은 그대로
        Assert.True(result.snapshot.CaptionText);                     // 제목 줄에 같은 한글 제목
        Assert.True(result.snapshot.CloseIsControl);                  // 뜬 창에서 ✕ 는 UIA 제어 요소다
        Assert.True(result.snapshot.OldIds);                          // 틀의 옛 식별자는 그대로
        Assert.Equal(Visibility.Collapsed, result.snapshot.Minimize); // NoResize 창 = ✕ 만(OS 와 같다)
        Assert.True(result.closed);                                   // ✕ 는 OS 닫기 길로 창을 닫는다
    }

    [Fact]
    public void should_leave_the_window_alone_when_the_frame_is_wrapped_by_a_preview_host()
    {
        var applied = OnSta(() =>
        {
            var view = new ConfirmPromptView { DataContext = new ConfirmPromptViewModel("확인", "미리보기") };
            var window = NewWindow(new Border { Margin = new Thickness(12), Child = view }, "미리보기", ResizeMode.NoResize);
            window.Show();
            Pump();
            var result = ConsoleWindowChrome.GetIsApplied(window);
            window.Close();
            return result;
        });

        Assert.False(applied);
    }

    /// <summary>뿌리가 커널 틀이 아닌 창 뷰(조립기 · 결선 · 지표 이력)는 스스로 켠다 — 빠지면 그 창만 크림색 제목 줄로 남는다.</summary>
    [Theory]
    [InlineData("Assembly", "AssemblyView.xaml")]
    [InlineData("Wiring", "WiringView.xaml")]
    [InlineData("Servers", "ServerMetricHistoryView.xaml")]
    public void should_opt_in_to_the_window_chrome_when_the_view_root_is_not_a_kernel_frame(string folder, string file)
    {
        var xaml = File.ReadAllText(Path.Combine(ConsolesFolder(), folder, file));

        Assert.Contains("c:ConsoleWindowChrome.DressWindow=\"True\"", xaml);
    }

    #region - Fixtures -
    private static Window NewWindow(object content, string title, ResizeMode mode) => new()
    {
        Title = title,
        Content = content,
        Width = 440,
        Height = 260,
        ResizeMode = mode,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -20000,
        Top = -20000,
        ShowInTaskbar = false,
        ShowActivated = false,
    };

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static Button? ById(DependencyObject root, string id)
        => Descendants<Button>(root).FirstOrDefault(b => AutomationProperties.GetAutomationId(b) == id);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static string ConsolesFolder([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Consoles"));

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }
    #endregion
}
