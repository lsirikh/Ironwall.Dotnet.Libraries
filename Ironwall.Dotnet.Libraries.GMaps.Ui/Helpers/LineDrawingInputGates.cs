using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 드래그 드로잉 입력 게이트(WPF 무의존) — 스트로크 중 휠 차단(C11) ·
                  스트로크 취소 후 팬 억제(C13) · 클릭 정점 최소 간격(C14)
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 라인 드로잉(캡처 드래그 스트로크 + 클릭 폴백)의 입력 판정 순수 함수 — 컨트롤(<c>GMapCustomControl</c>)·
/// 서비스(<c>LineDrawingService</c>)가 호출하며, WPF 무의존이라 단위테스트 소스링크 대상이다.
/// </summary>
public static class LineDrawingInputGates
{
    /// <summary>
    /// [C14] 클릭 폴백으로 추가되는 정점의 수용 여부 — 스트로크 축약기(<c>StrokeReducer</c>)와 같은 최소 간격 규칙을
    /// 클릭 경로에도 적용한다. 마지막 정점이 없으면 항상 수용, 있으면 거리(m)가 <paramref name="minSpacingM"/> 이상일 때만 수용.
    /// 더블클릭(같은 자리 두 번 눌림/릴리스)·스트로크 끝점 재클릭은 거리 0(또는 1px 미만)이라 두 번째 정점이 무시된다.
    /// 거리가 NaN(투영 실패)이면 수용한다(fail-open — 정점 유실보다 중복이 덜 해롭다).
    /// </summary>
    public static bool ShouldAcceptClickVertex(bool hasLastVertex, double distanceToLastM, double minSpacingM)
        => !hasLastVertex || !(distanceToLastM < minSpacingM);

    /// <summary>
    /// [C11] 휠(줌/디지털줌/회전) 차단 여부 — 스트로크 눌림(캡처) 중이면 뷰가 바뀌어 릴리스 시 이전 샘플이 '새 뷰' 기준으로
    /// 투영되므로 차단한다. 이미지 드래그 중 차단(NFR-3a)과 같은 자리에서 판정한다.
    /// </summary>
    public static bool ShouldBlockWheel(bool linePressActive, bool imageDragActive)
        => linePressActive || imageDragActive;
}

/// <summary>
/// [C13] 스트로크 취소(ESC/캡처 소실) 뒤 좌버튼이 아직 눌려 있으면 릴리스까지 맵 팬을 억제하는 상태기계.
/// 벤더 <c>GMapControl.OnMouseDown</c>(클래스 핸들러)은 파생 클래스의 base 미호출과 무관하게 항상 먼저 실행되어
/// <c>_core.MouseDown</c>(팬 Armed)을 세우므로, 스트로크 취소 후 <c>OnMouseMove</c> 가 base 로 흐르면 곧바로 팬이 시작된다.
/// 컨트롤은 눌림/이동/릴리스 지점에서 이 게이트를 호출한다.
/// </summary>
public sealed class PanSuppressGate
{
    /// <summary>억제 중(취소 후 릴리스 대기).</summary>
    public bool IsSuppressing { get; private set; }

    /// <summary>새 좌버튼 눌림 — 릴리스를 못 받은 잔여 억제를 푼다(새 제스처는 항상 깨끗한 상태에서).</summary>
    public void OnButtonDown() => IsSuppressing = false;

    /// <summary>스트로크 취소 — 좌버튼이 아직 눌린 상태면 억제를 세운다. 이미 풀렸으면 아무것도 하지 않는다.</summary>
    public void OnStrokeCancelled(bool leftButtonPressed)
    {
        if (leftButtonPressed) IsSuppressing = true;
    }

    /// <summary>
    /// 이동 — 억제 중이고 좌버튼이 눌려 있으면 true(base.OnMouseMove 차단). 릴리스 이벤트를 못 받은 채 버튼이 풀려 있으면
    /// 자동 해제하고 false 를 돌려준다(캡처 소실로 Up 이 다른 요소로 간 경우의 자가 복구).
    /// </summary>
    public bool ShouldBlockMove(bool leftButtonPressed)
    {
        if (!IsSuppressing) return false;
        if (leftButtonPressed) return true;
        IsSuppressing = false;
        return false;
    }

    /// <summary>좌버튼 릴리스 — 억제를 풀고, 억제 중이었으면 true(그 릴리스 자체도 소비해 클릭/팬 종료 처리를 건너뛴다).</summary>
    public bool OnButtonUp()
    {
        bool wasSuppressing = IsSuppressing;
        IsSuppressing = false;
        return wasSuppressing;
    }
}
