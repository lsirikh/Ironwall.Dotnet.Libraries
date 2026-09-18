using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 편제 계약 규칙(GOP API 8.0 §11-A) — 지역 검증·정규화
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 부대 편제 규칙을 <b>요청을 보내기 전에</b> 검사·정규화하는 순수 함수 모음.
/// </summary>
/// <remarks>
/// <para><b>왜 클라에 두는가</b> — 서버가 권위이지만 이 규칙들은 422 를 되돌려주는 규칙이고,
/// 그 중 상당수는 <b>되돌릴 수 없는 사고</b>(코드 오타 확정 · 인접 전삭제)로 이어진다.
/// 네트워크에 나가기 전에 막을 수 있는 것은 여기서 막는다.</para>
/// <para><b>서버가 권위인 것은 건드리지 않는다</b> — 코드 중복 · 상위/인접 상대의 실재 여부 ·
/// 자식까지 걸친 제대 정합은 서버만 알 수 있다. 여기서는 <b>알고 있는 값만으로 판정 가능한 것</b>만 본다.</para>
/// </remarks>
public static class UnitRules
{
    /// <summary>
    /// 부대 코드 형식 — <c>^[a-z0-9][a-z0-9_-]{0,31}$</c> (배포 스웨거 8.0.1 <c>UnitCreate.code</c> 설명 실측).
    /// <para>이 값은 <b>NATS subject 의 두 번째 토큰</b>이 된다. 그래서 소문자 단일 토큰이고
    /// <c>.</c>·<c>*</c>·<c>&gt;</c>·공백을 담을 수 없다.</para>
    /// </summary>
    public const string CODE_PATTERN = "^[a-z0-9][a-z0-9_-]{0,31}$";

    /// <summary>전역 사실을 싣는 예약 토큰. 부대 코드로 쓸 수 없다(<c>sensorway.global.all.sync.*</c>).</summary>
    public const string RESERVED_GLOBAL_CODE = "global";

    /// <summary>부대 코드 최대 길이(바이트가 아니라 문자 수).</summary>
    public const int CODE_MAX_LENGTH = 32;

    /// <summary>부대 이름 최대 길이.</summary>
    public const int NAME_MAX_LENGTH = 100;

    /// <summary>설명 최대 길이.</summary>
    public const int DESCRIPTION_MAX_LENGTH = 500;

    /// <summary><c>GET /api/units/{unit_id}?include=</c> 에 허용된 <b>닫힌 4종</b>. 그 밖의 값은 서버가 422 다.</summary>
    public static readonly string[] INCLUDE_TOKENS = { "parent", "children", "ancestors", "adjacent" };

    private static readonly Regex _codeRegex = new(CODE_PATTERN, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    #region - 코드 -
    /// <summary>
    /// 부대 코드가 계약을 만족하는가. 실패 사유를 <paramref name="error"/> 로 돌려준다.
    /// </summary>
    /// <remarks>
    /// 코드는 <b>등록 뒤 불변</b>이다(바꾸려 하면 422). 즉 <c>POST</c> 한 번의 오타가 영구히 남고,
    /// 그 부대 구독자의 subject 도 그 오타로 고정된다 — 그래서 생성 경로에서 반드시 먼저 본다.
    /// </remarks>
    public static bool TryValidateCode(string? code, out string? error)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            error = "부대 코드가 비어 있습니다. 1~32자의 소문자 단일 토큰이어야 합니다.";
            return false;
        }

        if (string.Equals(code, RESERVED_GLOBAL_CODE, StringComparison.Ordinal))
        {
            error = $"'{RESERVED_GLOBAL_CODE}' 는 전역 예약 토큰입니다 — 부대 코드로 쓸 수 없습니다.";
            return false;
        }

        if (code.Length > CODE_MAX_LENGTH)
        {
            error = $"부대 코드가 {CODE_MAX_LENGTH}자를 넘습니다(현재 {code.Length}자).";
            return false;
        }

        if (!_codeRegex.IsMatch(code))
        {
            error = $"부대 코드 '{code}' 가 형식({CODE_PATTERN})에 맞지 않습니다 — "
                  + "NATS subject 토큰이라 소문자·숫자·'_'·'-' 만 쓸 수 있고 첫 글자는 소문자나 숫자여야 합니다.";
            return false;
        }

