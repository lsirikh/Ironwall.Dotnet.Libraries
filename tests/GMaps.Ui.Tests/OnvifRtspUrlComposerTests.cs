using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// ONVIF 조회 RTSP URL 자격증명 조합(<see cref="OnvifRtspUrlComposer"/>) 단위 테스트 — 실코드 소스링크.
/// (CameraPopup_RtspSource_Priority FR-04 / NFR-02 / V-05)
/// </summary>
public class OnvifRtspUrlComposerTests
{
    [Fact]
    public void should_embed_credentials_when_uri_has_no_userinfo()
    {
        // VER-01 실측 형태: GetStreamUri는 자격증명 없는 URL을 반환
        var url = OnvifRtspUrlComposer.Compose("rtsp://192.168.202.66/video1s", "admin", "sensorway1");
        Assert.Equal("rtsp://admin:sensorway1@192.168.202.66/video1s", url);
    }

    [Fact]
    public void should_replace_existing_userinfo_when_present()
    {
        var url = OnvifRtspUrlComposer.Compose("rtsp://old:cred@192.168.0.5:554/stream", "admin", "pw");
        Assert.Equal("rtsp://admin:pw@192.168.0.5:554/stream", url);
    }

    [Fact]
    public void should_urlencode_special_characters_in_credentials()
    {
        // '@' ':' '#' 등은 URL 구조 문자 — 인코딩 없이는 호스트/경로 경계가 깨진다(V-05)
        var url = OnvifRtspUrlComposer.Compose("rtsp://192.168.0.5/x", "ad@min", "p@ss:w#rd");
        Assert.Equal("rtsp://ad%40min:p%40ss%3Aw%23rd@192.168.0.5/x", url);
    }

    [Fact]
    public void should_return_original_when_username_empty()
    {
        // 익명 카메라 — 원문 그대로
        Assert.Equal("rtsp://192.168.0.5/x", OnvifRtspUrlComposer.Compose("rtsp://192.168.0.5/x", "", "pw"));
        Assert.Equal("rtsp://192.168.0.5/x", OnvifRtspUrlComposer.Compose("rtsp://192.168.0.5/x", null, "pw"));
    }

    [Fact]
    public void should_preserve_path_and_query_when_composing()
    {
        var url = OnvifRtspUrlComposer.Compose(
            "rtsp://nvr.local:554/cam/realmonitor?channel=2&subtype=1", "admin", "pw");
        Assert.Equal("rtsp://admin:pw@nvr.local:554/cam/realmonitor?channel=2&subtype=1", url);
    }

    [Fact]
    public void should_compose_when_uri_has_no_path()
    {
        var url = OnvifRtspUrlComposer.Compose("rtsp://192.168.0.5:554", "admin", "pw");
        Assert.Equal("rtsp://admin:pw@192.168.0.5:554", url);
    }

    [Fact]
    public void should_embed_username_only_when_password_empty()
    {
        var url = OnvifRtspUrlComposer.Compose("rtsp://192.168.0.5/x", "admin", "");
        Assert.Equal("rtsp://admin@192.168.0.5/x", url);
    }
}
