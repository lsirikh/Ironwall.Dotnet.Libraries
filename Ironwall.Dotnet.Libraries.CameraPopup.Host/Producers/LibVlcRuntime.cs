using LibVLCSharp.Shared;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// LibVLC 인스턴스 하나를 게으르게 만든다 — 시험 무늬만 쓰는 호스트는 네이티브 라이브러리를 아예 올리지 않고,
/// 첫 LibVLC 스트림이 배경 스레드에서 초기화 비용을 낸다(시작 · 재시작 시간을 늘리지 않게).
/// </summary>
internal static class LibVlcRuntime
{
    private static readonly object Gate = new();
    private static LibVLC? _instance;

    /// <summary>LibVLC 대기 구현을 진짜 WaitOnAddress 로 이었는가(<see cref="LibVlcWaitPatch"/>).</summary>
    public static bool WaitPatchApplied { get; private set; }

    public static LibVLC Get(HostLog log)
    {
        lock (Gate)
        {
            if (_instance is not null) return _instance;
            var started = Environment.TickCount64;
            Core.Initialize();
            // 네이티브 라이브러리가 올라온 직후 · 인스턴스(스레드) 생성 전에만 안전하다.
            WaitPatchApplied = LibVlcWaitPatch.TryApply(log);
            // 소리 없음 · 화면 글자 없음. 스트림별 옵션(디코더 스레드 · 하드웨어 디코딩 · RTSP over TCP)은 DecodeProfile 이 정한다.
            _instance = new LibVLC("--no-audio", "--no-osd", "--no-video-title-show", "--no-snapshot-preview", "--quiet");
            log.Info($"libvlc initialized {_instance.Version} in {Environment.TickCount64 - started} ms");
            return _instance;
        }
    }
}
