using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 홈 포지션 — 앵커 중심 자동 세팅(FR-H1). (과녁 클릭 세팅 FR-H2는
                  GMapCustomControl.cs 편집 필요 → 다중세션 협의로 S-SHORT 머지 후.)
                  MapViewModel partial 분리(협의: 본체 미충돌).
                  PRD GMap_Zoom_Anchor_Home 증분3.
   Created By   : GHLee
   Created On   : 7/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
 ****************************************************************************/
public partial class MapViewModel
{
    /// <summary>
    /// 홈 포지션을 현재 앵커 구역 중심으로 세팅하고 저장(FR-H1).
    /// 앵커 설정/활성 시 호출(최초 적용·설정 저장). 줌은 기존 HomePosition.Zoom 유지.
    /// </summary>
    private async void SetHomeToAnchorCenter()
    {
        var center = _setupModel?.MapAnchor?.Center;
        if (center == null || HomePosition == null) return;

        HomePosition.Position = new CoordinateModel(center.Latitude, center.Longitude, 0.0);
        HomePosition.IsAvailable = true;
        MoveHomeLocationCommand?.RaiseCanExecuteChanged();
        SetHomeLocationCommand?.RaiseCanExecuteChanged();
        _log?.Info($"[MapAnchor] 홈=앵커중심 자동세팅 ({center.Latitude:F6},{center.Longitude:F6})");

        await MapSettingsHelper.SaveHomePositionAsync(HomePosition, _log);
    }
}
