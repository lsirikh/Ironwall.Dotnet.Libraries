using Ironwall.Dotnet.Libraries.Streaming.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/****************************************************************************
   Purpose      : Pre-Connection 시스템을 위한 스트림 프리워밍 인터페이스
   Created By   : GHLee
   Created On   : 12/03/2025
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 스트림 프리워밍 서비스 인터페이스
/// 이벤트 수신 시 미리 연결 컨텍스트를 준비하여 빠른 스트림 연결 지원
/// </summary>
public interface IStreamPrewarmer
{
    /// <summary>
    /// 현재 프리워밍된 컨텍스트 수
    /// </summary>
    int PrewarmedCount { get; }

    /// <summary>
    /// 스트림 연결을 미리 준비합니다
    /// </summary>
    /// <param name="contextId">컨텍스트 식별자</param>
    /// <param name="connectionInfo">RTSP 연결 정보</param>
    /// <param name="options">스트리밍 옵션 (선택)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    Task PrewarmAsync(
        string contextId,
        RtspConnectionInfo connectionInfo,
        StreamingOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 프리워밍된 컨텍스트를 가져옵니다
    /// </summary>
    /// <param name="contextId">컨텍스트 식별자</param>
    /// <param name="context">프리워밍된 컨텍스트 (출력)</param>
    /// <returns>성공 여부</returns>
    bool TryGetPrewarmedContext(string contextId, out PrewarmedContext? context);

    /// <summary>
    /// 만료된 프리워밍 컨텍스트를 정리합니다
    /// </summary>
    /// <returns>정리된 컨텍스트 수</returns>
    int CleanupStalePrewarms();

    /// <summary>
    /// 모든 프리워밍 컨텍스트를 정리합니다
    /// </summary>
    void ClearAll();
}
