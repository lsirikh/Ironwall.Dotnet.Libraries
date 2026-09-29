using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 펜스 뷰 읽기 좋게(PIDS 실창 사진 검토 · 2026-09-29) — 기둥 LOD · 묶음 번호 범위 · "뒤" 표지 자리 · 모양별 고장 구간 설명 · 숫자 뒤 조사.
/// </summary>
public class WiringFenceLegibilityTests
{
    #region - Post LOD -
    [Fact]
    public void should_keep_every_post_when_on_screen_spacing_is_wide_enough()
    {
        var posts = new[] { 0.0, 30, 60, 90 };
        Assert.Equal(posts, FenceScene.ThinPosts(posts, new[] { 30.0 }, zoom: 1));
    }

    [Fact]
    public void should_thin_posts_to_at_least_14px_but_keep_sensor_posts_and_ends_when_zoomed_out()
    {
        var posts = Enumerable.Range(0, 41).Select(i => i * 10.0).ToList();          // 10 단위 간격 · 배율 0.5 → 화면 5px
        var sensorPosts = new[] { 100.0, 250.0 };

        var shown = FenceScene.ThinPosts(posts, sensorPosts, zoom: 0.5);

        Assert.Contains(0.0, shown);
        Assert.Contains(400.0, shown);
        Assert.Contains(100.0, shown);
        Assert.Contains(250.0, shown);
        for (var i = 1; i < shown.Count; i++)
        {
            var mandatoryPair = new[] { shown[i - 1], shown[i] }.All(x => x == 0 || x == 400 || sensorPosts.Contains(x));
            if (!mandatoryPair) Assert.True((shown[i] - shown[i - 1]) * 0.5 >= FenceScene.MIN_POST_SPACING_PX - 1e-9, $"{shown[i - 1]} → {shown[i]}");
        }
        Assert.True(shown.Count < posts.Count);
    }
    #endregion

    #region - Group chip · back tag -
    [Fact]
    public void should_put_the_group_range_inside_the_card_on_its_solid_body()
    {
        var members = Enumerable.Range(1, 7).Select(i => Sensor(i, EnumDeviceType.Fence, order: 9 + i)).ToList();
        foreach (var zoom in new[] { 1.0, 0.6 })
        {
            var picture = FenceScene.Group(members, WiringShape.TwoBranch, FenceProjector.Tilt, selected: false, zoom);
            var body = picture.Shapes.Single(s => s.Ink == FenceInk.GroupBody);
            var card = new Rect(body.Points[0], body.Points[1]);
            var range = picture.Shapes.Single(s => s.Ink == FenceInk.GroupSub);

            Assert.Equal("L10–L16", range.Text);
            Assert.True(card.Contains(range.Points[0]), $"범위 글자 기준점 {range.Points[0]} 이 카드 {card} 안");
            Assert.True(range.FontSize * zoom >= FenceScene.MIN_TEXT - 1e-9);
        }
    }

    [Theory]
    [InlineData(EnumDeviceType.Multi, 1.0)]
    [InlineData(EnumDeviceType.Multi, 0.64)]
    [InlineData(EnumDeviceType.SmartSensor2, 0.5)]
    public void should_place_the_back_tag_left_of_the_number_plate_without_overlap_and_inside_the_hit_rect(EnumDeviceType type, double zoom)
    {
        var picture = FenceScene.Sensor(Sensor(1, type, facing: WiringFacing.Back), WiringShape.TwoBranch, FenceProjector.Tilt, selected: false, zoom);
        var plate = picture.Shapes.Single(s => s.Ink == FenceInk.Plate);
        var plateRect = new Rect(plate.Points[0], plate.Points[1]);
        var tag = picture.Shapes.Single(s => s.Ink == FenceInk.FacingTag);
        var tagRect = new Rect(tag.Points[0], tag.Points[1]);

        Assert.False(tagRect.IntersectsWith(plateRect) && tagRect.Right > plateRect.Left, $"표지 {tagRect} · 번호판 {plateRect}");
        Assert.True(tagRect.Right <= plateRect.Left);
        Assert.True(picture.Hit.Contains(tagRect) && picture.Hit.Contains(plateRect), $"적중 {picture.Hit}");
    }
    #endregion

    #region - Texts -
    [Theory]
    [InlineData("SmartController", "Sensor A 쪽 끝")]
    [InlineData("Controller", "왼쪽 가지 L n · 오른쪽 가지 R n")]
    public void should_explain_the_fault_section_relation_per_topology(string controllerType, string expected)
    {
        var types = controllerType == "SmartController" ? "SmartSensor2" : "Multi";
        var seeds = Enumerable.Range(0, 3).Select(i => new WiringSensorSeed(101 + i, i + 1, new SensorFacts(1101 + i, $"센서 {i + 1}", types, ""), new WiringPlacement(1, i + 1)));
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL", "10.0.0.1", controllerType), seeds, new[] { types }, null, new WiringFakeDialogs());

        Assert.Contains(expected, vm.FaultRelationText);
        if (controllerType == "Controller") Assert.DoesNotContain("Sensor A", vm.FaultRelationText);
    }

    [Fact]
    public void should_pick_the_particle_by_the_last_digit_when_names_end_in_numbers()
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(1, 4).Select(i => (100 + i, (int?)i, new SensorFacts(1100 + i, $"서측 펜스 {i}", "Fence", ""),
            (WiringPlacement?)new WiringPlacement(1, i), (string?)null, (IReadOnlyList<int>?)null)), "IoController");

        Assert.Equal("1차 3~4 → 서측 펜스 3과 서측 펜스 4 사이", WiringValidation.DescribeFaultSection(board, 1, 3, 4));
        Assert.Equal("1차 2~3 → 서측 펜스 2와 서측 펜스 3 사이", WiringValidation.DescribeFaultSection(board, 1, 2, 3));
        Assert.Equal("순번 1001이 쓸 수 있는 범위(1~1000) 밖입니다 — 다시 배치해 주세요.",
                     WiringSpec.Validate(Newtonsoft.Json.Linq.JObject.Parse("""{"wiring":{"line":1,"order":1001}}""")));
    }
    #endregion

    private static FenceSensor Sensor(int key, EnumDeviceType type, int order = 1, WiringFacing facing = WiringFacing.Front)
        => new(key, key, 1000 + key, $"센서 {key}", type, null, 1, order, null, false, false, false, facing);
}
