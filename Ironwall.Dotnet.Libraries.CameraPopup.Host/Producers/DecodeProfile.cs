namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// 상자 크기에 맞춘 LibVLC 재생 옵션(순수). 타일 60개를 소프트웨어로 풀어도 버티게 하는 것이 목적이다.
/// <list type="bullet">
/// <item>공통 — RTSP 를 TCP 로(<c>:rtsp-tcp</c>: 호스트가 들어오는 포트를 열지 않는다 → 방화벽 창 없음) · 소리 없음 ·
/// 낮은 캐싱 · 늦은 프레임 버리기.</item>
/// <item>작은 상자(타일) — 디코더 스레드 1개(기본은 코어 수만큼 — 32코어에서 스트림마다 스레드 · 프레임 버퍼 32벌) ·
/// 소프트웨어 디코딩(작은 서브 스트림은 GPU 왕복보다 싸다) · 디블로킹 생략.</item>
/// <item>큰 상자(크게 보기 · 큰 단독 타일 · 오버레이) — 하드웨어 디코딩(D3D11VA)을 먼저 시도하고, 재시도에서는 소프트웨어로
/// (드라이버 · 코덱이 맞지 않는 장비의 안전한 폴백).</item>
/// </list>
/// </summary>
internal static class DecodeProfile
{
    /// <summary>이 넓이(픽셀)를 넘으면 "큰 상자" — 640×480 보다 큰 상자.</summary>
    public const int LargeBoxPixels = 640 * 480;

    public static bool IsLarge(int boxWidth, int boxHeight) => (long)boxWidth * boxHeight > LargeBoxPixels;

    /// <param name="attempt">0 = 첫 시도. 1 이상이면 하드웨어 디코딩을 쓰지 않는다(폴백).</param>
    public static IReadOnlyList<string> MediaOptions(int boxWidth, int boxHeight, int attempt, bool hardwareAllowed = true)
    {
        var options = new List<string>
        {
            ":rtsp-tcp",
            ":no-audio",
            ":network-caching=300",
            ":live-caching=300",
            ":drop-late-frames",
            ":skip-frames",
        };
        if (IsLarge(boxWidth, boxHeight))
        {
            options.Add(":avcodec-threads=2");
            options.Add(UsesHardware(boxWidth, boxHeight, attempt, hardwareAllowed) ? ":avcodec-hw=d3d11va" : ":avcodec-hw=none");
        }
        else
        {
            options.Add(":avcodec-threads=1");
            options.Add(":avcodec-hw=none");
            options.Add(":avcodec-skiploopfilter=4");
        }
        return options;
    }

    public static bool UsesHardware(int boxWidth, int boxHeight, int attempt, bool hardwareAllowed = true)
        => hardwareAllowed && attempt <= 0 && IsLarge(boxWidth, boxHeight);
}
