using Ironwall.Dotnet.Libraries.Streaming.Models;
using LibVLCSharp.Shared;
using System;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Streaming.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:13:52 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// LibVLC 초기화 헬퍼
/// </summary>
public static class LibVLCInitializer
{
    private static volatile LibVLC? _instance;
    private static volatile LibVLC? _hubInstance;
    private static readonly object _lock = new();

    /// <summary>
    /// Hub 전용 LibVLC 인스턴스.
    /// SetVideoCallbacks(CPU 메모리 렌더)는 HW 가속(d3d11va)과 충돌하므로 반드시 소프트웨어 디코딩 사용.
    /// </summary>
    public static LibVLC InitializeForHub(StreamingSetupModel setupModel = null)
    {
        if (_hubInstance != null)
            return _hubInstance;

        lock (_lock)
        {
            if (_hubInstance != null)
                return _hubInstance;

            try
            {
                Core.Initialize();

                var networkCaching = setupModel?.DefaultNetworkCaching ?? 150;
                var clockJitter = setupModel?.ClockJitterMs ?? 500;
                var clockSync = (setupModel?.EnableClockSync ?? true) ? 1 : 0;

                var options = new List<string>
                {
                    "--verbose=1",
                    "--no-video-title-show",
                    "--no-snapshot-preview",
                    "--no-disable-screensaver",
                    $"--network-caching={networkCaching}",
                    $"--live-caching={networkCaching}",
                    $"--file-caching={networkCaching}",
                    $"--clock-jitter={clockJitter}",
                    $"--clock-synchro={clockSync}",
                    "--avcodec-hw=none",   // SetVideoCallbacks와 HW 가속 충돌 방지
                    "--avcodec-threads=2"
                };

                _hubInstance = new LibVLC(options.ToArray());
                return _hubInstance;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize LibVLC for Hub", ex);
            }
        }
    }

    public static LibVLC Initialize(StreamingSetupModel setupModel = null)
    {
        if (_instance != null)
            return _instance;

        lock (_lock)
        {
            if (_instance != null)
                return _instance;

            try
            {
                Core.Initialize();

                var networkCaching = setupModel?.DefaultNetworkCaching ?? 150;
                var clockJitter = setupModel?.ClockJitterMs ?? 500;
                var clockSync = (setupModel?.EnableClockSync ?? true) ? 1 : 0;

                var options = new List<string>
                    {
                        "--verbose=1",
                        "--no-video-title-show",
                        "--no-snapshot-preview",
                        "--no-disable-screensaver",
                        $"--network-caching={networkCaching}",
                        $"--live-caching={networkCaching}",
                        $"--file-caching={networkCaching}",
                        $"--clock-jitter={clockJitter}",
                        $"--clock-synchro={clockSync}"
                    };

                // 하드웨어 가속
                if (setupModel?.UseHardwareAcceleration ?? true)
                {
                    // "any" 대신 d3d11va 고정: Windows에서 GPU 디코더 컨텍스트 명시적 제어
                    // "any"는 fallback 체인이 길어 다중 스트림 동시 초기화 시 D3D11 컨텍스트 경쟁 발생
                    options.Add("--avcodec-hw=d3d11va");
                    options.Add("--avcodec-threads=2"); // 스트림당 디코더 스레드 제한 (전체 스레드 폭증 방지)
                }

                // 프레임 스킵 (다중 스트림 환경에서는 비활성화 — vout buffer deadlock 유발 가능)
                if (setupModel?.EnableFrameSkipping ?? false)
                {
                    options.Add("--avcodec-skip-frame=1");
                    options.Add("--avcodec-skip-idct=1");
                    options.Add("--avcodec-fast");
                }

                // 디버그 로깅
                if (setupModel?.EnableDebugLogging ?? false)
                {
                    var logDir = setupModel.LogPath ?? Path.GetTempPath();
                    Directory.CreateDirectory(logDir);

                    var logPath = Path.Combine(logDir, $"vlc-{DateTime.Now:yyyyMMdd}.log");

                    options.Add($"--logfile={logPath}");
                    options.Add("--log-verbose=2");
                }

                _instance = new LibVLC(options.ToArray());
                return _instance;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize LibVLC", ex);
            }
        }
    }

    public static void Cleanup()
    {
        lock (_lock)
        {
            _instance?.Dispose();
            _instance = null;
            _hubInstance?.Dispose();
            _hubInstance = null;
        }
    }
}