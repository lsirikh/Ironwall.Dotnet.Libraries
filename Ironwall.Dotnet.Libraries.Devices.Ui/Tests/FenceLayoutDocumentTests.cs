using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>펜스 구성 로컬 문서 ↔ JSON(fence-wiring-editor FR-11 · FR-16) — 저장소 구현이 쓰는 규칙 그대로.</summary>
public class FenceLayoutDocumentTests
{
    private static FenceLayoutDocument Sample() => new()
    {
        ControllerId = 10,
        Panels = new[]
        {
            new FencePanelSpec(EnumFenceStyle.ChainLink, null, 2.4, 6),
            new FencePanelSpec(EnumFenceStyle.ChainLinkRazor, "#4E565E", 2.4, 3),
            new FencePanelSpec(EnumFenceStyle.Brick, null, 2.2, 4.5),
        },
        Mounts = new Dictionary<int, SensorMountSpec>
        {
            [101] = new(0, FenceMountSpot.PostTop, -0.3, false),
            [102] = new(1, FenceMountSpot.PanelCenter, 0, true),
            [103] = new(2, FenceMountSpot.WallTop),
        },
        Bands = NumberBandSet.Tier4,
        FenceSpacingM = 2.5,
        Revision = 7,
    };

    [Fact]
    public void should_come_back_equal_when_a_document_is_serialized_and_read_again()
    {
        // Arrange
        var document = Sample();

        // Act
        var json = FenceLayoutJson.Serialize(document);
        var back = FenceLayoutJson.Deserialize(json, revision: 7)!;

        // Assert
        Assert.Equal(document.ControllerId, back.ControllerId);
        Assert.Equal(document.Panels, back.Panels);
        Assert.Equal(document.Mounts.OrderBy(p => p.Key), back.Mounts.OrderBy(p => p.Key));
        Assert.Equal(NumberBandSet.Tier4, back.Bands);
        Assert.Equal(2.5, back.FenceSpacingM);
        Assert.Equal(7, back.Revision);
    }

    [Fact]
    public void should_write_enums_as_names_and_keep_the_revision_out_of_the_body()
    {
        var json = JObject.Parse(FenceLayoutJson.Serialize(Sample()));

        Assert.Equal("ChainLinkRazor", (string?)json.SelectToken("panels[1].style"));
        Assert.Equal("WallTop", (string?)json.SelectToken("mounts.103.spot"));
        Assert.Equal(2.4, (double)json.SelectToken("panels[0].height_m")!);
        Assert.Equal("tier4", (string?)json.SelectToken("bands.preset"));
        Assert.Null(json["revision"]);
        Assert.Null(json.SelectToken("panels[0].color"));                  // 기본색(null)은 싣지 않는다
    }

    [Fact]
    public void should_clamp_values_and_fit_spots_to_the_panels_when_reading_a_hand_edited_body()
    {
        const string body = """
            {"controller_id":3,"panels":[{"style":"Brick","height_m":99,"span_m":0.01}],
             "mounts":{"5":{"panel":7,"spot":"PostTop","height_offset_m":-9}}}
            """;

        var document = FenceLayoutJson.Deserialize(body)!;

        Assert.Equal(FencePanelSpec.MAX_HEIGHT_M, document.Panels[0].HeightM);
        Assert.Equal(FencePanelSpec.MIN_SPAN_M, document.Panels[0].SpanM);
        Assert.Equal(new SensorMountSpec(0, FenceMountSpot.WallTop, SensorMountSpec.MIN_OFFSET_M), document.Mounts[5]);   // 담뿐이라 기둥이 없다
        Assert.Null(document.Bands);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("{\"panels\":[{\"style\":\"Unknown\"}]}")]
    public void should_read_nothing_when_the_body_cannot_be_parsed(string body)
        => Assert.Null(FenceLayoutJson.Deserialize(body));
}
