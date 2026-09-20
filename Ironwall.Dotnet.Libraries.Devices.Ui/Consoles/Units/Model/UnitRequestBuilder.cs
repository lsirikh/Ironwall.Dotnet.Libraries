using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;

/****************************************************************************
   Purpose      : 부대 쓰기 본문 조립 — 보낼 키만 싣는다 (N-11 FR-07 · FR-10 · FR-13)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>상세 폼이 쥐고 있는 편집 값. 원본과 견주어 <b>바뀐 칸만</b> 본문에 실린다.</summary>
public sealed record UnitEditValues(string Name, EnumUnitEchelon? Echelon, string? Description, bool IsEnable);

/// <summary>등록 폼이 쥐고 있는 값. <c>Code</c> 는 <b>여기서만</b> 정할 수 있다.</summary>
public sealed record UnitCreateValues(string Code, string Name, EnumUnitEchelon Echelon, int? ParentId, string? Description, bool IsEnable);

/// <summary>삭제를 막은 것들 — 409 <c>details.counts</c> 를 사람 말로 푼 결과.</summary>
public sealed record UnitDeleteBlock(IReadOnlyList<UnitDeleteBlockItem> Items)
{
    public int Total => Items.Sum(i => i.Count);
}

public sealed record UnitDeleteBlockItem(string Label, int Count);

/// <summary>
/// 부대 쓰기 요청을 만든다. <b>PATCH 만 쓴다</b> — <c>PUT</c> 은 생략한 필드를 기본값으로 되돌려
/// <c>parent_id</c> 생략이 곧 "루트로 끌어올림", <c>adjacent_unit_ids</c> 생략이 곧 "인접 전삭제"다
/// (와이어프레임 L370-373 §5-3).
/// </summary>
public static class UnitRequestBuilder
{
    #region - 트리 이동 -
    /// <summary>
    /// 상위 부대 바꾸기 — 본문에 <c>parent_id</c> <b>하나만</b> 싣는다. 폭발반경 1 이라 드롭 즉시 보낸다
    /// (스토리보드 L355 · 드래그 와이어프레임 L411).
    /// </summary>
    /// <param name="newParentId"><c>null</c> 이면 루트로 — <c>"parent_id": null</c> 을 <b>명시</b>해 보낸다.</param>
    public static UnitUpdateDto Move(int? newParentId)
    {
        var dto = new UnitUpdateDto();
        if (newParentId is int parentId) dto.ParentId = parentId;
        else dto.MoveToRoot();
        return dto;
    }
    #endregion

    #region - 인접 -
    /// <summary>
    /// 인접 <b>전체 집합</b> 교체. 중복 제거 · 자기 제외 · 오름차순은 <see cref="UnitRules.NormalizeAdjacency"/> 가 한다.
    /// </summary>
    public static UnitUpdateDto Adjacency(IEnumerable<int>? ids, int selfId)
        => new() { AdjacentUnitIds = UnitRules.NormalizeAdjacency(ids, selfId) ?? new List<int>() };
    #endregion

    #region - 상세 편집 -
    /// <summary>
    /// 바뀐 칸만 실은 PATCH 본문. 바뀐 것이 없으면 <c>null</c> — 호출 자체를 하지 않는다.
    /// </summary>
    /// <remarks>
    /// <para><c>code</c> 는 <b>절대 싣지 않는다</b> — 등록 뒤 불변이고 보내면 422 다(스토리보드 L397-399).</para>
    /// <para><c>description</c> 은 빈 문자열을 보내지 않는다(<c>EMPTY_STRING</c> 422). 원래 값이 있었는데
    /// 비웠으면 <b>명시적 null</b> 로 지우고, 원래도 비어 있었으면 키째 뺀다.</para>
    /// </remarks>
    public static UnitUpdateDto? Edit(UnitDto? original, UnitEditValues edited)
    {
        ArgumentNullException.ThrowIfNull(edited);

        var dto = new UnitUpdateDto();
        var name = (edited.Name ?? string.Empty).Trim();
        var description = edited.Description?.Trim();

        if (name.Length > 0 && !string.Equals(name, original?.Name, StringComparison.Ordinal))
            dto.Name = name;

        if (edited.Echelon is EnumUnitEchelon echelon && echelon != original?.Echelon)
            dto.Echelon = echelon;

        if (edited.IsEnable != (original?.IsEnable ?? true))
            dto.IsEnable = edited.IsEnable;

        var originalDescription = original?.Description?.Trim();
        if (!string.Equals(description ?? string.Empty, originalDescription ?? string.Empty, StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(description)) dto.ClearDescription();
            else dto.Description = description;
        }

        return dto.IsEmpty ? null : dto;
    }

    /// <summary>폼이 보낼 수 있는 값인가 — 길이 제약(이름 1~100 · 설명 ≤500)을 지역에서 먼저 본다.</summary>
    public static bool TryValidateEdit(UnitEditValues values, out string? error)
    {
        var name = (values?.Name ?? string.Empty).Trim();
        if (name.Length == 0) { error = "부대 이름을 입력하십시오."; return false; }
        if (name.Length > UnitRules.NAME_MAX_LENGTH) { error = $"부대 이름이 {UnitRules.NAME_MAX_LENGTH}자를 넘습니다(현재 {name.Length}자)."; return false; }

        var description = values!.Description?.Trim();
        if (description is { Length: > UnitRules.DESCRIPTION_MAX_LENGTH })
        {
            error = $"설명이 {UnitRules.DESCRIPTION_MAX_LENGTH}자를 넘습니다(현재 {description.Length}자).";
            return false;
        }

        error = null;
        return true;
    }
    #endregion

    #region - 등록 -
    /// <summary>등록 본문. 코드는 이 경로에서만 정해지고 <b>이후 영원히 바꿀 수 없다</b>.</summary>
    public static UnitCreateDto Create(UnitCreateValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var description = values.Description?.Trim();
        return new UnitCreateDto
        {
            Code = (values.Code ?? string.Empty).Trim(),
            Name = (values.Name ?? string.Empty).Trim(),
            Echelon = values.Echelon,
            ParentId = values.ParentId,
            Description = string.IsNullOrEmpty(description) ? null : description,
            IsEnable = values.IsEnable,
        };
    }

    /// <summary>
    /// 등록 전 지역 검사 — 코드 형식 · <c>global</c> 예약 · 전역 중복 · 이름 · 상위 제대.
    /// </summary>
    /// <param name="existingCodes">이미 쓰고 있는 코드(전역 유일이라 목록 대조로 선차단한다).</param>
    /// <param name="parent">고른 상위 부대(없으면 루트로 만든다).</param>
    public static bool TryValidateCreate(
        UnitCreateValues values,
        IReadOnlyCollection<string>? existingCodes,
        UnitTreeNode? parent,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(values);

        var code = (values.Code ?? string.Empty).Trim();
        if (!UnitRules.TryValidateCode(code, out error)) return false;

        if (existingCodes != null && existingCodes.Contains(code, StringComparer.Ordinal))
        {
            error = $"부대 코드 '{code}' 는 이미 쓰고 있습니다 — 코드는 전역에서 하나뿐이어야 합니다.";
            return false;
        }

        if (!TryValidateEdit(new UnitEditValues(values.Name, values.Echelon, values.Description, values.IsEnable), out error))
            return false;

        if (values.ParentId is int parentId)
        {
            if (parent == null || parent.Id != parentId)
            {
                error = "상위 부대를 편제에서 찾지 못했습니다.";
                return false;
            }
            if (parent.Echelon is not EnumUnitEchelon parentEchelon)
            {
                error = $"'{parent.Name}' 의 제대('{parent.EchelonRaw}')를 이 판본이 알지 못합니다 — 상위로 삼지 않습니다.";
                return false;
            }
            if (!UnitRules.IsAllowedParent(parentEchelon, values.Echelon))
            {
                error = $"{UnitDropRules.EchelonText(values.Echelon)}는 {UnitDropRules.EchelonText(parentEchelon)}에 붙일 수 없습니다 — 엄격히 상위 제대여야 합니다.";
                return false;
            }
        }

        error = null;
        return true;
    }
    #endregion

    #region - 409 삭제 차단 -
    /// <summary>
    /// 서버가 준 <c>details.counts</c> 8종을 사람 말로 푼다. 0 인 종류는 응답에 실리지 않는다(스토리보드 L392).
    /// </summary>
    /// <remarks>⚠ <c>error.message</c> 는 <b>문자열</b>이다 — 객체로 읽지 않는다(스토리보드 L393).</remarks>
    public static UnitDeleteBlock? ParseDeleteConflict(ApiError? error)
    {
        if (error == null) return null;
        if (!string.Equals(error.Code, ApiErrorCodes.Conflict, StringComparison.OrdinalIgnoreCase)) return null;

        var counts = error.DetailsToken as JObject;
        if (counts?["counts"] is JObject nested) counts = nested;
        if (counts == null) return new UnitDeleteBlock(Array.Empty<UnitDeleteBlockItem>());

        var items = new List<UnitDeleteBlockItem>();
        foreach (var property in counts.Properties())
        {
            if (property.Value is not JValue { Type: JTokenType.Integer } value) continue;
            var count = value.Value<int>();
            if (count <= 0) continue;
            items.Add(new UnitDeleteBlockItem(LabelOf(property.Name), count));
        }

        return new UnitDeleteBlock(items);
    }

    private static string LabelOf(string key) => key switch
    {
        "child_units" or "children" => "하위 부대",
        "devices" => "장비",
        "device_groups" => "장비 그룹",
        "servers" => "서버",
        "events" => "이벤트 이력",
        "actions" or "action_reports" => "조치 보고",
        "suppression_schedules" => "억제 스케줄",
        "system_events" => "시스템 이벤트",
        _ => key,
    };
    #endregion
}
