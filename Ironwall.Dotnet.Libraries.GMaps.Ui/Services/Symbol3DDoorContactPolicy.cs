using Ironwall.Dotnet.Libraries.Events.Ui.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
/****************************************************************************
   Purpose      : Symbol3D.DoorContactFallback 설정 → 접점 폴백 정책(FR-13 ③, FR-17)
   Created By   : Claude
   Created On   : 2026-09-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>appsettings <c>AppSettings.Symbol3D.DoorContactFallback</c>(기본 true, 프로세스당 1회 읽기)를 Events.Ui 정책으로 노출한다.</summary>
public sealed class Symbol3DDoorContactPolicy : IDoorContactPolicy
{
    public bool FallbackEnabled => Utils.Symbol3DFeature.DoorContactFallback;
}
