using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>「건물 높이」 판이 부르는 쪽(MapViewModel) — 손잡이 끌기 · 키 한 칸 · 판 안 되돌리기.</summary>
public interface IBuildingHeightPanelHost
{
    /// <summary>손잡이를 잡았다 — 지금 미리보기 높이를 끌기 시작 높이로.</summary>
    void BeginHeightDrag();
    /// <summary>손잡이 끄는 중 — totalDy = 누른 자리에서 세로로 움직인 px(위 = 음수).</summary>
    void DragHeight(double totalDy);
    /// <summary>끌기 끝 — canceled 면(Esc · 캡처 잃음) 끌기 시작 높이로.</summary>
    void EndHeightDrag(bool canceled);
    /// <summary>↑/↓ 한 칸(Shift = 0.1 m) — 처리했으면 true.</summary>
    bool StepHeightByKey(bool up, bool shift);
    /// <summary>판 안 되돌리기(Ctrl+Z) — 처리했으면 true.</summary>
    bool UndoHeight();
}

/// <summary>
/// 「건물 높이」 판(2026-10-19) — 3D 에서 고른 건물의 높이 · 층수를 보이고 직접 넣는다(보안 구역 층수).
/// GroupRotationPanelControl(OverlayWindow 패턴) 답습 — Control + Themes/BuildingHeightPanelStyle.xaml + 머리 띠 끌기(OverlayHeaderDrag)
/// + DynamicResource 토큰(라이트/다크). 폼은 DataContext(=MapViewModel)에 바인딩.
/// 높이 손잡이(Thumb, AutomationId Map.BuildingHeight.Handle) 끌기 · 판 안 키는 공용 바탕(HeightPanelControlBase)이 <see cref="HeightPanelControlBase.Host"/> 로 넘긴다.
/// </summary>
public class BuildingHeightPanelControl : HeightPanelControlBase
{
    static BuildingHeightPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BuildingHeightPanelControl),
            new FrameworkPropertyMetadata(typeof(BuildingHeightPanelControl)));
    }

    public BuildingHeightPanelControl() : base("Map.BuildingHeight.Panel", "건물 높이") { }

    protected override string HandleAutomationId => "Map.BuildingHeight.Handle";
}
