using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.Tests;
/****************************************************************************
   Purpose      : G50 — '중복 N건' 은 보낸 수 − 생성 − 없는 장비 − 실패 로 센다(skipped_config_ids 길이 아님)
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public class MappingCommitSkipCountTests
{
    private static MappingBoardRow Row(int deviceId) => MappingBoardRow.NewFor(MappingActionKind.Camera, deviceId);

    [Fact]
    public void 같은_쌍이_두_줄_쌓인_매핑에_한_건을_보내면_중복은_1건이다()
    {
        // 명세 §7.3.9: 같은 (mapping, camera) 쌍이 2줄이면 skipped_config_ids 는 그 둘을 다 싣는다 — 보낸 건 1건.
        var outcome = new MappingCommitOutcome();
        outcome.AcceptCreate(new[] { Row(101) }, new MappingBulkCreateResultDto
        {
            SkippedConfigIds = new List<int> { 11, 12 },
        });

        Assert.Equal(1, outcome.Skipped);
        Assert.Contains("중복 1건 제외", outcome.ToMessage());
        Assert.Contains(outcome.Diagnostics, d => d.Contains("11") && d.Contains("12"));
    }

    [Fact]
    public void 생성_없는장비_실패를_빼고_남은_수가_중복이다()
    {
        var outcome = new MappingCommitOutcome();
        var rows = new[] { Row(1), Row(2), Row(3), Row(4), Row(5) };
        outcome.AcceptCreate(rows, new MappingBulkCreateResultDto
        {
            CreatedIds = new List<int> { 701, 702 },
            NotFoundConfigIds = new List<int> { 3 },
            FailedItems = new List<MappingBulkFailedItemDto> { new() { Index = 3, Error = "Target preset with id 9 not found" } },
            SkippedConfigIds = new List<int> { 50, 51, 52 },
        });

        Assert.Equal(1, outcome.Skipped);     // 5 − 2 − 1 − 1
        Assert.Equal(2, outcome.Failed);
    }

    [Fact]
    public void 서버_목록이_짧거나_비어도_음수가_되지_않는다()
    {
        var outcome = new MappingCommitOutcome();
        outcome.AcceptCreate(new[] { Row(1) }, new MappingBulkCreateResultDto
        {
            CreatedIds = new List<int> { 701, 702 },   // 요청 내 중복 등으로 보낸 수보다 많아도
        });

        Assert.Equal(0, outcome.Skipped);
        Assert.Empty(outcome.Diagnostics);
    }
}
