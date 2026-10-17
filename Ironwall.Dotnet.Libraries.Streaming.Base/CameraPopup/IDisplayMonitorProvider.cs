namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 모니터 조회 창구 (camera-popup-modes PRD FR-12)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>모니터를 조회한다(구현: <see cref="Win32DisplayMonitorProvider"/>). 실패하면 빈 목록 — 예외를 내지 않는다.</summary>
public interface IDisplayMonitorProvider
{
    IReadOnlyList<DisplayMonitorInfo> GetMonitors();
}
