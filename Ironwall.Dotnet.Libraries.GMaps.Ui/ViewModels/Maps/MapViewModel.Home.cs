using GMap.NET;
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

        // ★ 홈 오염 차단(사용자 사고): 앵커 중심이 (0,0) 또는 CoordinateModel 기본값(37.648425,126.904284) 근방이면
        //   config binder가 채우지 못한 퇴화/기본 앵커 → 홈을 덮어쓰지 않는다(실제 그린 앵커=사이트 좌표만 허용).
        if (IsDegenerateOrDefaultCoord(center.Latitude, center.Longitude))
        {
            _log?.Warning($"[MapAnchor] 홈 자동세팅 스킵 — 앵커 중심이 퇴화/기본값({center.Latitude:F6},{center.Longitude:F6})");
            return;
        }

        HomePosition.Position = new CoordinateModel(center.Latitude, center.Longitude, 0.0);
        HomePosition.IsAvailable = true;
        MoveHomeLocationCommand?.RaiseCanExecuteChanged();
        SetHomeLocationCommand?.RaiseCanExecuteChanged();
        _log?.Info($"[MapAnchor] 홈=앵커중심 자동세팅 ({center.Latitude:F6},{center.Longitude:F6})");

        await MapSettingsHelper.SaveHomePositionAsync(HomePosition, _log);
    }

    /// <summary>(0,0) 또는 CoordinateModel 기본 위치(37.648425,126.904284) 근방 = 미설정/퇴화 좌표(홈 오염 방어용).</summary>
    private static bool IsDegenerateOrDefaultCoord(double lat, double lng)
        => (System.Math.Abs(lat) < 1e-6 && System.Math.Abs(lng) < 1e-6)
        || (System.Math.Abs(lat - 37.648425) < 1e-3 && System.Math.Abs(lng - 126.904284) < 1e-3);

    // ─────────── FR-H2: 홈 "설정" 과녁(크로스헤어) 클릭 (심볼배치 패턴 재사용) ───────────
    private bool _homePlacementSubscribed;

    /// <summary>홈 설정 버튼 → 과녁 배치 모드 진입. 커서 십자 + 다음 좌클릭 위치를 홈으로. ESC 취소.</summary>
    private void EnterHomePlacementMode()
    {
        if (MainMap == null) return;
        if (MainMap.IsTargetAimMode) ExitTargetAimMode();
        if (MainMap.IsSymbolPlacementMode) ExitSymbolPlacementMode();
        if (!_homePlacementSubscribed)
        {
            MainMap.HomePlacementClicked += OnHomePlacementClicked;
            _homePlacementSubscribed = true;
        }
        MainMap.IsHomePlacementMode = true;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
        EnsureAimEscWindowHook();   // ESC 취소(윈도우 후킹 재사용)
        MainMap.Focus();
        SetAimStatus("클릭한 위치를 홈으로 설정 — (ESC = 취소)");
    }

    /// <summary>홈 배치 모드 종료(취소/완료 공통) — 커서·상태 정리.</summary>
    private void ExitHomePlacementMode()
    {
        if (MainMap != null) MainMap.IsHomePlacementMode = false;
        if (System.Windows.Input.Mouse.OverrideCursor == System.Windows.Input.Cursors.Cross)
            System.Windows.Input.Mouse.OverrideCursor = null;
        IsAimStatusVisible = false;
    }

    /// <summary>홈 배치 모드 좌클릭 수신 — 클릭 위치를 홈으로 저장 후 종료(단발).</summary>
    private async void OnHomePlacementClicked(PointLatLng geo, System.Windows.Point screen)
    {
        if (MainMap == null || !MainMap.IsHomePlacementMode) return;
        ExitHomePlacementMode();   // 단발 — 먼저 종료
        if (HomePosition == null) return;

        HomePosition.Position = new CoordinateModel(geo.Lat, geo.Lng, 0.0);
        HomePosition.Zoom = Zoom;
        HomePosition.IsAvailable = true;
        MoveHomeLocationCommand?.RaiseCanExecuteChanged();
        SetHomeLocationCommand?.RaiseCanExecuteChanged();
        _log?.Info($"[Home] 과녁 클릭으로 홈 설정 ({geo.Lat:F6},{geo.Lng:F6}), zoom={Zoom}");
        await MapSettingsHelper.SaveHomePositionAsync(HomePosition, _log);
    }
}
