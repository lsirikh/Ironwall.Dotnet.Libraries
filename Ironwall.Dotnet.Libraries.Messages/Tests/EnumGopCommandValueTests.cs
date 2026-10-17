using System.Linq;
using Ironwall.Dotnet.Libraries.Enums;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// API 계약 결함 회귀 방지 — FR-04(EnumGopCommand 신규 SYNC_* 등재).
/// SYNC_* 라우팅은 이름(문자열) 기반 매칭이라 정수값은 유일성만 지키면 되지만,
/// 값이 우연히 충돌하면(Enum.TryParse 이름매칭과 무관하게) 정수 기반 로그/직렬화 지점에서
/// 다른 명령으로 오인될 수 있어 유일성을 고정한다.
/// </summary>
public class EnumGopCommandValueTests
{
    [Fact]
    public void should_register_new_global_sync_commands_with_expected_values()
    {
        // Arrange & Act & Assert — 서버 GLOBAL_CMDS(SYNC_CATALOG/SYNC_ACTION_REPORT_TEMPLATE/SYNC_UNIT) 등재값 고정.
        Assert.Equal(31, (int)EnumGopCommand.SYNC_CATALOG);
        Assert.Equal(32, (int)EnumGopCommand.SYNC_ACTION_REPORT_TEMPLATE);
        Assert.Equal(33, (int)EnumGopCommand.SYNC_UNIT);
    }

    [Fact]
    public void should_register_sync_unit_layout_with_value_34_when_server_is_v8_0_4()
    {
        // 서버 v8.0.4 · 브로커 §9.18 — SYNC_UNIT_LAYOUT(sensorway.global.all.sync.unit-layout) 신설. 라우팅은 이름 기반이라
        //   값은 유일성만 갖지만, 정수 로그 · 직렬화에서 다른 명령으로 오인되지 않게 고정한다(다음 빈 값 34).
        var parsed = Enum.TryParse<EnumGopCommand>("SYNC_UNIT_LAYOUT", out var layout);

        Assert.True(parsed);
        Assert.Equal(34, (int)layout);
    }

    [Fact]
    public void should_have_unique_values_when_enumerating_all_gop_commands()
    {
        // Arrange
        var values = Enum.GetValues<EnumGopCommand>().Cast<int>().ToList();

        // Act
        var distinctCount = values.Distinct().Count();

        // Assert — 신규 3종 추가로 인한 값 충돌이 없어야 한다.
        Assert.Equal(values.Count, distinctCount);
    }

    [Fact]
    public void should_parse_new_sync_commands_by_name_when_using_enum_tryparse()
    {
        // Arrange — SYNC_* 라우팅은 이름 기반(Enum.TryParse) 매칭이므로 문자열 매핑도 함께 고정.
        // Act
        var parsedCatalog = Enum.TryParse<EnumGopCommand>("SYNC_CATALOG", out var catalog);
        var parsedTemplate = Enum.TryParse<EnumGopCommand>("SYNC_ACTION_REPORT_TEMPLATE", out var template);
        var parsedUnit = Enum.TryParse<EnumGopCommand>("SYNC_UNIT", out var unit);

        // Assert
        Assert.True(parsedCatalog);
        Assert.Equal(EnumGopCommand.SYNC_CATALOG, catalog);
        Assert.True(parsedTemplate);
        Assert.Equal(EnumGopCommand.SYNC_ACTION_REPORT_TEMPLATE, template);
        Assert.True(parsedUnit);
        Assert.Equal(EnumGopCommand.SYNC_UNIT, unit);
    }
}
