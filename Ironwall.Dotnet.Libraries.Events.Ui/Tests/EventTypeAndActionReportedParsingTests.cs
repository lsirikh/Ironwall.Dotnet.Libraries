using Xunit;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : API 계약 결함 회귀 방지 — FR-01(EnumEventType 관용 파싱) ·
                  FR-02(action_reported 관용 판정). DtoToModelHelper 의
                  ParseOrDefault<T>/ToActionReported 는 private 이므로
                  ToDetectionEventModel/ToMalfunctionEventModel/ToConnectionEventModel
                  경유(관찰 가능한 공개 경로)로 단언한다.
   Created By   : GHLee
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventTypeAndActionReportedParsingTests
{
    private static DetectionEventDto BuildDto(string? typeEvent, string? actionReported)
        => new()
        {
            Id = 1,
            TypeEvent = typeEvent!,
            ActionReported = actionReported,
            Result = string.Empty,
        };

    // ═══════════════════════════════════════════════════════════════
    // FR-01: EnumEventType 관용 파싱 (ParseOrDefault<T>)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Alert", EnumEventType.Alert)]
    [InlineData("Operation", EnumEventType.Operation)]
    [InlineData("Intrusion", EnumEventType.Intrusion)]
    public void should_map_known_event_type_when_parsing_type_event(string typeEvent, EnumEventType expected)
    {
        // Arrange
        var dto = BuildDto(typeEvent, "False");

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(expected, model.MessageType);
    }

    [Theory]
    [InlineData("alert", EnumEventType.Alert)]
    [InlineData("ALERT", EnumEventType.Alert)]
    [InlineData("operation", EnumEventType.Operation)]
    public void should_ignore_case_when_parsing_known_type_event(string typeEvent, EnumEventType expected)
    {
        // Arrange
        var dto = BuildDto(typeEvent, "False");

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(expected, model.MessageType);
    }

    [Fact]
    public void should_return_default_when_type_event_is_unknown_string()
    {
        // Arrange — 서버가 카탈로그를 늘려 보낸 미지 값(예: 신설 프로토콜 값)
        var dto = BuildDto("RADAR_DETECT", "False");

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert — 예외 없이 default(EnumEventType.None)
        Assert.Equal(EnumEventType.None, model.MessageType);
    }

    [Fact]
    public void should_return_default_when_type_event_is_null()
    {
        // Arrange
        var dto = BuildDto(null, "False");

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(EnumEventType.None, model.MessageType);
    }

    [Fact]
    public void should_return_default_when_type_event_is_empty_string()
    {
        // Arrange
        var dto = BuildDto(string.Empty, "False");

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(EnumEventType.None, model.MessageType);
    }

    [Fact]
    public void should_not_throw_when_malfunction_type_event_is_unknown()
    {
        // Arrange — MalfunctionEventDto 경로도 동일 ParseOrDefault 사용(회귀 방지 범위 확장)
        var dto = new MalfunctionEventDto
        {
            Id = 1,
            TypeEvent = "UNKNOWN_FAULT_CATEGORY",
            ActionReported = "False",
            Reason = string.Empty,
        };

        // Act
        var model = dto.ToMalfunctionEventModel();

        // Assert
        Assert.Equal(EnumEventType.None, model.MessageType);
    }

    // ═══════════════════════════════════════════════════════════════
    // FR-02: action_reported 관용 판정 (ToActionReported)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("True", EnumTrueFalse.True)]
    [InlineData("true", EnumTrueFalse.True)]
    [InlineData("False", EnumTrueFalse.False)]
    [InlineData("false", EnumTrueFalse.False)]
    public void should_map_bool_like_strings_when_parsing_action_reported(string actionReported, EnumTrueFalse expected)
    {
        // Arrange
        var dto = BuildDto("Intrusion", actionReported);

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(expected, model.Status);
    }

    [Fact]
    public void should_default_to_false_when_action_reported_is_null()
    {
        // Arrange
        var dto = BuildDto("Intrusion", null);

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(EnumTrueFalse.False, model.Status);
    }

    [Fact]
    public void should_default_to_false_when_action_reported_is_empty_string()
    {
        // Arrange
        var dto = BuildDto("Intrusion", string.Empty);

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(EnumTrueFalse.False, model.Status);
    }

    [Fact]
    public void should_default_to_false_when_action_reported_is_non_bool_numeric_string()
    {
        // Arrange — bool.TryParse("1", ...) == false 이므로 관용 판정도 False 여야 한다.
        var dto = BuildDto("Intrusion", "1");

        // Act
        var model = dto.ToDetectionEventModel();

        // Assert
        Assert.Equal(EnumTrueFalse.False, model.Status);
    }
}
