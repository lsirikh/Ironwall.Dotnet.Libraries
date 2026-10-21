using System.Windows;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.Tests.Shared;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 실제 마우스 캡처(Win32 SetCapture)를 쥐어야 하는 시험 — 이 PC 에서 시험 창이 캡처를 얻지 못하면 건너뛴다.
/// </summary>
/// <remarks>
/// Win32 는 뒤쪽 창에 캡처를 실제 커서가 그 창 위에 있거나 프로세스가 전경일 때만 준다. 시험 창은 화면 밖 · 비활성(<see cref="QuietWindow"/>)이라
/// 사용자가 PC 를 쓰는 동안에는 캡처가 늘 실패한다(2026-10-21 실측: 화면 밖 · 활성화 · 숨은 데스크톱 · 마우스 메시지 · 원시 입력 보고 7가지 모두 실패).
/// 캡처를 얻으려고 포커스를 뺏지 않는다 — 환경이 안 되면 실패가 아니라 건너뜀으로 적는다.
/// </remarks>
public sealed class RealMouseCaptureFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> CanCapture = new(Probe);

    public RealMouseCaptureFactAttribute()
    {
        if (!CanCapture.Value)
            Skip = "실제 마우스 캡처를 얻지 못함 — 사용자가 PC 를 쓰는 중이면 시험 창(화면 밖 · 비활성)은 캡처를 받지 못한다";
    }

    private static bool Probe()
    {
        var captured = false;
        var thread = new Thread(() =>
        {
            var button = new Button { Width = 50, Height = 20 };
            var window = new Window { Content = button, Width = 200, Height = 100 };
            QuietWindow.Show(window);
            try
            {
                window.UpdateLayout();
                captured = button.CaptureMouse() && button.IsMouseCaptured;
                button.ReleaseMouseCapture();
            }
            finally
            {
                window.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return captured;
    }
}
