using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 브로셔 기반 재모델링(2026-09-30, 결정 D-2026-09-30-a6e348) 계약.
/// <para>재모델링 전에는 다중·스마트·복합 센서가 한 메시를 나눠 써서 지도 30 px 에서 구분되지 않았다 —
/// 실물 외장색과 실루엣이 유일한 식별 단서라 그것을 지킨다.</para>
/// </summary>
public class BrochureHousingModelTests
{
    [Theory]
    [InlineData("smartmulti", "mat_olive")]    // 스마트 복합센서 II — 군용 올리브
    [InlineData("sensor", "mat_black")]        // 스마트 센서 — 검정 PC/ABS
    [InlineData("multi", "mat_black")]         // PIDS 복합센서
    [InlineData("fence", "mat_black")]         // 펜스센서
    [InlineData("controller", "mat_black")]    // 스마트 제어기 SC-1U
    [InlineData("iocontroller", "mat_black")]  // 제어기 P104C
    [InlineData("enclosure", "mat_steel")]     // 감시시스템 함체
    public void should_paint_product_shell_with_brochure_finish_and_keep_fill_color_slot(string key, string finish) => HousingTests.Sta(() =>
    {
        var model = HousingModels.Get(key);
        Assert.Contains(model.Parts, p => p.Material == finish);
        // 채우기 색은 외장이 아니라 명판·띠(mat_body)가 받는다 — 속성창 채우기 색·강도 슬라이더가 무의미해지지 않도록.
        Assert.Contains(model.Parts, p => p.Material == "mat_body");
    });

    [Fact]
    public void should_give_each_brochure_sensor_its_own_silhouette() => HousingTests.Sta(() =>
    {
        var keys = new[] { "smartmulti", "sensor", "multi" };
        var shapes = keys.Select(k => HousingModels.Get(k).Bounds).Select(b => (Math.Round(b.SizeX, 3), Math.Round(b.SizeY, 3), Math.Round(b.SizeZ, 3))).ToList();
        Assert.Equal(keys.Length, shapes.Distinct().Count());
        Assert.NotSame(HousingModels.Get("sensor"), HousingModels.Get("multi"));
    });

    [Fact]
    public void should_open_underground_cutaway_toward_viewer_at_default_bearing() => HousingTests.Sta(() =>
    {
        // 카메라는 방위 0° 에서 −Z 쪽에 있다 — 흙을 잘라낸 칸(x>0, z<0)에는 바닥판 위로 흙이 없어야 단면이 보인다.
        var soil = HousingModels.Get("underground").Parts.Where(p => p.Material == "mat_soil").SelectMany(p => p.Mesh.Positions).ToList();
        Assert.NotEmpty(soil);
        Assert.DoesNotContain(soil, p => p.X > .01 && p.Z < -.01 && p.Y > .06);
        Assert.Contains(HousingModels.Get("underground").Parts, p => p.Material == "mat_black");   // 묻힌 원뿔대
    });

    [Fact]
    public void should_glow_status_led_in_current_event_color() => HousingTests.Sta(() =>
    {
        var view = new HousingVisual { ModelKey = "enclosure" };
        HousingTests.Layout(view, 83, 83);
        view.StatusBrush = Brushes.Red;
        var scene = (Model3DGroup)((ModelVisual3D)((Viewport3D)view.Children[0]).Children[0]).Content;
        var objects = (Model3DGroup)scene.Children.Last();
        var glows = objects.Children.OfType<GeometryModel3D>().Select(g => g.Material).OfType<MaterialGroup>()
                           .SelectMany(m => m.Children).OfType<EmissiveMaterial>().ToList();
        Assert.Contains(glows, e => ReferenceEquals(e.Brush, Brushes.Red));   // 상태등이 이벤트 색으로 빛난다
    });

    [Fact]
    public void should_share_one_lighting_rig_between_map_symbol_and_detail_preview() => HousingTests.Sta(() =>
    {
        var scene = new Model3DGroup();
        HousingLighting.AddTo(scene);
        var ambient = Assert.Single(scene.Children.OfType<AmbientLight>());
        // 평탄광이 전체의 절반 가까이 되면 윗면·옆면 명암이 눌려 입체감이 죽는다(종전 112,125,145).
        Assert.True(ambient.Color.R + ambient.Color.G + ambient.Color.B < 3 * 100, $"평탄광이 너무 밝다: {ambient.Color}");
        Assert.True(scene.Children.OfType<DirectionalLight>().Count() >= 2);
    });
}