        error = null;
        return true;
    }
    #endregion

    #region - 제대 -
    /// <summary>
    /// 제대 서열(작을수록 상위). <see cref="EnumUnitEchelon"/> 의 선언 순서를 그대로 쓴다.
    /// </summary>
    public static int Rank(EnumUnitEchelon echelon) => (int)echelon;

    /// <summary>
    /// <paramref name="parent"/> 가 <paramref name="child"/> 의 상위 부대가 될 수 있는가.
    /// <b>엄격히 상위</b>여야 하고(같은 제대·역전 금지), <b>건너뛰기는 허용</b>된다(연대 없는 편제 OK).
    /// </summary>
    /// <remarks>이 규칙 덕분에 편제에 <b>순환이 구조적으로 불가능</b>하다.</remarks>
    public static bool IsAllowedParent(EnumUnitEchelon parent, EnumUnitEchelon child)
        => Rank(parent) < Rank(child);

    /// <summary>
    /// 두 부대가 인접 관계를 맺을 수 있는가 — <b>같은 제대끼리만</b>이고 자기 자신은 금지다.
    /// </summary>
    public static bool IsAllowedAdjacency(EnumUnitEchelon a, EnumUnitEchelon b) => a == b;
    #endregion

    #region - 인접 목록 -
    /// <summary>
    /// 인접 부대 id 목록을 서버와 같은 형태로 정규화한다 — <b>중복 제거 + 오름차순</b>.
    /// <paramref name="selfId"/> 를 주면 자기 자신을 제거한다(서버는 422 로 거절한다).
    /// </summary>
    /// <returns><paramref name="ids"/> 가 <c>null</c> 이면 <c>null</c> 을 그대로 돌려준다 —
    /// "보내지 않음"과 "빈 목록으로 교체"는 <b>다른 뜻</b>이라 여기서 섞지 않는다.</returns>
    public static List<int>? NormalizeAdjacency(IEnumerable<int>? ids, int? selfId = null)
    {
        if (ids is null) return null;

        var query = ids.Where(x => x > 0);
        if (selfId.HasValue) query = query.Where(x => x != selfId.Value);

        return query.Distinct().OrderBy(x => x).ToList();
    }
    #endregion

    #region - include -
    /// <summary>
    /// <c>?include=</c> 값(쉼표 구분)을 검사·정규화한다. 모르는 토큰이 하나라도 있으면 실패다
    /// (서버가 <c>모르는 include 값: [...]</c> 로 422 를 준다).
    /// </summary>
    /// <param name="include">원문. <c>null</c>·빈 문자열이면 성공이고 <paramref name="normalized"/> 는 <c>null</c>.</param>
    public static bool TryValidateInclude(string? include, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(include)) return true;

        var tokens = include.Split(',')
                            .Select(t => t.Trim())
                            .Where(t => t.Length > 0)
                            .ToList();

        var unknown = tokens.Where(t => !INCLUDE_TOKENS.Contains(t, StringComparer.Ordinal)).ToList();
        if (unknown.Count > 0)
        {
            error = $"모르는 include 값: [{string.Join(", ", unknown)}] — "
                  + $"허용: {string.Join(" · ", INCLUDE_TOKENS)}";
            return false;
        }

        if (tokens.Count == 0) return true;

        normalized = string.Join(",", tokens.Distinct(StringComparer.Ordinal));
        return true;
    }
    #endregion

    #region - 제대 문자열 -
    /// <summary>
    /// 와이어 문자열을 <see cref="EnumUnitEchelon"/> 으로 읽는다. <b>모르는 값이면 <c>null</c></b>.
    /// </summary>
    /// <remarks>
    /// 읽기 경로에서 예외를 던지지 않는 것이 요점이다 — 서버가 나중에 제대를 추가하면
    /// (PostgreSQL enum 은 값 추가가 가능하다) 목록 응답 <b>전체</b>가 역직렬화 예외로 죽는다.
    /// 그래서 읽기 DTO 는 <c>echelon</c> 을 문자열로 받고 이 함수로 해석한다.
    /// </remarks>
    public static EnumUnitEchelon? ParseEchelon(string? wire)
    {
        if (string.IsNullOrWhiteSpace(wire)) return null;
        return Enum.TryParse<EnumUnitEchelon>(wire.Trim(), ignoreCase: false, out var parsed)
             ? parsed
             : null;
    }

    /// <summary>제대를 와이어 문자열로. 멤버명이 곧 와이어 값이다.</summary>
    public static string ToWire(EnumUnitEchelon echelon) => echelon.ToString();
    #endregion
}
