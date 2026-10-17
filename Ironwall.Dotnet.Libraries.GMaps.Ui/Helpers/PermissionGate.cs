using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 권한 판정의 "모를 때 기본값" 정책 — PRD symbol-detail-and-door-control FR-27
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 권한 엔진이 <b>대답하지 못할 때</b>(미등록·미로그인) 무엇을 기본값으로 할지 정하는 단 하나의 자리.
///
/// <para><b>왜 두 갈래인가</b>:</para>
/// <list type="bullet">
///   <item><b>명령류</b>(<c>control</c> — 방송·장비 개폐·PTZ)는 <b>되돌릴 수 없는 외부 행위</b>다.
///   권한 정보가 없다는 이유로 누구나 문을 열고 방송을 내보낼 수 있으면 안 된다 → <b>fail-closed</b>.</item>
///   <item><b>조회·편집</b>(<c>view</c>/<c>edit</c>)은 화면 안에서 끝나고 되돌릴 수 있다.
///   권한 엔진이 없는 환경(단독 실행·테스트·오프라인)에서 앱이 통째로 잠기면 못 쓴다 → <b>fail-open</b>.</item>
/// </list>
///
/// <para>이 규칙을 호출부마다 <c>?? false</c>/<c>?? true</c> 로 흩어 쓰면 하나가 빠져도 아무도 모른다 —
/// 실제로 1차 구현에서 <c>cameras:control</c> 하나가 <c>?? true</c> 로 남아 있었다.</para>
/// </summary>
public static class PermissionGate
{
    /// <summary>되돌릴 수 없는 외부 행위인가 — 이것만 fail-closed 다.</summary>
    public const string ControlVerb = "control";

    /// <summary>이 동작이 명령류인가.</summary>
    public static bool IsCommand(string? verb)
        => string.Equals(verb, ControlVerb, StringComparison.OrdinalIgnoreCase);

    /// <summary>권한 정보를 모를 때의 기본값. 명령류만 <c>false</c>.</summary>
    public static bool FallbackFor(string? verb) => !IsCommand(verb);

    /// <summary>
    /// 권한 판정 결과를 정책에 태워 확정한다.
    /// </summary>
    /// <param name="granted">권한 엔진의 답. <c>null</c> = 물어볼 수 없었다(미등록·미로그인).</param>
    /// <param name="verb"><c>control</c> / <c>edit</c> / <c>view</c>.</param>
    public static bool Resolve(bool? granted, string? verb) => granted ?? FallbackFor(verb);
}
