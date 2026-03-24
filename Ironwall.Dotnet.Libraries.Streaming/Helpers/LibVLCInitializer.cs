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
    private static LibVLC _instance;
    private static readonly object _lock = new();

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

                var options = new List<string>
                    {
                        "--verbose=1",
                        "--no-video-title-show",
                        "--no-snapshot-preview",
                        "--no-disable-screensaver",
                        "--network-caching=" + (setupModel?.DefaultNetworkCaching ?? 300),
                        "--live-caching=" + (setupModel?.DefaultNetworkCaching ?? 300),
                        "--file-caching=" + (setupModel?.DefaultNetworkCaching ?? 300),
                        "--clock-jitter=0",
                        "--clock-synchro=0"
                    };

                // 하드웨어 가속
                if (setupModel?.UseHardwareAcceleration ?? true)
                {
                    options.Add("--avcodec-hw=any");
                }

                // 프레임 스킵
                if (setupModel?.EnableFrameSkipping ?? true)
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
        }
    }
}