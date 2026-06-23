using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// RTSP 맵 팝업 핵심 로직 단위 테스트.
///
/// CameraConnectionAdapter / 팝업 위치 계산은 GMaps.Ui(WPF, net8.0-windows) +
/// Streaming.Base(WPF) 의존이라 net8.0 격리 테스트에서 직접 호출 불가 →
/// 결정 로직을 로컬 복제하여 동치를 검증한다(MBTilesTests.ToTmsRow 패턴).
/// </summary>
public class RtspMapPopupTests
{
    // ── CameraConnectionAdapter URL 우선순위 복제 ──────────────────────────
    //   실제: Urls.RtspSub→RtspMain→Ip조합 (preferSub=true). 빈 URL+빈 IP면 null.
    private static string? ResolveUrl(string? rtspMain, string? rtspSub, string? ip = null, bool preferSub = true)
    {
        string? Pick(params string?[] vs)
        {
            foreach (var v in vs)
                if (!string.IsNullOrWhiteSpace(v)) return v;
            return null;
        }

        var url = preferSub ? Pick(rtspSub, rtspMain) : Pick(rtspMain, rtspSub);
        if (!string.IsNullOrWhiteSpace(url)) return url;        // URL 우선
        if (!string.IsNullOrWhiteSpace(ip)) return $"ip:{ip}";  // IsValid: IP 있으면 유효(조립 대상)
        return null;                                            // URL/IP 둘 다 없음 → null
    }

    [Fact]
    public void should_prefer_sub_stream_when_available()
        => Assert.Equal("subUrl", ResolveUrl("mainUrl", "subUrl"));

    [Fact]
    public void should_fall_back_to_main_when_sub_empty()
        => Assert.Equal("mainUrl", ResolveUrl("mainUrl", null));

    [Fact]
    public void should_fall_back_to_ip_when_no_url()
        => Assert.Equal("ip:192.168.0.10", ResolveUrl(null, "   ", "192.168.0.10"));

    [Fact]
    public void should_return_null_when_no_url_and_no_ip()
        => Assert.Null(ResolveUrl(null, null, null));

    [Fact]
    public void should_use_main_first_when_prefer_sub_false()
        => Assert.Equal("mainUrl", ResolveUrl("mainUrl", "subUrl", null, preferSub: false));

    // ── 최초 위치(우상단) 계산 복제 ────────────────────────────────────────
    //   실제: c=심볼중점, left=c.X+100, top=c.Y-100-PopupHeight  (팝업 좌하단을 우상단점에 맞춤)
    private const double OffsetRight = 100;
    private const double OffsetUp = 100;
    private const double DefaultHeight = 300;

    private static (double left, double top) InitialTopRight(double cx, double cy)
        => (cx + OffsetRight, cy - OffsetUp - DefaultHeight);

    [Fact]
    public void should_place_popup_top_right_of_symbol_center()
    {
        var (left, top) = InitialTopRight(500, 400);
        Assert.Equal(600, left);             // 오른쪽 100
        Assert.Equal(0, top);                // 400 - 100 - 300 = 0 (좌하단이 중점 위 100)
    }

    [Fact]
    public void should_keep_bottomleft_corner_100px_above_center()
    {
        // 팝업 좌하단 Y = top + height = (cy-100-H) + H = cy-100  → 중점에서 위 100px
        var (_, top) = InitialTopRight(0, 1000);
        Assert.Equal(1000 - 100, top + DefaultHeight);
    }
}
