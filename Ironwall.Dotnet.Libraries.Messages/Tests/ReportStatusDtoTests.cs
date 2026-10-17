using Newtonsoft.Json;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// API 계약 결함 회귀 방지 — FR-07(GET /api/reports/status DTO shape 변경).
/// 서버가 실제로 주는 모양은 <c>last_completed</c>=객체, <c>in_progress</c>=객체 배열이다.
/// 종전 <c>int?</c>/<c>List&lt;int&gt;</c> 선언은 이 JSON 에 대해 역직렬화 예외를 던졌다.
/// </summary>
public class ReportStatusDtoTests
{
    [Fact]
    public void should_deserialize_without_exception_when_server_shape_has_object_fields()
    {
        // Arrange — 실제 서버 응답 모양(객체/객체배열)
        var json = @"{
            ""busy"": false,
            ""ready"": true,
            ""in_progress_count"": 2,
            ""in_progress"": [
                { ""id"": 10, ""status"": ""running"", ""created_at"": ""2026-09-18T09:00:00+09:00"" },
                { ""id"": 11, ""status"": ""queued"" }
            ],
            ""last_completed"": { ""id"": 9, ""status"": ""done"", ""completed_at"": ""2026-09-18T08:00:00+09:00"" }
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<ReportStatusDto>(json);

        // Assert — 예외 없이 필드가 채워진다.
        Assert.NotNull(dto);
        Assert.False(dto!.Busy);
        Assert.True(dto.Ready);
        Assert.Equal(2, dto.InProgressCount);
        Assert.NotNull(dto.InProgress);
        Assert.Equal(2, dto.InProgress!.Count);
        Assert.Equal(10, dto.InProgress[0].Id);
        Assert.Equal("running", dto.InProgress[0].Status);
        Assert.NotNull(dto.LastCompleted);
        Assert.Equal(9, dto.LastCompleted!.Id);
        Assert.Equal("done", dto.LastCompleted.Status);
    }

    [Fact]
    public void should_deserialize_without_exception_when_in_progress_and_last_completed_are_absent()
    {
        // Arrange — 진행/완료 이력이 없는 초기 상태(둘 다 생략)
        var json = @"{ ""busy"": false, ""ready"": true, ""in_progress_count"": 0 }";

        // Act
        var dto = JsonConvert.DeserializeObject<ReportStatusDto>(json);

        // Assert
        Assert.NotNull(dto);
        Assert.Null(dto!.InProgress);
        Assert.Null(dto.LastCompleted);
    }

    [Fact]
    public void should_ignore_unknown_keys_when_progress_item_has_extra_fields()
    {
        // Arrange — 서버 shape 미확정 필드 대비(느슨한 파싱 계약)
        var json = @"{
            ""busy"": true,
            ""ready"": false,
            ""in_progress_count"": 1,
            ""in_progress"": [ { ""id"": 5, ""status"": ""running"", ""unexpected_field"": ""x"" } ]
        }";

        // Act
        var dto = JsonConvert.DeserializeObject<ReportStatusDto>(json);

        // Assert — 미지 키가 있어도 예외 없이 알려진 필드만 채워진다.
        Assert.NotNull(dto);
        Assert.NotNull(dto!.InProgress);
        Assert.Equal(5, dto.InProgress![0].Id);
    }
}
