using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Xunit;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// 커널 다이얼로그 틀(T4)의 모서리 — 네 모서리 호에 테두리색이 살아 있고, 위 · 아래 선이 한 줄로 또렷한가(2026-10-01 "종료 확인" 스크린샷).
/// </summary>
/// <remarks>
/// 확인 · 안내 · 진행 팝업, 로그인 · 로그아웃 · 내 정보 카드, 계정 편집 · 등록 · 비밀번호 · 삭제 창, 호스트 장비 · 이벤트 창이 전부 이 틀 하나다.
/// 화면 밖 창에 실제 토큰(라이트/다크)으로 띄우고 100 · 150 % 로 그려 잰다. <c>FRAME_CORNERS_DIR</c> 가 있으면 그림을 남긴다.
/// </remarks>
public class FrameCornerPixelTests
{
    private const double CornerFloor = 0.83;   // 덮어 칠하던 때 네 모서리 최저 0.52~0.57(100 %), 위에 그리면 0.85~1.00
    private const double EdgeFloor = 0.90;     // 소수 좌표에서 위 · 아래 선 0.58~0.65

    [Theory]
    [InlineData(false, 96)]
    [InlineData(false, 144)]
    [InlineData(true, 96)]
    [InlineData(true, 144)]
    public void should_keep_the_border_colour_on_all_four_corners_when_a_dialog_card_is_rendered(bool dark, int dpi) => AppHost.Run(() =>
    {
        AppHost.SetDark(dark);
        try
        {
            var frame = new ConsoleDialogFrame
            {
                Title = "종료 확인",
                Size = DialogSize.Small,
                PrimaryText = "확인",
                SecondaryText = "취소",
                Content = new TextBlock { Text = "프로그램을 종료하시겠습니까?" },
            };
            // 셸의 팝업층과 같은 조건 — 카드가 칸 가운데에 오며 (칸 − 카드)/2 가 소수가 되는 높이
            var layer = new Grid { Width = 520, Height = 300, Children = { frame } };
            layer.SetResourceReference(Panel.BackgroundProperty, "BgBrush");
            var window = AppHost.Show(layer);
            try
            {
                var scores = Measure((FrameworkElement)window.Content, frame, $"dialog-{(dark ? "dark" : "light")}-{dpi}.png", dpi);
                AssertCorners(scores, $"{(dark ? "다크" : "라이트")} {dpi * 100 / 96}%");
            }
            finally { window.Close(); }
        }
        finally { AppHost.SetDark(false); }
    });

    [Theory]
    [InlineData("Logout", false)]
    [InlineData("Logout", true)]
    [InlineData("Login", false)]
    [InlineData("Login", true)]
    public void should_keep_the_border_colour_on_all_four_corners_when_an_account_card_is_shown(string name, bool dark) => AppHost.Run(() =>
    {
        AppHost.SetDark(dark);
        var hosted = SelfService.Host(name);
        try
        {
            var frame = SelfService.Visuals(hosted.View).OfType<ConsoleDialogFrame>().First();
            var scores = Measure((FrameworkElement)hosted.Window.Content, frame, $"{name.ToLowerInvariant()}-{(dark ? "dark" : "light")}-96.png", 96);
            Assert.True(scores.MinCorner >= CornerFloor, $"{name} {(dark ? "다크" : "라이트")} — 모서리 호의 테두리가 지워졌다: {scores}");
        }
        finally
        {
            hosted.Window.Close();
            AppHost.SetDark(false);
        }
    });

    private static FrameCornerPixels.Scores Measure(FrameworkElement root, ConsoleDialogFrame frame, string file, int dpi)
    {
        var card = (FrameworkElement)frame.Template.FindName(ConsoleDialogFrame.PartCard, frame);
        var stroke = ((SolidColorBrush)frame.FindResource("TextMutedBrush")).Color;
        var dir = Environment.GetEnvironmentVariable("FRAME_CORNERS_DIR");
        return FrameCornerPixels.Measure(root, card, radius: 10, stroke, dpi, dir is null ? null : Path.Combine(dir, file));
    }

    /// <remarks>
    /// 125 % 는 단언하지 않는다 — 화면 밖 창은 96 으로 배치되므로 120 으로 크게 그리면 96 기준 정수 좌표가 반 픽셀에 걸린다
    /// (실제 125 % 모니터에서는 배치 반올림이 그 배율의 장치 픽셀에 맞춘다). 뿌리 DPI(SetRootDpi)로 흉내 내 보았으나 배치 반올림이 따라오지 않았다.
    /// 100 · 150 % 는 96 기준 정수 좌표가 장치 픽셀과 맞아 실제와 같다.
    /// </remarks>
    private static void AssertCorners(FrameCornerPixels.Scores scores, string label)
    {
        Assert.True(scores.MinCorner >= CornerFloor, $"{label} — 모서리 호의 테두리가 지워졌다: {scores}");
        Assert.True(scores.TopEdge >= EdgeFloor, $"{label} — 윗선이 두 줄로 번졌다: {scores}");
        Assert.True(scores.BottomEdge >= EdgeFloor, $"{label} — 아랫선이 두 줄로 번졌다: {scores}");
    }
}
