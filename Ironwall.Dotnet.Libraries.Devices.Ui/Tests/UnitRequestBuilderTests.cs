using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// N-11 FR-07 · FR-10 · FR-13 · FR-17 — 부대 쓰기 본문에는 <b>보낼 키만</b> 실린다.
/// </summary>
/// <remarks>
/// <c>PATCH</c> 는 RFC 7396 병합이라 <b>안 보낸 키는 그대로 남고 <c>null</c> 은 삭제</b>다.
/// 그래서 "무엇이 실렸는가" 를 와이어 글자로 직접 본다 — 프로퍼티만 보면 <c>ShouldSerialize</c> 를 놓친다.
/// </remarks>
[Collection("CaliburnIoC")]   // 컬렉션 수를 늘리면 정적 IoC 를 바꾸는 이웃과 겹칠 확률이 올라간다 — 같이 직렬화한다.
public class UnitRequestBuilderTests
{
    private static JObject Wire(object dto) => JObject.Parse(JsonConvert.SerializeObject(dto));

    private static UnitDto Original() => new()
    {
        Id = 6,
        Code = "c0206",
        Name = "6중대",
        EchelonRaw = "Company",
        ParentId = 3,
        IsEnable = true,
        Description = "북측 담당",
        AdjacentUnitIds = new List<int> { 5, 7 },
    };

    #region - 이동 -
    [Fact]
    public void should_send_only_parent_id_when_moving_a_node()
    {
        var body = Wire(UnitRequestBuilder.Move(9));

        Assert.Equal(new[] { "parent_id" }, body.Properties().Select(p => p.Name));
        Assert.Equal(9, (int)body["parent_id"]!);
    }

    [Fact]
    public void should_send_explicit_null_parent_when_moving_to_root()
    {
        // 키를 빼면 "안 바꿈" 이고 명시적 null 이라야 "루트로" 다 — 둘은 다른 뜻이다.
        var body = Wire(UnitRequestBuilder.Move(null));

        Assert.Equal(new[] { "parent_id" }, body.Properties().Select(p => p.Name));
        Assert.Equal(JTokenType.Null, body["parent_id"]!.Type);
    }
    #endregion

    #region - 인접 -
    [Fact]
    public void should_send_the_whole_adjacency_set_normalized()
    {
        var body = Wire(UnitRequestBuilder.Adjacency(new[] { 9, 5, 5, 6 }, selfId: 6));

        Assert.Equal(new[] { "adjacent_unit_ids" }, body.Properties().Select(p => p.Name));
        Assert.Equal(new[] { 5, 9 }, body["adjacent_unit_ids"]!.Select(t => (int)t));   // 중복 제거 · 자기 제외 · 오름차순
    }

    [Fact]
    public void should_send_an_empty_array_when_clearing_every_adjacency()
    {
        var body = Wire(UnitRequestBuilder.Adjacency(System.Array.Empty<int>(), selfId: 6));

        Assert.Equal(JTokenType.Array, body["adjacent_unit_ids"]!.Type);
        Assert.Empty(body["adjacent_unit_ids"]!);
    }
    #endregion

    #region - 편집 -
    [Fact]
    public void should_return_null_when_nothing_changed()
    {
        var dto = UnitRequestBuilder.Edit(Original(), new UnitEditValues("6중대", EnumUnitEchelon.Company, "북측 담당", true));

        Assert.Null(dto);
    }

    [Fact]
    public void should_send_only_the_changed_field_when_saving()
    {
        var dto = UnitRequestBuilder.Edit(Original(), new UnitEditValues("6중대 (개편)", EnumUnitEchelon.Company, "북측 담당", true));

        var body = Wire(dto!);
        Assert.Equal(new[] { "name" }, body.Properties().Select(p => p.Name));
        Assert.Equal("6중대 (개편)", (string?)body["name"]);
    }

