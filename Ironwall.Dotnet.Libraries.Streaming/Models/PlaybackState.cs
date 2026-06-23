using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 2:25:26 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 재생 상태 열거형
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// 초기 상태
    /// </summary>
    None = 0,

    /// <summary>
    /// 연결 중
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// 버퍼링 중
    /// </summary>
    Buffering = 2,

    /// <summary>
    /// 재생 중
    /// </summary>
    Playing = 3,

    /// <summary>
    /// 일시 정지
    /// </summary>
    Paused = 4,

    /// <summary>
    /// 정지됨
    /// </summary>
    Stopped = 5,

    /// <summary>
    /// 에러 발생
    /// </summary>
    Error = 6,

    /// <summary>
    /// 재연결 중
    /// </summary>
    Reconnecting = 7,

    /// <summary>
    /// 연결 끊김
    /// </summary>
    Disconnected = 8,

    /// <summary>
    /// 이미지 표시 모드
    /// </summary>
    ImageDisplay = 9,

    /// <summary>
    /// 감시 제한 상태
    /// </summary>
    Restricted = 10,

    /// <summary>
    /// 프리워밍 중 (Pre-Connection 준비)
    /// </summary>
    Prewarming = 11,

    /// <summary>
    /// 준비 완료 (연결 준비됨, 재생 대기)
    /// </summary>
    Ready = 12,

    /// <summary>
    /// 성능 저하 상태 (프레임 드롭 발생 중이지만 재생 중)
    /// </summary>
    Degraded = 13,

    // ── Phase 1: Shared Session Pool 상태 ───────────────────────────────

    /// <summary>
    /// Secondary Context 전용.
    /// Phase 1(sout 없음): 영상 없이 Primary 세션 공유 대기 중.
    /// Phase 2 미적용 시 이 상태가 유지되며 UI에 "공유 스트림 준비 중" 표시.
    /// </summary>
    SharedStreamWaiting = 14,

    // ── Phase 2: sout 로컬 릴레이 상태 ────────────────────────────────

    /// <summary>
    /// Secondary Context 전용.
    /// sout 릴레이를 통해 localhost RTSP 스트림 수신 중 (정상 재생).
    /// </summary>
    SharedStreamPlaying = 15
}