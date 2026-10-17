using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 라인/그룹/구역 드로잉 키 라우팅 순수 판정(WPF 무의존) — ESC 취소(C8) ·
                  포커스 무관 터널 처리(C9) · 드로잉 중 Ctrl+Z/Delete/방향키 선점 차단(C10)
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>드로잉 키 라우팅이 구분하는 키 — WPF <c>Key</c> 를 어댑터(<c>DrawingKeyRouter.Wpf.cs</c>)가 여기로 사상한다.</summary>
public enum DrawingKey
{
    Other,
    Escape,
    Enter,
    Back,
    Z,
    Y,
    Delete,
    Left,
    Right,
    Up,
    Down,
}

/// <summary>수정키 — 값은 WPF <c>ModifierKeys</c>(Alt=1, Control=2, Shift=4)와 동일하게 두어 어댑터가 캐스팅만 한다.</summary>
[Flags]
public enum DrawingKeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
}

/// <summary>라우터 판정 결과. <see cref="PassThrough"/> 만 미소비, 나머지는 전부 소비(<c>e.Handled=true</c>).</summary>
public enum DrawingKeyAction
{
    /// <summary>드로잉과 무관 — 기존 핸들러(전역 Undo·Delete·방향키 격자이동·편집모드 ESC 등)로 흘려보낸다.</summary>
    PassThrough,
    /// <summary>ESC(스트로크 눌림 중) — 스트로크만 취소(원위치, 기존 정점 유지). 드로잉 자체는 계속.</summary>
    CancelStroke,
    /// <summary>ESC(스트로크 없음) — 드로잉 자체 취소(HUD·커서·정점 제거). drag-first ③ 계약.</summary>
    CancelDrawing,
    /// <summary>Enter — 드로잉 완료(유효성은 서비스가 판정).</summary>
    CompleteDrawing,
    /// <summary>Backspace / Ctrl+Z — 마지막 정점 1개 제거.</summary>
    UndoLastPoint,
    /// <summary>드로잉 중 의미 없는 편집 단축키(Delete·방향키·Ctrl+Y·Ctrl+Shift+Z, 스트로크 중 Enter/Backspace) — 소비만 하고 아무것도 하지 않는다.
    /// 전역 Undo/Redo·선택 심볼 삭제·격자 이동으로 새는 것을 막는다(C10).</summary>
    Ignore,
}

/// <summary>
/// 드로잉 키 라우팅 SSOT — 윈도우/맵 <c>PreviewKeyDown</c> 터널(MapViewModel, 1차)과 맵 클래스 핸들러
/// <c>GMapCustomControl.OnKeyDown</c>(2차 가드)이 같은 판정을 쓴다. 판정만 하고 실행은 호출자가 한다(WPF 무의존 → 헤드리스 테스트).
/// </summary>
public static partial class DrawingKeyRouter
{
    /// <summary>
    /// 키 → 드로잉 액션 판정.
    /// </summary>
    /// <param name="key">눌린 키(어댑터 사상값).</param>
    /// <param name="modifiers">수정키.</param>
    /// <param name="isDrawing">드로잉 모드 활성(<c>LineDrawingService.IsDrawing</c>).</param>
    /// <param name="isStrokePressed">좌버튼 눌림(캡처) 중 — 데드존 통과 전 클릭 후보 포함(<c>_linePress.HasValue</c>).</param>
    public static DrawingKeyAction Route(DrawingKey key, DrawingKeyModifiers modifiers, bool isDrawing, bool isStrokePressed)
    {
        // 드로잉도 눌림도 없으면 라우터는 관여하지 않는다 — 기존 단축키 동작 100% 유지.
        if (!isDrawing && !isStrokePressed) return DrawingKeyAction.PassThrough;

        bool ctrl = (modifiers & DrawingKeyModifiers.Control) != 0;
        bool shift = (modifiers & DrawingKeyModifiers.Shift) != 0;

        switch (key)
        {
            case DrawingKey.Escape:
                // 첫 ESC = 스트로크만 취소, 스트로크가 없으면 드로잉 취소(주석 :1762 '두 번째 ESC' 계약을 실제로 성립시킨다).
                return isStrokePressed ? DrawingKeyAction.CancelStroke : DrawingKeyAction.CancelDrawing;

            case DrawingKey.Enter:
                // 눌린 채 완료하면 릴리스 경로(FinishLineDrag)가 종료된 서비스에 스트로크를 확정하려 든다 — 릴리스 후에만 허용.
                return isStrokePressed ? DrawingKeyAction.Ignore : DrawingKeyAction.CompleteDrawing;

            case DrawingKey.Back:
                return isStrokePressed ? DrawingKeyAction.Ignore : DrawingKeyAction.UndoLastPoint;

            case DrawingKey.Z:
                if (!ctrl) return DrawingKeyAction.PassThrough;
                if (shift) return DrawingKeyAction.Ignore;                       // Ctrl+Shift+Z(Redo) — 드로잉엔 Redo 없음, 전역 Redo 차단
                return isStrokePressed ? DrawingKeyAction.Ignore : DrawingKeyAction.UndoLastPoint;

            case DrawingKey.Y:
                return ctrl ? DrawingKeyAction.Ignore : DrawingKeyAction.PassThrough;   // Ctrl+Y(Redo) 차단

            case DrawingKey.Delete:
                return DrawingKeyAction.Ignore;   // 드로잉 시작 직후 잔존 선택 심볼 삭제 확인팝업 차단(C10 정정: 점 0개/스트로크-only 구간)

            case DrawingKey.Left:
            case DrawingKey.Right:
            case DrawingKey.Up:
            case DrawingKey.Down:
                return DrawingKeyAction.Ignore;   // 격자 이동·회전(Ctrl/Ctrl+Shift+←→)·포커스 네비 전부 차단 — 뷰가 바뀌면 스트로크 투영이 어긋난다

            default:
                return DrawingKeyAction.PassThrough;
        }
    }

    /// <summary><see cref="DrawingKeyAction.PassThrough"/> 를 제외한 모든 판정은 소비된다.</summary>
    public static bool IsConsumed(DrawingKeyAction action) => action != DrawingKeyAction.PassThrough;
}