    [Fact]
    public void should_never_send_code_when_saving_an_existing_unit()
    {
        // 코드는 등록 뒤 불변이다 — 실리면 422 이고, 실렸다는 사실 자체가 설계 위반이다.
        var dto = UnitRequestBuilder.Edit(Original(), new UnitEditValues("다른 이름", EnumUnitEchelon.Battalion, "다른 설명", false));

        var body = Wire(dto!);
        Assert.DoesNotContain("code", body.Properties().Select(p => p.Name));
        Assert.Equal(new[] { "name", "echelon", "is_enable", "description" }.OrderBy(x => x),
                     body.Properties().Select(p => p.Name).OrderBy(x => x));
        Assert.Equal("Battalion", (string?)body["echelon"]);
        Assert.False((bool)body["is_enable"]!);
    }

    [Fact]
    public void should_send_explicit_null_description_when_the_operator_cleared_it()
    {
        var dto = UnitRequestBuilder.Edit(Original(), new UnitEditValues("6중대", EnumUnitEchelon.Company, "   ", true));

        var body = Wire(dto!);
        Assert.Equal(JTokenType.Null, body["description"]!.Type);   // RFC 7396: null = 삭제
    }

    [Fact]
    public void should_omit_description_when_it_was_empty_and_stays_empty()
    {
        var original = Original();
        original.Description = null;

        var dto = UnitRequestBuilder.Edit(original, new UnitEditValues("새 이름", EnumUnitEchelon.Company, "", true));

        var body = Wire(dto!);
        Assert.DoesNotContain("description", body.Properties().Select(p => p.Name));   // 빈 문자열은 EMPTY_STRING 422 다
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("정상 이름", true)]
    public void should_reject_edit_when_the_name_is_blank(string name, bool expected)
        => Assert.Equal(expected, UnitRequestBuilder.TryValidateEdit(new UnitEditValues(name, EnumUnitEchelon.Company, null, true), out _));

    [Fact]
    public void should_reject_edit_when_the_description_is_too_long()
    {
        var values = new UnitEditValues("이름", EnumUnitEchelon.Company, new string('가', UnitRules.DESCRIPTION_MAX_LENGTH + 1), true);

        Assert.False(UnitRequestBuilder.TryValidateEdit(values, out var error));
        Assert.Contains("500", error);
    }
    #endregion

    #region - 등록 -
    private static UnitTreeNode BattalionNode() => UnitTreeBuilder.Build(new UnitGraphDto
    {
        Nodes = new List<UnitListDto> { new() { Id = 3, Code = "b01", Name = "2대대", EchelonRaw = "Battalion" } },
    }).Find(3)!;

    [Theory]
    [InlineData("c0206", true)]
    [InlineData("global", false)]          // 전역 예약 토큰
    [InlineData("C0206", false)]           // 대문자 금지
    [InlineData("c.0206", false)]          // subject 구분자
    [InlineData("_c0206", false)]          // 첫 글자
    [InlineData("", false)]
    public void should_check_the_code_pattern_before_sending(string code, bool expected)
    {
        var values = new UnitCreateValues(code, "6중대", EnumUnitEchelon.Company, 3, null, true);

        Assert.Equal(expected, UnitRequestBuilder.TryValidateCreate(values, new[] { "d01" }, BattalionNode(), out _));
    }

    [Fact]
    public void should_reject_create_when_the_code_is_already_used()
    {
        var values = new UnitCreateValues("c0206", "6중대", EnumUnitEchelon.Company, 3, null, true);

        Assert.False(UnitRequestBuilder.TryValidateCreate(values, new[] { "c0206" }, BattalionNode(), out var error));
        Assert.Contains("이미 쓰고", error);
    }

    [Fact]
    public void should_reject_create_when_the_parent_is_not_a_higher_echelon()
    {
        var values = new UnitCreateValues("b0207", "3대대", EnumUnitEchelon.Battalion, 3, null, true);

        Assert.False(UnitRequestBuilder.TryValidateCreate(values, null, BattalionNode(), out var error));
        Assert.Contains("더 높은 제대", error);
    }

    [Fact]
    public void should_allow_create_at_root_when_no_parent_is_chosen()
    {
        var values = new UnitCreateValues("d01", "제○○사단", EnumUnitEchelon.Division, null, null, true);

        Assert.True(UnitRequestBuilder.TryValidateCreate(values, System.Array.Empty<string>(), null, out _));
    }

