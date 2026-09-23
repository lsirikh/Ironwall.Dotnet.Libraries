using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// console-kernel — <see cref="ConsoleShell.EffectiveListWidth"/> / <see cref="ConsoleShell.EffectiveListWidthChanged"/> 계약.
/// 서랍이 목록 위에 겹칠 때 셸 자신의 <see cref="FrameworkElement.ActualWidth"/> 는 바뀌지 않는다(D-03) —
/// 이 계약이 그 간극을 메운다. 실제 템플릿(Utils/Themes/Generic.xaml)을 적용해 검증한다: 판정 자체는
/// <see cref="ConsoleLayoutMath"/> 순수 함수가 하지만(<c>ConsoleLayoutMathTests</c>), 발화 타이밍 · 중복
/// 억제는 <see cref="ConsoleShell"/> 쪽 상태이므로 컨트롤을 직접 만들어야 한다.
/// </summary>
/// <remarks>
/// WPF 요소는 STA 스레드에서만 만들 수 있어 테스트마다 전용 STA 스레드를 하나 띄운다
/// (<see cref="DialogFrameMeasureTests"/> 와 같은 패턴). <see cref="ConsoleShell"/> 은 (그 컨트롤과 달리)
/// <c>OnApplyTemplate</c> 의 <c>GetTemplateChild</c> 로 실제 템플릿 파트를 찾아야 동작하는데, 앱 없이는
/// <see cref="DependencyObject"/> 의 기본(테마) 스타일이 자동으로 붙지 않는다(실측) — 그래서 <c>Themes/Generic.xaml</c>
/// 을 직접 <see cref="Application.LoadComponent(Uri)"/> 로 읽어 <see cref="FrameworkElement.Style"/> 에 물린다.
/// Measure 가 그 뒤 <c>ApplyTemplate</c> 을 유발한다. <see cref="UIElement.Arrange"/> 는 <c>ActualWidth</c> 자체는
/// 정확히 갱신하지만, <see cref="PresentationSource"/> 없이 수동으로 부르면 <see cref="FrameworkElement.SizeChanged"/>
/// 는 올라오지 않는다(실측 — 실제 창에서는 문제 없다). 그래서 <see cref="ArrangedShell"/> 이 Arrange 뒤
/// <see cref="ConsoleShell.IsDetailRequested"/> 를 한 번 켰다 꺼서 지금의(올바른) 폭으로 재판정을 강제한다.
/// </remarks>
public class ConsoleShellTests
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

    /// <summary>
    /// <c>Utils/Themes/Generic.xaml</c> 에서 <see cref="ConsoleShell"/> 의 기본 스타일(템플릿 포함)을 읽어 물린다.
    /// 헤드리스(앱 없음) 환경에서는 <see cref="FrameworkElement.DefaultStyleKeyProperty"/> 기반 테마 스타일이
    /// 자동으로 해석되지 않는다(실측) — <see cref="ConsoleDialogFrame"/> 처럼 <c>MeasureOverride</c> 를 직접
    /// 재정의하는 컨트롤은 이 문제를 겪지 않지만, <see cref="ConsoleShell"/> 은 템플릿 파트에 의존하므로 필요하다.
    /// <see cref="Style"/> 은 <see cref="System.Windows.Threading.DispatcherObject"/> 라 스레드 소유권이 생긴다 —
    /// 테스트마다 별 STA 스레드를 쓰므로 정적 캐시 없이 <b>매번 새로</b> 읽는다.
    /// </summary>
    private static readonly object EnsureApplicationGate = new();

    private static ConsoleShell NewShellWithTemplate()
    {
        lock (EnsureApplicationGate)
        {
            if (Application.Current == null) _ = new Application();
        }

        var style = (Style)((ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml", UriKind.Relative)))[typeof(ConsoleShell)];

        return new ConsoleShell { Style = style };
    }

    /// <summary>
    /// 헤드리스(윈도우 없음) 환경에서는 수동 <see cref="UIElement.Measure"/> / <see cref="UIElement.Arrange"/>
    /// 가 <see cref="FrameworkElement.SizeChanged"/> 를 스스로 올리지 않는다(실측 — <c>ActualWidth</c> 자체는
    /// Arrange 직후 정확히 갱신되지만, 이 이벤트는 실제 <c>PresentationSource</c> 에 얹힌 레이아웃 패스에서만
    /// 발화한다). 실제 창에서는 리사이즈가 이 이벤트로 <c>ApplyLayout</c> 을 다시 부르므로 문제가 없다.
    /// 여기서는 대신 <see cref="ConsoleShell.IsDetailRequested"/> 를 한 번 켰다 끄는 "settle" 로
    /// <c>ApplyLayout</c> 을 지금의(올바른) <c>ActualWidth</c> 로 다시 돌게 만들고 닫힌 기준 상태로 되돌린다 —
    /// 실제 배치 판정 로직(무엇이 바뀌었을 때 다시 도는가)은 하나도 우회하지 않는다.
    /// </summary>
    private static ConsoleShell ArrangedShell(double width, double height = 900)
    {
        var shell = NewShellWithTemplate();
        shell.Measure(new Size(width, height));
        shell.Arrange(new Rect(0, 0, width, height));

        shell.IsDetailRequested = true;
        shell.IsDetailRequested = false;

        return shell;
    }

    [Fact]
    public void should_report_full_list_width_when_docked()
    {
        var effective = OnSta(() => ArrangedShell(1400).EffectiveListWidth);

        // 도킹은 상세가 제 칸을 가져 인셋이 0 — ListWidth 와 같다(1400 - 레일184 - 상세340).
        Assert.Equal(1400 - 184 - 340, effective);
    }

    [Fact]
    public void should_report_full_list_width_when_drawer_is_closed()
    {
        var effective = OnSta(() => ArrangedShell(1100).EffectiveListWidth);

        // 서랍이 닫혀 있으면(선택 없음) 덮을 게 없다 — 레일만 뺀 전체 폭.
        Assert.Equal(1100 - 184, effective);
    }

    [Fact]
    public void should_shrink_list_width_when_drawer_opens_at_medium_width()
    {
        var effective = OnSta(() =>
        {
            var shell = ArrangedShell(1100);
            shell.IsDetailRequested = true;    // 행 선택 = 서랍이 목록 위로 밀려나온다
            return shell.EffectiveListWidth;
        });

        Assert.Equal(1100 - 184 - 360, effective);   // 서랍폭 min(360, 1100*0.86)=360 만큼 줄어든다
    }

    [Fact]
    public void should_shrink_list_width_when_drawer_opens_while_compact()
    {
        var effective = OnSta(() =>
        {
            var shell = ArrangedShell(900);
            shell.IsDetailRequested = true;
            return shell.EffectiveListWidth;
        });

        Assert.Equal(900 - 56 - 360, effective);      // 컴팩트 = 레일 56 · 서랍폭 min(360, 900*0.86)=360
    }

    [Fact]
    public void should_restore_full_list_width_when_detail_closes_again()
    {
        var (beforeOpen, afterClose) = OnSta(() =>
        {
            var shell = ArrangedShell(1100);
            var before = shell.EffectiveListWidth;
            shell.IsDetailRequested = true;
            shell.IsDetailRequested = false;
            return (before, shell.EffectiveListWidth);
        });

        Assert.Equal(beforeOpen, afterClose);
    }

    [Fact]
    public void should_raise_changed_event_when_detail_opens_and_closes()
    {
        var seen = OnSta(() =>
        {
            var shell = ArrangedShell(1100);
            var values = new List<double>();
            shell.EffectiveListWidthChanged += (_, w) => values.Add(w);

            shell.IsDetailRequested = true;    // 열림 — 줄어든 폭으로 발화
            shell.IsDetailRequested = false;   // 닫힘 — 원래 폭으로 발화

            return values;
        });

        Assert.Equal(2, seen.Count);
        Assert.Equal(1100 - 184 - 360, seen[0]);
        Assert.Equal(1100 - 184, seen[1]);
    }

    [Fact]
    public void should_not_reannounce_when_effective_width_is_unchanged()
    {
        var seen = OnSta(() =>
        {
            var shell = ArrangedShell(1400);    // 도킹 — IsDetailRequested 은 도킹에서 아무 효과가 없다
            var values = new List<double>();
            shell.EffectiveListWidthChanged += (_, w) => values.Add(w);

            shell.IsDetailRequested = true;     // ApplyLayout 은 다시 돌지만 계산값은 그대로
            shell.IsDetailRequested = false;

            return values;
        });

        Assert.Empty(seen);   // 값이 안 바뀌었으니 재발화가 없어야 한다(비-chatty 계약)
    }

    [Fact]
    public void should_keep_dependency_property_in_sync_with_the_event_payload()
    {
        var (dpValue, eventValue) = OnSta(() =>
        {
            var shell = ArrangedShell(1100);
            double last = double.NaN;
            shell.EffectiveListWidthChanged += (_, w) => last = w;

            shell.IsDetailRequested = true;

            return (shell.EffectiveListWidth, last);
        });

        Assert.Equal(dpValue, eventValue);
    }
}
