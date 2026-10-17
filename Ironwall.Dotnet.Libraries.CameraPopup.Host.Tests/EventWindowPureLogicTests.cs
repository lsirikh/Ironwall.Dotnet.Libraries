using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Themes;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Tests;

/// <summary>끌기 판정 · 배치 보정 · 테마 이름(순수 함수).</summary>
public class EventWindowPureLogicTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(8, 0, false)]      // 데드존 경계 = 클릭
    [InlineData(5.6, 5.6, false)]  // 7.92
    [InlineData(8.01, 0, true)]
    [InlineData(6, 6, true)]       // 8.49
    public void should_start_drag_only_past_8_diu_when_pointer_moves(double dx, double dy, bool expected)
    {
        Assert.Equal(expected, TileReorder.IsDrag(dx, dy));
    }

    [Theory]
    [InlineData(2, true, 4, 2)]
    [InlineData(2, false, 4, 3)]
    [InlineData(3, false, 4, 4)]
    [InlineData(-1, true, 4, -1)]
    [InlineData(4, true, 4, -1)]
    public void should_compute_insert_index_when_hovering_tile_half(int target, bool before, int count, int expected)
    {
        Assert.Equal(expected, TileReorder.InsertIndex(target, before, count));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(0, 2, true)]
    [InlineData(3, 3, false)]
    [InlineData(3, 4, false)]
    [InlineData(3, 0, true)]
    public void should_treat_neighbour_gaps_as_no_move_when_insert_index_is_own_edge(int from, int insert, bool expected)
    {
        Assert.Equal(expected, TileReorder.IsMove(from, insert, 4));
    }

    [Fact]
    public void should_reorder_without_mutating_source_when_moved()
    {
        var source = new[] { "a", "b", "c", "d" };

        Assert.Equal(new[] { "b", "c", "a", "d" }, TileReorder.Move(source, 0, 3));
        Assert.Equal(new[] { "d", "a", "b", "c" }, TileReorder.Move(source, 3, 0));
        Assert.Equal(new[] { "a", "b", "c", "d" }, TileReorder.Move(source, 1, 2));
        Assert.Equal(new[] { "a", "b", "c", "d" }, source);
    }

    [Theory]
    [InlineData(0, 1, 3, 2)]
    [InlineData(1, -1, 3, 0)]
    [InlineData(2, 1, 3, -1)]
    [InlineData(0, -1, 3, -1)]
    [InlineData(1, 0, 3, -1)]
    public void should_map_alt_arrow_to_insert_index_when_keyboard_fallback(int from, int delta, int count, int expected)
    {
        Assert.Equal(expected, TileReorder.KeyboardInsertIndex(from, delta, count));
    }

    [Fact]
    public void should_pull_window_inside_work_area_when_it_hangs_off_screen()
    {
        var work = new PixelRect { X = 1920, Y = 0, Width = 1920, Height = 1040 };

        var r = WindowPlacement.ClampToWorkArea(new PixelRect { X = 3500, Y = 900, Width = 800, Height = 500 }, work);

        Assert.Equal((3040, 540, 800, 500), (r.X, r.Y, r.Width, r.Height));
    }

    [Fact]
    public void should_shrink_window_when_larger_than_work_area()
    {
        var work = new PixelRect { X = 0, Y = 0, Width = 1280, Height = 680 };

        var r = WindowPlacement.ClampToWorkArea(new PixelRect { X = -50, Y = -50, Width = 3000, Height = 2000 }, work);

        Assert.Equal((0, 0, 1280, 680), (r.X, r.Y, r.Width, r.Height));
    }

    [Fact]
    public void should_apply_minimum_size_only_when_work_area_unknown()
    {
        var r = WindowPlacement.ClampToWorkArea(new PixelRect { X = -3000, Y = 5, Width = 10, Height = 10 }, new PixelRect());

        Assert.Equal((-3000, 5, WindowPlacement.MinWidth, WindowPlacement.MinHeight), (r.X, r.Y, r.Width, r.Height));
    }

    [Theory]
    [InlineData(1920, 1.5, 1280)]
    [InlineData(1920, 0, 1920)]
    [InlineData(300, 1.25, 240)]
    public void should_convert_pixels_to_diu_when_dpi_scale_given(int pixels, double scale, double expected)
    {
        Assert.Equal(expected, WindowPlacement.ToDiu(pixels, scale), 3);
    }

    [Theory]
    [InlineData("light", HostTheme.Light)]
    [InlineData(" Light ", HostTheme.Light)]
    [InlineData("Dark", HostTheme.Dark)]
    [InlineData(null, HostTheme.Dark)]
    [InlineData("neon", HostTheme.Dark)]
    public void should_normalize_theme_name_when_gis_sends_any_case(string? input, string expected)
    {
        Assert.Equal(expected, HostTheme.Normalize(input));
    }
}
