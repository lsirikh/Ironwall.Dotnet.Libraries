using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 틀이 <b>제 크기를 보고</b> 카드 폭을 정하면 재면 잴수록 좁아진다 — 실측에서 400 · 560 · 720 이 전부
/// 최소 폭 320 으로 수렴했다(N-05 PNG 에서 발견). 부모가 내주는 자리를 봐야 한다.
/// </summary>
/// <remarks>
/// WPF 요소는 STA 스레드에서만 만들 수 있어 테스트마다 전용 STA 스레드를 하나 띄운다.
/// 디스패처 루프는 돌리지 않는다 — <see cref="UIElement.Measure"/> 만으로 충분하다.
/// </remarks>
public class DialogFrameMeasureTests
{
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

    [Theory]
    [InlineData(DialogSize.Small, 400d)]
    [InlineData(DialogSize.Medium, 560d)]
    [InlineData(DialogSize.Large, 720d)]
    public void should_hold_the_nominal_width_when_measured_again_and_again(DialogSize size, double expected)
    {
        var width = OnSta(() =>
        {
            var frame = new ConsoleDialogFrame { Size = size };
            // 창이 넉넉한데도 다시 잴 때마다 좁아지면 되먹임이 남아 있다는 뜻이다.
            for (var i = 0; i < 5; i++) frame.Measure(new Size(1280, 900));
            return frame.CardWidth;
        });

        Assert.Equal(expected, width);
    }

    [Fact]
    public void should_shrink_only_to_what_the_parent_offers_when_the_window_is_narrow()
    {
        var width = OnSta(() =>
        {
            var frame = new ConsoleDialogFrame { Size = DialogSize.Large };
            for (var i = 0; i < 5; i++) frame.Measure(new Size(600, 900));
            return frame.CardWidth;
        });

        Assert.Equal(DialogSizeRules.ResolveWidth(DialogSize.Large, 600d), width);
    }

    [Fact]
    public void should_keep_the_nominal_width_when_the_parent_offers_infinity()
    {
        // 가로 StackPanel 안 — 부모가 무한 폭을 준다. 그때는 규격 폭이다.
        var width = OnSta(() =>
        {
            var frame = new ConsoleDialogFrame { Size = DialogSize.Medium };
            for (var i = 0; i < 3; i++) frame.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return frame.CardWidth;
        });

        Assert.Equal(560d, width);
    }

    [Fact]
    public void should_shrink_step_by_step_when_a_dialog_measures_itself()
    {
        // 되먹임이 왜 위험한지 — 규칙 자체는 제 출력물을 다시 먹이면 계속 줄어든다.
        // 이것이 틀이 ActualWidth 를 보면 안 되는 이유다.
        var once = DialogSizeRules.ResolveWidth(DialogSize.Large, 720d);
        var twice = DialogSizeRules.ResolveWidth(DialogSize.Large, once);

        Assert.True(once < 720d);
        Assert.True(twice < once);
    }
}
