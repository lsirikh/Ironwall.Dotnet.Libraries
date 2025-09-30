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
    Restricted = 10
}