using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 화면 중앙 십자가(비상호작용 시각 기준점) 토글.
                  MapViewModel partial 분리 — 공유 본체(MapViewModel.cs) 미접촉(다중세션 충돌 회피).
   Created By   : GHLee
   Created On   : 7/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
 ****************************************************************************/
public partial class MapViewModel
{
    private bool _isCenterCrosshairVisible;
    /// <summary>화면 정중앙 십자가 표시 여부(툴바 토글). 순수 시각 기준점 — 클릭/히트테스트 없음.</summary>
    public bool IsCenterCrosshairVisible
    {
        get => _isCenterCrosshairVisible;
        set { _isCenterCrosshairVisible = value; NotifyOfPropertyChange(nameof(IsCenterCrosshairVisible)); }
    }

    private RelayCommand? _toggleCenterCrosshairCommand;
    /// <summary>화면 중앙 십자가 표시 토글.</summary>
    public RelayCommand ToggleCenterCrosshairCommand
        => _toggleCenterCrosshairCommand ??= new RelayCommand(_ => IsCenterCrosshairVisible = !IsCenterCrosshairVisible);
}
