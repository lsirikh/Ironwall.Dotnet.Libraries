using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// Url 모드 카메라 계정 → RTSP URL 결합 · 로그 마스킹(<see cref="RtspUrlCredentials"/>) — 실코드 소스링크.
/// 서버 계약은 URL 에 계정을 싣지 못하게 한다(connection.urls userinfo 422) → 계정은 connection.credentials 에만 있고,
/// Url 모드 팝업은 그 계정을 URL 에 합쳐야 카메라 401 을 넘는다.
/// </summary>
public class RtspUrlCredentialsTests
{
    private const string DahuaSub = "rtsp://192.168.202.108:554/cam/realmonitor?channel=1&subtype=1&unicast=true&proto=Onvif";

    [Fact]
    public void should_return_url_unchanged_when_no_credentials()
    {
        Assert.Equal(DahuaSub, RtspUrlCredentials.WithCredentials(DahuaSub, null, null));
        Assert.Equal(DahuaSub, RtspUrlCredentials.WithCredentials(DahuaSub, "", "pw"));
    }

    [Fact]
    public void should_inject_credentials_when_url_has_no_userinfo()
    {
        Assert.Equal(
            "rtsp://admin:pw1@192.168.202.108:554/cam/realmonitor?channel=1&subtype=1&unicast=true&proto=Onvif",
            RtspUrlCredentials.WithCredentials(DahuaSub, "admin", "pw1"));
    }

    [Fact]
    public void should_percent_encode_reserved_characters_when_injecting()
    {
        var url = RtspUrlCredentials.WithCredentials("rtsp://10.0.0.5/video1", "ad@m:in", "p@ss:w/r#d?%");
        Assert.Equal("rtsp://ad%40m%3Ain:p%40ss%3Aw%2Fr%23d%3F%25@10.0.0.5/video1", url);
    }

    [Fact]
    public void should_keep_existing_userinfo_when_url_already_has_it()
    {
        const string manual = "rtsp://operator:secret@10.0.0.5:554/stream";
        Assert.Equal(manual, RtspUrlCredentials.WithCredentials(manual, "admin", "pw1"));
    }

    [Fact]
    public void should_not_treat_at_sign_in_query_as_userinfo()
    {
        const string url = "rtsp://10.0.0.5/live?tag=a@b";
        Assert.False(RtspUrlCredentials.HasUserInfo(url));
        Assert.Equal("rtsp://admin:pw@10.0.0.5/live?tag=a@b", RtspUrlCredentials.WithCredentials(url, "admin", "pw"));
    }

    [Fact]
    public void should_inject_username_only_when_password_empty()
    {
        Assert.Equal("rtsp://admin@10.0.0.5/v", RtspUrlCredentials.WithCredentials("rtsp://10.0.0.5/v", "admin", null));
    }

    [Fact]
    public void should_leave_value_without_scheme_untouched()
    {
        Assert.Equal("10.0.0.5/v", RtspUrlCredentials.WithCredentials("10.0.0.5/v", "admin", "pw"));
        Assert.Equal(string.Empty, RtspUrlCredentials.WithCredentials(null, "admin", "pw"));
    }

    [Fact]
    public void should_mask_userinfo_when_logging_composed_url()
    {
        var composed = RtspUrlCredentials.WithCredentials(DahuaSub, "admin", "p@ss:w/r#d");
        var masked = RtspUrlCredentials.Mask(composed);
        Assert.Equal("rtsp://***@192.168.202.108:554/cam/realmonitor?channel=1&subtype=1&unicast=true&proto=Onvif", masked);
        Assert.DoesNotContain("admin", masked);
    }

    [Fact]
    public void should_mask_raw_unencoded_password_containing_at_sign()
    {
        Assert.Equal("rtsp://***@10.0.0.5/v", RtspUrlCredentials.Mask("rtsp://admin:p@ss@10.0.0.5/v"));
    }

    [Fact]
    public void should_mask_urls_inside_free_text_when_logging_exception_messages()
    {
        var text = "open failed: rtsp://admin:pw1@10.0.0.5/v (401) retry rtsp://10.0.0.6/v?x=a@b";
        Assert.Equal("open failed: rtsp://***@10.0.0.5/v (401) retry rtsp://10.0.0.6/v?x=a@b", RtspUrlCredentials.Mask(text));
    }

    [Fact]
    public void should_leave_url_without_userinfo_unmasked()
    {
        Assert.Equal(DahuaSub, RtspUrlCredentials.Mask(DahuaSub));
        Assert.Equal(string.Empty, RtspUrlCredentials.Mask(null));
    }
}
