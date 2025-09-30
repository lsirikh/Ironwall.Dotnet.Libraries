using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Streaming.Controls;
using Ironwall.Dotnet.Libraries.Streaming.Events;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using Ironwall.Dotnet.Libraries.Streaming.ViewModel;
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
        RtspConnectionInfo connectionInfo,
        StreamingOptions? options = null, VideoView? videoView = null);
    Task DisconnectAllAsync();
    Task DisconnectAsync(string contextId);
    Task ExecuteAsync(CancellationToken token = default);
    MediaPlayer GetMediaPlayer(string contextId);
    PlaybackState GetPlaybackState(string contextId);
    ImprovedRtspPlayer GetPlayer(string contextId);
    StreamingStatistics GetStatistics(string contextId);
    bool HasPlayer(string contextId);
    bool IsConnected(string contextId);
    void Pause(string contextId);
    void Play(string contextId);
    bool RegisterPlayer(string contextId, ImprovedRtspPlayer player);
    void SetFrameSkip(string contextId, int skipFrames);
    void SetQuality(string contextId, int quality);
    void SetVolume(string contextId, int volume);
    void Stop(string contextId);
    Task StopAsync(CancellationToken token = default);
    Task<bool> TakeSnapshotAsync(string contextId, string filePath);
    void ToggleMute(string contextId);
    bool UnregisterPlayer(string contextId);

    void RestrictStream(string contextId, string message = "Surveillance Not Allowed");
    Task UnrestrictStreamAsync(string contextId);

}