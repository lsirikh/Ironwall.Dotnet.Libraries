using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumEventType : int
    {
        None = 0,
        // 침입 (90: 0x5A)
        Intrusion = 90,
        // 접점 켜기 (86: 0x56)        
        ContactOn = 86,
        // 접점 끄기 (102: 0x66)
        ContactOff = 102,
        // 연결보고 (104: 0x68)
        Connection = 104,
        // 조치보고 (192: 0xC0)       
        Action = 192,
        // 장애보고 (115: 0x73)
        Fault = 115,
        // 풍량모드 (118: 0x76)
        WindyMode = 118,

        // ── 서버 확장 어휘 (API 7.0+) ────────────────────────────────────────
        //  ⚠ 아래 두 값은 **PIDS 프로토콜 바이트가 아니다.** 서버가 문자열로만 주고받는
        //     카테고리라 지정된 바이트 값이 없어, 기존 프로토콜 값(0·86·90·102·104·115·118·192)과
        //     겹치지 않는 자리를 클라 내부용으로 잡은 것이다.
        //     프로토콜에 정식 배정이 생기면 그 값으로 교체해야 한다.
        /// <summary>사전 경보 — 탐지(detection) 카테고리로 실려 온다(서버 API 7.0 신설).</summary>
        Alert = 160,
        /// <summary>운영 이벤트 — 운영(operation) 카테고리 전용(서버 API 6.3 후속 신설).</summary>
        Operation = 161,
    }
}
