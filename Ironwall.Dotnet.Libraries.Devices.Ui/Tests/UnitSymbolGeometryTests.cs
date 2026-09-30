using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// TEST-10 — 부대 기호 기하(FR-20 · FR-22 · NFR-08). 틀 3:2 · 단계별 크기 · 틀 위 제대 표지 · 모르는 제대 "?" ·
/// 틀 안 빈(결정 #5) · 반 픽셀 정렬 · 지도 변환기와 표지 대응 일치.
/// </summary>
public class UnitSymbolGeometryTests
{
    public static IEnumerable<object[]> AllEchelons()
        => Enum.GetValues<EnumUnitEchelon>().Select(e => new object[] { e });

    #region - 틀 크기 -
    [Theory]
    [InlineData(UnitMapLevel.L1, 30, 20)]
    [InlineData(UnitMapLevel.L2, 36, 24)]
    public void should_keep_3_to_2_frame_when_level_is_L1_or_L2(UnitMapLevel level, double width, double height)
    {
        foreach (var echelon in Enum.GetValues<EnumUnitEchelon>())
        {
            var size = UnitSymbolGeometry.FrameSize(level, echelon);

            Assert.Equal(width, size.Width);
            Assert.Equal(height, size.Height);
            Assert.Equal(1.5, size.Width / size.Height, 6);
        }
    }

    [Theory]
    [InlineData(EnumUnitEchelon.Division, 20, 13)]
    [InlineData(EnumUnitEchelon.Regiment, 17, 11)]
    [InlineData(EnumUnitEchelon.Battalion, 14, 9)]
    [InlineData(EnumUnitEchelon.Company, 12, 8)]
    [InlineData(EnumUnitEchelon.Outpost, 8, 6)]
    public void should_size_L0_frame_by_echelon_when_level_is_L0(EnumUnitEchelon echelon, double width, double height)
    {
        var size = UnitSymbolGeometry.FrameSize(UnitMapLevel.L0, echelon);

        Assert.Equal(width, size.Width);
        Assert.Equal(height, size.Height);
        Assert.InRange(size.Width / size.Height, 1.3, 1.6);       // 정수 픽셀 표라 3:2 근사
    }

    [Fact]
    public void should_shrink_L0_frame_as_echelon_goes_down_when_ordered_from_division()
    {
        var widths = Enum.GetValues<EnumUnitEchelon>().Select(e => UnitSymbolGeometry.FrameSize(UnitMapLevel.L0, e).Width).ToList();

        Assert.Equal(widths.OrderByDescending(w => w), widths);
    }
    #endregion

    #region - 제대 표지 -
    [Theory]
    [InlineData(EnumUnitEchelon.Division, UnitMarkKind.Crosses, 2, "XX")]
    [InlineData(EnumUnitEchelon.Regiment, UnitMarkKind.Bars, 3, "|||")]
    [InlineData(EnumUnitEchelon.Battalion, UnitMarkKind.Bars, 2, "||")]
    [InlineData(EnumUnitEchelon.Company, UnitMarkKind.Bars, 1, "|")]
    [InlineData(EnumUnitEchelon.Outpost, UnitMarkKind.Dots, 3, "●●●")]
    public void should_map_echelon_to_app6d_mark_when_echelon_is_known(EnumUnitEchelon echelon, UnitMarkKind kind, int count, string glyph)
    {
        var mark = UnitSymbolGeometry.MarkOf(echelon);

        Assert.Equal(kind, mark.Kind);
        Assert.Equal(count, mark.Count);
        Assert.Equal(glyph, mark.Glyph);
    }

    [Fact]
    public void should_use_question_mark_when_echelon_is_unknown()
    {
        var mark = UnitSymbolGeometry.MarkOf(null);
        var shape = UnitSymbolGeometry.Build(UnitMapLevel.L2, null);

        Assert.Equal(UnitMarkKind.Unknown, mark.Kind);
        Assert.Equal("?", mark.Glyph);
        Assert.Equal(UnitMarkKind.Unknown, shape.Mark.Kind);
        Assert.Equal(string.Empty, shape.MarkStrokePathData);   // "?" 는 경로가 아니라 글자로 — 닻 자리에 호출부가 쓴다
        Assert.Equal(string.Empty, shape.MarkDotPathData);
        Assert.True(shape.MarkAnchor.Y <= shape.Frame.Top);      // 표지 자리(틀 위)
    }

