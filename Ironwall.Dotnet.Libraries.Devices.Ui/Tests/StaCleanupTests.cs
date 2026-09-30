using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 시험용 STA 스레드의 뒷정리 — 디스패처를 닫지 않고 끝나는 도우미가 다시 생기지 않게 한다.
/// </summary>
public class StaCleanupTests
{
    [Fact]
    public void should_destroy_the_window_and_finish_the_dispatcher_when_the_sta_body_ends()
    {
        // Arrange
        var handle = IntPtr.Zero;
        var aliveBefore = false;
        var aliveAfter = true;
        Dispatcher? dispatcher = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Left = -20000, Top = -20000, Width = 200, Height = 100,
                    WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
                };
                window.Show();
                handle = new WindowInteropHelper(window).Handle;
                dispatcher = Dispatcher.CurrentDispatcher;
                aliveBefore = IsWindow(handle);

                // Act
                StaCleanup.ShutdownDispatcher();
                aliveAfter = IsWindow(handle);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 스레드가 끝나지 않았다");

        // Assert
        Assert.Null(failure);
        Assert.True(aliveBefore, "창이 떠 있어야 시험이 뜻이 있다");
        Assert.False(aliveAfter, "스레드가 끝나기 전에 창이 WPF 길로 닫혀야 한다");
        Assert.True(dispatcher!.HasShutdownFinished, "디스패처가 닫혀야 한다");
    }

    [Fact]
    public void should_shut_the_dispatcher_down_in_every_test_file_when_it_starts_an_sta_thread()
    {
        // Arrange
        var folder = TestsFolder();

        // Act
        var offenders = Directory.GetFiles(folder, "*.cs")
            .Where(path => Path.GetFileName(path) != Path.GetFileName(ThisFile()))
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.Contains("SetApartmentState(ApartmentState.STA)")
                    && !text.Contains("InvokeShutdown()")
                    && !text.Contains("StaCleanup.ShutdownDispatcher()");
            })
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();

        // Assert
        Assert.True(offenders.Count == 0, "STA 스레드를 만들고 디스패처를 닫지 않는 시험 파일: " + string.Join(", ", offenders));
    }

    private static string ThisFile([CallerFilePath] string? thisFile = null) => thisFile!;

    private static string TestsFolder() => Path.GetDirectoryName(ThisFile())!;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hwnd);
}
