using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using LibVLCSharp.Shared;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      : 프리워밍된 스트림 연결 컨텍스트
   Created By   : GHLee
   Created On   : 12/03/2025
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 프리워밍된 스트림 연결 컨텍스트
/// 미리 준비된 연결 정보와 LibVLC 리소스를 보관
/// </summary>
public class PrewarmedContext : IDisposable
{
    /// <summary>
    /// 컨텍스트 식별자
    /// </summary>
    public string ContextId { get; }

    /// <summary>
    /// RTSP 연결 정보
    /// </summary>
    public RtspConnectionInfo? ConnectionInfo { get; set; }

    /// <summary>
    /// 스트리밍 옵션
    /// </summary>
    public StreamingOptions? Options { get; set; }

    /// <summary>
    /// 미리 생성된 Media 객체 (선택적)
    /// </summary>
    public Media? PrebuiltMedia { get; set; }

    /// <summary>
    /// 생성 시간
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 마지막 액세스 시간
    /// </summary>
    public DateTime LastAccessedAt { get; set; }

    /// <summary>
    /// 프리워밍 상태
    /// </summary>
    public PrewarmState State { get; set; }

    /// <summary>
    /// 생성자
    /// </summary>
    public PrewarmedContext(string contextId)
    {
        ContextId = contextId ?? throw new ArgumentNullException(nameof(contextId));
        CreatedAt = DateTime.Now;
        LastAccessedAt = DateTime.Now;
        State = PrewarmState.Pending;
    }

    /// <summary>
    /// 만료 여부 확인
    /// </summary>
    public bool IsExpired(TimeSpan timeout)
    {
        return DateTime.Now - CreatedAt > timeout;
    }

    /// <summary>
    /// 액세스 시간 갱신
    /// </summary>
    public void Touch()
    {
        LastAccessedAt = DateTime.Now;
    }

    /// <summary>
    /// 리소스 해제
    /// </summary>
    public void Dispose()
    {
        PrebuiltMedia?.Dispose();
        PrebuiltMedia = null;
        State = PrewarmState.Disposed;
    }
}

/// <summary>
/// 프리워밍 상태
/// </summary>
public enum PrewarmState
{
    /// <summary>
    /// 대기 중
    /// </summary>
    Pending,

    /// <summary>
    /// 프리워밍 진행 중
    /// </summary>
    Prewarming,

    /// <summary>
    /// 프리워밍 완료 (사용 준비됨)
    /// </summary>
    Ready,

    /// <summary>
    /// 프리워밍 실패
    /// </summary>
    Failed,

    /// <summary>
    /// 사용됨
    /// </summary>
    Used,

    /// <summary>
    /// 해제됨
    /// </summary>
    Disposed
}
