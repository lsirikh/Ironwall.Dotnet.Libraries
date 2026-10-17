namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : 통문·함체 접점(ContactOn/Off) 이벤트를 개폐 형태로 유도할지(FR-13 ③ 폴백 채널, R-02)
   Created By   : Claude
   Created On   : 2026-09-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 접점 폴백 정책. GMaps.Ui 가 <c>Symbol3D.DoorContactFallback</c> 설정으로 구현을 등록하고(Symbol3DDoorContactPolicy),
/// 등록이 없으면 <see cref="DefaultDoorContactPolicy"/>(true) 가 쓰인다. 어느 쪽이든 통문/함체의 접점 이벤트는
/// 카드·탐지음·조치보고 큐에 들어가지 않는다 — 정책은 '형태를 바꿀지'만 정한다.
/// </summary>
public interface IDoorContactPolicy
{
    bool FallbackEnabled { get; }
}

public sealed class DefaultDoorContactPolicy : IDoorContactPolicy
{
    public bool FallbackEnabled => true;
}
