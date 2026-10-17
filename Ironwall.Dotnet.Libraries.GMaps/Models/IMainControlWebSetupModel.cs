namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : PIDS 마커 우클릭 상세보기 — MainControl 웹서버 설정 인터페이스
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public interface IMainControlWebSetupModel
{
    /// <summary>MainControl 웹서버 IP 주소</summary>
    string IpAddrerssWebServer { get; set; }

    /// <summary>MainControl 웹서버 포트</summary>
    int PortWebServer { get; set; }

    /// <summary>웹서버 기능 활성화 여부. false이면 ContextMenu 미표시.</summary>
    bool IsWebServerEnabled { get; set; }
}
