using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 카메라 스트림 공유 키(<see cref="RtspCameraKey.Derive"/>) 단위 테스트 — 실코드 소스링크(복제 아님).
///
/// 배경: 키가 쿼리스트링을 버려서 <c>?channel=</c>류로만 구분되는 서로 다른 카메라가
/// Hub 같은 디코더로 병합 → 두 팝업이 동일 영상을 공유하던 버그(A/B 동일영상,
/// docs/analyses/Rtsp_Popup_Streaming_Ptz-analysis.md §5)의 회귀 방지.
/// </summary>
public class RtspCameraKeyTests
{
    // ── 핵심 회귀: 쿼리로만 구분되는 두 카메라는 서로 다른 키 ──────────────────

    [Fact]
    public void should_produce_distinct_keys_when_urls_differ_only_by_query()
    {
        var a = RtspCameraKey.Derive("rtsp://admin:pw@192.168.0.5:554/cam/monitor?channel=1&subtype=1", "", 554, "");
        var b = RtspCameraKey.Derive("rtsp://admin:pw@192.168.0.5:554/cam/monitor?channel=2&subtype=1", "", 554, "");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void should_produce_same_key_when_urls_identical_except_credentials()
    {
        // 자격증명은 키에 미포함 — 같은 스트림은 계정이 달라도 디코더 공유(설계 의도)
        var a = RtspCameraKey.Derive("rtsp://admin:pw1@192.168.0.5:554/stream?ch=1", "", 554, "");
        var b = RtspCameraKey.Derive("rtsp://other:pw2@192.168.0.5:554/stream?ch=1", "", 554, "");
        Assert.Equal(a, b);
    }

    [Fact]
    public void should_exclude_credentials_from_key_when_url_has_userinfo()
    {
        // 키는 로그에 그대로 찍히므로 자격증명이 절대 포함되면 안 된다(security.md)
        var key = RtspCameraKey.Derive("rtsp://admin:secret@192.168.0.5/stream?ch=1", "", 554, "");
        Assert.DoesNotContain("admin", key);
        Assert.DoesNotContain("secret", key);
    }

    // ── 하위 호환: 무쿼리 URL의 키 포맷은 기존과 동일(불필요한 엔트리 리셋 없음) ──

    [Fact]
    public void should_keep_legacy_key_shape_when_url_has_no_query()
    {
        var key = RtspCameraKey.Derive("rtsp://192.168.0.5:554/stream1", "", 554, "");
        Assert.Equal("192.168.0.5:554/stream1", key);
    }

    [Fact]
    public void should_default_port_554_when_url_omits_port()
    {
        // rtsp는 .NET 기본 스킴이 아니라 포트 미지정 시 -1 → 554 폴백
        var key = RtspCameraKey.Derive("rtsp://192.168.0.5/stream1", "", 0, "");
        Assert.Equal("192.168.0.5:554/stream1", key);
    }

    // ── 폴백 브랜치: URL 파싱 불가 시 IP 구성요소 키 ─────────────────────────

    [Fact]
    public void should_fall_back_to_ip_fields_when_url_unparseable()
    {
        var key = RtspCameraKey.Derive("not a url ::", "192.168.0.7", 554, "ch0");
        Assert.Equal("192.168.0.7:554/ch0", key);
    }
}