    [Theory]
    [MemberData(nameof(AllEchelons))]
    public void should_draw_echelon_mark_above_the_frame_when_level_is_L1_or_L2(EnumUnitEchelon echelon)
    {
        foreach (var level in new[] { UnitMapLevel.L1, UnitMapLevel.L2 })
        {
            var shape = UnitSymbolGeometry.Build(level, echelon);
            var bounds = OnSta(() => Union(Bounds(shape.MarkStrokePathData), Bounds(shape.MarkDotPathData)));

            Assert.False(bounds.IsEmpty, $"{level} {echelon}: 표지가 없다");
            Assert.True(bounds.Bottom <= shape.Frame.Top - 2, $"{level} {echelon}: 표지 밑변 {bounds.Bottom} 이 틀 위 {shape.Frame.Top} 에 닿는다");
            Assert.InRange(bounds.Left + bounds.Width / 2, shape.Frame.Left + shape.Frame.Width / 2 - 0.01, shape.Frame.Left + shape.Frame.Width / 2 + 0.01);
            Assert.True(bounds.Top >= 0, $"{level} {echelon}: 표지가 노드 상자 밖(위)");
        }
    }

    [Theory]
    [MemberData(nameof(AllEchelons))]
    public void should_draw_the_right_number_of_strokes_when_echelon_mark_is_drawn(EnumUnitEchelon echelon)
    {
        var shape = UnitSymbolGeometry.Build(UnitMapLevel.L2, echelon);
        var (strokeFigures, dotFigures) = OnSta(() => (Figures(shape.MarkStrokePathData), Figures(shape.MarkDotPathData)));

        switch (shape.Mark.Kind)
        {
            case UnitMarkKind.Crosses: Assert.Equal(shape.Mark.Count * 2, strokeFigures); Assert.Equal(0, dotFigures); break;   // X 하나 = 사선 둘
            case UnitMarkKind.Bars: Assert.Equal(shape.Mark.Count, strokeFigures); Assert.Equal(0, dotFigures); break;
            case UnitMarkKind.Dots: Assert.Equal(0, strokeFigures); Assert.Equal(shape.Mark.Count, dotFigures); break;
            default: Assert.Fail($"예상 밖 표지 {shape.Mark.Kind}"); break;
        }
    }

    [Theory]
    [MemberData(nameof(AllEchelons))]
    public void should_not_draw_echelon_mark_when_level_is_L0(EnumUnitEchelon echelon)
    {
        var shape = UnitSymbolGeometry.Build(UnitMapLevel.L0, echelon);

        Assert.Equal(string.Empty, shape.MarkStrokePathData);    // 3px 밑이라 읽을 수 없다(SB S2)
        Assert.Equal(string.Empty, shape.MarkDotPathData);
    }
    #endregion

    #region - 틀 안은 비운다(결정 #5) -
    [Theory]
    [MemberData(nameof(AllEchelons))]
    public void should_leave_the_frame_empty_when_no_branch_field_exists(EnumUnitEchelon echelon)
    {
        foreach (var level in Enum.GetValues<UnitMapLevel>())
        {
            var shape = UnitSymbolGeometry.Build(level, echelon);
            var (figures, segments, bounds) = OnSta(() =>
            {
                var path = PathGeometry.CreateFromGeometry(Geometry.Parse(shape.FramePathData));
                return (path.Figures.Count, path.Figures.Sum(f => f.Segments.Count), path.Bounds);
            });

            Assert.Equal(string.Empty, shape.BranchPathData);    // 병과 경로 0
            Assert.Equal(1, figures);                            // 틀은 닫힌 사각 하나뿐
            Assert.Equal(1, segments);                           // PolyLine 한 묶음(모서리 4) — 안쪽 선 없음
            Assert.Equal(shape.Frame, bounds);
        }
    }

    [Fact]
    public void should_build_the_frame_for_unknown_echelon_when_echelon_is_null()
    {
        var shape = UnitSymbolGeometry.Build(UnitMapLevel.L1, null);

        Assert.Equal(new Size(30, 20), shape.Frame.Size);
        Assert.Equal(string.Empty, shape.BranchPathData);
    }
    #endregion

