//using Ironwall.Dotnet.Libraries.Base.Services;
//using Ironwall.Dotnet.Libraries.Streaming.Events;
//using Ironwall.Dotnet.Libraries.Streaming.Models;
//using LibVLCSharp.Shared;

//namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
///// <summary>
///// RTSP 스트리밍 서비스 인터페이스
///// </summary>
//public interface IRtspStreamingService : IService, IDisposable
//{
//    // 연결 관리
//    Task<bool> ConnectAsync(string contextId, RtspConnectionInfo connectionInfo, StreamingOptions? options = null,
//        LibVLCSharp.WPF.VideoView? videoView = null);
//    Task DisconnectAsync(string contextId);
//    Task DisconnectAllAsync();

//    // 재생 제어
//    void Play(string contextId);
//    void Pause(string contextId);
//    void Stop(string contextId);

//    // 볼륨 제어
//    void SetVolume(string contextId, int volume);
//    void ToggleMute(string contextId);

//    // 상태 조회
//    MediaPlayer GetMediaPlayer(string contextId);
//    PlaybackState GetPlaybackState(string contextId);
//    StreamingStatistics GetStatistics(string contextId);
//    bool IsConnected(string contextId);

//    // 스냅샷
//    Task<bool> TakeSnapshotAsync(string contextId, string filePath);

//    // 성능 최적화
//    void SetFrameSkip(string contextId, int skipFrames);
//    void SetQuality(string contextId, int quality);

//    // 이벤트
//    event EventHandler<StreamingStateChangedEventArgs> StateChanged;
//    event EventHandler<StreamingErrorEventArgs> ErrorOccurred;
//    event EventHandler<StreamingProgressEventArgs> ProgressUpdated;
//}