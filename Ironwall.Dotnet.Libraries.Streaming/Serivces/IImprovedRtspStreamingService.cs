using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Controls;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
public interface IImprovedRtspStreamingService: IService, IDisposable
{
    Dictionary<string, Delegate> EventHandlers { get; }

    event EventHandler<StreamingErrorEventArgs>? ErrorOccurred;
    event EventHandler<StreamingProgressEventArgs>? ProgressUpdated;
    event EventHandler<StreamingStateChangedEventArgs>? StateChanged;

    void ClearAllPlayers();
    Task<bool> ConnectAsync(string contextId,
        RtspConnectionInfo? connectionInfo,
        StreamingOptions? options = null, VideoView? videoView = null,
        CancellationToken ct = default);
    Task DisconnectAllAsync();
    Task DisconnectAsync(string contextId);
    MediaPlayer? GetMediaPlayer(string? contextId);
    PlaybackState GetPlaybackState(string contextId);
    ImprovedRtspPlayer? GetPlayer(string? contextId);
    StreamingStatistics? GetStatistics(string? contextId);
    bool HasPlayer(string contextId);
    bool IsConnected(string contextId);
    void Pause(string? contextId);
    void Play(string? contextId);
    void SetFrameSkip(string? contextId, int skipFrames);
    void ToggleMute(string? contextId);
    void SetVolume(string? contextId, int volume);
    void Stop(string contextId);
    bool RegisterPlayer(string? contextId, ImprovedRtspPlayer player);
    bool UnregisterPlayer(string contextId);
    void RestrictStream(string contextId, string message = "Surveillance Not Allowed");
    Task UnrestrictStreamAsync(string contextId);
    
    Task<bool> TakeSnapshotAsync(string contextId, string filePath);

    /// <summary>Hub 경로 플레이어를 타임아웃 트래킹에 등록. IsAutoDiscard 팝업에서 호출.</summary>
    void RegisterConnectionStartTime(string contextId);
}