    #region - 반 픽셀 정렬 · 상자 -
    [Theory]
    [MemberData(nameof(AllEchelons))]
    public void should_align_frame_edges_and_bars_on_half_pixels_when_built(EnumUnitEchelon echelon)
    {
        foreach (var level in Enum.GetValues<UnitMapLevel>())
        {
            var shape = UnitSymbolGeometry.Build(level, echelon);

            Assert.Equal(0.5, Frac(shape.Frame.Left));
            Assert.Equal(0.5, Frac(shape.Frame.Top));
            Assert.Equal(0.5, Frac(shape.Frame.Right));
            Assert.Equal(0.5, Frac(shape.Frame.Bottom));

            if (shape.Mark.Kind != UnitMarkKind.Bars || level == UnitMapLevel.L0) continue;
            var xs = OnSta(() => PathGeometry.CreateFromGeometry(Geometry.Parse(shape.MarkStrokePathData))
                                             .Figures.Select(f => f.StartPoint.X).ToList());
            Assert.All(xs, x => Assert.Equal(0.5, Frac(x)));
        }
    }

    [Theory]
    [MemberData(nameof(AllEchelons))]
    public void should_keep_the_frame_inside_the_node_box_centered_on_the_anchor_when_built(EnumUnitEchelon echelon)
    {
        foreach (var level in Enum.GetValues<UnitMapLevel>())
        {
            var shape = UnitSymbolGeometry.Build(level, echelon);

            Assert.True(new Rect(shape.Box).Contains(shape.Frame), $"{level}: 틀이 상자 밖");
            Assert.InRange(shape.Center.X, 0, shape.Box.Width);
            Assert.InRange(shape.Center.Y, 0, shape.Box.Height);
        }

        var l2 = UnitSymbolGeometry.Build(UnitMapLevel.L2, echelon);
        Assert.Equal(new Size(132, 56), l2.Box);                  // 카드 132×56
        Assert.Equal(new Point(66, 28), l2.Center);
    }
    #endregion

    #region - 지도 변환기와 대응(참조하지 않고 문자열로 대조) -
    [Theory]
    [InlineData(EnumUnitEchelon.Division, "Division")]
    [InlineData(EnumUnitEchelon.Regiment, "Regiment")]
    [InlineData(EnumUnitEchelon.Battalion, "Battalion")]
    [InlineData(EnumUnitEchelon.Company, "Company")]
    public void should_match_the_map_converter_glyph_when_compared_as_strings(EnumUnitEchelon echelon, string mapSize)
    {
        var source = File.ReadAllText(MapConverterPath());
        var glyph = UnitSymbolGeometry.MarkOf(echelon).Glyph;

        Assert.Contains($"EnumMilitaryUnitSize.{mapSize} => \"{glyph}\"", source);
    }

    [Fact]
    public void should_draw_three_dots_for_outpost_when_the_map_converter_still_says_two()
    {
        // APP-6D: 소대 ●●● · 분대 ● — 지도 변환기의 소대 ●● 는 지도 도메인(범위 밖)이라 이 PRD 는 따르지 않는다(SB S10 경고 상자).
        var source = File.ReadAllText(MapConverterPath());

        Assert.Equal("●●●", UnitSymbolGeometry.MarkOf(EnumUnitEchelon.Outpost).Glyph);
        Assert.Contains("EnumMilitaryUnitSize.Platoon => \"●●\"", source);
    }
    #endregion

    #region - Fixtures -
    private static double Frac(double value) => Math.Round(value - Math.Floor(value), 6);

    private static Rect Bounds(string data) => string.IsNullOrEmpty(data) ? Rect.Empty : Geometry.Parse(data).Bounds;

    private static int Figures(string data)
        => string.IsNullOrEmpty(data) ? 0 : PathGeometry.CreateFromGeometry(Geometry.Parse(data)).Figures.Count;

    private static Rect Union(Rect a, Rect b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;
        return Rect.Union(a, b);
    }

    private static string MapConverterPath([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..",
                                         "Ironwall.Dotnet.Libraries.GMaps.Ui", "Utils", "UnitSizeToSymbolConverter.cs"));

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }
    #endregion
}
