using System;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/7/2025 9:23:18 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public enum EnumDetectionType : int
{
    NONE = 0,               //0
    CABLE_CUTTING = 1,      //1
    CABLE_CONNECTED = 2,    //2
    PIR_SENSOR = 3,         //3
    THERMAL_SENSOR = 5,     //4
    VIBRATION_SENSOR = 6,   //5
    CONTACT_SENSOR = 10,     //10
    DISTANCE_SENSOR = 11,    //11
    AI_DETECT = 12,          //12

    // ── 서버 확장 어휘 (API 8.0.1) ──────────────────────────────────────────
    //  ⚠ 이 값은 **PIDS 프로토콜 바이트가 아니다.** 서버가 `result` 를 문자열로만
    //     주고받는(8.0.1 `EnumDetectionType`: 위 9값 + RADAR_DETECT) 어휘라 지정된
    //     바이트가 없어, 기존 값(0·1·2·3·5·6·10·11·12)과 겹치지 않는 자리를
    //     클라 내부용으로 잡았다. 프로토콜에 정식 배정이 생기면 그 값으로 교체한다.
    //     (선례: `EnumEventType.Alert = 160`)
    //  무회귀: 6.3.2 는 9값만 쓰므로(원격 스웨거 `DetectionEventCreate.result`
    //     description 실측) 값 추가만으로는 운영 동작이 바뀌지 않는다.
    /// <summary>레이더 탐지 — 서버 API 8.0 신설(<c>RADAR_DETECT</c>).</summary>
    RADAR_DETECT = 13,
}