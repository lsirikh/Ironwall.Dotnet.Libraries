using System.Windows;
using Ironwall.Dotnet.Libraries.CameraPopup.Wpf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// <see cref="HostedVideoView.AspectFillTolerance"/> — 상자 크기로 연 프레임이 짝수 자르기 · 다시 열기 허용 오차만큼만 어긋날 때는
/// 레터박스(한쪽 1 px 검은 줄) 대신 상자를 채우고, 비율이 크게 다르면 그대로 레터박스한다(2026-10-01 "창 가장자리 마감").
/// </summary>
public class HostedVideoViewFillTests
{
    [Theory]
    [InlineData(572, 384, 382, 256)]     // 150%: 382 DIU → 573 px → 짝수 572 — 1 px 어긋남
    [InlineData(478, 320, 382, 256)]     // 125%: 477.5 → 478
    [InlineData(382, 256, 378, 252)]     // 상자가 4 DIU 줄었지만 허용 오차(8 px) 안이라 다시 열지 않은 프레임
    public void should_fill_the_box_when_the_frame_differs_only_by_rounding(double sourceWidth, double sourceHeight, double boxWidth, double boxHeight)
        => Assert.True(HostedVideoView.FillsBox(new Size(sourceWidth, sourceHeight), new Size(boxWidth, boxHeight), 0.035));

    [Theory]
    [InlineData(382, 256, 382, 476)]     // 패널을 펼쳐 상자가 크게 바뀐 직후(다시 열기 전) — 찌그러뜨리지 않는다
    [InlineData(640, 480, 382, 256)]     // 4:3 원본을 그대로 받은 경우
    public void should_keep_the_letterbox_when_the_aspect_differs_a_lot(double sourceWidth, double sourceHeight, double boxWidth, double boxHeight)
        => Assert.False(HostedVideoView.FillsBox(new Size(sourceWidth, sourceHeight), new Size(boxWidth, boxHeight), 0.035));

    [Theory]
    [InlineData(0, 382, 256)]            // 끔(기본값)
    [InlineData(0.035, double.PositiveInfinity, 256)]
    [InlineData(0.035, 0, 256)]
    public void should_not_fill_when_disabled_or_the_box_is_unknown(double tolerance, double boxWidth, double boxHeight)
        => Assert.False(HostedVideoView.FillsBox(new Size(382, 256), new Size(boxWidth, boxHeight), tolerance));
}
