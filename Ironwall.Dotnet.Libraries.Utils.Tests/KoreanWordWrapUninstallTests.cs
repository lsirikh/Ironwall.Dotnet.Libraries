using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/****************************************************************************
   Purpose      : 한글 줄바꿈 전역 설치를 시험이 되돌리는 장치 (호스트 보고 R10 — 시험 환경)
   Created By   : Claude (t-20261011-111636-d46d)
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="KoreanWordWrap.Install"/> 은 TextBlock 클래스 처리기를 프로세스 전역에 건다 — 한 번 걸리면 같은 프로세스의 뒤 시험 글에
/// U+2060 이 섞여 글자 그대로 비교가 순서에 따라 깨졌다(호스트 전체 실행 실측). 시험만 쓰는 <c>Uninstall</c>(internal)로 끄고,
/// 다시 <see cref="KoreanWordWrap.Install"/> 하면 원래대로 돈다. 창은 띄우지 않는다(부모 없는 레이아웃 — SizeChanged 는 그래도 올라온다).
/// </summary>
[Collection(WpfFocusCollection.Name)]
public class KoreanWordWrapUninstallTests
{
    private const string Hint = "카테고리는 바꿀 수 없습니다";

    [Fact]
    [Trait("Scenario", "R10")]
    public void should_leave_new_and_tracked_text_untouched_when_the_wrap_is_uninstalled()
    {
        var (whileOn, freshAfterOff, trackedAfterOff, flagAfterOff) = OnSta(() =>
        {
            var was = KoreanWordWrap.IsInstalled;
            try
            {
                KoreanWordWrap.Install();
                var tracked = LaidOut(Hint);                     // 켜진 동안 붙잡힌 글
                var on = tracked.Text;

                KoreanWordWrap.Uninstall();
                var fresh = LaidOut(Hint);                       // 끈 뒤 새 글
                tracked.Text = Hint;                             // 끈 뒤 이미 붙잡힌 글이 바뀜
                return (on, fresh.Text, tracked.Text, KoreanWordWrap.IsInstalled);
            }
            finally
            {
                if (was) KoreanWordWrap.Install(); else KoreanWordWrap.Uninstall();
            }
        });

        Assert.Equal(KoreanWordWrap.Join(Hint), whileOn);          // 전제: 부모 없는 레이아웃에서도 켜져 있으면 결합자가 든다
        Assert.Equal(Hint, freshAfterOff);
        Assert.Equal(Hint, trackedAfterOff);
        Assert.False(flagAfterOff);
    }

    [Fact]
    [Trait("Scenario", "R10")]
    public void should_join_again_when_installed_after_an_uninstall()
    {
        var text = OnSta(() =>
        {
            var was = KoreanWordWrap.IsInstalled;
            try
            {
                KoreanWordWrap.Install();
                KoreanWordWrap.Uninstall();
                KoreanWordWrap.Install();
                return LaidOut(Hint).Text;
            }
            finally
            {
                if (!was) KoreanWordWrap.Uninstall();
            }
        });

        Assert.Equal(KoreanWordWrap.Join(Hint), text);
    }

    private static TextBlock LaidOut(string text)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, Width = 120, Text = text };
        tb.Measure(new Size(120, 1000));
        tb.Arrange(new Rect(0, 0, 120, tb.DesiredSize.Height));
        tb.UpdateLayout();
        return tb;
    }

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
}
