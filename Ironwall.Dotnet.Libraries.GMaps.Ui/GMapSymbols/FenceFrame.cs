using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/// <summary>프레임 계산 결과(FR-06). 위치 인자 4개는 종전 튜플 <c>(Layout, Lod, HeightPx, Hash)</c> 분해와 호환된다.</summary>
/// <param name="Layout">px 프레임 레이아웃(FenceLayout 을 로컬 px 로 실행 — 2D 선과 정합).</param>
/// <param name="Hash">평행이동 불변 해시 = <see cref="SettingsHash"/> + <see cref="RelativePoints"/>(C2/C4).</param>
public sealed record FenceFrame(FenceLayoutResult Layout, FenceLodLevel Lod, double HeightPx, int Hash)
{
    /// <summary>첫 정점 기준 상대 로컬 px(1 px 반올림) — 해시 입력이자 지터 동치 판정 기준.</summary>
    public IReadOnlyList<Point> RelativePoints { get; init; } = Array.Empty<Point>();
    /// <summary>점을 뺀 설정 해시(간격 px·모드·닫힘·높이 px). 지터 재사용은 이것이 같을 때만 허용된다.</summary>
    public int SettingsHash { get; init; }
}

/// <summary>재생성 게이트 판정(<see cref="GMapMarkerPidsGroup3DControl.DecideFrame"/>).</summary>
public enum FenceFrameAction
{
    /// <summary>해시·표시 단계 모두 동일 — 아무것도 하지 않는다.</summary>
    Skip,
    /// <summary>표시 단계·설정 동일 + 상대 기하가 ±<see cref="GMapMarkerPidsGroup3DControl.JitterTolerancePx"/> 이내 — 메시를 재사용하고 idle 에서 정확 레이아웃을 1회 재계산한다(C2/C4).</summary>
    Reuse,
    /// <summary>기하·설정 변경 또는 표시 단계 전이(C16) — 레이아웃 재할당·메시 재생성.</summary>
    Rebuild,
}