    [Fact]
    public void should_omit_blank_description_when_creating()
    {
        var body = Wire(UnitRequestBuilder.Create(new UnitCreateValues(" c0206 ", " 6중대 ", EnumUnitEchelon.Company, 3, "  ", true)));

        Assert.Equal("c0206", (string?)body["code"]);       // 앞뒤 공백은 다듬는다
        Assert.Equal("6중대", (string?)body["name"]);
        Assert.DoesNotContain("description", body.Properties().Select(p => p.Name));
    }
    #endregion

    #region - 409 삭제 차단 -
    [Fact]
    public void should_unpack_the_counts_when_delete_is_blocked()
    {
        var error = new ApiError
        {
            Code = ApiErrorCodes.Conflict,
            // 키 이름은 서버 정본 그대로다 — app/routers/units.py 의 _UNIT_DEPENDENTS (실측 2026-09-21).
            DetailsToken = JObject.Parse("""
                {"counts":{"child_units":1,"devices":18,"device_groups":2,"servers":0,
                           "events":431,"action_events":7,"event_suppression_schedules":3,"system_events":2}}
                """),
        };

        var block = UnitRequestBuilder.ParseDeleteConflict(error)!;

        Assert.Equal(new[] { "하위 부대", "장비", "장비 그룹", "이벤트 이력", "조치 이력", "억제 스케줄", "시스템 이벤트" },
                     block.Items.Select(i => i.Label));
        Assert.Equal(new[] { 1, 18, 2, 431, 7, 3, 2 }, block.Items.Select(i => i.Count));   // 0 인 종류는 싣지 않는다
        Assert.Equal(464, block.Total);
    }

    /// <summary>
    /// ★ 서버가 세는 <b>여덟 표 전부</b>가 한글로 풀린다 — 하나라도 영어로 새면 삭제 차단 안내가 반쪽이 된다.
    /// </summary>
    [Theory]
    [InlineData("child_units", "하위 부대")]
    [InlineData("devices", "장비")]
    [InlineData("device_groups", "장비 그룹")]
    [InlineData("servers", "서버")]
    [InlineData("events", "이벤트 이력")]
    [InlineData("action_events", "조치 이력")]
    [InlineData("event_suppression_schedules", "억제 스케줄")]
    [InlineData("system_events", "시스템 이벤트")]
    public void should_spell_every_server_dependent_key_in_korean(string key, string label)
    {
        var error = new ApiError
        {
            Code = ApiErrorCodes.Conflict,
            DetailsToken = JObject.Parse("{\"counts\":{\"" + key + "\":3}}"),
        };

        var item = Assert.Single(UnitRequestBuilder.ParseDeleteConflict(error)!.Items);
        Assert.Equal(label, item.Label);
        Assert.NotEqual(key, item.Label);
    }

    [Fact]
    public void should_show_an_unknown_key_verbatim_rather_than_hiding_it()
    {
        var error = new ApiError
        {
            Code = ApiErrorCodes.Conflict,
            DetailsToken = JObject.Parse("""{"counts":{"future_table":4}}"""),
        };

        Assert.Equal("future_table", Assert.Single(UnitRequestBuilder.ParseDeleteConflict(error)!.Items).Label);
    }

    [Fact]
    public void should_ignore_the_error_when_it_is_not_a_conflict()
        => Assert.Null(UnitRequestBuilder.ParseDeleteConflict(new ApiError { Code = ApiErrorCodes.ValidationError }));

    [Fact]
    public void should_not_throw_when_the_conflict_details_are_a_plain_string()
    {
        // error.message 는 문자열이다 — details 도 문자열로 오는 서버가 있어도 화면이 죽으면 안 된다.
        var error = new ApiError { Code = ApiErrorCodes.Conflict, Details = "unit has devices" };

        var block = UnitRequestBuilder.ParseDeleteConflict(error)!;

        Assert.Empty(block.Items);
    }

    [Fact]
    public void should_return_no_items_when_the_conflict_has_no_details()
        => Assert.Empty(UnitRequestBuilder.ParseDeleteConflict(new ApiError { Code = ApiErrorCodes.Conflict })!.Items);
    #endregion
}
