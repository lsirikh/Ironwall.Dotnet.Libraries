using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>
/// ROI 설정 창 상자 꽉 채우기 — 디코딩 크기(순수) · 제공자 정보 사본 · 파이프 직렬화가 꽉 채우기를 잃지 않는지.
/// </summary>
public class VideoFillBoxTests
{
    [Theory]
    [InlineData(720, 480, 960, 540)]     // cam66 서브(3:2) → 16:9 상자
    [InlineData(640, 480, 960, 540)]     // 4:3 서브
    [InlineData(1920, 1080, 960, 540)]   // 같은 비율 메인
    [InlineData(1280, 1024, 960, 768)]   // 5:4
    public void should_decode_at_the_box_size_when_filling(int srcW, int srcH, int boxW, int boxH)
    {
        _ = (srcW, srcH);   // 원본 비율과 관계없다
        Assert.Equal((boxW, boxH), VideoFit.Fill(boxW, boxH));
    }

    [Fact]
    public void should_keep_letterboxing_by_default()
    {
        // 비율 유지(기존 동작): 720×480 을 960×540 에 넣으면 810×540 — 양옆 75 px 띠.
        Assert.Equal((810, 540), VideoFit.Fit(720, 480, 960, 540));
    }

    [Fact]
    public void should_never_return_a_zero_size()
        => Assert.Equal((2, 2), VideoFit.Fill(0, -5));

    [Fact]
    public void should_carry_fill_box_through_provider_copies()
    {
        var info = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "10.0.0.66", Port = 80, Username = "admin", Password = "pw" };

        var filled = info.WithFillBox(true);
        var targeted = filled.WithTarget(960, 540);

        Assert.False(info.FillBox);                       // 원본은 그대로(init 전용 — 사본)
        Assert.True(filled.FillBox);
        Assert.True(targeted.FillBox);                    // 호스트가 상자 크기를 실어도 잃지 않는다
        Assert.Equal((960, 540), (targeted.TargetWidth, targeted.TargetHeight));
        Assert.Equal(("10.0.0.66", 80, "admin", "pw"), (targeted.Host, targeted.Port, targeted.Username, targeted.Password));
        Assert.False(targeted.WithFillBox(false).FillBox);
    }

    [Fact]
    public void should_send_fill_box_over_the_pipe_and_default_to_off_when_absent()
    {
        var bytes = IpcSerializer.Serialize(new OpenOverlayStream
        {
            StreamId = "mapping-roi-1",
            Provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif, Host = "h", FillBox = true },
            Width = 960,
            Height = 540,
        }, 1);
        var status = IpcSerializer.TryDeserialize(bytes, out var message, out _, out _);

        Assert.Equal(DecodeStatus.Ok, status);
        Assert.True(Assert.IsType<OpenOverlayStream>(message).Provider.FillBox);

        // 옛 GIS(칸 없음) → 꺼짐(비율 유지).
        var old = IpcSerializer.Serialize(new OpenOverlayStream { StreamId = "s", Provider = new VideoProviderInfo { Kind = VideoProviderKind.Onvif } }, 2);
        Assert.Equal(DecodeStatus.Ok, IpcSerializer.TryDeserialize(old, out var oldMessage, out _, out _));
        Assert.False(Assert.IsType<OpenOverlayStream>(oldMessage).Provider.FillBox);
    }
}
