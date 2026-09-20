using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 초안 → 저장 요청 본문(suppression-schedule PRD FR-40~FR-47 · V-13~V-20).
/// <para>PATCH 는 RFC 7396 이다 — 빈 DTO 로 만들면 null 이 값을 지우고 배열이 통째로 갈린다.</para>
/// </summary>
public class SuppressionRequestBuilderTests
{
    private static readonly DateTimeOffset Kst = new(2026, 9, 20, 9, 0, 0, TimeSpan.FromHours(9));

    private static SuppressionDraft NewDraft(string mode = SuppressionTargetDrop.ModeDevice, params int[] ids)
    {
        var draft = SuppressionDraft.NewSchedule(Kst);
        draft.Name = " 정문 보수 ";
        draft.TargetType = mode;
        foreach (var id in ids)
            draft.Targets.Add(new SuppressionTargetChip(
                mode == SuppressionTargetDrop.ModeGroup ? SuppressionTargetKind.Group : SuppressionTargetKind.Device,
                id, $"#{id}"));
        return draft;
    }

    #region - 생성(POST) -

    [Fact]
    public void should_trim_the_name_when_building_a_create_request()
        => Assert.Equal("정문 보수", SuppressionRequestBuilder.BuildCreate(NewDraft(ids: 1)).Name);

    [Fact]
    public void should_send_an_offset_aware_start_when_building_a_create_request()
    {
        var dto = SuppressionRequestBuilder.BuildCreate(NewDraft(ids: 1));

        // GIS → 서버 datetime 은 반드시 offset 이 붙은 ISO 8601 이다.
        Assert.Equal("2026-09-20T09:00:00.000+09:00", dto.WindowStart);
    }

    [Fact]
    public void should_send_an_explicit_null_end_when_the_window_is_unlimited()
    {
        var draft = NewDraft(ids: 1);
        draft.IsWeekly = true;
        draft.WindowEnd = null;

        var json = JObject.Parse(JsonConvert.SerializeObject(SuppressionRequestBuilder.BuildCreate(draft)));

        // 키를 생략하면 서버 model_fields_set 검사에서 422 다 — 명시적 null 이어야 한다.
        Assert.True(json.ContainsKey("window_end"));
        Assert.Equal(JTokenType.Null, json["window_end"]!.Type);
    }

