namespace Ironwall.Dotnet.Libraries.Events.Ui.Managers;
/****************************************************************************
   Purpose      : SymbolEventManager 인터페이스 (테스트 및 DI 주입 지원)
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public interface ISymbolEventManager
{
    /// <summary>
    /// 카메라 PTZ 데이터로 FOV 업데이트
    /// </summary>
    void ProcessCameraPtz(int cameraId, float pan, float tilt, float zoom);
}
