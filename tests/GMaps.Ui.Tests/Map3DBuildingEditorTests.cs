using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 2026-10-22 사용자 — "높이를 수정하면 다른 속성이 변경 · 건물 복수 선택이 안 되고 · 카피 페이스트 안 된다" 의 순수 함수:
/// ① 엔진 건물 줄의 외관은 look:{…} 안(map-3d heights.py 응답 = scene.json 줄) ② userbuilding.pick 의 ctrl · shift ③ 여러 개 강조 uids ④ 붙여넣기 자리.
/// </summary>
public class Map3DBuildingEditorTests
{
    // 엔진 heights._set_height 가 돌려주는 scene.json 줄 그대로(외관은 looks._set_row_look 이 look 안에 적는다)
    private static JObject EngineRow() => JObject.Parse("""
        {"id":8,"origin":"photo","height":30.0,"height_source":"manual","height_from":"manual","levels":10,"center":[127.0,37.5],
         "manual":{"uid":"a1b2c3d4e5f6","height":30.0,"floors":10},
         "look":{"uid":"0f0f0f0f0f0f","hidden":false,"wall_color":"#336699","roof_color":null,"facade":"glass"}}
        """);

    [Fact]
    public void should_read_look_nested_in_engine_building_row_after_height_save()
    {
        var pick = Map3DBuilding.Parse(EngineRow())!;
        Assert.Equal(30.0, pick.Height);
        Assert.Equal(("glass", "#336699", (string?)null, false), (pick.Look.Facade, pick.Look.WallColor, pick.Look.RoofColor, pick.Look.Hidden));
    }

    [Fact]
    public void should_keep_reading_top_level_look_keys_of_viewer_message_and_auto_without_any()
    {
        var o = EngineRow();
        o.Remove("look");
        o["wall_color"] = "#F2F2F2";
        Assert.Equal("#f2f2f2", Map3DBuilding.Parse(o)!.Look.WallColor);   // 뷰어 building 메시지 · 엔진 look 응답(바깥 키)
        o.Remove("wall_color");
        Assert.Equal(Map3DLookState.Auto, Map3DBuilding.Parse(o)!.Look);
    }

    [Fact]
    public void should_read_ctrl_and_shift_of_user_box_pick_and_default_false_for_old_engine()
    {
        var m = Map3DMath.ParseMessage("""{"type":"userbuilding.pick","uid":"0123456789ab","ctrl":true,"shift":false}""")!;
        Assert.Equal(("0123456789ab", true, false), (m.UserUid, m.Ctrl, m.Shift));
        var old = Map3DMath.ParseMessage("""{"type":"userbuilding.pick","uid":"0123456789ab"}""")!;
        Assert.Equal((false, false), (old.Ctrl, old.Shift));
    }

    [Fact]
    public void should_send_all_selected_uids_and_omit_uids_for_single_or_clear()
    {
        var many = JObject.Parse(Map3DUserBuilding.SelectMessageJson("bbbbbbbbbbbb", new[] { "aaaaaaaaaaaa", "bbbbbbbbbbbb" }));
        Assert.Equal("bbbbbbbbbbbb", many.Value<string>("uid"));
        Assert.Equal(new[] { "aaaaaaaaaaaa", "bbbbbbbbbbbb" }, many["uids"]!.Values<string>());
        Assert.Null(JObject.Parse(Map3DUserBuilding.SelectMessageJson("aaaaaaaaaaaa"))["uids"]);
        var clear = JObject.Parse(Map3DUserBuilding.SelectMessageJson(null, System.Array.Empty<string>()));
        Assert.Equal(JTokenType.Null, clear["uid"]!.Type);
        Assert.Null(clear["uids"]);
    }

    private static IReadOnlyList<UbPoint> Square(double lon, double lat, double d)
        => new[] { new UbPoint(lon, lat), new UbPoint(lon + d, lat), new UbPoint(lon + d, lat + d), new UbPoint(lon, lat + d) };

    [Fact]
    public void should_paste_group_east_by_width_plus_gap_and_one_more_step_each_time()
    {
        var a = Square(127.0, 37.5, 0.0002);
        var b = Square(127.0003, 37.5, 0.0001);
        var once = Map3DUserBuilding.PasteOutlines(new[] { a, b }, 1)!;
        double kx = 111320.0 * Math.Cos(37.50005 * Math.PI / 180.0);
        double shift = once[0][0].X - a[0].X;
        Assert.Equal(0.0004 + 3.0 / kx, shift, 6);                          // 묶음 폭(0.0004°) + 3 m
        Assert.Equal(shift, once[1][0].X - b[0].X, 9);                       // 묶음 안 간격 그대로
        Assert.Equal(a[0].Y, once[0][0].Y, 9);
        Assert.True(once.SelectMany(o => o).Min(p => p.X) > new[] { a, b }.SelectMany(o => o).Max(p => p.X));   // 겹치지 않는다
        var twice = Map3DUserBuilding.PasteOutlines(new[] { a, b }, 2)!;
        Assert.Equal(2 * shift, twice[0][0].X - a[0].X, 6);                  // 다시 붙이면 한 칸 더
    }

    [Fact]
    public void should_try_west_north_south_inside_area_and_give_null_when_no_room()
    {
        var a = Square(127.0, 37.5, 0.0002);
        var west = Map3DUserBuilding.PasteOutlines(new[] { a }, 1, p => p.X <= 127.0002)!;   // 동쪽은 영역 밖
        Assert.True(west[0].Max(p => p.X) < 127.0);
        var north = Map3DUserBuilding.PasteOutlines(new[] { a }, 1, p => p.X is >= 127.0 and <= 127.0002)!;
        Assert.True(north[0].Min(p => p.Y) > 37.5002);
        Assert.Null(Map3DUserBuilding.PasteOutlines(new[] { a }, 1, p => p.X is >= 127.0 and <= 127.0002 && p.Y is >= 37.5 and <= 37.5002));
        Assert.Null(Map3DUserBuilding.PasteOutlines(System.Array.Empty<IReadOnlyList<UbPoint>>(), 1));
    }
}