    [Fact]
    public void should_omit_recurrence_fields_when_the_schedule_is_one_shot()
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(SuppressionRequestBuilder.BuildCreate(NewDraft(ids: 1))));

        // recurrence_type=none 인데 반복 칸이 실리면 서버가 422 다.
        Assert.Equal("none", (string?)json["recurrence_type"]);
        Assert.False(json.ContainsKey("days_of_week"));
        Assert.False(json.ContainsKey("daily_start"));
        Assert.False(json.ContainsKey("daily_end"));
    }

    [Fact]
    public void should_send_wall_clock_daily_times_when_the_schedule_is_weekly()
    {
        var draft = NewDraft(ids: 1);
        draft.IsWeekly = true;
        draft.DaysOfWeekMask = SuppressionRules.DaysWeekdayPreset;
        draft.DailyStart = TimeSpan.FromHours(8);
        draft.DailyEnd = new TimeSpan(21, 30, 0);

        var json = JObject.Parse(JsonConvert.SerializeObject(SuppressionRequestBuilder.BuildCreate(draft)));

        // offset 이나 Z 가 붙으면 즉시 422 — 일일 시각은 벽시계다.
        Assert.Equal("08:00:00", (string?)json["daily_start"]);
        Assert.Equal("21:30:00", (string?)json["daily_end"]);
        Assert.Equal(SuppressionRules.DaysWeekdayPreset, (int?)json["days_of_week"]);
    }

    [Fact]
    public void should_send_only_the_matching_target_array_when_the_mode_is_device()
    {
        var dto = SuppressionRequestBuilder.BuildCreate(NewDraft(SuppressionTargetDrop.ModeDevice, 3, 5));

        Assert.Equal(new[] { 3, 5 }, dto.TargetDeviceIds);
        Assert.Empty(dto.TargetGroupIds);
    }

    [Fact]
    public void should_send_only_the_matching_target_array_when_the_mode_is_group()
    {
        var dto = SuppressionRequestBuilder.BuildCreate(NewDraft(SuppressionTargetDrop.ModeGroup, 8));

        Assert.Equal(new[] { 8 }, dto.TargetGroupIds);
        Assert.Empty(dto.TargetDeviceIds);
    }

    [Fact]
    public void should_send_no_targets_when_the_mode_is_all()
    {
        var draft = NewDraft(SuppressionTargetDrop.ModeDevice, 3);
        draft.TargetType = SuppressionTargetDrop.ModeAll;

        var dto = SuppressionRequestBuilder.BuildCreate(draft);

        Assert.Empty(dto.TargetDeviceIds);
        Assert.Empty(dto.TargetGroupIds);
    }

    [Fact]
    public void should_omit_unit_id_when_the_contract_is_older_than_v8()
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(SuppressionRequestBuilder.BuildCreate(NewDraft(ids: 1))));

        // 6.3.2 · 7.0.1 쓰기 스키마는 extra=forbid 라 이 키가 실리는 순간 422 다.
        Assert.False(json.ContainsKey("unit_id"));
    }

    [Fact]
    public void should_send_unit_id_when_the_caller_says_the_contract_allows_it()
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(SuppressionRequestBuilder.BuildCreate(NewDraft(ids: 1), unitId: 7)));

        Assert.Equal(7, (int?)json["unit_id"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void should_call_the_server_once_no_matter_how_many_targets(int count)
    {
        var draft = NewDraft(SuppressionTargetDrop.ModeDevice, Enumerable.Range(1, count).ToArray());

        // 대상 배열 계약이라 N개여도 호출은 하나다 — 부분 실패 · 재시도 화면이 필요 없는 근거.
        Assert.Equal(1, SuppressionRequestBuilder.ServerCallsForSave(draft));
    }

    #endregion

    #region - 수정(PATCH) — 받아 온 원본에서 다시 채운다 -

    private static EventSuppressionScheduleDto FullBaseline() => new()
    {
        Id = 42,
        Name = "탄약고 야간 점검",
        Description = "정비 창",
        UnitId = 7,
        TargetType = SuppressionTargetDrop.ModeDevice,
        TargetDeviceIds = new List<int> { 3, 5 },
        TargetGroupIds = new List<int>(),
        TargetSide = "detection",
        EventScope = "malfunction",
        WindowStart = "2026-09-20T09:00:00.000+09:00",
        WindowEnd = "2026-09-20T18:00:00.000+09:00",
        RecurrenceRule = "RRULE:FREQ=WEEKLY",
        RecurrenceType = "none",
        Status = "active",
    };

    [Fact]
    public void should_throw_when_building_a_patch_without_the_fetched_original()
    {
        var draft = NewDraft(ids: 1);            // Baseline 이 없다

        Assert.Throws<InvalidOperationException>(() => SuppressionRequestBuilder.BuildUpdate(draft));
    }

    /// <summary>
    /// <b>모든 칸 감사</b> — 손대지 않은 초안의 PATCH 는 원본을 <b>그대로</b> 되돌려 보내야 한다.
    /// <para>Update DTO 에 칸이 새로 생겼는데 빌더가 채우지 않으면 여기서 깨진다(그 칸이 서버에서 지워질 뻔한 것이다).</para>
    /// </summary>
    [Fact]
    public void should_round_trip_every_update_field_when_nothing_was_edited()
    {
        var baseline = FullBaseline();
        var draft = SuppressionDraft.FromDto(baseline);

        var patch = SuppressionRequestBuilder.BuildUpdate(draft, sendUnitId: true);

        var missed = new List<string>();
        foreach (var property in typeof(EventSuppressionScheduleUpdateDto).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var mirror = typeof(EventSuppressionScheduleDto).GetProperty(property.Name);
            Assert.True(mirror is not null,
                $"응답 DTO 에 '{property.Name}' 이(가) 없습니다 — 되돌려 보낼 값을 어디서 가져올지 정해야 합니다.");

            var sent = property.GetValue(patch);
            var original = mirror!.GetValue(baseline);
            if (!Same(sent, original)) missed.Add($"{property.Name}: 보낸 값 '{Show(sent)}' ≠ 원본 '{Show(original)}'");
        }

        Assert.True(missed.Count == 0, "PATCH 가 원본을 지우거나 바꿉니다 —\n" + string.Join("\n", missed));
    }

    [Fact]
    public void should_keep_the_unedited_recurrence_rule_when_patching()
    {
        var draft = SuppressionDraft.FromDto(FullBaseline());

        Assert.Equal("RRULE:FREQ=WEEKLY", SuppressionRequestBuilder.BuildUpdate(draft).RecurrenceRule);
    }

    [Fact]
    public void should_replace_the_target_array_when_targets_were_edited()
    {
        var draft = SuppressionDraft.FromDto(FullBaseline());
        draft.Targets.RemoveAll(t => t.Id == 3);
        draft.Targets.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, 9, "#9"));

        var patch = SuppressionRequestBuilder.BuildUpdate(draft);

        // 배열은 통째로 교체된다 — 그래서 남길 것을 전부 실어야 한다.
        Assert.Equal(new[] { 5, 9 }, patch.TargetDeviceIds);
    }

    [Fact]
    public void should_never_send_unit_id_when_the_contract_is_older_than_v8()
    {
        var draft = SuppressionDraft.FromDto(FullBaseline());

        var json = JObject.Parse(JsonConvert.SerializeObject(SuppressionRequestBuilder.BuildUpdate(draft, sendUnitId: false)));

        Assert.False(json.ContainsKey("unit_id"));
    }

    [Fact]
    public void should_send_an_explicit_null_end_when_patching_an_unlimited_window()
    {
        var baseline = FullBaseline();
        baseline.RecurrenceType = "weekly";
        baseline.WindowEnd = null;
        var draft = SuppressionDraft.FromDto(baseline);

        var json = JObject.Parse(JsonConvert.SerializeObject(SuppressionRequestBuilder.BuildUpdate(draft)));

        Assert.True(json.ContainsKey("window_end"));
        Assert.Equal(JTokenType.Null, json["window_end"]!.Type);
    }

    [Fact]
    public void should_carry_no_recurrence_field_in_a_patch_body()
    {
        var baseline = FullBaseline();
        baseline.RecurrenceType = "weekly";
        baseline.DaysOfWeek = 31;
        baseline.DailyStart = "08:00:00";
        baseline.DailyEnd = "21:00:00";

        var json = JObject.Parse(JsonConvert.SerializeObject(
            SuppressionRequestBuilder.BuildUpdate(SuppressionDraft.FromDto(baseline))));

        // 서버 Update 스키마에는 반복 4칸이 없다(extra=forbid) — 실리면 모든 PATCH 가 422 다.
        foreach (var key in new[] { "recurrence_type", "days_of_week", "daily_start", "daily_end" })
            Assert.False(json.ContainsKey(key), $"PATCH 본문에 '{key}' 가 실렸습니다 — 서버가 422 로 막습니다.");
    }

    [Fact]
    public void should_lock_recurrence_editing_when_the_schedule_already_exists()
        => Assert.False(SuppressionDraft.FromDto(FullBaseline()).CanEditRecurrence);

    [Fact]
    public void should_allow_recurrence_editing_when_the_schedule_is_new()
        => Assert.True(SuppressionDraft.NewSchedule(Kst).CanEditRecurrence);

    #endregion

    #region - 초안 채우기 -

    [Fact]
    public void should_load_weekly_fields_when_the_original_is_weekly()
    {
        var baseline = FullBaseline();
        baseline.RecurrenceType = "weekly";
        baseline.DaysOfWeek = SuppressionRules.DaysWeekendPreset;
        baseline.DailyStart = "22:00:00";
        baseline.DailyEnd = "06:00:00";

        var draft = SuppressionDraft.FromDto(baseline);

        Assert.True(draft.IsWeekly);
        Assert.Equal(SuppressionRules.DaysWeekendPreset, draft.DaysOfWeekMask);
        Assert.Equal(TimeSpan.FromHours(22), draft.DailyStart);
        Assert.Equal(TimeSpan.FromHours(6), draft.DailyEnd);
    }

    [Fact]
    public void should_keep_the_server_offset_when_loading_the_original()
    {
        var baseline = FullBaseline();
        baseline.WindowStart = "2026-09-20T09:00:00.000+09:00";

        var draft = SuppressionDraft.FromDto(baseline);

        Assert.Equal(TimeSpan.FromHours(9), draft.WindowStart.Offset);
    }

    [Fact]
    public void should_read_an_absent_end_as_unlimited()
    {
        var baseline = FullBaseline();
        baseline.WindowEnd = null;

        Assert.True(SuppressionDraft.FromDto(baseline).IsUnlimited);
    }

    [Fact]
    public void should_name_the_targets_when_a_resolver_is_given()
    {
        var draft = SuppressionDraft.FromDto(FullBaseline(), id => $"센서-{id}", id => $"그룹-{id}");

        Assert.Equal(new[] { "센서-3", "센서-5" }, draft.Targets.Select(t => t.Label));
    }

    [Fact]
    public void should_fall_back_to_the_id_when_no_resolver_is_given()
        => Assert.Equal("#3", SuppressionDraft.FromDto(FullBaseline()).Targets[0].Label);

    #endregion

    #region - 대상 확인 문구 -

    [Fact]
    public void should_echo_target_names_when_saving_devices()
    {
        var echo = SuppressionRequestBuilder.TargetEcho(FullBaseline(), id => $"센서-{id}");

        Assert.Contains("센서-3", echo);
        Assert.Contains("장비 2개", echo);
    }

    [Fact]
    public void should_fold_the_echo_when_there_are_many_targets()
    {
        var dto = FullBaseline();
        dto.TargetDeviceIds = Enumerable.Range(1, 9).ToList();

        Assert.Contains("외 6개", SuppressionRequestBuilder.TargetEcho(dto, id => $"센서-{id}"));
    }

    [Fact]
    public void should_echo_the_side_when_the_target_is_everything()
    {
        var dto = FullBaseline();
        dto.TargetType = SuppressionTargetDrop.ModeAll;
        dto.TargetSide = "surveillance";

        Assert.Contains("감시", SuppressionRequestBuilder.TargetEcho(dto));
    }

    #endregion

    #region - Helpers -

    private static bool Same(object? a, object? b)
    {
        if (a is IEnumerable left and not string && b is IEnumerable right and not string)
            return left.Cast<object>().SequenceEqual(right.Cast<object>());
        return Equals(a, b);
    }

    private static string Show(object? value) => value switch
    {
        null => "(null)",
        IEnumerable e and not string => "[" + string.Join(",", e.Cast<object>()) + "]",
        _ => value.ToString() ?? string.Empty,
    };

    #endregion
}
