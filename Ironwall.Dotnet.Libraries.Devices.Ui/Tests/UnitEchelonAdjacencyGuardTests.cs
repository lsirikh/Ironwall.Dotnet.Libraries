using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 인접 부대가 있는 부대의 제대 바꾸기(GOP API 8.0.15 대조 G33) — 서버는 남는 인접 부대의 제대와 달라지면 422 다(감사 F37).
/// 저장 전에 막고 쉬운 말로 "인접 연결을 먼저 끊으세요" 를 보인다.
/// </summary>
public class UnitEchelonAdjacencyGuardTests
{
    private static UnitDto Original(params int[] adjacent) => new()
    {
        Id = 7, Code = "u007", Name = "1중대", EchelonRaw = "Company", IsEnable = true,
        AdjacentUnitIds = new List<int>(adjacent),
    };

    [Fact]
    public void should_block_echelon_change_when_original_has_adjacent_units()
    {
        var original = Original(6, 8);
        var dto = UnitRequestBuilder.Edit(original, new UnitEditValues("1중대", EnumUnitEchelon.Battalion, null, true))!;

        var block = UnitRequestBuilder.EchelonAdjacencyBlock(original, null, dto);

        Assert.NotNull(block);
        Assert.Contains("인접 연결을 먼저 끊", block);
        Assert.DoesNotContain("adjacent", block, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("echelon", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void should_block_echelon_change_when_form_shows_adjacent_chips_even_if_original_list_is_empty()
    {
        // 상세 응답이 adjacent_unit_ids 대신 adjacent(객체 목록)로만 줄 때 — 폼 칩이 그 목록이다.
        var original = Original();
        var dto = UnitRequestBuilder.Edit(original, new UnitEditValues("1중대", EnumUnitEchelon.Outpost, null, true))!;

        Assert.NotNull(UnitRequestBuilder.EchelonAdjacencyBlock(original, new[] { 6 }, dto));
    }

    [Fact]
    public void should_allow_echelon_change_when_unit_has_no_adjacent_units()
    {
        var original = Original();
        var dto = UnitRequestBuilder.Edit(original, new UnitEditValues("1중대", EnumUnitEchelon.Battalion, null, true))!;

        Assert.Null(UnitRequestBuilder.EchelonAdjacencyBlock(original, Array.Empty<int>(), dto));
    }

    [Fact]
    public void should_allow_other_edits_when_echelon_is_not_changed_even_with_adjacent_units()
    {
        var original = Original(6, 8);
        var dto = UnitRequestBuilder.Edit(original, new UnitEditValues("1중대(개명)", EnumUnitEchelon.Company, null, true))!;

        Assert.Null(dto.Echelon);
        Assert.Null(UnitRequestBuilder.EchelonAdjacencyBlock(original, new[] { 6, 8 }, dto));
    }
}
